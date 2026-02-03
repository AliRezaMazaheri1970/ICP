using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using MudBlazor;
using WebUI.Services;
using System;

namespace WebUI.Pages.Process
{
    public partial class CrmCalibration
    {
        [SupplyParameterFromQuery]
        public Guid? projectId { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; } = default!;

        // Chart references
        private ElementReference chart1Canvas;
        private ElementReference chart2Canvas;
        private IJSObjectReference? chartModule;

        private Guid? _projectId;
        private decimal _minDiff = -10m;
        private decimal _maxDiff = 10m;
        private int _maxIterations = 100;
        private int _populationSize = 50;
        private bool _useMultiModel = true;
        private bool _detailsExpanded = true;
        private IEnumerable<string> _selectedElements = new HashSet<string>();
        private List<string> _allElements = new();
        private string? _focusElement;
        private decimal _previewBlank = 0m;
        private double _previewScale = 1.0;
        private string _sampleFilter = "";
        private BlankScaleOptimizationResult? _result;
        private ManualBlankScaleResult? _manualResult;
        private List<OptimizedSampleRow> _optimizedRows = new();
        private List<OptimizedSampleRow> _manualRows = new();
        private bool _isLoading = false;
        private string? _projectName;
        private List<CrmMethodOptionDto> _crmOptions = new();
        private Dictionary<string, string> _crmSelections = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _includedCrmIds = new(StringComparer.OrdinalIgnoreCase);
        private string _excludedLabelsInput = string.Empty;
        private List<CrmSelectionRowDto> _crmSelectionRows = new();

        // UI toggles to match Python controls
        private bool _showCertified = true;
        private bool _showCrm = true;
        private bool _showVerification = true;
        private bool _showAcceptableRange = true;

        // Scale Application Range (Python feature)
        private decimal? _scaleRangeMin;
        private decimal? _scaleRangeMax;
        private bool _scaleAbove50Only = false;

        // Acceptable Ranges (Python feature - magnitude-based thresholds)
        private decimal _rangeLow = 2.0m;     // |x| < 10: absolute ±
        private decimal _rangeMid = 20.0m;    // 10 ≤ |x| < 100: percentage
        private decimal _rangeHigh1 = 10.0m;  // 100 ≤ |x| < 1000: percentage
        private decimal _rangeHigh2 = 8.0m;   // 1000 ≤ |x| < 10000: percentage
        private decimal _rangeHigh3 = 5.0m;   // 10000 ≤ |x| < 100000: percentage
        private decimal _rangeHigh4 = 3.0m;   // |x| ≥ 100000: percentage
        private bool _rangesDialogVisible = false;

        // Details panel UX state
        private int _detailsTabIndex = 0;
        private bool _detailsMaximized = false;

        // Results tabs state
        private int _resultsTabIndex = 0;

        // Pivot tab state
        private PivotValueMode _pivotMode = PivotValueMode.Crm;
        private HashSet<string> _pivotSelectedElements = new(StringComparer.OrdinalIgnoreCase);
        private List<PivotRowVm> _pivotRows = new();

        private bool _showAllDataInPivot = false;

        private enum PivotValueMode
        {
            Original,
            Optimized,
            Crm,
            DiffAfter
        }


        private enum PivotRowType
        {
            Sample,      // CRM 258 A
            CrmRef,      // OREAS 258 ... CRM
            DiffAfter    // CRM 258 A Diff (%)
        }

        private sealed class PivotRowVm
        {
            public int Order { get; set; }
            public string SolutionLabel { get; set; } = "";
            public PivotRowType RowType { get; set; }
            public string? CrmId { get; set; }
            public Dictionary<string, decimal?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private void ToggleDetailsMaximize()
        {
            _detailsMaximized = !_detailsMaximized;
            StateHasChanged();
        }

        private int FilteredManualCount() => FilterRows(_manualRows).Count();
        private int FilteredOptimizedCount() => FilterRows(_optimizedRows).Count();

        private static string FormatDec(decimal? v)
        {
            if (v == null) return "-";
            // اگر می‌خواهی تعداد اعشار دقیق‌تر باشد، اینجا تنظیم کن
            return v.Value.ToString("0.####");
        }

        private IEnumerable<string> PivotColumns()
        {
            // اگر کاربر چیزی انتخاب نکرد، حداقل FocusElement را نشان بده
            if (_pivotSelectedElements.Count == 0 && !string.IsNullOrWhiteSpace(_focusElement))
                return new[] { _focusElement! };

            // تعداد ستون‌ها را برای UX محدود کن (مثلاً 12)
            //return _pivotSelectedElements.Take(12);
            return _pivotSelectedElements;
        }

