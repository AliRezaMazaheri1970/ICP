using Application.DTOs;
using Application.Services; // این خط برای رفع خطای CS1061 ضروری است
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using WebUI.Services;

namespace WebUI.Pages.App
{
    public partial class Import
    {
        private IBrowserFile? selectedFile;
        private byte[]? fileContent;
        private string fileName = "";
        private string fileContentType = "";
        private long fileSizeBytes;

        private bool isViewer = false;
        private bool isLoading;
        private bool showAdvancedOptions = false;

        private string projectName = "";
        private string selectedDevice = "";
        private string selectedFileType = "";

        private string delimiter = ",";
        private int headerRow = 1;
        private bool skipLastRow = true;
        private bool autoDetectType = true;

        private List<string> deviceOptions = new()
        {
            "Mass elan9000 1",
            "Mass elan9000 2",
            "OES 715",
            "OES 735 1",
            "OES 735 2"
        };

        private List<string> fileTypeOptions = new()
        {
            "oes 4cc",
            "oes 6cc",
            "txt format",
            "xlsx format"
        };
        private AnalysisPreviewResult? analysisData;

        //private List<RecentImport> recentImports = new();



        private string fileIcon => GetFileIcon();
        private string fileSize => fileSizeBytes > 0 ? FormatFileSize(fileSizeBytes) : "";

        private bool CanImport => fileContent != null
                          // && !string.IsNullOrWhiteSpace(projectName)  <-- این خط را حذف کنید
                          && !string.IsNullOrWhiteSpace(selectedDevice)
                          && !isLoading
                          && !isViewer;

        protected override void OnInitialized()
        {
            var user = AuthService.GetCurrentUser();
            if (user == null || !user.IsAuthenticated)
            {
                NavManager.NavigateTo("/login");
                return;
            }

            if (string.Equals(user.Position, "Viewer", StringComparison.OrdinalIgnoreCase))
            {
                isViewer = true;
            }
            selectedDevice = deviceOptions.FirstOrDefault() ?? "";
            selectedFileType = fileTypeOptions.FirstOrDefault() ?? "";
        }

        private void GoToDashboard() => NavManager.NavigateTo("/dashboard");

        private string GetDropZoneClass()
        {
            return "d-flex flex-column align-center justify-center";
            //return fileContent != null
            //    ? "d-flex flex-column align-center justify-center border-success"
            //    : "d-flex flex-column align-center justify-center";
        }

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
                fileContentType = file.ContentType ?? "application/octet-stream";
                fileSizeBytes = file.Size;

                using var ms = new MemoryStream();
                await file.OpenReadStream(maxAllowedSize: 200 * 1024 * 1024).CopyToAsync(ms);
                fileContent = ms.ToArray();

                //if (string.IsNullOrWhiteSpace(projectName))
                //{
                //    projectName = Path.GetFileNameWithoutExtension(fileName);
                //}

                await DoPreview();
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

        private void ClearFile()
        {
            selectedFile = null;
            fileContent = null;
            fileName = "";
            fileContentType = "";
            fileSizeBytes = 0;
            analysisData = null; // پاک کردن نتایج آنالیز
        }

        //private async Task PreviewFile()
        //{
        //    await DoPreview();
        //}

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
                    // previewData = null; // اگر متغیر previewData هنوز در کد هست آن نال کنید
                    Snackbar.Add("Analysis completed", Severity.Success);
                }
                else
                {
                    // اصلاح خطای Message
                    var errorMsg = result.Messages?.FirstOrDefault() ?? "Analysis failed";
                    Snackbar.Add(errorMsg, Severity.Error);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error: {ex.Message}", Severity.Error);
            }
        }

        private async Task ReselectFile(InputFileChangeEventArgs e)
        {
            // همان منطق انتخاب فایل
            await OnInputFileChangeStandard(e);
        }

        private async Task ImportFile()
        {
            if (isViewer)
            {
                Snackbar.Add("Viewers cannot import data.", Severity.Error);
                return;
            }

            if (fileContent == null || string.IsNullOrWhiteSpace(projectName)) return;

            isLoading = true;
            StateHasChanged();

            try
            {
                var user = AuthService.GetCurrentUser();
                using var stream = new MemoryStream(fileContent);
                var finalProjectName = string.IsNullOrWhiteSpace(projectName)
                            ? Path.GetFileNameWithoutExtension(fileName)
                            : projectName;

                var request = new AdvancedImportRequest(projectName, user?.Name)
                {
                    SkipLastRow = skipLastRow
                };

                // 3. فراخوانی متد با امضای صحیح (Stream, fileName, Request)
                var result = await ImportService.ImportAdvancedAsync(
                    stream,
                    fileName,
                    request);

                if (result.Succeeded)
                {
                    //Snackbar.Add($"Project '{projectName}' imported!", Severity.Success);
                    //recentImports.Insert(0, new RecentImport { Name = projectName, Status = "Success" });
                    //if (recentImports.Count > 5) recentImports.RemoveAt(5);
                    //ClearFile();
                    //projectName = "";
                    Snackbar.Add($"Project '{projectName}' imported successfully!", Severity.Success);
                    ClearFile(); // بازگشت به صفحه اول
                    projectName = "";
                }
                else
                {
                    // اصلاح خطای Message: استفاده از Messages.FirstOrDefault()
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

        //private class RecentImport
        //{
        //    public string Name { get; set; } = "";
        //    public string Status { get; set; } = "";
        //}

        private async Task OnBeforeNavigation(LocationChangingContext context)
        {
            if (isLoading)
            {
                context.PreventNavigation();
            }
        }
    }
}