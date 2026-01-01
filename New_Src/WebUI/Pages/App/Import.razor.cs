using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using WebUI.Services;
using WebUI.Models;
using WebUI.Shared;

namespace WebUI.Pages.App
{
    public partial class Import
    {
        // کنترل نمایش پنجره دو تبی (وقتی یک فایل انتخاب شد)
        private bool isWindowOpen = false;

        // بعد از آپلود موفق: دکمه آپلود غیرفعال بماند
        private bool uploadCompleted = false;

        // ============================================
        // متغیرهای بخش آپلود (Tab 1)
        // ============================================
        private IBrowserFile? selectedFile;
        private byte[]? fileContent;
        private string fileName = "";

        // نام نمایشی فایل (برای نام پروژه)
        private string displayFileName = "";

        private string fileContentType = "";
        private long fileSizeBytes;

        private bool isViewer = false;
        private bool isLoading;

        private string description = "";
        private string selectedDevice = "";
        private string selectedFileType = "";
        private bool skipLastRow = true;

        private List<string> deviceOptions = new() { "Mass elan9000 1", "Mass elan9000 2", "OES 715", "OES 735 1", "OES 735 2" };
        private List<string> fileTypeOptions = new() { "oes 4cc", "oes 6cc", "txt format", "xlsx format" };

        private AnalysisPreviewResult? analysisData;

        // ============================================
        // متغیرهای بخش مدیریت فایل‌ها (Tab 2)
        // ============================================
        private string filterContract = "";
        private List<ProjectUiModel> existingFiles = new();
        private ProjectUiModel? selectedItem;
        private bool isItemSelected => selectedItem != null;

        // ============================================
        // متدها
        // ============================================

        protected override void OnInitialized()
        {
            var user = AuthService.GetCurrentUser();
            if (user == null || !user.IsAuthenticated)
            {
                NavManager.NavigateTo("/login");
                return;
            }
            if (string.Equals(user.Position, "Viewer", StringComparison.OrdinalIgnoreCase)) isViewer = true;

            selectedDevice = deviceOptions.FirstOrDefault() ?? "";
            selectedFileType = fileTypeOptions.FirstOrDefault() ?? "";

            // demo data
            existingFiles = new List<ProjectUiModel>
            {
                new ProjectUiModel
                {
                    Id = Guid.NewGuid(),
                    ProjectName = "1404-08-26 oes RET 1713 1745 1748 22009",
                    CreatedBy = "Device Operator",
                    Created = DateTime.Now.AddDays(-1),
                    Device = "Mass elan9000 1"
                },
                new ProjectUiModel
                {
                    Id = Guid.NewGuid(),
                    ProjectName = "1404-09-01 oes RET 1714 (test file)",
                    CreatedBy = "Admin",
                    Created = DateTime.Now,
                    Device = "OES 735 1"
                }
            };
        }

        private void GoToDashboard() => NavManager.NavigateTo("/dashboard");

        private async Task OnInputFileChangeStandard(InputFileChangeEventArgs e)
        {
            if (isViewer) return;
            var file = e.File;
            if (file == null) return;
            await ProcessFile(file);
        }

        private async Task ProcessFile(IBrowserFile file)
        {
            isLoading = true;
            StateHasChanged();

            try
            {
                selectedFile = file;
                fileName = file.Name;
                displayFileName = file.Name; // نام پیش‌فرض پروژه
                fileContentType = file.ContentType ?? "application/octet-stream";
                fileSizeBytes = file.Size;

                using var ms = new MemoryStream();
                await file.OpenReadStream(maxAllowedSize: 200 * 1024 * 1024).CopyToAsync(ms);
                fileContent = ms.ToArray();

                await DoPreview();
                // باز نگه داشتن پنجره دو تب بعد از پردازش فایل
                isWindowOpen = true;
                // Load existing projects for Manage tab
                await LoadExistingFilesAsync();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error reading file: {ex.Message}", Severity.Error);
                ClearFile();
            }
            finally
            {
                isLoading = false;
            }
        }

        private async Task ReselectFile(InputFileChangeEventArgs e)
        {
            await OnInputFileChangeStandard(e);
        }