        private async Task RebuildPivot()
        {
            if (!_projectId.HasValue) return;
            _isLoading = true;
            StateHasChanged();

            try
            {
                // لود کل دیتای پروژه (854 ردیف) از سرویس اصلی پیوت
                var request = new AdvancedPivotRequest(
                    ProjectId: _projectId.Value,
                    SearchText: _sampleFilter,
                    SelectedElements: _allElements.ToList(), // Argument 4
                    NumberFilters: null,                    // Argument 5
                    UseOxide: false,                        // Argument 6
                    UseInt: false,                          // Argument 7
                    DecimalPlaces: 4,                       // Argument 8
                    Page: 1,                                // Argument 9
                    PageSize: 2000,                         // لود کامل برای اسکرول (Argument 10)
                    MergeRepeats: false,                    // Argument 11
                    Aggregation: "First"                    // Argument 12
                );

                var result = await PivotService.GetAdvancedPivotTableAsync(request);

                if (result.Succeeded && result.Data != null)
                {
                    var cols = PivotColumns().ToList();
                    var rows = new List<PivotRowVm>();
                    int order = 0;

                    // دسترسی به دیتای محدود شده CRMها
                    var optimizedData = _manualResult?.OptimizedData ?? _result?.OptimizedData;

                    foreach (var s in result.Data.Rows)
                    {
                        // 1. اضافه کردن ردیف اصلی نمونه (برای همه 854 ردیف)
                        rows.Add(new PivotRowVm
                        {
                            Order = order++,
                            SolutionLabel = s.SolutionLabel,
                            RowType = PivotRowType.Sample,
                            Values = s.Values
                        });

                        // 2. اگر این ردیف جزو CRMهای کالیبراسیون بود، ردیف‌های مرجع و Diff را تزریق کن
                        var crmMatch = optimizedData?.FirstOrDefault(x => x.SolutionLabel == s.SolutionLabel);
                        if (crmMatch != null && !string.IsNullOrEmpty(crmMatch.CrmId))
                        {
                            // ردیف زرد (Reference)
                            rows.Add(new PivotRowVm
                            {
                                Order = order++,
                                SolutionLabel = $"{crmMatch.CrmId} CRM",
                                RowType = PivotRowType.CrmRef,
                                Values = BuildDictValues(crmMatch.CrmValues, cols)
                            });

                            // ردیف صورتی (Diff %)
                            rows.Add(new PivotRowVm
                            {
                                Order = order++,
                                SolutionLabel = $"{s.SolutionLabel} Diff (%)",
                                RowType = PivotRowType.DiffAfter,
                                Values = BuildDiffValues(crmMatch.DiffPercentAfter, cols)
                            });
                        }
                    }
                    _pivotRows = rows;
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error rebuilding pivot: {ex.Message}", Severity.Error);
            }
            finally
            {
                _isLoading = false;
                StateHasChanged();
            }
        }

        private Dictionary<string, decimal?> BuildSampleValues(OptimizedSampleDto s, List<string> cols)
        {
            var dict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);

            foreach (var el in cols)
            {
                decimal? v = null;

                // اینجا تعیین می‌کنیم Sample Row مقدار Original باشد یا Optimized
                // (پیشنهاد: فقط Original/Optimized را برای _pivotMode نگه دار)
                if (_pivotMode == PivotValueMode.Original)
                    s.OriginalValues.TryGetValue(el, out v);
                else
                    s.OptimizedValues.TryGetValue(el, out v);

                dict[el] = v;
            }

            return dict;
        }

        private Dictionary<string, decimal?> BuildDictValues(Dictionary<string, decimal?> source, List<string> cols)
        {
            var dict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            foreach (var el in cols)
                dict[el] = source.TryGetValue(el, out var v) ? v : null;
            return dict;
        }

        private Dictionary<string, decimal?> BuildDiffValues(Dictionary<string, decimal> source, List<string> cols)
        {
            var dict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            foreach (var el in cols)
                dict[el] = source.TryGetValue(el, out var v) ? v : null;
            return dict;
        }

