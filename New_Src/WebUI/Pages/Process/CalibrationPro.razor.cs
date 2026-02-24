using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using System.Text.Json;
using WebUI.Services;

namespace WebUI.Pages.Process
{
    public partial class CalibrationPro
    {
        public class RMElement
        {
            public string Label { get; set; } = string.Empty;
            public double Orig { get; set; }
            public double Curr { get; set; }
            /// <summary>شماره گروه RM از لیبل (مثلاً RM 1-15 → 1) برای فیلتر Current RM Only.</summary>
            public int RmGroup { get; set; }
            /// <summary>ترتیب اکتساب برای مرتب‌سازی مثل پایتون.</summary>
            public int OriginalIndex { get; set; }
        }

        /// <summary>همه نقطه‌های RM برای المنت فعلی؛ جدول فقط زیرمجموعهٔ گروه RM فعلی را نشان می‌دهد.</summary>
        private List<RMElement> Elements { get; set; } = new()
        {
            new RMElement { Label = "RM 1-15", Orig = -0.04, Curr = -0.04, RmGroup = 1, OriginalIndex = 0 },
            new RMElement { Label = "RM 1-49", Orig = -0.98, Curr = -0.98, RmGroup = 1, OriginalIndex = 1 },
            new RMElement { Label = "RM 1-70", Orig = -1.41, Curr = -1.41, RmGroup = 1, OriginalIndex = 2 },
            new RMElement { Label = "RM 1-98", Orig = -0.29, Curr = -0.29, RmGroup = 1, OriginalIndex = 3 },
            new RMElement { Label = "RM 1-122", Orig = -0.17, Curr = -0.17, RmGroup = 1, OriginalIndex = 4 }
        };

        /// <summary>شماره‌های گروه RM یکتا و مرتب؛ Current RM = _rmGroupNumbers[_currentRmIndex].</summary>
        private List<int> _rmGroupNumbers { get; set; } = new() { 1 };

        // Element / file selection (top shared settings)
        private List<string> _elements = new();
        private string? _selectedElement;
        private List<string> _files = new();
        private string? _selectedFile = "All Files";

        /// <summary>ایندکس گروه RM فعلی (مثل پایتون: Current RM: 1, 2, 3, ...).</summary>
        private int _currentRmIndex = 0;

        // Shared preview parameters (blank / scale / filter)
        private double _previewBlank = 0.0;
        private double _previewScale = 1.0;
        private string _filterSolution = string.Empty;

        private bool _isLoading;

        private int? CurrentRmGroupNumber =>
            (_rmGroupNumbers != null && _currentRmIndex >= 0 && _currentRmIndex < _rmGroupNumbers.Count)
                ? _rmGroupNumbers[_currentRmIndex]
                : null;

        /// <summary>فقط نقطه‌های RM گروه فعلی — مطابق «RM Points — Current RM Only» در پایتون.</summary>
        private List<RMElement> VisibleRmPoints =>
            CurrentRmGroupNumber is int g
                ? Elements.Where(e => e.RmGroup == g).OrderBy(e => e.OriginalIndex).ToList()
                : new List<RMElement>();

        private string CurrentRmLabel =>
            CurrentRmGroupNumber is int n ? n.ToString() : "-";

        /// <summary>اولین عدد بعد از «RM» در لیبل = شماره گروه (مثلاً RM 1-15 → 1، RM 2-20 → 2).</summary>
        private static int ParseRmGroupFromLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return 1;
            var m = Regex.Match(label.Trim(), @"^RM\s*(\d+)", RegexOptions.IgnoreCase);
            return m.Success && int.TryParse(m.Groups[1].Value, out var num) ? num : 1;
        }

        /// <summary>برای نمایش یکسان مقادیر عددی در جدول RM Points.</summary>
        private static string FormatValue(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) ? "—" : value.ToString("0.00");

        /// <summary>نسبت Current/Original مثل پایتون؛ اگر Orig=0 مقدار N/A.</summary>
        private static string GetRatio(RMElement row) =>
            Math.Abs(row.Orig) < 1e-12 ? "N/A" : (row.Curr / row.Orig).ToString("0.00");

        /// <summary>برچسب «Next RM» در جدول فعلی (فقط گروه فعلی).</summary>
        private string GetNextRmLabel(RMElement context)
        {
            var visible = VisibleRmPoints;
            var idx = visible.IndexOf(context);
            if (idx < 0 || idx >= visible.Count - 1) return "N/A";
            return visible[idx + 1].Label;
        }

