using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using MudBlazor;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebUI.Services;

namespace WebUI.Pages.Process
{
    public partial class CrmCalibration : IDisposable
    {
        [SupplyParameterFromQuery]
        public Guid? projectId { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; } = default!;

        // قفل برای جلوگیری از خطای Concurrency در دیتابیس
        private readonly SemaphoreSlim _loadingLock = new(1, 1);

        // --- فیلدها و وضعیت‌های اصلی ---
        private Guid? _projectId;
        private string? _projectName;
        private bool _isLoading = false;
        private string? _focusElement;
        private List<string> _allElements = new();
        private IEnumerable<string> _selectedElements = new HashSet<string>();
        private int _resultsTabIndex = 0;
        private int _detailsTabIndex = 0;
        private bool _detailsMaximized = false;

        // --- تنظیمات کالیبراسیون ---
        private decimal _minDiff = -10m;
        private decimal _maxDiff = 10m;
        private int _maxIterations = 100;
        private int _populationSize = 15;
        private bool _useMultiModel = true;

        // --- تنظیمات دستی (Manual) ---
        private decimal _previewBlank = 0m;
        private double _previewScale = 1.0;
        private string _sampleFilter = "";
        private decimal? _scaleRangeMin;
        private decimal? _scaleRangeMax;
        private bool _scaleAbove50Only = false;
        private string _excludedLabelsInput = string.Empty;
        private readonly List<string> _blankLabelsForFocus = new();

        // متغیرهای جدید برای مدیریت فیلتر
        private string _filterText = "";
        private bool _showOriginal = true;
        private bool _showCorrected = true;

        // --- داده‌ها و مراجع ---
        private List<AdvancedPivotRowDto> _secondaryRows = new();
        private BlankScaleOptimizationResult? _result;
        private List<OptimizedSampleRow> _optimizedRows = new();
        private List<OptimizedSampleRow> _manualRows = new();
        private List<CrmSelectionRowDto> _crmSelectionRows = new();
        private Dictionary<string, List<CrmListItemDto>> _crmReferenceById = new(StringComparer.OrdinalIgnoreCase);
        private List<RawCrmBaseValueRow> _rawCrmBaseValues = new();
        private readonly List<RawBlankValueRow> _rawBlankValues = new();

        // --- وضعیت جدول Pivot ---
        private PivotValueMode _pivotMode = PivotValueMode.Crm;
        private HashSet<string> _pivotSelectedElements = new(StringComparer.OrdinalIgnoreCase);
        private List<PivotRowVm> _pivotRows = new();

        // --- تنظیمات بازه مجاز (Tolerance) ---
        private decimal _rangeLow = 2.0m;
        private decimal _rangeMid = 20.0m;
        private decimal _rangeHigh1 = 10.0m;
        private decimal _rangeHigh2 = 8.0m;
        private decimal _rangeHigh3 = 5.0m;
        private decimal _rangeHigh4 = 3.0m;
        private bool _rangesDialogVisible = false;
        private bool _showCertified = true;
        private bool _showSampleValues = true;
        private bool _showAcceptableRange = true;


        private CancellationTokenSource _refreshCts;
        private DateTime _lastRefreshTime = DateTime.MinValue;
        private readonly TimeSpan _refreshThrottleInterval = TimeSpan.FromMilliseconds(300);
        private bool _isDisposed = false;
        private bool _initialLoadCompleted = false;

