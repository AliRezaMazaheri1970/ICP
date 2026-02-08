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
        private int _populationSize = 50;
        private bool _useMultiModel = true;

        // --- تنظیمات دستی (Manual) ---
        private decimal _previewBlank = 0m;
        private double _previewScale = 1.0;
        private string _sampleFilter = "";
        private decimal? _scaleRangeMin;
        private decimal? _scaleRangeMax;
        private bool _scaleAbove50Only = false;
        private string _excludedLabelsInput = string.Empty;

        // --- داده‌ها و مراجع ---
        private List<AdvancedPivotRowDto> _secondaryRows = new();
        private BlankScaleOptimizationResult? _result;
        private List<OptimizedSampleRow> _optimizedRows = new();
        private List<OptimizedSampleRow> _manualRows = new();
        private List<CrmSelectionRowDto> _crmSelectionRows = new();
        private Dictionary<string, List<CrmListItemDto>> _crmReferenceById = new(StringComparer.OrdinalIgnoreCase);
        private List<RawCrmBaseValueRow> _rawCrmBaseValues = new();

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

        // ریجکس برای تشخیص برچسب‌های CRM (مطابق تصویر پایتون V 252b)
        private static readonly Regex CrmIdRegex = new(@"(?i)(?:\bCRM\b|\bOREAS\b|\bV\b)[^\d]*(\d+[a-zA-Z]?)", RegexOptions.Compiled);
        private static readonly Regex BlankLabelRegex = new(@"(?:CRM\s*)?(?:BLANK|BLNK)(?:\s+.*)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

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

        // ============================================================
        // چرخه حیات و بارگذاری (Lifecycle)
        // ============================================================

        protected override async Task OnInitializedAsync()
        {
            _projectId = projectId ?? ProjectService.CurrentProjectId;
            if (!_projectId.HasValue) return;

            var projectResult = await ProjectService.GetProjectAsync(_projectId.Value);
            if (projectResult.Succeeded) _projectName = projectResult.Data?.ProjectName;

            await LoadInitialDataInternalAsync();
        }

        private void OnBeforeNavigation(LocationChangingContext context)
        {
            if (_isLoading) context.PreventNavigation();
        }

        private async Task LoadInitialDataInternalAsync()
        {
            if (!await _loadingLock.WaitAsync(0)) return;
            _isLoading = true;
            try
            {
                await AwaitWithTimeout(LoadElements(), TimeSpan.FromSeconds(25), "Load elements");
                await AwaitWithTimeout(LoadCrmReferenceData(), TimeSpan.FromSeconds(25), "Load CRM references");
                await AwaitWithTimeout(LoadCrmSelections(), TimeSpan.FromSeconds(25), "Load CRM selections");
                await AwaitWithTimeout(LoadSecondaryPlotRowsAsync(), TimeSpan.FromSeconds(40), "Load pivot rows");
                await AwaitWithTimeout(LoadRawCrmBaseValuesAsync(), TimeSpan.FromSeconds(60), "Load raw CRM values");
                await AwaitWithTimeout(GetCurrentStats(), TimeSpan.FromSeconds(40), "Load current stats");
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
                _isLoading = false;
                _loadingLock.Release();
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

        private void ResetPreview() { _previewBlank = 0m; _previewScale = 1.0; StateHasChanged(); }

        private async Task OnResultsTabChanged(int i) { _resultsTabIndex = i; if (i == 1) await RefreshChartsAsync(); }

        private void OnPivotModeChanged(PivotValueMode m) => _pivotMode = m;

        private void OnPivotElementsChanged(IEnumerable<string> e) => _pivotSelectedElements = new HashSet<string>(e, StringComparer.OrdinalIgnoreCase);

        private void ResetPivotColumns() => _pivotSelectedElements.Clear();

        private void OpenRangesDialog() => _rangesDialogVisible = true;

        private async Task ApplyRangesAsync() { _rangesDialogVisible = false; await RefreshChartsAsync(); }

        private void ResetRanges() { _rangeLow = 2; _rangeMid = 20; _rangeHigh1 = 10; _rangeHigh2 = 8; _rangeHigh3 = 5; _rangeHigh4 = 3; }

        // ============================================================
        // منطق نمودار و محاسبات (Logic)
        // ============================================================

        private List<CalibrationRow> BuildCalibrationRows()
        {
            var rows = new List<CalibrationRow>();
            if (string.IsNullOrWhiteSpace(_focusElement)) return rows;

            var excludedLabels = ParseExcludedLabels();
            var selectionQueues = BuildSelectionQueueByLabel();
            var (rawValuesByLabel, rawValuesByCrmId) = BuildRawBaseValueQueuesForFocusElement();

            // Primary source: optimization stats (already CRM-aligned and exactly 8 rows for this project)
            var optimizedData = _result?.OptimizedData;
            if (optimizedData != null && optimizedData.Any())
            {
                var order = 0;
                foreach (var sample in optimizedData)
                {
                    var solutionLabel = sample.SolutionLabel?.Trim() ?? "";
                    var normalizedSolutionLabel = NormalizeSolutionLabel(solutionLabel);
                    if (string.IsNullOrWhiteSpace(solutionLabel)) continue;
                    if (BlankLabelRegex.IsMatch(solutionLabel)) continue;

                    string? selectedOption = null;
                    if (selectionQueues.TryGetValue(normalizedSolutionLabel, out var optionQueue) && optionQueue.Count > 0)
                        selectedOption = optionQueue.Dequeue();

                    var crmToken = NormalizeCrmIdToken(sample.CrmId);
                    if (string.IsNullOrWhiteSpace(crmToken))
                    {
                        var crmFromLabel = CrmIdRegex.Match(solutionLabel);
                        if (!crmFromLabel.Success) continue;
                        crmToken = NormalizeCrmIdToken(crmFromLabel.Groups[1].Value);
                    }

                    decimal rawValue;
                    if (!TryDequeueRawBaseValue(solutionLabel, crmToken, rawValuesByLabel, rawValuesByCrmId, out rawValue))
                    {
                        if (!TryGetElementValue(sample.OriginalValues, _focusElement, out var rawValueMaybe) || !rawValueMaybe.HasValue)
                        {
                            if (!TryGetElementValue(sample.OptimizedValues, _focusElement, out rawValueMaybe) || !rawValueMaybe.HasValue)
                                continue;
                        }
                        rawValue = rawValueMaybe.Value;
                    }

                    decimal? certValue = null;
                    if (TryGetElementValue(sample.CrmValues, _focusElement, out var crmValueMaybe) && crmValueMaybe.HasValue)
                        certValue = crmValueMaybe.Value;

                    if (!certValue.HasValue)
                    {
                        var crmRef = ResolveCrmReferenceForRow(crmToken, selectedOption);
                        if (crmRef != null && TryGetReferenceElementValue(crmRef.Elements, _focusElement, out var certFromRef))
                            certValue = certFromRef;
                    }

                    if (!certValue.HasValue) continue;

                    var correctedValue = rawValue;
                    if (ShouldApplyManualCorrection(solutionLabel, rawValue, excludedLabels))
                        correctedValue = (rawValue - _previewBlank) * (decimal)_previewScale;

                    rows.Add(new CalibrationRow
                    {
                        SolutionLabel = solutionLabel,
                        CrmId = crmToken,
                        OriginalIndex = order++,
                        RawValue = rawValue,
                        CorrectedValue = correctedValue,
                        CrmValue = certValue.Value
                    });
                }

                if (rows.Any()) return rows;
            }

            if (!_secondaryRows.Any()) return rows;

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
                    if (!TryGetElementValue(sourceRow.Values, _focusElement, out var rawValueMaybe)) continue;
                    rawValue = rawValueMaybe ?? 0m;
                }

                var crmRef = ResolveCrmReferenceForRow(crmId, selectedOption);
                if (crmRef == null || !TryGetReferenceElementValue(crmRef.Elements, _focusElement, out var certValue)) continue;

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
            var data = GetCalibrationChartData();
            if (data == null)
            {
                await JSRuntime.InvokeVoidAsync("destroyChart", "calibrationChart");
                return;
            }

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
                        }
                    }
                }
            };
            await JSRuntime.InvokeVoidAsync("createChart", "calibrationChart", config);
        }

        private async Task RenderSecondaryChartAsync()
        {
            if (string.IsNullOrWhiteSpace(_focusElement) || !_secondaryRows.Any())
            {
                await JSRuntime.InvokeVoidAsync("destroyChart", "secondaryChart");
                return;
            }

            var orderedRows = _secondaryRows
                .OrderBy(r => r.OriginalIndex)
                .ThenBy(r => r.SetIndex)
                .ToList();

            var originalPoints = new List<object>();
            var correctedPoints = new List<object>();
            var xValues = new List<double>();

            foreach (var row in orderedRows)
            {
                if (!TryGetElementValue(row.Values, _focusElement, out var valueMaybe)) continue;

                var rawValue = valueMaybe ?? 0m;
                var x = (double)row.OriginalIndex;
                var y = (double)rawValue;
                var corrected = (double)((rawValue - _previewBlank) * (decimal)_previewScale);

                xValues.Add(x);
                originalPoints.Add(new { x, y, label = row.SolutionLabel });
                correctedPoints.Add(new { x, y = corrected, label = row.SolutionLabel });
            }

            if (!originalPoints.Any() && !correctedPoints.Any())
            {
                await JSRuntime.InvokeVoidAsync("destroyChart", "secondaryChart");
                return;
            }

            var minX = xValues.Any() ? xValues.Min() - 1 : 0;
            var maxX = xValues.Any() ? xValues.Max() + 1 : 1;

            var config = new
            {
                type = "scatter",
                data = new
                {
                    datasets = new object[]
                    {
                        new
                        {
                            label = "Original",
                            data = originalPoints,
                            backgroundColor = "#2196F3",
                            borderColor = "#2196F3",
                            pointStyle = "circle",
                            pointRadius = 4,
                            showLine = false
                        },
                        new
                        {
                            label = "Corrected",
                            data = correctedPoints,
                            backgroundColor = "#F44336",
                            borderColor = "#F44336",
                            pointStyle = "crossRot",
                            pointRadius = 5,
                            showLine = false
                        }
                    }
                },
                options = new
                {
                    responsive = true,
                    maintainAspectRatio = false,
                    animation = false,
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
                        }
                    }
                }
            };
            await JSRuntime.InvokeVoidAsync("createChart", "secondaryChart", config);
        }

        private async Task RefreshChartsAsync() { await Task.Delay(250); await RenderCalibrationChartAsync(); await RenderSecondaryChartAsync(); await JSRuntime.InvokeVoidAsync("resizeAllCharts"); }

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
                    if (TryParseRawCrmBaseValueRow(rawRow.ColumnData, sequence, out var parsed) && parsed != null)
                        _rawCrmBaseValues.Add(parsed);

                    sequence++;
                }

                if (result.Data.Count < pageSize) break;
                skip += result.Data.Count;
            }
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

        private (Dictionary<string, Queue<decimal>> ByLabel, Dictionary<string, Queue<decimal>> ByCrmId) BuildRawBaseValueQueuesForFocusElement()
        {
            var byLabel = new Dictionary<string, Queue<decimal>>(StringComparer.OrdinalIgnoreCase);
            var byCrmId = new Dictionary<string, Queue<decimal>>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(_focusElement) || !_rawCrmBaseValues.Any())
                return (byLabel, byCrmId);

            var normalizedFocusElement = NormalizeElement(_focusElement);
            foreach (var row in _rawCrmBaseValues.OrderBy(r => r.Sequence))
            {
                var rowElement = NormalizeElement(row.Element);
                if (!string.Equals(rowElement, normalizedFocusElement, StringComparison.OrdinalIgnoreCase) &&
                    !rowElement.StartsWith(normalizedFocusElement, StringComparison.OrdinalIgnoreCase))
                    continue;

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

            return true;
        }

        private async Task OnShowCertifiedChanged(bool value)
        {
            _showCertified = value;
            await RefreshChartsAsync();
        }

        private async Task OnShowSampleChanged(bool value)
        {
            _showSampleValues = value;
            await RefreshChartsAsync();
        }

        private async Task OnShowAcceptableRangeChanged(bool value)
        {
            _showAcceptableRange = value;
            await RefreshChartsAsync();
        }

        private static string NormalizeElement(string raw) => raw.Split(new[] { ' ', '_', '.' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim().ToLower();
        private static string NormalizeSolutionLabel(string? raw) => string.IsNullOrWhiteSpace(raw) ? string.Empty : MultiWhitespaceRegex.Replace(raw.Trim(), " ");

        private static bool TryGetElementValue(IReadOnlyDictionary<string, decimal?> values, string? el, out decimal? v)
        {
            v = null; if (values == null || string.IsNullOrEmpty(el)) return false;
            if (values.TryGetValue(el, out v)) return true;
            var norm = NormalizeElement(el);
            var match = values.FirstOrDefault(k => NormalizeElement(k.Key).StartsWith(norm));
            if (match.Key != null) { v = match.Value; return true; }
            return false;
        }

        private static bool TryGetReferenceElementValue(IReadOnlyDictionary<string, decimal> values, string? el, out decimal v)
        {
            v = 0; if (values == null || string.IsNullOrEmpty(el)) return false;
            var norm = NormalizeElement(el);
            var match = values.FirstOrDefault(k => NormalizeElement(k.Key) == norm || NormalizeElement(k.Key).StartsWith(norm));
            if (match.Key != null) { v = match.Value; return true; }
            return false;
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
            if (!await _loadingLock.WaitAsync(0)) return;
            try
            {
                _focusElement = el;
                await AwaitWithTimeout(LoadSecondaryPlotRowsAsync(), TimeSpan.FromSeconds(30), "Reload pivot rows");
                await AwaitWithTimeout(RefreshChartsAsync(), TimeSpan.FromSeconds(20), "Render charts");
            }
            catch (TimeoutException timeoutEx)
            {
                Snackbar.Add(timeoutEx.Message, Severity.Warning);
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Element update failed: {ex.Message}", Severity.Error);
            }
            finally { _loadingLock.Release(); StateHasChanged(); }
        }

        private async Task PrevElement() { var i = _allElements.IndexOf(_focusElement ?? ""); if (i > 0) await SetFocusElement(_allElements[i - 1]); }
        private async Task NextElement() { var i = _allElements.IndexOf(_focusElement ?? ""); if (i < _allElements.Count - 1) await SetFocusElement(_allElements[i + 1]); }
        private async Task RunCalibration()
        {
            if (!_projectId.HasValue) return;
            if (!await _loadingLock.WaitAsync(0)) return;

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
                _loadingLock.Release();
                StateHasChanged();
            }
        }
        private void ResetAll()
        {
            _minDiff = -10m;
            _maxDiff = 10m;
            //use multi-model
            //filter element _selectedElements
            _previewBlank = 0m;
            _previewScale = 1.0;
            _scaleRangeMin = null;
            _scaleRangeMax = null;
            // > 50 only
            //_scaleAbove50Only = false;
            ResetRanges();
            //RenderCharts();
        }


        private async Task LoadElements() { var r = await PivotService.GetElementsAsync(_projectId!.Value); if (r.Succeeded) _allElements = r.Data ?? new(); if (_allElements.Any() && string.IsNullOrEmpty(_focusElement)) _focusElement = _allElements[0]; }
        private async Task LoadCrmReferenceData() { var r = await CrmService.GetCrmListAsync(pageSize: 0); if (r.Succeeded) _crmReferenceById = r.Data.Items.GroupBy(x => x.CrmId).ToDictionary(g => g.Key, g => g.ToList()); }
        private async Task LoadCrmSelections() { var r = await OptimizationService.GetCrmSelectionOptionsAsync(_projectId!.Value); if (r.Succeeded) _crmSelectionRows = r.Data.Items; }
        private async Task LoadSecondaryPlotRowsAsync() { var r = await PivotService.GetAdvancedPivotTableAsync(new AdvancedPivotRequest(ProjectId: _projectId!.Value, SearchText: null, SelectedElements: null, NumberFilters: null, UseOxide: false, UseInt: false, DecimalPlaces: 4, Page: 1, PageSize: 5000, Aggregation: "First", MergeRepeats: false)); if (r.Succeeded) _secondaryRows = r.Data.Rows; }
        private async Task GetCurrentStats() { var r = await OptimizationService.GetCurrentStatsAsync(_projectId!.Value, _minDiff, _maxDiff); if (r.Succeeded) { _result = r.Data; _optimizedRows = BuildOptimizedRows(_result?.OptimizedData, _focusElement); } }
        private List<OptimizedSampleRow> BuildOptimizedRows(IEnumerable<OptimizedSampleDto>? d, string? e) { if (d == null || string.IsNullOrEmpty(e)) return new(); return d.Select(s => { TryGetElementValue(s.OriginalValues, e, out var orig); TryGetElementValue(s.OptimizedValues, e, out var opt); TryGetReferenceElementValue(s.CrmValues.ToDictionary(k => k.Key, v => v.Value ?? 0m), e, out var refV); decimal db = s.DiffPercentBefore.TryGetValue(e, out var v1) ? v1 : 0; decimal da = s.DiffPercentAfter.TryGetValue(e, out var v2) ? v2 : 0; bool p = s.PassStatusAfter.TryGetValue(e, out var ps) && ps; return new OptimizedSampleRow(s.SolutionLabel, s.CrmId, e, orig, opt, refV, db, da, p); }).ToList(); }
        private IEnumerable<OptimizedSampleRow> FilterRows(IEnumerable<OptimizedSampleRow> rows) => string.IsNullOrEmpty(_sampleFilter) ? rows : rows.Where(r => r.SolutionLabel.Contains(_sampleFilter, StringComparison.OrdinalIgnoreCase));
        private List<string> GetRowOptions(CrmSelectionRowDto r) => r.PreferredOptions.Concat(r.AllOptions).Distinct().ToList();
        private EventCallback<string> GetRowSelectionChangedHandler(CrmSelectionRowDto r) => EventCallback.Factory.Create<string>(this, async v => { r.SelectedOption = v; if (_projectId != null) await OptimizationService.SaveCrmSelectionsAsync(new CrmSelectionSaveRequest { ProjectId = _projectId.Value, Selections = new List<CrmSelectionItemDto> { new CrmSelectionItemDto { SolutionLabel = r.SolutionLabel, RowIndex = r.RowIndex, SelectedCrmKey = v } } }); });
        private async Task OnPreviewScaleChanged(double v) { _previewScale = v; await RefreshChartsAsync(); }
        private async Task OnRangeMinChanged(decimal? v) { _scaleRangeMin = v; await RefreshChartsAsync(); }
        private async Task OnRangeMaxChanged(decimal? v) { _scaleRangeMax = v; await RefreshChartsAsync(); }

        public void Dispose() => _loadingLock.Dispose();
    }
}