        private async Task LoadRmTableForCurrentElementAsync(Guid projectId)
        {
            if (string.IsNullOrWhiteSpace(_selectedElement))
                return;

            var request = new AdvancedPivotRequest(
                ProjectId: projectId,
                SelectedElements: new List<string> { _selectedElement },
                Page: 1,
                PageSize: 2000
            );

            var pivotResult = await PivotService.GetAdvancedPivotTableAsync(request);
            if (!pivotResult.Succeeded || pivotResult.Data is null)
                return;

            // مثل پایتون: فقط ردیف‌هایی که با "RM" شروع می‌شوند (نه CRM)
            // find_rm.py: pivot_df['Solution Label'].str.match(rf'^{keyword}', ...)
            var rmRows = pivotResult.Data.Rows
                .Where(r => !string.IsNullOrEmpty(r.SolutionLabel) &&
                    r.SolutionLabel.Trim().StartsWith("RM", StringComparison.OrdinalIgnoreCase) &&
                    !r.SolutionLabel.Trim().StartsWith("CRM", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (rmRows.Count == 0)
            {
                Elements = new List<RMElement>();
                _rmGroupNumbers = new List<int>();
                _currentRmIndex = 0;
                return;
            }

            // ترتیب اکتساب مثل پایتون (بر اساس originalIndex)
            var ordered = rmRows.OrderBy(r => r.OriginalIndex).ToList();

            Elements = ordered
                .Select((r, i) =>
                {
                    double value = 0;
                    if (r.Values != null &&
                        r.Values.TryGetValue(_selectedElement, out var v) &&
                        v.HasValue)
                    {
                        value = (double)v.Value;
                    }

                    int rmGroup = ParseRmGroupFromLabel(r.SolutionLabel);

                    return new RMElement
                    {
                        Label = r.SolutionLabel,
                        Orig = value,
                        Curr = value,
                        RmGroup = rmGroup,
                        OriginalIndex = r.OriginalIndex
                    };
                })
                .ToList();

            // لیست یکتای گروه‌ها برای PREVIOUS/NEXT RM (مثل پایتون)
            _rmGroupNumbers = Elements.Select(e => e.RmGroup).Distinct().OrderBy(x => x).ToList();
            _currentRmIndex = _rmGroupNumbers.Count > 0 ? 0 : -1;
        }

        protected override async Task OnInitializedAsync()
        {
            _isLoading = true;
            try
            {
                // اگر پروژه‌ای انتخاب نشده باشد، همان داده‌های نمایشی اولیه استفاده می‌شوند
                if (ProjectService.CurrentProjectId is Guid projectId)
                {
                    // عناصر و RM‌ها را شبیه منطق پایتون از Pivot می‌خوانیم
                    var request = new AdvancedPivotRequest(
                        ProjectId: projectId,
                        Page: 1,
                        PageSize: 2000
                    );

                    var pivotResult = await PivotService.GetAdvancedPivotTableAsync(request);
                    if (pivotResult.Succeeded && pivotResult.Data is not null)
                    {
                        _elements = pivotResult.Data.Metadata?.AllElements ?? new List<string>();
                        _selectedElement ??= _elements.FirstOrDefault();

                        // فایل‌ها فقط برای نمایش drop-down؛ فعلاً از متادیتا می‌خوانیم اگر موجود بود
                        _files = new List<string> { "All Files" };

                        if (_selectedElement is not null)
                        {
                            await LoadRmTableForCurrentElementAsync(projectId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing CalibrationPro page");
                Snackbar.Add("Failed to load calibration data.", Severity.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        // --- Top shared settings actions (Python parity style) ---

        private void OnFileChanged(string? value)
        {
            _selectedFile = value;
            // در نسخه‌ی وب فعلاً فقط فیلتر نمایشی است؛ منطق دقیق per-file
            // مشابه پایتون در سرور پیاده‌سازی خواهد شد.
        }

        private async Task OnElementChanged(string? value)
        {
            _selectedElement = value;

            if (ProjectService.CurrentProjectId is Guid projectId &&
                !string.IsNullOrWhiteSpace(_selectedElement))
            {
                await LoadRmTableForCurrentElementAsync(projectId);
                StateHasChanged();
            }
        }

        private void OnFilterSolutionChanged(string value)
        {
            _filterSolution = value ?? string.Empty;
            // این فیلتر بعداً برای جدول‌های پشتی (pivot) و نمودارها استفاده می‌شود
            // مشابه handler_apply_solution_filter در نسخه‌ی پایتونی.
        }

        private void PrevRm()
        {
            if (_rmGroupNumbers == null || _rmGroupNumbers.Count == 0)
                return;
            if (_currentRmIndex > 0)
                _currentRmIndex--;
        }

        private void NextRm()
        {
            if (_rmGroupNumbers == null || _rmGroupNumbers.Count == 0)
                return;
            if (_currentRmIndex < _rmGroupNumbers.Count - 1)
                _currentRmIndex++;
        }

        private void ResetAll()
        {
            foreach (var el in Elements)
            {
                el.Curr = el.Orig;
            }

            _previewBlank = 0.0;
            _previewScale = 1.0;
            _filterSolution = string.Empty;

            Snackbar.Add("All calibration changes reset to original values.", Severity.Info);
        }

        private async Task RunCalibrationAsync()
        {
            // این متد معادل run_calibration / start_check_rm_thread در پایتون است.
            // در وب نسخه‌ی کامل نیاز به API سمت سرور دارد؛ فعلاً یک placeholder است
            // که بعداً به endpoint واقعی متصل می‌شود.
            await Task.Yield();
            Snackbar.Add("Calibration run is not implemented on the server yet.", Severity.Warning);
        }

        // --- CRM verification preview (blank / scale) ---

        private void UpdatePreviewBlank(string value)
        {
            if (!double.TryParse(value, out _previewBlank))
            {
                _previewBlank = 0.0;
            }
            // در نسخه‌ی کامل اینجا باید نمودارها و جداول به‌روزرسانی شوند
            // مشابه update_preview_params در کد پایتون.
        }

        private void UpdatePreviewScale(double value)
        {
            _previewScale = value;
            // در نسخه‌ی کامل اینجا باید نمودارها و جداول به‌روزرسانی شوند.
        }

        private void ResetBlankAndScale()
        {
            _previewBlank = 0.0;
            _previewScale = 1.0;
            Snackbar.Add("Blank and scale set to defaults.", Severity.Info);
        }
    }
}