        private void ClearFile()
        {
            selectedFile = null;
            fileContent = null;
            fileName = "";
            displayFileName = "";
            fileContentType = "";
            fileSizeBytes = 0;
            analysisData = null;
            description = "";
            // بستن پنجره دو تب وقتی کاربر Close یا X را می‌زند
            isWindowOpen = false;
            // ریست وضعیت آپلود
            uploadCompleted = false;
            selectedItem = null;
        }

        private async Task DoPreview()
        {
            if (fileContent == null || fileContent.Length == 0) return;
            try
            {
                using var ms = new MemoryStream(fileContent);
                var result = await ImportService.AnalyzeFileAsync(ms, fileName);
                if (result.Succeeded && result.Data != null)
                {
                    analysisData = result.Data;
                    Snackbar.Add("Analysis completed", Severity.Success);
                }
                else
                {
                    var errorMsg = result.Messages?.FirstOrDefault() ?? "Analysis failed";
                    Snackbar.Add(errorMsg, Severity.Error);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error: {ex.Message}", Severity.Error);
            }
        }

        private async Task ImportFile()
        {
            if (isViewer)
            {
                Snackbar.Add("Viewers cannot import data.", Severity.Error);
                return;
            }

            if (fileContent == null) return;

            isLoading = true;
            StateHasChanged();

            try
            {
                var user = AuthService.GetCurrentUser();
                using var stream = new MemoryStream(fileContent);

                // استفاده از نام ویرایش شده توسط کاربر
                var finalProjectName = string.IsNullOrWhiteSpace(displayFileName)
                                     ? fileName
                                     : displayFileName;

                var request = new AdvancedImportRequest(finalProjectName, user?.Name)
                {
                    SkipLastRow = skipLastRow,
                    Device = selectedDevice,
                    FileType = selectedFileType,
                    Description = description
                };

                var result = await ImportService.ImportAdvancedAsync(stream, fileName, request);

                if (result.Succeeded)
                {
                    Snackbar.Add($"Project '{finalProjectName}' imported successfully!", Severity.Success);
                    // علامت می‌زنیم که عملیات آپلود کامل شده تا دکمه Upload غیرفعال بماند
                    uploadCompleted = true;
                    // رفرش لیست پروژه‌های موجود از سرور/دیتابیس
                    await LoadExistingFilesAsync();
                }
                else
                {
                    var errorMsg = result.Messages?.FirstOrDefault() ?? "Import failed";
                    Snackbar.Add(errorMsg, Severity.Error);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error: {ex.Message}", Severity.Error);
            }
            finally
            {
                isLoading = false;
            }
        }

        private async Task LoadExistingFilesAsync()
        {
            try
            {
                var res = await ProjectService.GetProjectsAsync();
                if (res.Succeeded && res.Data != null)
                {
                    existingFiles = res.Data.Select(p => new ProjectUiModel
                    {
                        Id = p.ProjectId,
                        ProjectName = p.ProjectName,
                        CreatedBy = p.Owner ?? "",
                        Created = p.CreatedAt,
                        Device = p.Device ?? string.Empty,
                        FileType = p.FileType ?? string.Empty,
                        Description = p.Description ?? string.Empty
                    }).ToList();
                }
                else
                {
                    Snackbar.Add(res.Message ?? "Failed to load projects", Severity.Warning);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error loading projects: {ex.Message}", Severity.Error);
            }
            StateHasChanged();
        }

        private void OnRowClicked(TableRowClickEventArgs<ProjectUiModel> args)
        {
            selectedItem = args.Item;
            StateHasChanged();
        }

        private string? RowClassFunc(ProjectUiModel item) => item == selectedItem ? "selected" : null;

        private async Task ConfirmDelete()
        {
            if (selectedItem == null) return;

            var parameters = new MudBlazor.DialogParameters { ["ProjectName"] = selectedItem.ProjectName };
            var options = new MudBlazor.DialogOptions { CloseButton = false };
            var dialog = await DialogService.ShowAsync<ConfirmDeleteDialog>("Are you sure?", parameters, options);
            var res = await dialog.Result;
            if (res != null && !res.Canceled)
            {
                // Check admin role
                var user = AuthService.GetCurrentUser();
                if (user != null && string.Equals(user.Position, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    var serverRes = await ProjectService.DeleteProjectAsync(selectedItem.Id);
                    if (serverRes.Succeeded)
                    {
                        Snackbar.Add("Project deleted.", Severity.Success);
                        await LoadExistingFilesAsync();
                        selectedItem = null;
                    }
                    else
                    {
                        Snackbar.Add(serverRes.Message ?? "Failed to delete project.", Severity.Error);
                    }
                }
                else
                {
                    Snackbar.Add("You do not have permission to delete projects.", Severity.Error);
                }
            }
        }

        private async Task OpenEditDialog()
        {
            if (selectedItem == null) return;
            // Fetch latest project details from server to prefill dialog
            var projRes = await ProjectService.GetProjectAsync(selectedItem.Id);
            if (!projRes.Succeeded || projRes.Data == null)
            {
                Snackbar.Add(projRes.Message ?? "Failed to load project details.", Severity.Error);
                return;
            }

            var info = projRes.Data;

            // Ensure the options lists contain the current values so MudSelect can display them
            var deviceOpts = new List<string>(deviceOptions);
            var fileTypeOpts = new List<string>(fileTypeOptions);
            if (!string.IsNullOrWhiteSpace(info.Device) && !deviceOpts.Contains(info.Device))
                deviceOpts.Insert(0, info.Device);
            if (!string.IsNullOrWhiteSpace(info.FileType) && !fileTypeOpts.Contains(info.FileType))
                fileTypeOpts.Insert(0, info.FileType);

            var parameters = new MudBlazor.DialogParameters
            {
                ["Item"] = new ProjectUiModel
                {
                    Id = info.ProjectId,
                    ProjectName = info.ProjectName ?? string.Empty,
                    CreatedBy = info.Owner ?? string.Empty,
                    Created = info.CreatedAt,
                    Device = info.Device ?? string.Empty,
                    FileType = info.FileType ?? string.Empty,
                    Description = info.Description ?? string.Empty
                },
                ["DeviceOptions"] = deviceOpts,
                ["FileTypeOptions"] = fileTypeOpts
            };

            var dialog = await DialogService.ShowAsync<EditProjectDialog>("Edit Project", parameters);
            var res = await dialog.Result;
            if (res != null && !res.Canceled)
            {
                var updated = res.Data as ProjectUiModel;
                if (updated != null)
                {
                    var serverRes = await ProjectService.UpdateProjectAsync(updated.Id, updated.ProjectName, updated.Device, updated.FileType, updated.Description);
                    if (serverRes.Succeeded)
                    {
                        Snackbar.Add("Project updated successfully.", Severity.Success);
                        // refresh from server to ensure DB state is shown
                        await LoadExistingFilesAsync();
                        selectedItem = existingFiles.FirstOrDefault(x => x.Id == updated.Id);
                    }
                    else
                    {
                        Snackbar.Add(serverRes.Message ?? "Failed to update project.", Severity.Error);
                    }
                }
            }
        }

        private async Task RefreshList()
        {
            await LoadExistingFilesAsync();
        }

        // Helpers
        private string GetFileIcon()
        {
            if (string.IsNullOrEmpty(fileName)) return Icons.Material.Filled.InsertDriveFile;
            var ext = Path.GetExtension(fileName).ToLower();
            return ext switch
            {
                ".csv" => Icons.Material.Filled.TableChart,
                ".xlsx" or ".xls" => Icons.Material.Filled.GridOn,
                ".txt" => Icons.Material.Filled.Description,
                _ => Icons.Material.Filled.InsertDriveFile
            };
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / 1048576.0:F1} MB";
        }

        private bool CanImport => fileContent != null
                          && !string.IsNullOrWhiteSpace(displayFileName)
                          && !string.IsNullOrWhiteSpace(selectedDevice)
                          && !isLoading
                          && !isViewer
                          && !uploadCompleted;

        private async Task OnBeforeNavigation(LocationChangingContext context)
        {
            if (isLoading) context.PreventNavigation();
        }
    }
}