        // ریجکس برای تشخیص برچسب‌های CRM (مطابق تصویر پایتون V 252b)
        private static readonly Regex CrmIdRegex = new(@"(?i)(?:\bCRM\b|\bOREAS\b|\bV\b)[^\d]*(\d+[a-zA-Z]?)", RegexOptions.Compiled);
        private static readonly Regex BlankLabelRegex = new(@"(?:CRM\s*)?(?:BLANK|BLNK)(?:\s+.*)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex ReplicaSuffixRegex = new(@"_(\d+)$", RegexOptions.Compiled);
        private static readonly Regex ElementWavelengthRegex = new(@"^([a-zA-Z]+)\s*([0-9]+(?:\.[0-9]+)?)(?:_[0-9]+)?$", RegexOptions.Compiled);

        // ============================================================
        // مدل‌های داده داخلی (Models)
        // ============================================================

        public enum PivotValueMode { Original, Optimized, Crm, DiffAfter }
        public enum PivotRowType { Sample, CrmRef, DiffAfter }

        public sealed class PivotRowVm
        {
            public int Order { get; set; }
            public string SolutionLabel { get; set; } = "";
            public PivotRowType RowType { get; set; }
            public Dictionary<string, decimal?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class CalibrationRow
        {
            public string SolutionLabel { get; set; } = "";
            public string CrmId { get; set; } = "";
            public int OriginalIndex { get; set; }
            public decimal RawValue { get; set; }
            public decimal CorrectedValue { get; set; }
            public decimal CrmValue { get; set; }
        }

        private sealed record CalibrationChartPayload(
            string[] Labels,
            List<object> Datasets,
            double MinY,
            double MaxY
        );

        private sealed record OptimizedSampleRow(
            string SolutionLabel, string CrmId, string Element,
            decimal? OriginalValue, decimal? OptimizedValue, decimal? CrmValue,
            decimal DiffBefore, decimal DiffAfter, bool IsPassed);

        private sealed class RawCrmBaseValueRow
        {
            public int Sequence { get; set; }
            public string SolutionLabel { get; set; } = "";
            public string NormalizedLabel { get; set; } = "";
            public string CrmId { get; set; } = "";
            public string Element { get; set; } = "";
            public decimal BaseValue { get; set; }
        }

        private sealed class RawBlankValueRow
        {
            public int Sequence { get; set; }
            public string SolutionLabel { get; set; } = "";
            public string Element { get; set; } = "";
            public decimal BlankValue { get; set; }
        }

        // ============================================================
        // توابع کمکی برای کار با عناصر و طول موج‌ها
        // ============================================================

        // نرمالایز کردن کامل عنصر با طول موج
        private static string NormalizeElementFull(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            return StripReplicaSuffix(raw).Trim().ToLowerInvariant();
        }

        private static string StripReplicaSuffix(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            return ReplicaSuffixRegex.Replace(raw.Trim(), string.Empty);
        }

        // استخراج فقط نام عنصر (بدون طول موج)
        private static string NormalizeElementName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var normalizedRaw = StripReplicaSuffix(raw);
            var match = ElementWavelengthRegex.Match(normalizedRaw.Trim());
            if (match.Success)
            {
                return match.Groups[1].Value.Trim().ToLowerInvariant();
            }

            var parts = normalizedRaw.Split(new[] { ' ', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0].Trim().ToLowerInvariant() : normalizedRaw.Trim().ToLowerInvariant();
        }

        // استخراج طول موج از نام عنصر
        private static string NormalizeElementWavelength(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var normalizedRaw = StripReplicaSuffix(raw);
            var match = ElementWavelengthRegex.Match(normalizedRaw.Trim());
            if (match.Success)
            {
                return match.Groups[2].Value.Trim();
            }
            return string.Empty;
        }

        // ترکیب نام عنصر و طول موج
        private static string CombineElementNameAndWavelength(string elementName, string wavelength)
        {
            if (string.IsNullOrWhiteSpace(elementName)) return string.Empty;
            if (string.IsNullOrWhiteSpace(wavelength)) return elementName;
            return $"{elementName.Trim()} {wavelength.Trim()}";
        }

        // ============================================================
        // چرخه حیات و بارگذاری (Lifecycle)
        // ============================================================

        protected override async Task OnInitializedAsync()
        {
            _projectId = projectId ?? ProjectService.CurrentProjectId;
            if (!_projectId.HasValue) return;

            var projectResult = await ProjectService.GetProjectAsync(_projectId.Value);
            if (projectResult.Succeeded) _projectName = projectResult.Data?.ProjectName;

            // اجرای دیباگ برای بررسی داده‌ها
            await DebugElementDataSources();
            if (_isDisposed) return;

            await LoadInitialDataInternalAsync();
        }

        private bool IsCoreDataReady =>
            _allElements.Any() &&
            !string.IsNullOrWhiteSpace(_focusElement) &&
            _crmSelectionRows.Any();

        private bool IsUiBusyOrBlocked =>
            _projectId.HasValue &&
            (_isLoading || !IsCoreDataReady || !_initialLoadCompleted);

        private string LoadingOverlayText =>
            _isLoading
                ? "Processing..."
                : "Loading elements and CRM data...";
        private void OnBeforeNavigation(LocationChangingContext context)
        {
            if (_isLoading) context.PreventNavigation();
        }

        private async Task LoadInitialDataInternalAsync()
        {
            if (_isDisposed) return;

            _isLoading = true;
            if (!_isDisposed)
                StateHasChanged();

            var lockTaken = false;
            try
            {
                lockTaken = await _loadingLock.WaitAsync(TimeSpan.FromSeconds(15));
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (!lockTaken || _isDisposed) return;
            try
            {
                await AwaitWithTimeout(LoadElements(), TimeSpan.FromSeconds(40), "Load elements");
                await AwaitWithTimeout(LoadCrmReferenceData(), TimeSpan.FromSeconds(25), "Load CRM references");
                await AwaitWithTimeout(LoadSecondaryPlotRowsAsync(), TimeSpan.FromSeconds(40), "Load pivot rows");
                await AwaitWithTimeout(LoadRawCrmBaseValuesAsync(), TimeSpan.FromSeconds(60), "Load raw CRM values");
                await AwaitWithTimeout(GetCurrentStats(), TimeSpan.FromSeconds(40), "Load current stats");
                await AwaitWithTimeout(LoadCrmSelections(), TimeSpan.FromSeconds(25), "Load CRM selections");

                if (!IsCoreDataReady)
                {
                    await AwaitWithTimeout(LoadElements(), TimeSpan.FromSeconds(20), "Retry load elements");
                    await AwaitWithTimeout(LoadSecondaryPlotRowsAsync(), TimeSpan.FromSeconds(20), "Retry load pivot rows");
                    await AwaitWithTimeout(LoadCrmSelections(), TimeSpan.FromSeconds(20), "Retry load CRM selections");
                }

                await AwaitWithTimeout(RefreshChartsAsync(), TimeSpan.FromSeconds(20), "Render charts");
            }
            catch (TimeoutException timeoutEx)
            {
                Snackbar.Add(timeoutEx.Message, Severity.Warning);
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Calibration load failed: {ex.Message}", Severity.Error);
            }
            finally
            {
                _initialLoadCompleted = true;
                _isLoading = false;
                if (lockTaken)
                {
                    try { _loadingLock.Release(); } catch (ObjectDisposedException) { }
                }

                if (!_isDisposed)
                    StateHasChanged();
            }
        }

        // ============================================================
        // متدهای فراخوانی شده از Razor (UI-Bound Methods)
        // ============================================================

        private void ToggleDetailsMaximize() => _detailsMaximized = !_detailsMaximized;

        private async Task ApplyManualAsync()
        {
            if (_projectId == null || string.IsNullOrWhiteSpace(_focusElement)) return;
            _isLoading = true;
            try
            {
                var result = await OptimizationService.ApplyManualAsync(_projectId.Value, _focusElement, _previewBlank, (decimal)_previewScale);
                if (result.Succeeded)
                {
                    Snackbar.Add("Correction applied.", Severity.Success);
                    await RefreshChartsAsync();
                    await GetCurrentStats();
                }
            }
            finally { _isLoading = false; }
        }

        private async Task ApplyOurModelAsync()
        {
            if (_projectId == null || string.IsNullOrWhiteSpace(_focusElement))
            {
                Snackbar.Add("Please select an element first.", Severity.Warning);
                return;
            }

            if (_isDisposed) return;

            bool lockTaken;
            try
            {
                lockTaken = await _loadingLock.WaitAsync(0);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (!lockTaken)
            {
                Snackbar.Add("System is busy. Please wait...", Severity.Info);
                return;
            }

            _isLoading = true;
            try
            {
                var request = new BlankScaleOptimizationRequest
                {
                    ProjectId = _projectId.Value,
                    Elements = new List<string> { _focusElement },
                    MinDiffPercent = _minDiff,
                    MaxDiffPercent = _maxDiff,
                    MaxIterations = _maxIterations,
                    PopulationSize = _populationSize,
                    UseMultiModel = true,
                    PreviewOnly = true,
                    RangeLow = _rangeLow,
                    RangeMid = _rangeMid,
                    RangeHigh1 = _rangeHigh1,
                    RangeHigh2 = _rangeHigh2,
                    RangeHigh3 = _rangeHigh3,
                    RangeHigh4 = _rangeHigh4,
                    ScaleRangeMin = null,
                    ScaleRangeMax = null,
                    ScaleAbove50Only = false,
                    ExcludedSolutionLabels = null,
                    CrmSelections = BuildCrmMethodSelectionByCrmId()
                };

                var result = await OptimizationService.OptimizeAsync(request);
                if (!result.Succeeded || result.Data == null)
                {
                    Snackbar.Add(result.Message ?? "Model optimization failed.", Severity.Warning);
                    return;
                }

                _result = result.Data;
                _optimizedRows = BuildOptimizedRows(_result.OptimizedData, _focusElement);
                ExtractElementsFromOptimizedData();

                if (!TryGetElementOptimizationForFocus(result.Data, out var optimization))
                {
                    Snackbar.Add("Model completed, but no recommendation found for selected element.", Severity.Warning);
                    await RefreshChartsAsync(refreshCalibration: true, refreshSecondary: true);
                    return;
                }

                // Python behavior:
                // blank_edit.setText(f"{recommended_blank:.3f}")  -> keep 3 decimals
                // scale_slider.setValue(int(recommended_scale * 100)) -> truncate to 2 decimals
                var recommendedBlank = optimization.Blank;
                _previewBlank = Math.Round(recommendedBlank, 3, MidpointRounding.ToEven);

                var recommendedScale = (double)optimization.Scale;
                var clampedScale = Math.Clamp(recommendedScale, 0d, 2d);
                var pythonUiScale = Math.Truncate(clampedScale * 100d) / 100d;
                _previewScale = pythonUiScale;

                await RefreshChartsAsync(refreshCalibration: true, refreshSecondary: true);

                var modelUsed = string.IsNullOrWhiteSpace(optimization.SelectedModel) ? "A" : optimization.SelectedModel;
                if (Math.Abs(pythonUiScale - recommendedScale) > 0.000001d || Math.Abs(_previewBlank - recommendedBlank) > 0.000001m)
                {
                    Snackbar.Add(
                        $"Model {modelUsed} applied in preview. Blank {recommendedBlank:F6}→{_previewBlank:F3}, Scale {recommendedScale:F6}→{pythonUiScale:F2}.",
                        Severity.Info);
                }
                else
                {
                    Snackbar.Add(
                        $"Model {modelUsed} applied in preview. Blank={_previewBlank:F3}, Scale={_previewScale:F3}.",
                        Severity.Success);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Apply Our Model failed: {ex.Message}", Severity.Error);
            }
            finally
            {
                _isLoading = false;
                try { _loadingLock.Release(); } catch (ObjectDisposedException) { }
                if (!_isDisposed)
                    StateHasChanged();
            }
        }

        private Dictionary<string, string> BuildCrmMethodSelectionByCrmId()
        {
            var selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in _crmSelectionRows)
            {
                var crmId = NormalizeCrmIdToken(row.CrmId);
                if (string.IsNullOrWhiteSpace(crmId))
                {
                    var crmMatch = CrmIdRegex.Match(row.SolutionLabel ?? string.Empty);
                    if (crmMatch.Success)
                        crmId = NormalizeCrmIdToken(crmMatch.Groups[1].Value);
                }

                if (string.IsNullOrWhiteSpace(crmId))
                    continue;

                var (_, methodHint) = ParseSelectedOption(row.SelectedOption);
                if (string.IsNullOrWhiteSpace(methodHint))
                    continue;

                if (!selections.ContainsKey(crmId))
                    selections[crmId] = methodHint.Trim();
            }

            return selections;
        }

        private bool TryGetElementOptimizationForFocus(BlankScaleOptimizationResult result, out ElementOptimization optimization)
        {
            optimization = new ElementOptimization();
            if (result.ElementOptimizations == null || result.ElementOptimizations.Count == 0 || string.IsNullOrWhiteSpace(_focusElement))
                return false;

            if (result.ElementOptimizations.TryGetValue(_focusElement, out var direct))
            {
                optimization = direct;
                return true;
            }

            var pairMatch = result.ElementOptimizations
                .FirstOrDefault(kvp => string.Equals(kvp.Key, _focusElement, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(pairMatch.Key))
            {
                optimization = pairMatch.Value;
                return true;
            }

            var valueMatch = result.ElementOptimizations.Values
                .FirstOrDefault(item => string.Equals(item.Element, _focusElement, StringComparison.OrdinalIgnoreCase));
            if (valueMatch != null)
            {
                optimization = valueMatch;
                return true;
            }

            return false;
        }

        private async Task UndoManualAsync()
        {
            if (_projectId == null) return;
            var result = await CorrectionService.UndoLastCorrectionAsync(_projectId.Value);
            if (result.Succeeded)
            {
                Snackbar.Add("Undo successful.", Severity.Success);
                await LoadInitialDataInternalAsync();
            }
        }

        private string GetSampleDetailsTitle() => _manualRows.Any() ? "Manual Preview Details" : "Optimized Sample Details";

        private string FormatDec(decimal? v, string f = "F3") => v?.ToString(f) ?? "-";

        private int FilteredOptimizedCount() => FilterRows(_optimizedRows).Count();

        private int FilteredManualCount() => FilterRows(_manualRows).Count();

        private List<string> PivotColumns() => _pivotSelectedElements.OrderBy(x => x).ToList();

        private async Task OnResultsTabChanged(int i) { _resultsTabIndex = i; if (i == 1) await RefreshChartsAsync(); }

        private Task OnPivotModeChanged(PivotValueMode mode)
        {
            _pivotMode = mode;
            RebuildPivot();
            StateHasChanged();
            return Task.CompletedTask;
        }

        private Task OnPivotElementsChanged(IEnumerable<string> values)
        {
            _pivotSelectedElements = new HashSet<string>(values ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            RebuildPivot();
            StateHasChanged();
            return Task.CompletedTask;
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
                    SelectedElements: _allElements.ToList(),
                    NumberFilters: null,
                    UseOxide: false,
                    UseInt: false,
                    DecimalPlaces: 4,
                    Page: 1,
                    PageSize: 2000,
                    MergeRepeats: false,
                    Aggregation: "First"
                );

                var result = await PivotService.GetAdvancedPivotTableAsync(request);

                if (result.Succeeded && result.Data != null)
                {
                    var cols = PivotColumns().ToList();
                    var rows = new List<PivotRowVm>();
                    int order = 0;

                    var optimizedData = _manualRows.Any() ? null : _result?.OptimizedData;

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

        private void ResetPivotColumns() => _pivotSelectedElements.Clear();

        private void OpenRangesDialog() => _rangesDialogVisible = true;
        private void CancelRangesAsync() => _rangesDialogVisible = false;

        private async Task ApplyRangesAsync() { _rangesDialogVisible = false; await RefreshChartsAsync(); }

        private void ResetRanges() { _rangeLow = 2; _rangeMid = 20; _rangeHigh1 = 10; _rangeHigh2 = 8; _rangeHigh3 = 5; _rangeHigh4 = 3; }

        // ============================================================
        // منطق نمودار و محاسبات (Logic)
        // ============================================================
        private List<CalibrationRow> BuildCalibrationRows()
        {
            var rows = new List<CalibrationRow>();
            if (string.IsNullOrWhiteSpace(_focusElement))
            {
                Console.WriteLine("BuildCalibrationRows: No focus element selected");
                return rows;
            }

            Console.WriteLine($"BuildCalibrationRows: Building rows for element '{_focusElement}'");

            var excludedLabels = ParseExcludedLabels();
            var selectionQueues = BuildSelectionQueueByLabel();
            var (rawValuesByLabel, rawValuesByCrmId) = BuildRawBaseValueQueuesForFocusElement();

            // ابتدا از داده‌های بهینه‌سازی استفاده کن
            var optimizedData = _result?.OptimizedData;
            if (optimizedData != null && optimizedData.Any())
            {
                Console.WriteLine($"Using optimized data: {optimizedData.Count()} samples");

                var order = 0;
                foreach (var sample in optimizedData)
                {
                    var solutionLabel = sample.SolutionLabel?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(solutionLabel)) continue;
                    if (BlankLabelRegex.IsMatch(solutionLabel)) continue;

                    Console.WriteLine($"Processing sample: {solutionLabel}");

                    string? selectedOption = null;
                    var normalizedSolutionLabel = NormalizeSolutionLabel(solutionLabel);
                    if (selectionQueues.TryGetValue(normalizedSolutionLabel, out var optionQueue) && optionQueue.Count > 0)
                        selectedOption = optionQueue.Dequeue();

                    var crmToken = NormalizeCrmIdToken(sample.CrmId);
                    if (string.IsNullOrWhiteSpace(crmToken))
                    {
                        var crmFromLabel = CrmIdRegex.Match(solutionLabel);
                        if (!crmFromLabel.Success) continue;
                        crmToken = NormalizeCrmIdToken(crmFromLabel.Groups[1].Value);
                    }

                    // پیدا کردن مقدار اصلی:
                    // برای همسانی با Python اولویت با داده خام (Soln Conc / base value) است.
                    decimal rawValue = 0;
                    var foundRawValue = false;

                    if (TryDequeueRawBaseValue(solutionLabel, crmToken, rawValuesByLabel, rawValuesByCrmId, out rawValue))
                    {
                        foundRawValue = true;
                        Console.WriteLine($"  Raw value from base values: {rawValue}");
                    }

                    // fallback: اگر داده خام موجود نبود، از originalValues استفاده کن.
                    if (!foundRawValue &&
                        TryGetElementValueExact(sample.OriginalValues, _focusElement, out var rawValueMaybe) &&
                        rawValueMaybe.HasValue)
                    {
                        rawValue = rawValueMaybe.Value;
                        foundRawValue = true;
                        Console.WriteLine($"  Raw value from original: {rawValue}");
                    }

                    if (!foundRawValue)
                    {
                        Console.WriteLine($"  Could not find raw value for {_focusElement}");
                        continue;
                    }

                    // پیدا کردن مقدار مرجع CRM
                    decimal? certValue = null;

                    // اول از داده‌های بهینه‌سازی بگیر
                    if (TryGetElementValueExact(sample.CrmValues, _focusElement, out var crmValueMaybe) && crmValueMaybe.HasValue)
                    {
                        certValue = crmValueMaybe.Value;
                        Console.WriteLine($"  CRM value from optimized data: {certValue}");
                    }

                    // اگر پیدا نکردی، از مرجع CRM بگیر
                    if (!certValue.HasValue)
                    {
                        var crmRef = ResolveCrmReferenceForRow(crmToken, selectedOption);
                        if (crmRef != null && TryGetReferenceElementValueExact(crmRef.Elements, _focusElement, out var certFromRef))
                        {
                            certValue = certFromRef;
                            Console.WriteLine($"  CRM value from reference: {certValue}");
                        }
                    }

                    if (!certValue.HasValue)
                    {
                        Console.WriteLine($"  Could not find CRM value for {_focusElement}");
                        continue;
                    }

                    // اعمال تصحیح دستی
                    var correctedValue = rawValue;
                    if (ShouldApplyManualCorrection(solutionLabel, rawValue, excludedLabels))
                    {
                        correctedValue = (rawValue - _previewBlank) * (decimal)_previewScale;
                        Console.WriteLine($"  Corrected value: {correctedValue} (Blank: {_previewBlank}, Scale: {_previewScale})");
                    }

                    rows.Add(new CalibrationRow
                    {
                        SolutionLabel = solutionLabel,
                        CrmId = crmToken,
                        OriginalIndex = order++,
                        RawValue = rawValue,
                        CorrectedValue = correctedValue,
                        CrmValue = certValue.Value
                    });

                    Console.WriteLine($"  Added row: {solutionLabel} - CRM: {crmToken} - Raw: {rawValue} - Corrected: {correctedValue} - CRM Ref: {certValue.Value}");
                }

                if (rows.Any())
                {
                    Console.WriteLine($"Returning {rows.Count} rows from optimized data");
                    return rows;
                }
            }
            else
            {
                Console.WriteLine("No optimized data available");
            }

            // اگر داده‌های بهینه‌سازی نبود، از داده‌های پیوت استفاده کن
            if (!_secondaryRows.Any())
            {
                Console.WriteLine("No secondary rows available");
                return rows;
            }

            Console.WriteLine($"Using secondary data: {_secondaryRows.Count} rows");

            var source = _secondaryRows
                .OrderBy(r => r.OriginalIndex)
                .ThenBy(r => r.SetIndex)
                .ToList();

            foreach (var sourceRow in source)
            {
                var solutionLabel = sourceRow.SolutionLabel?.Trim() ?? "";
                var normalizedSolutionLabel = NormalizeSolutionLabel(solutionLabel);
                if (string.IsNullOrWhiteSpace(solutionLabel)) continue;
                if (BlankLabelRegex.IsMatch(solutionLabel)) continue;

                string? selectedOption = null;
                if (selectionQueues.TryGetValue(normalizedSolutionLabel, out var queue) && queue.Count > 0)
                    selectedOption = queue.Dequeue();

                var crmMatch = CrmIdRegex.Match(solutionLabel);
                if (!crmMatch.Success) continue;
                var crmId = NormalizeCrmIdToken(crmMatch.Groups[1].Value);

                decimal rawValue;
                if (!TryDequeueRawBaseValue(solutionLabel, crmId, rawValuesByLabel, rawValuesByCrmId, out rawValue))
                {
                    if (!TryGetElementValueExact(sourceRow.Values, _focusElement, out var rawValueMaybe)) continue;
                    rawValue = rawValueMaybe ?? 0m;
                }

                var crmRef = ResolveCrmReferenceForRow(crmId, selectedOption);
                if (crmRef == null || !TryGetReferenceElementValueExact(crmRef.Elements, _focusElement, out var certValue)) continue;

                var correctedValue = rawValue;
                if (ShouldApplyManualCorrection(solutionLabel, rawValue, excludedLabels))
                    correctedValue = (rawValue - _previewBlank) * (decimal)_previewScale;

                rows.Add(new CalibrationRow
                {
                    SolutionLabel = solutionLabel,
                    CrmId = crmId,
                    OriginalIndex = sourceRow.OriginalIndex,
                    RawValue = rawValue,
                    CorrectedValue = correctedValue,
                    CrmValue = certValue
                });
            }

            Console.WriteLine($"Returning {rows.Count} total rows");
            return rows;
        }


        private CalibrationChartPayload? GetCalibrationChartData()
        {
            if (string.IsNullOrWhiteSpace(_focusElement)) return null;
            var calibrationRows = BuildCalibrationRows();
            if (!calibrationRows.Any()) return null;

            var uniqueIds = calibrationRows
                .Select(r => r.CrmId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var idMap = uniqueIds
                .Select((id, idx) => new { id, idx })
                .ToDictionary(x => x.id, x => x.idx, StringComparer.OrdinalIgnoreCase);

            var orderedRows = calibrationRows
                .OrderBy(r => idMap[r.CrmId])
                .ThenBy(r => r.OriginalIndex)
                .ThenBy(r => r.SolutionLabel, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var samplePoints = new List<object>();
            var certPoints = new List<object>();
            var rangeLines = new List<object>();
            var allYValues = new List<double>();

            foreach (var row in orderedRows)
            {
                var x = idMap[row.CrmId];
                var sampleY = (double)row.CorrectedValue;
                var certY = (double)row.CrmValue;
                var tol = (double)GetToleranceValue(row.CrmValue);

                samplePoints.Add(new { x, y = sampleY, label = row.SolutionLabel });
                certPoints.Add(new { x, y = certY });

                allYValues.Add(sampleY);
                allYValues.Add(certY);
                allYValues.Add(certY - tol);
                allYValues.Add(certY + tol);

                if (_showAcceptableRange)
                {
                    rangeLines.Add(new { x = x - 0.2, y = certY + tol });
                    rangeLines.Add(new { x = x + 0.2, y = certY + tol });
                    rangeLines.Add(new { x = (double?)null, y = (double?)null });
                    rangeLines.Add(new { x = x - 0.2, y = certY - tol });
                    rangeLines.Add(new { x = x + 0.2, y = certY - tol });
                    rangeLines.Add(new { x = (double?)null, y = (double?)null });
                }
            }

            if (!allYValues.Any()) return null;

            var minY = allYValues.Min();
            var maxY = allYValues.Max();
            var span = maxY - minY;
            var margin = span > 0 ? span * 0.10 : Math.Max(1.0, Math.Abs(maxY) * 0.10);

            var datasets = new List<object>();

            if (_showSampleValues)
            {
                datasets.Add(new
                {
                    label = "Sample Value",
                    data = samplePoints,
                    backgroundColor = "#0000FF",
                    borderColor = "#0000FF",
                    pointStyle = "triangle",
                    pointRotation = 180,
                    pointRadius = 7,
                    pointHitRadius = 14,
                    pointHoverRadius = 8,
                    showLine = false,
                    order = 1
                });
            }

            if (_showCertified)
            {
                datasets.Add(new
                {
                    label = "Certificate Value",
                    data = certPoints,
                    backgroundColor = "#008000",
                    borderColor = "#008000",
                    pointStyle = "circle",
                    pointRadius = 6,
                    pointHitRadius = 14,
                    pointHoverRadius = 8,
                    showLine = false,
                    order = 2
                });
            }

            if (_showAcceptableRange)
            {
                datasets.Add(new
                {
                    label = "Acceptable Range",
                    data = rangeLines,
                    borderColor = "#FF0000",
                    borderWidth = 2,
                    pointRadius = 0,
                    fill = false,
                    showLine = true,
                    spanGaps = false,
                    order = 10
                });
            }

            if (!datasets.Any()) return null;

            return new CalibrationChartPayload(
                Labels: uniqueIds.Select(id => $"V {id}").ToArray(),
                Datasets: datasets,
                MinY: minY - margin,
                MaxY: maxY + margin
            );
        }

        // ============================================================
        // رندرینگ و ارتباط با JS
        // ============================================================

        private async Task RenderCalibrationChartAsync()
        {
            Console.WriteLine($"=== RenderCalibrationChartAsync for '{_focusElement}' ===");

            var data = GetCalibrationChartData();
            if (data == null)
            {
                Console.WriteLine("No data for calibration chart");
                await JSRuntime.InvokeVoidAsync("destroyChart", "calibrationChart");
                return;
            }

            Console.WriteLine($"Chart data: {data.Labels.Length} labels, {data.Datasets.Count} datasets");
            Console.WriteLine($"Y Range: {data.MinY} to {data.MaxY}");

            var config = new
            {
                type = "scatter",
                data = new { datasets = data.Datasets },
                options = new
                {
                    responsive = true,
                    maintainAspectRatio = false,
                    xLabels = data.Labels,
                    animation = false,
                    interaction = new
                    {
                        mode = "nearest",
                        intersect = false,
                        axis = "xy"
                    },
                    scales = new
                    {
                        x = new
                        {
                            type = "linear",
                            min = -0.5,
                            max = data.Labels.Length - 0.5,
                            title = new { display = true, text = "Verification ID" },
                            ticks = new { stepSize = 1, autoSkip = false, maxRotation = 0, minRotation = 0 },
                            grid = new { color = "rgba(0,0,0,0.12)" }
                        },
                        y = new
                        {
                            min = data.MinY,
                            max = data.MaxY,
                            ticks = new { display = true },
                            title = new { display = true, text = $"{_focusElement} Value" },
                            grid = new { color = "rgba(0,0,0,0.12)" }
                        }
                    },
                    plugins = new
                    {
                        legend = new
                        {
                            display = true,
                            position = "top",
                            labels = new { usePointStyle = true, boxWidth = 26 }
                        },
                        tooltip = new
                        {
                            enabled = true,
                            mode = "nearest",
                            intersect = false,
                            displayColors = true
                        },
                        zoom = new
                        {
                            limits = new
                            {
                                x = new { min = "original", max = "original" },
                                y = new { min = "original", max = "original" }
                            },
                            pan = new
                            {
                                enabled = true,
                                mode = "xy"
                            },
                            zoom = new
                            {
                                wheel = new { enabled = true },
                                pinch = new { enabled = true },
                                drag = new { enabled = true },
                                mode = "xy"
                            }
                        }
                    }
                }
            };

            await JSRuntime.InvokeVoidAsync("createChart", "calibrationChart", config);
            Console.WriteLine("Chart rendered successfully");
        }

        private async Task RenderSecondaryChartAsync()
        {
            if (string.IsNullOrWhiteSpace(_focusElement) || !_secondaryRows.Any())
            {
                await JSRuntime.InvokeVoidAsync("destroyChart", "secondaryChart");
                return;
            }

            // اعمال فیلتر بر اساس متن وارد شده
            var filteredRows = GetFilteredSecondaryRows()
                .OrderBy(r => r.OriginalIndex)
                .ThenBy(r => r.SetIndex)
                .ToList();

            var originalPoints = new List<object>();
            var correctedPoints = new List<object>();
            var xValues = new List<double>();

            for (var plotIndex = 0; plotIndex < filteredRows.Count; plotIndex++)
            {
                var row = filteredRows[plotIndex];
                if (!TryGetElementValueExact(row.Values, _focusElement, out var valueMaybe))
                    continue;

                var rawValue = valueMaybe ?? 0m;
                var x = (double)plotIndex;
                var y = (double)rawValue;
                var corrected = (double)((rawValue - _previewBlank) * (decimal)_previewScale);

                xValues.Add(x);

                if (_showOriginal)
                {
                    originalPoints.Add(new { x, y, label = row.SolutionLabel });
                }

                if (_showCorrected)
                {
                    correctedPoints.Add(new { x, y = corrected, label = row.SolutionLabel });
                }
            }

            if (!originalPoints.Any() && !correctedPoints.Any())
            {
                await JSRuntime.InvokeVoidAsync("destroyChart", "secondaryChart");
                return;
            }

            var datasets = new List<object>();

            if (_showOriginal && originalPoints.Any())
            {
                datasets.Add(new
                {
                    label = "Original",
                    data = originalPoints,
                    backgroundColor = "#2196F3",
                    borderColor = "#2196F3",
                    pointStyle = "circle",
                    pointRadius = 4,
                    pointHitRadius = 12,
                    pointHoverRadius = 7,
                    showLine = false
                });
            }

            if (_showCorrected && correctedPoints.Any())
            {
                datasets.Add(new
                {
                    label = "Corrected",
                    data = correctedPoints,
                    backgroundColor = "#F44336",
                    borderColor = "#F44336",
                    pointStyle = "crossRot",
                    pointRadius = 5,
                    pointHitRadius = 12,
                    pointHoverRadius = 8,
                    showLine = false
                });
            }

            if (!datasets.Any())
            {
                await JSRuntime.InvokeVoidAsync("destroyChart", "secondaryChart");
                return;
            }

            var minX = xValues.Any() ? xValues.Min() - 1 : 0;
            var maxX = xValues.Any() ? xValues.Max() + 1 : 1;

            var config = new
            {
                type = "scatter",
                data = new { datasets = datasets.ToArray() },
                options = new
                {
                    responsive = true,
                    maintainAspectRatio = false,
                    animation = false,
                    interaction = new
                    {
                        mode = "nearest",
                        intersect = false,
                        axis = "xy"
                    },
                    scales = new
                    {
                        x = new
                        {
                            type = "linear",
                            min = minX,
                            max = maxX,
                            title = new { display = true, text = "Index" },
                            grid = new { color = "rgba(0,0,0,0.12)" }
                        },
                        y = new
                        {
                            beginAtZero = false,
                            title = new { display = true, text = "Value" },
                            grid = new { color = "rgba(0,0,0,0.12)" }
                        }
                    },
                    plugins = new
                    {
                        legend = new
                        {
                            display = true,
                            position = "top",
                            labels = new { usePointStyle = true }
                        },
                        tooltip = new
                        {
                            enabled = true,
                            mode = "nearest",
                            intersect = false,
                            displayColors = true
                        },
                        zoom = new
                        {
                            limits = new
                            {
                                x = new { min = "original", max = "original" },
                                y = new { min = "original", max = "original" }
                            },
                            pan = new
                            {
                                enabled = true,
                                mode = "xy"
                            },
                            zoom = new
                            {
                                wheel = new { enabled = true },
                                pinch = new { enabled = true },
                                drag = new { enabled = true },
                                mode = "xy"
                            }
                        }
                    }
                }
            };

            await JSRuntime.InvokeVoidAsync("createChart", "secondaryChart", config);
        }

        private async Task RefreshChartsAsync(bool refreshCalibration = true, bool refreshSecondary = true)
        {
            await Task.Delay(50);

            if (refreshCalibration)
                await RenderCalibrationChartAsync();

            if (refreshSecondary)
                await RenderSecondaryChartAsync();

            await JSRuntime.InvokeVoidAsync("resizeAllCharts");
        }

        // ============================================================
        // سرویس‌ها و متدهای کمکی
        // ============================================================

        private static async Task AwaitWithTimeout(Task task, TimeSpan timeout, string operationName)
        {
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout));
            if (completedTask != task)
                throw new TimeoutException($"{operationName} timed out after {timeout.TotalSeconds:0} seconds.");

            await task;
        }

        private Dictionary<string, Queue<string?>> BuildSelectionQueueByLabel()
        {
            var map = new Dictionary<string, Queue<string?>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in _crmSelectionRows)
            {
                var normalizedLabel = NormalizeSolutionLabel(row.SolutionLabel);
                if (string.IsNullOrWhiteSpace(normalizedLabel)) continue;
                if (!map.TryGetValue(normalizedLabel, out var queue))
                {
                    queue = new Queue<string?>();
                    map[normalizedLabel] = queue;
                }
                queue.Enqueue(row.SelectedOption);
            }
            return map;
        }

        private async Task LoadRawCrmBaseValuesAsync()
        {
            _rawCrmBaseValues.Clear();
            _rawBlankValues.Clear();
            if (!_projectId.HasValue) return;

            const int pageSize = 2000;
            var skip = 0;
            var sequence = 0;

            while (true)
            {
                var result = await ProjectService.GetProjectRawRowsAsync(_projectId.Value, skip, pageSize);
                if (!result.Succeeded || result.Data == null)
                {
                    Snackbar.Add(result.Message ?? "Failed to load raw CRM values.", Severity.Warning);
                    break;
                }

                foreach (var rawRow in result.Data)
                {
                    if (TryParseRawBlankValueRow(rawRow.ColumnData, sequence, out var parsedBlank) && parsedBlank != null)
                        _rawBlankValues.Add(parsedBlank);

                    if (TryParseRawCrmBaseValueRow(rawRow.ColumnData, sequence, out var parsed) && parsed != null)
                        _rawCrmBaseValues.Add(parsed);

                    sequence++;
                }

                if (result.Data.Count < pageSize) break;
                skip += result.Data.Count;
            }

            UpdateBlankLabelsForFocusElement();
        }

        private static bool TryParseRawCrmBaseValueRow(string? columnData, int sequence, out RawCrmBaseValueRow? row)
        {
            row = null;
            if (string.IsNullOrWhiteSpace(columnData)) return false;

            try
            {
                using var doc = JsonDocument.Parse(columnData);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;

                var jsonMap = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in doc.RootElement.EnumerateObject())
                    jsonMap[prop.Name] = prop.Value;

                var solutionLabel = GetJsonString(jsonMap, "Solution Label", "SolutionLabel", "Sample ID", "SampleId", "Sample", "Label", "Name");
                if (string.IsNullOrWhiteSpace(solutionLabel)) return false;
                if (BlankLabelRegex.IsMatch(solutionLabel)) return false;

                var crmMatch = CrmIdRegex.Match(solutionLabel);
                if (!crmMatch.Success) return false;

                var element = GetJsonString(jsonMap, "Element");
                if (string.IsNullOrWhiteSpace(element)) return false;

                var solnConc = GetJsonDecimal(jsonMap, "Soln Conc", "SolnConc");
                var actVol = GetJsonDecimal(jsonMap, "Act Vol", "ActVol");
                var actWgt = GetJsonDecimal(jsonMap, "Act Wgt", "ActWgt");
                var df = GetJsonDecimal(jsonMap, "DF");
                var corrCon = GetJsonDecimal(jsonMap, "Corr Con", "CorrCon", "Concentration", "Conc", "Calibrated Conc");

                decimal? baseValue = null;
                if (solnConc.HasValue)
                {
                    var factor = 1m;
                    if (actVol.HasValue && actWgt.HasValue && actWgt.Value != 0m)
                        factor = actVol.Value / actWgt.Value;

                    if (df.HasValue)
                        factor *= df.Value;

                    baseValue = solnConc.Value * factor;
                }

                baseValue ??= corrCon ?? solnConc;
                if (!baseValue.HasValue) return false;

                row = new RawCrmBaseValueRow
                {
                    Sequence = sequence,
                    SolutionLabel = solutionLabel.Trim(),
                    NormalizedLabel = NormalizeSolutionLabel(solutionLabel),
                    CrmId = NormalizeCrmIdToken(crmMatch.Groups[1].Value),
                    Element = element.Trim(),
                    BaseValue = baseValue.Value
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseRawBlankValueRow(string? columnData, int sequence, out RawBlankValueRow? row)
        {
            row = null;
            if (string.IsNullOrWhiteSpace(columnData)) return false;

            try
            {
                using var doc = JsonDocument.Parse(columnData);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;

                var jsonMap = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in doc.RootElement.EnumerateObject())
                    jsonMap[prop.Name] = prop.Value;

                var solutionLabel = GetJsonString(jsonMap, "Solution Label", "SolutionLabel", "Sample ID", "SampleId", "Sample", "Label", "Name");
                if (string.IsNullOrWhiteSpace(solutionLabel)) return false;
                if (!BlankLabelRegex.IsMatch(solutionLabel)) return false;

                var element = GetJsonString(jsonMap, "Element");
                if (string.IsNullOrWhiteSpace(element)) return false;

                var solnConc = GetJsonDecimal(jsonMap, "Soln Conc", "SolnConc");
                var actVol = GetJsonDecimal(jsonMap, "Act Vol", "ActVol");
                var actWgt = GetJsonDecimal(jsonMap, "Act Wgt", "ActWgt");
                var df = GetJsonDecimal(jsonMap, "DF");
                var corrCon = GetJsonDecimal(jsonMap, "Corr Con", "CorrCon", "Concentration", "Conc", "Calibrated Conc");

                decimal? blankValue = null;
                if (solnConc.HasValue)
                {
                    var factor = 1m;
                    if (actVol.HasValue && actWgt.HasValue && actWgt.Value != 0m)
                        factor = actVol.Value / actWgt.Value;

                    if (df.HasValue)
                        factor *= df.Value;

                    blankValue = solnConc.Value * factor;
                }

                blankValue ??= corrCon ?? solnConc;
                if (!blankValue.HasValue) return false;

                row = new RawBlankValueRow
                {
                    Sequence = sequence,
                    SolutionLabel = solutionLabel.Trim(),
                    Element = element.Trim(),
                    BlankValue = blankValue.Value
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        private (Dictionary<string, Queue<decimal>> ByLabel, Dictionary<string, Queue<decimal>> ByCrmId) BuildRawBaseValueQueuesForFocusElement()
        {
            var byLabel = new Dictionary<string, Queue<decimal>>(StringComparer.OrdinalIgnoreCase);
            var byCrmId = new Dictionary<string, Queue<decimal>>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(_focusElement) || !_rawCrmBaseValues.Any())
                return (byLabel, byCrmId);

            var normalizedFocusElement = NormalizeElementFull(_focusElement);
            var focusElementName = NormalizeElementName(_focusElement);
            var focusWavelength = NormalizeElementWavelength(_focusElement);
            var hasFocusWavelength = !string.IsNullOrWhiteSpace(focusWavelength);

            foreach (var row in _rawCrmBaseValues.OrderBy(r => r.Sequence))
            {
                var rowElementFull = NormalizeElementFull(row.Element);
                var rowElementName = NormalizeElementName(row.Element);
                var rowWavelength = NormalizeElementWavelength(row.Element);

                var isMatch = string.Equals(rowElementFull, normalizedFocusElement, StringComparison.OrdinalIgnoreCase);

                if (!isMatch)
                {
                    if (hasFocusWavelength)
                    {
                        isMatch =
                            string.Equals(rowElementName, focusElementName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(rowWavelength, focusWavelength, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        isMatch = string.Equals(rowElementName, focusElementName, StringComparison.OrdinalIgnoreCase);
                    }
                }

                if (!isMatch) continue;

                if (!byLabel.TryGetValue(row.NormalizedLabel, out var labelQueue))
                {
                    labelQueue = new Queue<decimal>();
                    byLabel[row.NormalizedLabel] = labelQueue;
                }
                labelQueue.Enqueue(row.BaseValue);

                if (!string.IsNullOrWhiteSpace(row.CrmId))
                {
                    if (!byCrmId.TryGetValue(row.CrmId, out var crmQueue))
                    {
                        crmQueue = new Queue<decimal>();
                        byCrmId[row.CrmId] = crmQueue;
                    }
                    crmQueue.Enqueue(row.BaseValue);
                }
            }

            return (byLabel, byCrmId);
        }

        private void UpdateBlankLabelsForFocusElement()
        {
            _blankLabelsForFocus.Clear();

            if (string.IsNullOrWhiteSpace(_focusElement) || !_rawBlankValues.Any())
                return;

            var normalizedFocusElement = NormalizeElementFull(_focusElement);
            var focusElementName = NormalizeElementName(_focusElement);
            var focusWavelength = NormalizeElementWavelength(_focusElement);
            var hasFocusWavelength = !string.IsNullOrWhiteSpace(focusWavelength);

            foreach (var row in _rawBlankValues.OrderBy(r => r.Sequence))
            {
                var rowElementFull = NormalizeElementFull(row.Element);
                var rowElementName = NormalizeElementName(row.Element);
                var rowWavelength = NormalizeElementWavelength(row.Element);

                var isMatch = string.Equals(rowElementFull, normalizedFocusElement, StringComparison.OrdinalIgnoreCase);

                if (!isMatch)
                {
                    if (hasFocusWavelength)
                    {
                        isMatch =
                            string.Equals(rowElementName, focusElementName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(rowWavelength, focusWavelength, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        isMatch = string.Equals(rowElementName, focusElementName, StringComparison.OrdinalIgnoreCase);
                    }
                }

                if (!isMatch) continue;

                _blankLabelsForFocus.Add($"{row.SolutionLabel}: {row.BlankValue:0.###}");
            }
        }

        private static bool TryDequeueRawBaseValue(
            string? solutionLabel,
            string? crmId,
            Dictionary<string, Queue<decimal>> rawValuesByLabel,
            Dictionary<string, Queue<decimal>> rawValuesByCrmId,
            out decimal rawValue)
        {
            rawValue = 0m;

            var normalizedLabel = NormalizeSolutionLabel(solutionLabel);
            if (!string.IsNullOrWhiteSpace(normalizedLabel) &&
                rawValuesByLabel.TryGetValue(normalizedLabel, out var byLabelQueue) &&
                byLabelQueue.Count > 0)
            {
                rawValue = byLabelQueue.Dequeue();
                return true;
            }

            var normalizedCrmId = NormalizeCrmIdToken(string.IsNullOrWhiteSpace(crmId) ? solutionLabel : crmId);
            if (!string.IsNullOrWhiteSpace(normalizedCrmId) &&
                rawValuesByCrmId.TryGetValue(normalizedCrmId, out var byCrmQueue) &&
                byCrmQueue.Count > 0)
            {
                rawValue = byCrmQueue.Dequeue();
                return true;
            }

            return false;
        }

        private static string? GetJsonString(IReadOnlyDictionary<string, JsonElement> map, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!map.TryGetValue(key, out var value)) continue;

                if (value.ValueKind == JsonValueKind.String)
                    return value.GetString();

                if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var numericValue))
                    return numericValue.ToString(CultureInfo.InvariantCulture);
            }

            return null;
        }

        private static decimal? GetJsonDecimal(IReadOnlyDictionary<string, JsonElement> map, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!map.TryGetValue(key, out var value)) continue;

                if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var numericValue))
                    return numericValue;

                if (value.ValueKind != JsonValueKind.String) continue;
                var valueText = value.GetString();
                if (decimal.TryParse(valueText, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue))
                    return parsedValue;
                if (decimal.TryParse(valueText, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue))
                    return parsedValue;
            }

            return null;
        }

        private HashSet<string> ParseExcludedLabels()
        {
            if (string.IsNullOrWhiteSpace(_excludedLabelsInput))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return _excludedLabelsInput
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private bool ShouldApplyManualCorrection(string solutionLabel, decimal rawValue, HashSet<string> excludedLabels)
        {
            if (string.IsNullOrWhiteSpace(solutionLabel)) return false;
            if (BlankLabelRegex.IsMatch(solutionLabel)) return false;
            if (excludedLabels.Contains(solutionLabel)) return false;

            if (_scaleAbove50Only && rawValue <= 50m) return false;

            if (_scaleRangeMin.HasValue && _scaleRangeMax.HasValue)
            {
                var minRange = Math.Min(_scaleRangeMin.Value, _scaleRangeMax.Value);
                var maxRange = Math.Max(_scaleRangeMin.Value, _scaleRangeMax.Value);

                if (rawValue < minRange || rawValue > maxRange) return false;
            }
            else if (_scaleRangeMin.HasValue || _scaleRangeMax.HasValue)
            {
                if (_scaleRangeMin.HasValue && rawValue < _scaleRangeMin.Value) return false;
                if (_scaleRangeMax.HasValue && rawValue > _scaleRangeMax.Value) return false;
            }

            return true;
        }

        private static string NormalizeSolutionLabel(string? raw) => string.IsNullOrWhiteSpace(raw) ? string.Empty : MultiWhitespaceRegex.Replace(raw.Trim(), " ");

        // این تابع را جایگزین TryGetElementValueExact کنید
        // این تابع ساده و مستقیم را جایگزین کنید
        private static bool TryGetElementValueExact(IReadOnlyDictionary<string, decimal?> values, string? el, out decimal? v)
        {
            v = null;
            if (values == null || string.IsNullOrEmpty(el)) return false;
            static string NormalizeLookup(string raw) =>
                MultiWhitespaceRegex.Replace((raw ?? string.Empty).Replace('_', ' ').Trim(), " ");

            // 1. تطبیق دقیق - مهمترین حالت
            if (values.TryGetValue(el, out v))
            {
                return true;
            }

            // 2. تطبیق بدون حساسیت به حروف
            var exactMatch = values.FirstOrDefault(kvp =>
                string.Equals(kvp.Key, el, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(exactMatch.Key))
            {
                v = exactMatch.Value;
                return true;
            }

            var normalizedTarget = NormalizeLookup(el);
            var normalizedMatch = values.FirstOrDefault(kvp =>
                string.Equals(NormalizeLookup(kvp.Key), normalizedTarget, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(normalizedMatch.Key))
            {
                v = normalizedMatch.Value;
                return true;
            }

            var targetName = NormalizeElementName(el);
            var targetWave = NormalizeElementWavelength(el);
            if (!string.IsNullOrWhiteSpace(targetWave))
            {
                var waveMatches = values.Where(kvp =>
                    string.Equals(NormalizeElementName(kvp.Key), targetName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(NormalizeElementWavelength(kvp.Key), targetWave, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (waveMatches.Count > 0)
                {
                    v = waveMatches[0].Value;
                    return true;
                }

                return false;
            }

            // 3. اگر el شامل فاصله است (مثلاً "Ag 328.068")
            if (el.Contains(' '))
            {
                var parts = el.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var elementName = parts[0];

                    // پیدا کردن کلیدی که با نام عنصر شروع می‌شود
                    var nameMatch = values.FirstOrDefault(kvp =>
                        kvp.Key.StartsWith(elementName, StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrEmpty(nameMatch.Key))
                    {
                        v = nameMatch.Value;
                        return true;
                    }
                }
            }

            // 4. اگر el نام ساده است (مثلاً "Ag")
            var simpleMatch = values.FirstOrDefault(kvp =>
                kvp.Key.StartsWith(el, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(simpleMatch.Key))
            {
                v = simpleMatch.Value;
                return true;
            }

            return false;
        }

        // تابع مشابه برای داده‌های مرجع
        private static bool TryGetReferenceElementValueExact(IReadOnlyDictionary<string, decimal> values, string? el, out decimal v)
        {
            v = 0;
            if (values == null || string.IsNullOrEmpty(el)) return false;

            // 1. تطبیق دقیق
            if (values.TryGetValue(el, out v))
            {
                return true;
            }

            // 2. تطبیق بدون حساسیت به حروف
            var exactMatch = values.FirstOrDefault(kvp =>
                string.Equals(kvp.Key, el, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(exactMatch.Key))
            {
                v = exactMatch.Value;
                return true;
            }

            // 3. اگر el شامل فاصله است
            if (el.Contains(' '))
            {
                var parts = el.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var elementName = parts[0];

                    var nameMatch = values.FirstOrDefault(kvp =>
                        kvp.Key.StartsWith(elementName, StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrEmpty(nameMatch.Key))
                    {
                        v = nameMatch.Value;
                        return true;
                    }
                }
            }

            // 4. اگر el نام ساده است
            var simpleMatch = values.FirstOrDefault(kvp =>
                kvp.Key.StartsWith(el, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(simpleMatch.Key))
            {
                v = simpleMatch.Value;
                return true;
            }

            return false;
        }

        private string FindSimilarElementInData(string element)
        {
            if (string.IsNullOrEmpty(element)) return element;

            // اگر عنصر از قبل در لیست عناصر وجود دارد، برگردان
            if (_allElements.Contains(element))
                return element;

            // اگر عنصر شامل طول موج است (مثلاً "Ag 328.068")
            if (element.Contains(' '))
            {
                var elementName = element.Split(' ')[0];

                // پیدا کردن اولین عنصر در لیست که با این نام شروع می‌شود
                var similar = _allElements.FirstOrDefault(e =>
                    e.StartsWith(elementName, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(similar))
                {
                    Console.WriteLine($"Found similar element for '{element}': '{similar}'");
                    return similar;
                }
            }
            else
            {
                // اگر فقط نام عنصر است، پیدا کردن اولین تطابق
                var similar = _allElements.FirstOrDefault(e =>
                    e.StartsWith(element, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(similar))
                {
                    Console.WriteLine($"Found similar element for '{element}': '{similar}'");
                    return similar;
                }
            }

            return element;
        }

        private CrmListItemDto? ResolveCrmReferenceForRow(string id, string? opt)
        {
            var normalizedId = NormalizeCrmIdToken(id);
            var (selectedCrmKey, selectedMethodHint) = ParseSelectedOption(opt);

            List<CrmListItemDto>? candidateList = null;

            if (!string.IsNullOrWhiteSpace(selectedCrmKey))
            {
                if (!_crmReferenceById.TryGetValue(selectedCrmKey, out candidateList))
                {
                    candidateList = _crmReferenceById
                        .FirstOrDefault(kvp => string.Equals(kvp.Key, selectedCrmKey, StringComparison.OrdinalIgnoreCase))
                        .Value;
                }
            }

            if (candidateList == null || candidateList.Count == 0)
            {
                candidateList = _crmReferenceById
                    .Where(kvp => kvp.Key.StartsWith("OREAS", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault(kvp => string.Equals(NormalizeCrmIdToken(kvp.Key), normalizedId, StringComparison.OrdinalIgnoreCase))
                    .Value;
            }

            if (candidateList == null || candidateList.Count == 0)
            {
                candidateList = _crmReferenceById
                    .FirstOrDefault(kvp => string.Equals(NormalizeCrmIdToken(kvp.Key), normalizedId, StringComparison.OrdinalIgnoreCase))
                    .Value;
            }

            if (candidateList == null || candidateList.Count == 0) return null;

            if (!string.IsNullOrWhiteSpace(selectedMethodHint))
            {
                var exactMethod = candidateList.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x.AnalysisMethod) &&
                    string.Equals(x.AnalysisMethod.Trim(), selectedMethodHint, StringComparison.OrdinalIgnoreCase));
                if (exactMethod != null) return exactMethod;

                var containsMethod = candidateList.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x.AnalysisMethod) &&
                    (selectedMethodHint.Contains(x.AnalysisMethod, StringComparison.OrdinalIgnoreCase) ||
                     x.AnalysisMethod.Contains(selectedMethodHint, StringComparison.OrdinalIgnoreCase)));
                if (containsMethod != null) return containsMethod;
            }

            return candidateList.FirstOrDefault(x =>
                       !string.IsNullOrWhiteSpace(x.AnalysisMethod) &&
                       (x.AnalysisMethod.Contains("4-Acid", StringComparison.OrdinalIgnoreCase) ||
                        x.AnalysisMethod.Contains("Aqua Regia", StringComparison.OrdinalIgnoreCase)))
                   ?? candidateList.First();
        }

        private static string NormalizeCrmIdToken(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var match = CrmIdRegex.Match(text.Trim());
            return match.Success
                ? match.Groups[1].Value.Trim().ToLowerInvariant()
                : text.Trim().ToLowerInvariant();
        }

        private static (string? CrmKey, string? MethodHint) ParseSelectedOption(string? option)
        {
            if (string.IsNullOrWhiteSpace(option)) return (null, null);

            var text = option.Trim();
            var methodMatch = Regex.Match(text, @"^(?<key>.+?)\s*\((?<method>[^)]+)\)\s*$");
            if (methodMatch.Success)
            {
                return (
                    methodMatch.Groups["key"].Value.Trim(),
                    methodMatch.Groups["method"].Value.Trim()
                );
            }

            return (text, text);
        }

        private decimal GetToleranceValue(decimal v)
        {
            var a = Math.Abs(v); if (a < 10) return _rangeLow;
            var pct = a < 100 ? _rangeMid : a < 1000 ? _rangeHigh1 : a < 10000 ? _rangeHigh2 : a < 100000 ? _rangeHigh3 : _rangeHigh4;
            return a * (pct / 100m);
        }

        private async Task SetFocusElement(string? el)
        {
            if (_isDisposed) return;

            bool lockTaken;
            try
            {
                lockTaken = await _loadingLock.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (!lockTaken)
            {
                Snackbar.Add("System is busy. Please wait...", Severity.Warning);
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(el))
                {
                    Console.WriteLine($"=== SetFocusElement: '{el}' ===");

                    // بررسی آیا عنصر در لیست وجود دارد
                    if (_allElements.Contains(el))
                    {
                        _focusElement = el;
                        Console.WriteLine($"Element found in list: {_focusElement}");
                    }
                    else
                    {
                        // پیدا کردن عنصر مشابه
                        var similarElement = FindSimilarElementInData(el);
                        _focusElement = similarElement;
                        Console.WriteLine($"Using similar element: {_focusElement}");
                    }

                    // لاگ عناصر مشابه
                    var similarElements = _allElements
                        .Where(e => e.StartsWith(el.Split(' ')[0], StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    Console.WriteLine($"Similar elements found: {similarElements.Count}");
                    foreach (var similar in similarElements)
                    {
                        Console.WriteLine($"  - {similar}");
                    }
                }

                UpdateBlankLabelsForFocusElement();
                await LoadSecondaryPlotRowsAsync();
                await RefreshChartsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SetFocusElement: {ex.Message}");
                Snackbar.Add($"Element update failed: {ex.Message}", Severity.Error);
            }
            finally
            {
                try { _loadingLock.Release(); } catch (ObjectDisposedException) { }
                if (!_isDisposed)
                    StateHasChanged();
            }
        }
        private async Task PrevElement()
        {
            _isLoading = true;

            var currentIndex = _allElements.IndexOf(_focusElement ?? "");
            if (currentIndex > 0)
            {
                var prevElement = _allElements[currentIndex - 1];
                await SetFocusElement(prevElement);
            }
            else
            {
                Snackbar.Add("Already at first element", Severity.Info);
            }
            _isLoading = false;
        }

        private async Task NextElement()
        {
            _isLoading = true;
            var currentIndex = _allElements.IndexOf(_focusElement ?? "");
            if (currentIndex < _allElements.Count - 1 && currentIndex >= 0)
            {
                var nextElement = _allElements[currentIndex + 1];
                await SetFocusElement(nextElement);
            }
            else if (currentIndex < 0 && _allElements.Any())
            {
                await SetFocusElement(_allElements[0]);
            }
            else
            {
                Snackbar.Add("Already at last element", Severity.Info);
            }
            _isLoading = false;
        }

        private async Task RunCalibration()
        {
            if (!_projectId.HasValue) return;
            if (_isDisposed) return;

            bool lockTaken;
            try
            {
                lockTaken = await _loadingLock.WaitAsync(0);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (!lockTaken) return;

            _isLoading = true;
            try
            {
                await AwaitWithTimeout(LoadSecondaryPlotRowsAsync(), TimeSpan.FromSeconds(40), "Load pivot rows");
                await AwaitWithTimeout(GetCurrentStats(), TimeSpan.FromSeconds(40), "Load current stats");
                await AwaitWithTimeout(RefreshChartsAsync(), TimeSpan.FromSeconds(20), "Render charts");
            }
            catch (TimeoutException timeoutEx)
            {
                Snackbar.Add(timeoutEx.Message, Severity.Warning);
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Calibration failed: {ex.Message}", Severity.Error);
            }
            finally
            {
                _isLoading = false;
                try { _loadingLock.Release(); } catch (ObjectDisposedException) { }
                if (!_isDisposed)
                    StateHasChanged();
            }
        }

        private async Task ResetAll()
        {
            _minDiff = -10m;
            _maxDiff = 10m;
            _useMultiModel = true;
            _previewBlank = 0m;
            _previewScale = 1.0;
            _scaleRangeMin = null;
            _scaleRangeMax = null;
            _scaleAbove50Only = false;
            _excludedLabelsInput = string.Empty;
            _filterText = string.Empty;
            _sampleFilter = string.Empty;

            ResetRanges();

            _selectedElements = new HashSet<string>();

            await RefreshChartsAsync(refreshCalibration: true, refreshSecondary: true);

            Snackbar.Add("All settings have been reset to default values.", Severity.Info);

            StateHasChanged();
        }


        private async Task LoadElements()
        {
            Console.WriteLine("=== LoadElements START ===");

            var allElementKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. از PivotService بگیر (اما احتمالاً بدون طول موج است)
            var r = await PivotService.GetElementsAsync(_projectId!.Value);
            if (r.Succeeded && r.Data != null)
            {
                foreach (var el in r.Data)
                {
                    allElementKeys.Add(el);
                }
                Console.WriteLine($"Added {r.Data.Count} elements from PivotService");
            }

            // 2. از داده‌های پیوت استخراج کن (احتمالاً با طول موج)
            try
            {
                var pivotRequest = new AdvancedPivotRequest(
                    ProjectId: _projectId.Value,
                    SearchText: null,
                    SelectedElements: null,
                    NumberFilters: null,
                    UseOxide: false,
                    UseInt: false,
                    DecimalPlaces: 4,
                    Page: 1,
                    PageSize: 100,
                    Aggregation: "First",
                    MergeRepeats: false
                );

                var pivotResult = await PivotService.GetAdvancedPivotTableAsync(pivotRequest);
                if (pivotResult.Succeeded && pivotResult.Data != null && pivotResult.Data.Rows.Any())
                {
                    foreach (var row in pivotResult.Data.Rows)
                    {
                        foreach (var key in row.Values.Keys)
                        {
                            // فقط اضافه کن اگر شامل عدد باشد (طول موج)
                            if (key.Any(char.IsDigit) && key.Contains(' '))
                            {
                                allElementKeys.Add(key);
                            }
                        }
                    }
                    Console.WriteLine($"Added elements from pivot data, total: {allElementKeys.Count}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading from pivot: {ex.Message}");
            }

            // 3. لیست نهایی را مرتب کن
            _allElements = allElementKeys
                .OrderBy(e => e)
                .ToList();

            Console.WriteLine($"Total elements loaded: {_allElements.Count}");

            // نمایش عناصر با طول موج
            var elementsWithWavelength = _allElements.Where(e => e.Contains(' ') && e.Any(char.IsDigit)).ToList();
            Console.WriteLine($"Elements with wavelength: {elementsWithWavelength.Count}");
            foreach (var el in elementsWithWavelength.Take(20))
            {
                Console.WriteLine($"  '{el}'");
            }

            // گروه‌بندی عناصر مشابه
            var elementGroups = _allElements
                .GroupBy(e =>
                {
                    var parts = e.Split(' ');
                    return parts.Length > 0 ? parts[0].ToUpper() : e.ToUpper();
                })
                .Where(g => g.Count() > 1)
                .ToList();

            Console.WriteLine($"Found {elementGroups.Count} element groups with multiple wavelengths");
            foreach (var group in elementGroups)
            {
                Console.WriteLine($"  {group.Key}: {string.Join(", ", group.Take(5))}");
            }

            // انتخاب عنصر فوکوس اولیه
            if (_allElements.Any())
            {
                // اولویت با عناصری که طول موج دارند
                var firstWithWavelength = _allElements.FirstOrDefault(e => e.Contains(' ') && e.Any(char.IsDigit));
                _focusElement = firstWithWavelength ?? _allElements[0];
                Console.WriteLine($"Focus element set to: {_focusElement}");
            }

            UpdateBlankLabelsForFocusElement();

            Console.WriteLine("=== LoadElements END ===");
        }


        private async Task LoadCrmReferenceData()
        {
            var r = await CrmService.GetCrmListAsync(pageSize: 0);
            var items = r.Data?.Items ?? new List<CrmListItemDto>();

            if (r.Succeeded && items.Any())
            {
                _crmReferenceById = items
                    .GroupBy(x => x.CrmId)
                    .ToDictionary(g => g.Key, g => g.ToList());
                return;
            }

            _crmReferenceById = new Dictionary<string, List<CrmListItemDto>>(StringComparer.OrdinalIgnoreCase);
        }

        private async Task LoadCrmSelections()
        {
            var r = await OptimizationService.GetCrmSelectionOptionsAsync(_projectId!.Value);
            var apiRows = r.Data?.Items?.Where(x => x != null).ToList() ?? new List<CrmSelectionRowDto>();

            if (r.Succeeded && apiRows.Any())
            {
                _crmSelectionRows = apiRows;
                return;
            }

            var fallbackRows = BuildFallbackCrmSelectionRowsFromSecondary();
            if (fallbackRows.Any())
            {
                _crmSelectionRows = fallbackRows;
                if (r.Succeeded)
                    Snackbar.Add("CRM options API returned empty list; fallback CRM rows loaded from data.", Severity.Warning);
                else
                    Snackbar.Add(r.Message ?? "CRM options API failed; fallback CRM rows loaded from data.", Severity.Warning);
                return;
            }

            _crmSelectionRows = apiRows;
            if (!r.Succeeded)
                Snackbar.Add(r.Message ?? "Failed to load CRM rows.", Severity.Warning);
        }

        private List<CrmSelectionRowDto> BuildFallbackCrmSelectionRowsFromSecondary()
        {
            var rows = new List<CrmSelectionRowDto>();
            if (_secondaryRows == null || _secondaryRows.Count == 0)
                return rows;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in _secondaryRows
                .OrderBy(r => r.OriginalIndex)
                .ThenBy(r => r.SetIndex))
            {
                var solutionLabel = row.SolutionLabel?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(solutionLabel)) continue;
                if (BlankLabelRegex.IsMatch(solutionLabel)) continue;

                var crmMatch = CrmIdRegex.Match(solutionLabel);
                if (!crmMatch.Success) continue;

                var crmId = NormalizeCrmIdToken(crmMatch.Groups[1].Value);
                if (string.IsNullOrWhiteSpace(crmId)) continue;

                var rowKey = $"{solutionLabel}::{row.OriginalIndex}";
                if (!seen.Add(rowKey))
                    continue;

                var options = BuildCrmMethodOptions(crmId);
                if (!options.Any())
                    options.Add($"V {crmId}");

                rows.Add(new CrmSelectionRowDto
                {
                    SolutionLabel = solutionLabel,
                    RowIndex = row.OriginalIndex,
                    CrmId = crmId,
                    PreferredOptions = options.ToList(),
                    AllOptions = options.ToList(),
                    SelectedOption = options.LastOrDefault()
                });
            }

            return rows;
        }

        private List<string> BuildCrmMethodOptions(string crmId)
        {
            if (string.IsNullOrWhiteSpace(crmId))
                return new List<string>();

            var normalizedCrmId = NormalizeCrmIdToken(crmId);

            var optionsFromKeys = _crmReferenceById.Keys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Where(key => string.Equals(NormalizeCrmIdToken(key), normalizedCrmId, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (optionsFromKeys.Any())
                return optionsFromKeys;

            return _crmReferenceById.Values
                .SelectMany(list => list)
                .Where(item => !string.IsNullOrWhiteSpace(item.CrmId))
                .Where(item => string.Equals(NormalizeCrmIdToken(item.CrmId), normalizedCrmId, StringComparison.OrdinalIgnoreCase))
                .Select(item =>
                    !string.IsNullOrWhiteSpace(item.AnalysisMethod)
                        ? $"{item.CrmId.Trim()} ({item.AnalysisMethod.Trim()})"
                        : item.CrmId.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }


        private async Task LoadSecondaryPlotRowsAsync()
        {
            Console.WriteLine("=== LoadSecondaryPlotRowsAsync START ===");
            var r = await PivotService.GetAdvancedPivotTableAsync(new AdvancedPivotRequest(
                ProjectId: _projectId!.Value,
                SearchText: null,
                SelectedElements: null,
                NumberFilters: null,
                UseOxide: false,
                UseInt: false,
                DecimalPlaces: 4,
                Page: 1,
                PageSize: 5000,
                Aggregation: "First",
                MergeRepeats: false));

            if (r.Succeeded)
            {
                _secondaryRows = r.Data.Rows;

                // استخراج کلیدهای عناصر از داده‌های پیوت (اینها احتمالاً شامل طول موج هستند)
                if (_secondaryRows.Any() && _secondaryRows.First().Values.Any())
                {
                    var allElementKeys = _secondaryRows
                        .SelectMany(r => r.Values.Keys)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(k => k)
                        .ToList();

                    Console.WriteLine($"Found {allElementKeys.Count} element keys in pivot data");

                    // اگر لیست عناصر خالی است یا فقط نام عناصر بدون طول موج دارد،
                    // از کلیدهای پیوت استفاده کن
                    if (!_allElements.Any() ||
                        (_allElements.Count > 0 && !allElementKeys.Any(k => k.Contains(" "))))
                    {
                        _allElements = allElementKeys;
                        Console.WriteLine($"Updated _allElements from pivot data: {_allElements.Count} elements");

                        if (!string.IsNullOrEmpty(_focusElement) && _allElements.Contains(_focusElement))
                        {
                            // عنصر فوکوس در لیست جدید وجود دارد
                        }
                        else if (_allElements.Any())
                        {
                            _focusElement = _allElements[0];
                            Console.WriteLine($"Focus element updated to: {_focusElement}");
                        }
                    }

                    // نمایش نمونه‌ای از عناصر
                    Console.WriteLine("First 20 element keys in pivot data:");
                    foreach (var key in allElementKeys.Take(20))
                    {
                        Console.WriteLine($"  '{key}'");
                    }

                    // گروه‌بندی عناصر بر اساس نام
                    var elementGroups = allElementKeys
                        .GroupBy(k =>
                        {
                            var parts = k.Split(' ');
                            return parts.Length > 0 ? parts[0] : k;
                        })
                        .Where(g => g.Count() > 1)
                        .ToList();

                    Console.WriteLine($"Found {elementGroups.Count} elements with multiple wavelengths");
                    foreach (var group in elementGroups)
                    {
                        Console.WriteLine($"  {group.Key}: {string.Join(", ", group)}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"Failed to load pivot rows: {r.Message}");
            }
            UpdateBlankLabelsForFocusElement();
            Console.WriteLine("=== LoadSecondaryPlotRowsAsync END ===");
        }


        private void ExtractElementsFromOptimizedData()
        {
            if (_result?.OptimizedData == null || !_result.OptimizedData.Any())
                return;

            Console.WriteLine("=== ExtractElementsFromOptimizedData START ===");

            // استخراج کلیدهای عناصر از داده‌های بهینه‌سازی
            var allElementKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sample in _result.OptimizedData)
            {
                foreach (var key in sample.OriginalValues.Keys)
                {
                    allElementKeys.Add(key);
                }
                foreach (var key in sample.OptimizedValues.Keys)
                {
                    allElementKeys.Add(key);
                }
                foreach (var key in sample.CrmValues.Keys)
                {
                    allElementKeys.Add(key);
                }
            }

            var elementKeysList = allElementKeys.OrderBy(k => k).ToList();
            Console.WriteLine($"Found {elementKeysList.Count} element keys in optimized data");

            // به‌روزرسانی لیست عناصر اگر نیاز باشد
            if (elementKeysList.Any() && elementKeysList.Any(k => k.Contains(" ")))
            {
                _allElements = elementKeysList;
                Console.WriteLine($"Updated _allElements from optimized data: {_allElements.Count} elements");

                // اگر عنصر فوکوس در لیست نیست، اولین عنصر را انتخاب کن
                if (!string.IsNullOrEmpty(_focusElement) && _allElements.Contains(_focusElement))
                {
                    // عنصر فوکوس در لیست جدید وجود دارد
                }
                else if (_allElements.Any())
                {
                    _focusElement = _allElements[0];
                    Console.WriteLine($"Focus element updated to: {_focusElement}");
                }
            }

            // نمایش نمونه‌ای
            Console.WriteLine("First 20 element keys in optimized data:");
            foreach (var key in elementKeysList.Take(20))
            {
                Console.WriteLine($"  '{key}'");
            }

            UpdateBlankLabelsForFocusElement();
            Console.WriteLine("=== ExtractElementsFromOptimizedData END ===");
        }

        private async Task GetCurrentStats()
        {
            var r = await OptimizationService.GetCurrentStatsAsync(_projectId!.Value, _minDiff, _maxDiff);
            if (r.Succeeded)
            {
                _result = r.Data;
                _optimizedRows = BuildOptimizedRows(_result?.OptimizedData, _focusElement);

                // استخراج عناصر از داده‌های بهینه‌سازی
                ExtractElementsFromOptimizedData();

                // برای دیباگ
                if (_result?.OptimizedData != null && _result.OptimizedData.Any())
                {
                    var firstSample = _result.OptimizedData.First();
                    Console.WriteLine("First sample element keys:");
                    Console.WriteLine($"  Original: {string.Join(", ", firstSample.OriginalValues.Keys.Take(5))}");
                    Console.WriteLine($"  Optimized: {string.Join(", ", firstSample.OptimizedValues.Keys.Take(5))}");
                    Console.WriteLine($"  CRM: {string.Join(", ", firstSample.CrmValues.Keys.Take(5))}");
                }
            }
        }
        private List<OptimizedSampleRow> BuildOptimizedRows(IEnumerable<OptimizedSampleDto>? d, string? e)
        {
            if (d == null || string.IsNullOrEmpty(e)) return new();

            return d.Select(s =>
            {
                TryGetElementValueExact(s.OriginalValues, e, out var orig);
                TryGetElementValueExact(s.OptimizedValues, e, out var opt);
                TryGetReferenceElementValueExact(s.CrmValues.ToDictionary(k => k.Key, v => v.Value ?? 0m), e, out var refV);
                decimal db = s.DiffPercentBefore.TryGetValue(e, out var v1) ? v1 : 0;
                decimal da = s.DiffPercentAfter.TryGetValue(e, out var v2) ? v2 : 0;
                bool p = s.PassStatusAfter.TryGetValue(e, out var ps) && ps;
                return new OptimizedSampleRow(s.SolutionLabel, s.CrmId, e, orig, opt, refV, db, da, p);
            }).ToList();
        }

        private IEnumerable<OptimizedSampleRow> FilterRows(IEnumerable<OptimizedSampleRow> rows) =>
            string.IsNullOrEmpty(_sampleFilter) ? rows : rows.Where(r => r.SolutionLabel.Contains(_sampleFilter, StringComparison.OrdinalIgnoreCase));

        private List<string> GetRowOptions(CrmSelectionRowDto r) => r.PreferredOptions.Concat(r.AllOptions).Distinct().ToList();

        private EventCallback<string> GetRowSelectionChangedHandler(CrmSelectionRowDto r) =>
            EventCallback.Factory.Create<string>(this, async v =>
            {
                r.SelectedOption = v;
                if (_projectId != null)
                    await OptimizationService.SaveCrmSelectionsAsync(new CrmSelectionSaveRequest
                    {
                        ProjectId = _projectId.Value,
                        Selections = new List<CrmSelectionItemDto>
                        {
                            new CrmSelectionItemDto
                            {
                                SolutionLabel = r.SolutionLabel,
                                RowIndex = r.RowIndex,
                                SelectedCrmKey = v
                            }
                        }
                    });
            });

        private async Task OnPreviewBlankChanged(decimal v)
        {
            _previewBlank = v;
            await RefreshChartsAsync(refreshCalibration: true, refreshSecondary: true);
        }

        private async Task OnPreviewScaleChanged(double v)
        {
            _previewScale = Math.Clamp(v, 0d, 2d);
            await RefreshChartsAsync(refreshCalibration: true, refreshSecondary: true);
        }

        private async Task OnRangeMinChanged(decimal? v)
        {
            _scaleRangeMin = v;

            if (_scaleRangeMin.HasValue && _scaleRangeMax.HasValue)
            {
                if (_scaleRangeMin.Value > _scaleRangeMax.Value)
                {
                    Snackbar.Add("Min Limit cannot be greater than Max Limit!", Severity.Error);
                    _scaleRangeMin = null;
                    _scaleRangeMax = null;
                    StateHasChanged();
                }
            }

            await RefreshChartsAsync(refreshCalibration: true, refreshSecondary: true);
        }

        private async Task OnRangeMaxChanged(decimal? v)
        {
            _scaleRangeMax = v;

            if (_scaleRangeMin.HasValue && _scaleRangeMax.HasValue)
            {
                if (_scaleRangeMin.Value > _scaleRangeMax.Value)
                {
                    Snackbar.Add("Min Limit cannot be greater than Max Limit!", Severity.Error);
                    _scaleRangeMin = null;
                    _scaleRangeMax = null;
                    StateHasChanged();
                }
            }

            await RefreshChartsAsync(refreshCalibration: true, refreshSecondary: true);
        }

        private IEnumerable<AdvancedPivotRowDto> GetFilteredSecondaryRows()
        {
            if (string.IsNullOrWhiteSpace(_filterText))
                return _secondaryRows;

            var filter = _filterText.Trim().ToLower();
            return _secondaryRows.Where(row =>
                !string.IsNullOrWhiteSpace(row.SolutionLabel) &&
                row.SolutionLabel.ToLower().Contains(filter));
        }

        private async Task ThrottledRefreshChartsAsync(bool refreshCalibration = true, bool refreshSecondary = true)
        {
            if (DateTime.Now - _lastRefreshTime < _refreshThrottleInterval)
                return;

            _lastRefreshTime = DateTime.Now;

            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();

            try
            {
                await Task.Delay(_refreshThrottleInterval, _refreshCts.Token);
                await RefreshChartsAsync(refreshCalibration, refreshSecondary);
            }
            catch (TaskCanceledException)
            {
                // اگر cancel شد، ignore کنیم
            }
        }

        private EventCallback<string> OnFilterTextChangedCallback =>
            EventCallback.Factory.Create<string>(this, async (value) =>
            {
                _filterText = value;
                await ThrottledRefreshChartsAsync(refreshCalibration: false, refreshSecondary: true);
            });

        public void Dispose()
        {
            _isDisposed = true;
            try { _refreshCts?.Cancel(); } catch (ObjectDisposedException) { }
            try { _refreshCts?.Dispose(); } catch (ObjectDisposedException) { }
        }



        private async Task DebugElementDataSources()
        {
            Console.WriteLine("=== DebugElementDataSources START ===");

            // 1. بررسی عناصر از PivotService
            var elementsResult = await PivotService.GetElementsAsync(_projectId!.Value);
            Console.WriteLine($"Elements from PivotService: {elementsResult.Data?.Count ?? 0}");
            if (elementsResult.Succeeded && elementsResult.Data != null)
            {
                foreach (var el in elementsResult.Data.Take(10))
                {
                    Console.WriteLine($"  - '{el}'");
                }
            }

            // 2. بررسی داده‌های پیوت
            var pivotRequest = new AdvancedPivotRequest(
                ProjectId: _projectId.Value,
                SearchText: null,
                SelectedElements: null,
                NumberFilters: null,
                UseOxide: false,
                UseInt: false,
                DecimalPlaces: 4,
                Page: 1,
                PageSize: 50,
                Aggregation: "First",
                MergeRepeats: false
            );

            var pivotResult = await PivotService.GetAdvancedPivotTableAsync(pivotRequest);
            if (pivotResult.Succeeded && pivotResult.Data != null && pivotResult.Data.Rows.Any())
            {
                var firstRow = pivotResult.Data.Rows.First();
                Console.WriteLine($"Element keys in first pivot row: {firstRow.Values.Count}");
                foreach (var key in firstRow.Values.Keys.Take(20))
                {
                    Console.WriteLine($"  - '{key}'");
                }
            }

            // 3. بررسی داده‌های بهینه‌سازی
            var statsResult = await OptimizationService.GetCurrentStatsAsync(_projectId.Value, _minDiff, _maxDiff);
            if (statsResult.Succeeded && statsResult.Data != null && statsResult.Data.OptimizedData != null)
            {
                var firstSample = statsResult.Data.OptimizedData.FirstOrDefault();
                if (firstSample != null)
                {
                    Console.WriteLine($"Element keys in optimized data:");
                    Console.WriteLine($"  Original: {firstSample.OriginalValues.Keys.Count()}");
                    Console.WriteLine($"  Optimized: {firstSample.OptimizedValues.Keys.Count()}");
                    Console.WriteLine($"  CRM: {firstSample.CrmValues.Keys.Count()}");

                    foreach (var key in firstSample.CrmValues.Keys.Take(10))
                    {
                        Console.WriteLine($"    - '{key}'");
                    }
                }
            }

            Console.WriteLine("=== DebugElementDataSources END ===");
        }
    }
}