        private Task OnPivotElementsChanged(IEnumerable<string> values)
        {
            _pivotSelectedElements = new HashSet<string>(values ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            RebuildPivot();
            StateHasChanged();
            return Task.CompletedTask;
        }

        private Task OnPivotModeChanged(PivotValueMode mode)
        {
            _pivotMode = mode;
            RebuildPivot();
            StateHasChanged();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when result tabs change - re-render charts if Tab2 is selected
        /// </summary>
        private async Task OnResultsTabChanged(int newTabIndex)
        {
            _resultsTabIndex = newTabIndex;

            // If Tab2 (index 1) is selected and we have results, re-render charts
            if (newTabIndex == 1 && _result != null)
            {
                await Task.Delay(100); // Wait for tab animation
                await RenderChartsAsync();
            }
        }

        private void ResetPivotColumns()
        {
            _pivotSelectedElements.Clear();
            if (!string.IsNullOrWhiteSpace(_focusElement))
                _pivotSelectedElements.Add(_focusElement!);

            RebuildPivot();
        }


        private void UpdateOptimizedRows()
        {
            _optimizedRows = BuildRows(_result?.OptimizedData, _focusElement);
            RebuildPivot();
        }

        private void UpdateManualRows()
        {
            _manualRows = BuildRows(_manualResult?.OptimizedData, _focusElement);
            RebuildPivot();
        }

        private async Task SetFocusElement(string? element)
        {
            if (string.IsNullOrWhiteSpace(element))
                return;

            _focusElement = element;
            UpdateOptimizedRows();
            UpdateManualRows();

            // اگر user هنوز ستون انتخاب نکرده بود، focus رو ستون اول کن
            if (_pivotSelectedElements.Count == 0)
            {
                _pivotSelectedElements.Add(_focusElement);
                RebuildPivot();
            }

            // Ensure UI updates and then refresh charts that depend on focus element
            StateHasChanged();
            await Task.Delay(50);

            // Re-render the calibration scatter and update the element improvement metric
            await RefreshCalibrationAsync();
            await UpdateElementImprovementChartAsync();
            StateHasChanged();
        }

        protected override async Task OnInitializedAsync()
        {
            _projectId = projectId ?? ProjectService.CurrentProjectId;
            if (!_projectId.HasValue)
                return;

            var projectResult = await ProjectService.GetProjectAsync(_projectId.Value);
            if (projectResult.Succeeded && projectResult.Data != null)
            {
                _projectName = projectResult.Data.ProjectName;
            }
            else if (!string.IsNullOrWhiteSpace(projectResult.Message))
            {
                Snackbar.Add(projectResult.Message, Severity.Warning);
            }

            await LoadElements();
            await LoadCrmOptions();
            await LoadCrmSelections();
            //await GetCurrentStats();
        }

        private async Task LoadCrmOptions()
        {
            if (_projectId == null) return;

            var result = await OptimizationService.GetCrmOptionsAsync(_projectId.Value);
            if (result.Succeeded && result.Data != null)
            {
                _crmOptions = result.Data.Items;
                _crmSelections.Clear();
                _includedCrmIds.Clear();

                foreach (var option in _crmOptions)
                {
                    if (!string.IsNullOrWhiteSpace(option.DefaultMethod))
                    {
                        _crmSelections[option.CrmId] = option.DefaultMethod!;
                    }
                    _includedCrmIds.Add(option.CrmId);
                }
            }
            else if (!string.IsNullOrWhiteSpace(result.Message))
            {
                Snackbar.Add(result.Message, Severity.Warning);
            }
        }

        private async Task LoadCrmSelections()
        {
            if (_projectId == null) return;

            var result = await OptimizationService.GetCrmSelectionOptionsAsync(_projectId.Value);
            if (result.Succeeded && result.Data != null)
            {
                _crmSelectionRows = result.Data.Items;
            }
            else if (!string.IsNullOrWhiteSpace(result.Message))
            {
                Snackbar.Add(result.Message, Severity.Warning);
            }
        }

        private List<string> GetRowOptions(CrmSelectionRowDto row)
        {
            var options = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var opt in row.PreferredOptions)
            {
                if (seen.Add(opt))
                    options.Add(opt);
            }

            foreach (var opt in row.AllOptions)
            {
                if (seen.Add(opt))
                    options.Add(opt);
            }

            if (!string.IsNullOrWhiteSpace(row.SelectedOption) && seen.Add(row.SelectedOption))
                options.Insert(0, row.SelectedOption);

            return options;
        }

        private EventCallback<string> GetRowSelectionChangedHandler(CrmSelectionRowDto row)
        {
            return EventCallback.Factory.Create<string>(this, v => SaveRowSelectionAsync(row, v));
        }

        private async Task SaveRowSelectionAsync(CrmSelectionRowDto row, string? selected)
        {
            if (_projectId == null || string.IsNullOrWhiteSpace(selected))
                return;

            row.SelectedOption = selected;

            var request = new CrmSelectionSaveRequest
            {
                ProjectId = _projectId.Value,
                Selections = new List<CrmSelectionItemDto>
                {
                    new CrmSelectionItemDto
                    {
                        SolutionLabel = row.SolutionLabel,
                        RowIndex = row.RowIndex,
                        SelectedCrmKey = selected
                    }
                }
            };

            var result = await OptimizationService.SaveCrmSelectionsAsync(request);
            if (!result.Succeeded)
            {
                Snackbar.Add(result.Message ?? "Failed to save CRM selection", Severity.Error);
            }
        }

        private string? GetCrmSelection(string crmId)
        {
            return _crmSelections.TryGetValue(crmId, out var method) ? method : null;
        }

        private void SetCrmSelection(string crmId, string? method)
        {
            if (string.IsNullOrWhiteSpace(method))
            {
                _crmSelections.Remove(crmId);
                return;
            }

            _crmSelections[crmId] = method;
        }

        private void ToggleIncludedCrmId(string crmId, bool isIncluded)
        {
            if (isIncluded)
                _includedCrmIds.Add(crmId);
            else
                _includedCrmIds.Remove(crmId);
        }

        private List<string> ParseExcludedLabels()
        {
            if (string.IsNullOrWhiteSpace(_excludedLabelsInput))
                return new List<string>();

            return _excludedLabelsInput
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task LoadElements()
        {
            var result = await PivotService.GetElementsAsync(_projectId!.Value);
            if (result.Succeeded && result.Data != null)
            {
                _allElements = result.Data;
                if (_allElements.Count > 0 && string.IsNullOrWhiteSpace(_focusElement))
                {
                    _focusElement = _allElements[0];
                }
            }
            else
            {
                Snackbar.Add(result.Message ?? "Failed to load elements", Severity.Error);
            }
        }

        private async Task GetCurrentStats()
        {
            if (_projectId == null) return;

            _isLoading = true;
            StateHasChanged();

            var result = await OptimizationService.GetCurrentStatsAsync(_projectId.Value, _minDiff, _maxDiff);

            if (result.Succeeded && result.Data != null)
            {
                _result = result.Data;
                UpdateOptimizedRows();
                StateHasChanged(); // Ensure UI updates before rendering charts
                await Task.Delay(150); // Wait for DOM to update
                await RenderChartsAsync();
                StateHasChanged(); // Refresh UI after charts are rendered
            }
            else
            {
                Snackbar.Add(result.Message ?? "Failed to get stats", Severity.Error);
            }

            _isLoading = false;
            StateHasChanged();
        }

        /// <summary>
        /// دکمه Calibration - برای کالیبریشن فوری و نمایش نمودارها
        /// </summary>
        private async Task RunCalibration()
        {
            if (_projectId == null) return;

            _isLoading = true;
            StateHasChanged();

            Snackbar.Add("Starting Calibration...", Severity.Info);

            var result = await OptimizationService.GetCurrentStatsAsync(_projectId.Value, _minDiff, _maxDiff);

            if (result.Succeeded && result.Data != null)
            {
                _result = result.Data;
                UpdateOptimizedRows();

                // تغییر به Tab2 برای نمایش نمودارها
                _resultsTabIndex = 1;

                StateHasChanged(); // Ensure UI updates before rendering charts
                await Task.Delay(250); // Wait for tab animation and DOM to update
                await RenderChartsAsync();
                StateHasChanged(); // Refresh UI after charts are rendered

                Snackbar.Add($"Calibration Complete! Improvement: {_result.ImprovementPercent:F1}%", Severity.Success);
            }
            else
            {
                Snackbar.Add(result.Message ?? "Calibration failed", Severity.Error);
            }

            _isLoading = false;
            StateHasChanged();
        }

        private async Task RunOptimization()
        {
            if (_projectId == null) return;

            _isLoading = true;
            StateHasChanged();

            if (_crmSelectionRows.Any(r => string.IsNullOrWhiteSpace(r.SelectedOption)))
            {
                Snackbar.Add("Please select CRM method for all CRM rows before optimization.", Severity.Warning);
                _isLoading = false;
                return;
            }

            var request = new BlankScaleOptimizationRequest
            {
                ProjectId = _projectId.Value,
                MinDiffPercent = _minDiff,
                MaxDiffPercent = _maxDiff,
                MaxIterations = _maxIterations,
                PopulationSize = _populationSize,
                UseMultiModel = _useMultiModel,
                Elements = _selectedElements.Any() ? _selectedElements.ToList() : null,
                // Acceptable Ranges (Python: calculate_dynamic_range)
                RangeLow = _rangeLow,
                RangeMid = _rangeMid,
                RangeHigh1 = _rangeHigh1,
                RangeHigh2 = _rangeHigh2,
                RangeHigh3 = _rangeHigh3,
                RangeHigh4 = _rangeHigh4,
                // Scale Application Range
                ScaleRangeMin = _scaleRangeMin,
                ScaleRangeMax = _scaleRangeMax,
                ScaleAbove50Only = _scaleAbove50Only,
                CrmSelections = _crmSelections.Count > 0 ? new Dictionary<string, string>(_crmSelections) : null,
                IncludedCrmIds = _includedCrmIds.Count > 0 ? _includedCrmIds.ToList() : null,
                ExcludedSolutionLabels = ParseExcludedLabels()
            };

            var result = await OptimizationService.OptimizeAsync(request);

            if (result.Succeeded && result.Data != null)
            {
                _result = result.Data;
                UpdateOptimizedRows();
                StateHasChanged(); // Ensure UI updates before rendering charts
                await Task.Delay(150); // Wait for DOM to update
                await RenderChartsAsync();
                StateHasChanged(); // Refresh UI after charts are rendered
                Snackbar.Add($"Optimization complete! Improvement: {_result.ImprovementPercent:F1}%", Severity.Success);
            }
            else
            {
                Snackbar.Add(result.Message ?? "Optimization failed", Severity.Error);
            }

            _isLoading = false;
            StateHasChanged();
        }


        private async Task OnBeforeNavigation(LocationChangingContext context)
        {
            if (_isLoading)
            {
                context.PreventNavigation();
            }
        }

        private async Task PreviewManualAsync()
        {
            if (_projectId == null || string.IsNullOrWhiteSpace(_focusElement))
            {
                Snackbar.Add("Please select a Project and Focus Element first.", Severity.Warning);
                return;
            }

            _isLoading = true;
            StateHasChanged();

            try
            {
                var result = await OptimizationService.PreviewManualDetailsAsync(
                    _projectId.Value,
                    _focusElement,
                    _previewBlank,
                    (decimal)_previewScale);

                if (result.Succeeded && result.Data != null)
                {
                    _manualResult = result.Data;
                    UpdateManualRows(); // این متد لیست _manualRows را پر می‌کند

                    _detailsExpanded = true; // اجبار به باز شدن پنل
                    _result = _result ?? new BlankScaleOptimizationResult(); // یک مقدار غیر نال موقت برای عبور از شرط نمایش
                }
                else
                {
                    Snackbar.Add(result.Message ?? "Preview failed", Severity.Error);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error: {ex.Message}", Severity.Error);
            }
            finally
            {
                _isLoading = false;
                StateHasChanged(); // رندر مجدد صفحه
            }
        }

        private async Task ApplyManualAsync()
        {
            if (_projectId == null || string.IsNullOrWhiteSpace(_focusElement))
                return;

            _isLoading = true;
            StateHasChanged();

            var result = await OptimizationService.ApplyManualAsync(
                _projectId.Value,
                _focusElement,
                _previewBlank,
                (decimal)_previewScale);

            if (result.Succeeded && result.Data != null)
            {
                _manualResult = result.Data;
                UpdateManualRows();
                Snackbar.Add("Manual blank/scale applied.", Severity.Success);
            }
            else
            {
                Snackbar.Add(result.Message ?? "Apply failed", Severity.Error);
            }

            _isLoading = false;
        }

        private async Task UndoManualAsync()
        {
            if (_projectId == null)
                return;

            _isLoading = true;
            StateHasChanged();

            var result = await CorrectionService.UndoLastCorrectionAsync(_projectId.Value);
            if (result.Succeeded)
            {
                Snackbar.Add("Undo applied.", Severity.Success);
                await GetCurrentStats();
            }
            else
            {
                Snackbar.Add(result.Message ?? "Undo failed", Severity.Error);
            }

            _isLoading = false;
        }

        private void ResetPreview()
        {
            _previewBlank = 0m;
            _previewScale = 1.0;
        }

        //private void SetFocusElement(string? element)
        //{
        //    if (string.IsNullOrWhiteSpace(element))
        //        return;

        //    _focusElement = element;
        //    UpdateOptimizedRows();
        //    UpdateManualRows();
        //}

        private async Task PrevElement()
        {
            if (_allElements.Count == 0 || string.IsNullOrWhiteSpace(_focusElement))
                return;

            var idx = _allElements.IndexOf(_focusElement);
            if (idx > 0)
                await SetFocusElement(_allElements[idx - 1]);
        }

        private async Task NextElement()
        {
            if (_allElements.Count == 0 || string.IsNullOrWhiteSpace(_focusElement))
                return;

            var idx = _allElements.IndexOf(_focusElement);
            if (idx < _allElements.Count - 1)
                await SetFocusElement(_allElements[idx + 1]);
        }

        //private void UpdateOptimizedRows()
        //{
        //    _optimizedRows = BuildRows(_result?.OptimizedData, _focusElement);
        //}

        //private void UpdateManualRows()
        //{
        //    _manualRows = BuildRows(_manualResult?.OptimizedData, _focusElement);
        //}

        private List<OptimizedSampleRow> BuildRows(IEnumerable<OptimizedSampleDto>? data, string? element)
        {
            if (data == null || string.IsNullOrWhiteSpace(element))
                return new List<OptimizedSampleRow>();

            var rows = new List<OptimizedSampleRow>();
            foreach (var sample in data)
            {
                sample.OriginalValues.TryGetValue(element, out var original);
                sample.OptimizedValues.TryGetValue(element, out var optimized);
                sample.CrmValues.TryGetValue(element, out var crmValue);
                sample.DiffPercentBefore.TryGetValue(element, out var diffBefore);
                sample.DiffPercentAfter.TryGetValue(element, out var diffAfter);
                var passed = sample.PassStatusAfter.TryGetValue(element, out var p) && p;

                if (original == null && optimized == null && crmValue == null)
                    continue;

                rows.Add(new OptimizedSampleRow(
                    sample.SolutionLabel,
                    sample.CrmId,
                    element,
                    original,
                    optimized,
                    crmValue,
                    diffBefore,
                    diffAfter,
                    passed));
            }

            return rows;
        }

        private IEnumerable<OptimizedSampleRow> FilterRows(IEnumerable<OptimizedSampleRow> rows)
        {
            if (string.IsNullOrWhiteSpace(_sampleFilter))
                return rows;

            return rows.Where(r =>
                r.SolutionLabel.Contains(_sampleFilter, StringComparison.OrdinalIgnoreCase));
        }

        private sealed record OptimizedSampleRow(
            string SolutionLabel,
            string CrmId,
            string Element,
            decimal? OriginalValue,
            decimal? OptimizedValue,
            decimal? CrmValue,
            decimal DiffBefore,
            decimal DiffAfter,
            bool IsPassed);

        /// <summary>
        /// Opens the Acceptable Ranges dialog (matches Python's open_range_dialog)
        /// </summary>
        private void OpenRangesDialog()
        {
            _rangesDialogVisible = true;
        }

        /// <summary>
        /// Closes the Acceptable Ranges dialog
        /// </summary>
        private void CloseRangesDialog()
        {
            _rangesDialogVisible = false;
        }

        /// <summary>
        /// Applies the ranges and refreshes statistics
        /// </summary>
        private async Task ApplyRangesAsync()
        {
            _rangesDialogVisible = false;
            await GetCurrentStats();
            Snackbar.Add("Acceptable ranges updated", Severity.Success);
        }

        /// <summary>
        /// Resets the ranges to default values
        /// </summary>
        private void ResetRanges()
        {
            _rangeLow = 2.0m;
            _rangeMid = 20.0m;
            _rangeHigh1 = 10.0m;
            _rangeHigh2 = 8.0m;
            _rangeHigh3 = 5.0m;
            _rangeHigh4 = 3.0m;
        }


        private string GetSampleDetailsTitle()
        {
            if (_manualRows.Any())
                return "Manual Preview Details";
            return "Optimized Sample Details";
        }

        private string GetImprovementCardClass()
        {
            var baseClass = "summary-card";
            var statusClass = _result?.ImprovementPercent >= 0 ? "success" : "error";
            return $"{baseClass} {statusClass}";
        }

        // ==========================================
        // Chart Rendering Methods for Tab2
        // ==========================================

        /// <summary>
        /// Renders both charts on Tab2
        /// </summary>
        private async Task RenderChartsAsync()
        {
            if (_result == null) return;

            try
            {
                // Clear existing charts first
                await JSRuntime.InvokeVoidAsync("destroyChart", "elementChart");
                await JSRuntime.InvokeVoidAsync("destroyChart", "calibrationChart");

                // Wait for DOM to be fully ready
                await Task.Delay(200);

                // Chart 1: Element-wise Improvement (top)
                await RenderElementImprovementChartAsync();
                await Task.Delay(100);

                // Chart 2: Calibration scatter (per CRM) - mirrors Python plot_calib (bottom)
                await RenderCalibrationChartAsync();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error rendering charts: {ex.Message}", Severity.Error);
                Console.WriteLine($"Error rendering charts: {ex.Message}");
            }
        }

        /// <summary>
        /// Renders the Pass/Fail statistics chart
        /// </summary>
        private async Task RenderPassFailChartAsync()
        {
            try
            {
                var chartData = GetPassFailChartData();

                var chartConfig = new
                {
                    type = "bar",
                    data = chartData,
                    options = new
                    {
                        responsive = true,
                        maintainAspectRatio = false,
                        animation = new
                        {
                            duration = 750,
                            easing = "easeInOutQuart"
                        },
                        plugins = new
                        {
                            legend = new { display = true, position = "top" },
                            tooltip = new { backgroundColor = "rgba(0,0,0,0.7)" }
                        },
                        scales = new
                        {
                            y = new { beginAtZero = true }
                        }
                    }
                };

                // Create chart via JS interop
                await JSRuntime.InvokeVoidAsync("createChart", "passFailChart", chartConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rendering pass/fail chart: {ex.Message}");
            }
        }

        /// <summary>
        /// Renders the Element-wise improvement chart
        /// </summary>
        private async Task RenderElementImprovementChartAsync()
        {
            try
            {
                var chartData = GetElementImprovementChartData();

                var chartConfig = new
                {
                    type = "line",
                    data = chartData,
                    options = new
                    {
                        responsive = true,
                        maintainAspectRatio = false,
                        animation = new
                        {
                            duration = 750,
                            easing = "easeInOutQuart"
                        },
                        plugins = new
                        {
                            legend = new { display = true, position = "top" },
                            tooltip = new { backgroundColor = "rgba(0,0,0,0.7)" }
                        },
                        scales = new
                        {
                            y = new
                            {
                                // اصلاح مهم: حذف max = 100 و ticks ثابت
                                // این کار باعث می‌شود نمودار مثل پایتون بر اساس داده‌ها اسکیل شود
                                beginAtZero = true
                            }
                        }
                    }
                };

                // Create chart via JS interop
                await JSRuntime.InvokeVoidAsync("createChart", "elementChart", chartConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rendering element improvement chart: {ex.Message}");
            }
        }
        // ==========================================
        // Chart Data Methods for Tab2
        // ==========================================

        /// <summary>
        /// Gets chart data for Pass/Fail statistics per Element (Chart 1)
        /// FIXED: Uses ElementOptimizations instead of global PassedBefore/After
        /// </summary>
        public object GetPassFailChartData()
        {
            if (_result?.ElementOptimizations == null || !_result.ElementOptimizations.Any())
                return new { labels = Array.Empty<string>(), datasets = Array.Empty<object>() };

            var elements = _result.ElementOptimizations.Keys.OrderBy(x => x).ToList();
            var passedBeforeList = new List<int>();
            var passedAfterList = new List<int>();

            foreach (var element in elements)
            {
                var opt = _result.ElementOptimizations[element];
                passedBeforeList.Add(opt.PassedBefore);
                passedAfterList.Add(opt.PassedAfter);
            }

            return new
            {
                labels = elements.ToArray(),
                datasets = new object[]
                {
                    new
                    {
                        label = "Passed Before",
                        data = passedBeforeList.ToArray(),
                        backgroundColor = "#ff9800",
                        borderColor = "#e65100",
                        borderWidth = 1
                    },
                    new
                    {
                        label = "Passed After",
                        data = passedAfterList.ToArray(),
                        backgroundColor = "#4caf50",
                        borderColor = "#2e7d32",
                        borderWidth = 1
                    }
                }
            };
        }

        /// <summary>
        /// Gets chart data for Element-wise Average Diff % (Chart 2)
        /// FIXED: Uses MeanDiffBefore/After instead of Pass Rate %
        /// </summary>
        public object GetElementImprovementChartData()
        {
            if (_result?.ElementOptimizations == null || !_result.ElementOptimizations.Any())
                return new { labels = Array.Empty<string>(), datasets = Array.Empty<object>() };

            var elements = _result.ElementOptimizations.Keys.OrderBy(x => x).ToList();
            var diffBeforeList = new List<decimal>();
            var diffAfterList = new List<decimal>();

            foreach (var element in elements)
            {
                var opt = _result.ElementOptimizations[element];
                diffBeforeList.Add(opt.MeanDiffBefore);
                diffAfterList.Add(opt.MeanDiffAfter);
            }

            return new
            {
                labels = elements.ToArray(),
                datasets = new object[]
                {
                    new
                    {
                        label = "Avg Diff % (Before)",
                        data = diffBeforeList.Select(x => (double)x).ToArray(),
                        borderColor = "#ff9800",
                        backgroundColor = "rgba(255, 152, 0, 0.1)",
                        borderWidth = 2,
                        tension = 0.3,
                        fill = false
                    },
                    new
                    {
                        label = "Avg Diff % (After)",
                        data = diffAfterList.Select(x => (double)x).ToArray(),
                        borderColor = "#4caf50",
                        backgroundColor = "rgba(76, 175, 80, 0.1)",
                        borderWidth = 2,
                        tension = 0.3,
                        fill = false
                    }
                }
            };
        }

        /// <summary>
        /// Builds chart data for the Calibration scatter similar to Python's plot_calib
        /// </summary>
        public object GetCalibrationChartData()
        {
            var dataSource = _manualResult?.OptimizedData ?? _result?.OptimizedData;
            if (dataSource == null || string.IsNullOrWhiteSpace(_focusElement))
                return new { labels = Array.Empty<string>(), datasets = Array.Empty<object>() };

            // فیلتر کردن داده‌ها برای عنصر انتخاب شده
            var rows = BuildRows(dataSource, _focusElement).ToList();

            // حذف موارد Exclude شده (مشابه پایتون)
            var excludedLabels = ParseExcludedLabels();
            if (excludedLabels.Any())
                rows = rows.Where(r => !excludedLabels.Contains(r.SolutionLabel, StringComparer.OrdinalIgnoreCase)).ToList();

            var crmRows = rows.Where(r => !string.IsNullOrWhiteSpace(r.CrmId)).ToList();
            if (!crmRows.Any())
                return new { labels = Array.Empty<string>(), datasets = Array.Empty<object>() };

            // گروه‌بندی بر اساس CRM ID برای محور X
            var crmIds = crmRows.Select(r => r.CrmId).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
            var crmToIndex = crmIds.Select((id, i) => new { id, i }).ToDictionary(x => x.id, x => x.i, StringComparer.OrdinalIgnoreCase);

            var datasets = new List<object>();

            if (_showAcceptableRange)
            {
                var rangePoints = new List<object>();

                foreach (var crm in crmIds)
                {
                    // پیدا کردن مقدار Certified برای این CRM
                    var refRow = crmRows.FirstOrDefault(r => r.CrmId == crm && r.CrmValue.HasValue);
                    if (refRow == null) continue;

                    var certVal = refRow.CrmValue.Value;
                    var tol = GetToleranceValue(certVal);

                    var lower = (double)(certVal - tol);
                    var upper = (double)(certVal + tol);
                    var x = crmToIndex[crm];

                    // خط پایین
                    rangePoints.Add(new { x = x - 0.25, y = lower });
                    rangePoints.Add(new { x = x + 0.25, y = lower });
                    rangePoints.Add(new { x = (double?)null, y = (double?)null }); // قطع اتصال

                    // خط بالا
                    rangePoints.Add(new { x = x - 0.25, y = upper });
                    rangePoints.Add(new { x = x + 0.25, y = upper });
                    rangePoints.Add(new { x = (double?)null, y = (double?)null }); // قطع اتصال
                }

                if (rangePoints.Count > 0)
                {
                    datasets.Add(new
                    {
                        type = "line",
                        label = "Acceptable Range",   // فقط یک Legend
                        data = rangePoints,
                        borderColor = "#FF0000",
                        borderWidth = 2,
                        showLine = true,
                        pointRadius = 0,
                        pointHoverRadius = 0,
                        fill = false
                    });
                }
            }

            // --- 2. رسم مقادیر سرتیفاید (Green Circles) ---
            if (_showCertified)
            {
                var certPoints = new List<object>();
                foreach (var crm in crmIds)
                {
                    var refRow = crmRows.FirstOrDefault(r => r.CrmId == crm && r.CrmValue.HasValue);
                    if (refRow != null)
                    {
                        certPoints.Add(new { x = crmToIndex[crm], y = (double)refRow.CrmValue.Value });
                    }
                }
                if (certPoints.Any())
                {
                    datasets.Add(new
                    {
                        label = "Certificate Value",
                        data = certPoints,
                        backgroundColor = "green", // سبز مشابه پایتون
                        borderColor = "green",
                        pointStyle = "circle",
                        pointRadius = 8,
                        showLine = false
                    });
                }
            }

            // --- 3. رسم مقادیر نمونه (Blue Triangles) ---
            // در پایتون، نمونه‌ها (Corrected) با مثلث آبی نمایش داده می‌شوند
            var samplePoints = new List<object>();
            var outlierPoints = new List<object>(); // اگر بخواهید Outlierها را جدا کنید (نارنجی)

            foreach (var r in crmRows)
            {
                // در پایتون وریفیکیشن‌ها مهم هستند، اینجا همه نمونه‌های CRM را رسم می‌کنیم
                decimal valToUse = r.OriginalValue ?? r.OptimizedValue ?? 0;

                // محاسبه مقدار با تنظیمات Preview
                var displayVal = (double)GetPreviewValue(valToUse);

                // تشخیص Outlier (اختیاری: اگر خارج از بازه بود رنگش فرق کند)
                // فعلا همه را آبی رسم می‌کنیم مگر اینکه منطق Outlier را داشته باشید
                samplePoints.Add(new
                {
                    x = crmToIndex[r.CrmId] + ((new Random().NextDouble() - 0.5) * 0.1), // کمی Jitter برای جلوگیری از همپوشانی
                    y = displayVal,
                    label = r.SolutionLabel
                });
            }

            if (samplePoints.Any())
            {
                datasets.Add(new
                {
                    label = "Sample Value",
                    data = samplePoints,
                    backgroundColor = "blue", // آبی مشابه پایتون
                    borderColor = "blue",
                    pointStyle = "triangle", // مثلث
                    pointRadius = 8,
                    rotation = 0,
                    showLine = false
                });
            }

            return new
            {
                labels = crmIds.ToArray(),
                datasets = datasets.ToArray()
            };
        }

        /// <summary>
        /// Renders the calibration scatter chart by invoking the JS helper
        /// </summary>
        private async Task RenderCalibrationChartAsync()
        {
            try
            {
                var chartData = GetCalibrationChartData();

                var chartConfig = new
                {
                    type = "scatter",
                    data = chartData,
                    options = new
                    {
                        responsive = true,
                        maintainAspectRatio = false,
                        xLabels = ((dynamic)chartData).labels, // used by client-side helper to map ticks
                        plugins = new
                        {
                            legend = new { display = true, position = "top" },
                            tooltip = new { backgroundColor = "rgba(0,0,0,0.7)" }
                        },
                        scales = new
                        {
                            x = new { },
                            y = new { beginAtZero = false }
                        }
                    }
                };

                await JSRuntime.InvokeVoidAsync("createChart", "calibrationChart", chartConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rendering calibration chart: {ex.Message}");
            }
        }

        private async Task RefreshCalibrationAsync()
        {
            // Re-render only the calibration chart
            await Task.Delay(10);
            await RenderCalibrationChartAsync();
            StateHasChanged();
        }


        private decimal GetToleranceValue(decimal crmValue)
        {
            var absVal = Math.Abs(crmValue);

            // طبق کد پایتون: if abs_value < 10: return self.w.range_low
            // این یعنی برای اعداد کوچک، مقدار rangeLow یک عدد ثابت (Absolute) است، نه درصد.
            if (absVal < 10) return _rangeLow;

            // برای سایر بازه‌ها، درصد محاسبه می‌شود
            decimal percentage = 0;
            if (absVal < 100) percentage = _rangeMid;
            else if (absVal < 1000) percentage = _rangeHigh1;
            else if (absVal < 10000) percentage = _rangeHigh2;
            else if (absVal < 100000) percentage = _rangeHigh3;
            else percentage = _rangeHigh4;

            return absVal * (percentage / 100m);
        }

        private decimal GetPreviewValue(decimal originalValue)
        {
            // طبق لاجیک پایتون (خط 239 فایل ارسالی):
            // شرط‌ها روی pivot_val (مقدار اصلی) چک می‌شوند.

            // 1. بررسی شرط Min روی مقدار اصلی
            if (_scaleRangeMin.HasValue && originalValue < _scaleRangeMin.Value)
                return originalValue; // تغییر نمی‌کند

            // 2. بررسی شرط Max روی مقدار اصلی
            if (_scaleRangeMax.HasValue && originalValue > _scaleRangeMax.Value)
                return originalValue; // تغییر نمی‌کند

            // 3. بررسی شرط > 50 روی مقدار اصلی
            if (_scaleAbove50Only && originalValue <= 50)
                return originalValue; // تغییر نمی‌کند

            // 4. اعمال فرمول: (Val - Blank) * Scale
            return (originalValue - _previewBlank) * (decimal)_previewScale;
        }


        private async Task UpdateElementImprovementChartAsync()
        {
            var data = _manualResult?.OptimizedData ?? _result?.OptimizedData;
            if (data == null || string.IsNullOrWhiteSpace(_focusElement)) return;

            decimal totalDiff = 0;
            int count = 0;

            foreach (var row in data)
            {
                // دریافت مقادیر از دیکشنری
                if (!row.CrmValues.TryGetValue(_focusElement, out var certified) || certified == null || certified == 0) continue;
                if (!row.OriginalValues.TryGetValue(_focusElement, out var original) || original == null) continue;

                // محاسبه مقدار جدید با تنظیمات فعلی
                var calculated = GetPreviewValue(original.Value);

                // محاسبه درصد خطا: (Calc - Cert) / Cert * 100
                var diff = (calculated - certified.Value) / certified.Value * 100m;

                // در نمودار "Avg Diff %"، معمولا قدرمطلق خطا میانگین گرفته می‌شود تا خطاهای مثبت و منفی همدیگر را خنثی نکنند
                totalDiff += Math.Abs(diff);
                count++;
            }

            if (count > 0)
            {
                var newMeanDiff = totalDiff / count;

                // آپدیت مقدار در حافظه
                if (_result.ElementOptimizations.ContainsKey(_focusElement))
                {
                    _result.ElementOptimizations[_focusElement].MeanDiffAfter = newMeanDiff;
                }

                // رندر مجدد نمودار بالا
                await RenderElementImprovementChartAsync();
            }
        }

        private async Task OnPreviewParamChanged()
        {
            // جلوگیری از رفرش‌های خیلی سریع (اختیاری)
            // await Task.Delay(50); 

            // 1. آپدیت نمودار پایین (Calibration Plot)
            await RenderCalibrationChartAsync();

            // 2. آپدیت نمودار بالا (Element Improvement)
            await UpdateElementImprovementChartAsync();
        }

        // هندلرهای اختصاصی برای بایندینگ
        private async Task OnPreviewScaleChanged(double newVal)
        {
            _previewScale = newVal;
            await OnPreviewParamChanged();
        }

        private async Task OnRangeMinChanged(decimal? newVal)
        {
            _scaleRangeMin = newVal;
            await OnPreviewParamChanged();
        }

        private async Task OnRangeMaxChanged(decimal? newVal)
        {
            _scaleRangeMax = newVal;
            await OnPreviewParamChanged();
        }

    }
}