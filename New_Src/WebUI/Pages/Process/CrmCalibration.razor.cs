using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using MudBlazor;
using System;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebUI.Services;

namespace WebUI.Pages.Process
{
    public partial class CrmCalibration
    {
        [SupplyParameterFromQuery]
        public Guid? projectId { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; } = default!;

        // Chart references
        private ElementReference chart2Canvas;
        private bool _chartsRendered = false;

        private Guid? _projectId;
        private decimal _minDiff = -10m;
        private decimal _maxDiff = 10m;
        private int _maxIterations = 100;
        private int _populationSize = 50;
        private bool _useMultiModel = true;
        private IEnumerable<string> _selectedElements = new HashSet<string>();
        private List<string> _allElements = new();
        private string? _focusElement;
        private decimal _previewBlank = 0m;
        private double _previewScale = 1.0;
        private string _sampleFilter = "";

        // فیلدهای مربوط به نمودار پایین (Index vs Value)
        private List<AdvancedPivotRowDto> _secondaryRows = new();
        private List<string> _blankLabelLines = new();
        private string _calibrationRange = "[0 to 0]";
        private HashSet<string> _excludedLabels = new(StringComparer.OrdinalIgnoreCase);
        private List<ExcludeLabelRow> _excludeLabelRows = new();
        private Dictionary<string, CrmListItemDto> _crmReference = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Regex CrmIdRegex = new(
            @"(?i)(?:\bCRM\b|\bOREAS\b)?[\s-]*(\d+[a-zA-Z]?)[\s-]*(?:\bpar\b)?",
            RegexOptions.Compiled);

        private BlankScaleOptimizationResult? _result;
        private ManualBlankScaleResult? _manualResult;
        private List<OptimizedSampleRow> _optimizedRows = new();
        private List<OptimizedSampleRow> _manualRows = new();
        private bool _isLoading = false;
        private string? _projectName;
        private List<CrmMethodOptionDto> _crmOptions = new();
        private Dictionary<string, string> _crmSelections = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _includedCrmIds = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _includedCrmLabels = new(StringComparer.OrdinalIgnoreCase);
        private string _excludedLabelsInput = string.Empty;
        private List<CrmSelectionRowDto> _crmSelectionRows = new();

        // UI toggles to match Python controls
        private bool _showCertified = true;
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

        // Dialog visibility flags
        private bool _selectVerificationsDialogVisible = false;
        private bool _excludeDialogVisible = false;
        private bool _reportDialogVisible = false;

        // Report values
        private decimal _reportBlank = 0m;
        private decimal _reportScale = 1m;

        // CRM label options for selection
        private List<string> _crmLabelOptions = new();

        private enum PivotValueMode
        {
            Original,
            Optimized,
            Crm,
            DiffAfter
        }

        private int FocusElementIndex => string.IsNullOrWhiteSpace(_focusElement) ? -1 : _allElements.IndexOf(_focusElement);
        private bool CanPrev => FocusElementIndex > 0;
        private bool CanNext => FocusElementIndex >= 0 && FocusElementIndex < _allElements.Count - 1;
        private string ScaleRangeDisplay =>
            _scaleRangeMin.HasValue && _scaleRangeMax.HasValue
                ? $"Scale Range: {_scaleRangeMin.Value:0.###} to {_scaleRangeMax.Value:0.###}"
                : "Scale Range: Not Set";

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

        // کلاس‌های کمکی برای نمودار پایین
        private sealed class ExcludeLabelRow
        {
            public string SolutionLabel { get; set; } = "";
            public decimal? Value { get; set; }
            public string ValueDisplay => Value.HasValue ? Value.Value.ToString("0.####") : "---";
        }

        private sealed class CalibrationRow
        {
            public string SolutionLabel { get; set; } = "";
            public string CrmId { get; set; } = "";
            public decimal? OriginalValue { get; set; }
            public decimal? CrmValue { get; set; }
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
            return v.Value.ToString("0.####");
        }

        private IEnumerable<string> PivotColumns()
        {
            if (_pivotSelectedElements.Count == 0 && !string.IsNullOrWhiteSpace(_focusElement))
                return new[] { _focusElement! };

            return _pivotSelectedElements;
        }

        private async Task RebuildPivot()
        {
            if (!_projectId.HasValue) return;
            _isLoading = true;
            StateHasChanged();

            try
            {
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

                    var optimizedData = _manualResult?.OptimizedData ?? _result?.OptimizedData;

                    foreach (var s in result.Data.Rows)
                    {
                        rows.Add(new PivotRowVm
                        {
                            Order = order++,
                            SolutionLabel = s.SolutionLabel,
                            RowType = PivotRowType.Sample,
                            Values = s.Values
                        });

                        var crmMatch = optimizedData?.FirstOrDefault(x => x.SolutionLabel == s.SolutionLabel);
                        if (crmMatch != null && !string.IsNullOrEmpty(crmMatch.CrmId))
                        {
                            rows.Add(new PivotRowVm
                            {
                                Order = order++,
                                SolutionLabel = $"{crmMatch.CrmId} CRM",
                                RowType = PivotRowType.CrmRef,
                                Values = BuildDictValues(crmMatch.CrmValues, cols)
                            });

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

            if (newTabIndex == 1 && _result != null)
            {
                await Task.Delay(100);
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

            if (_pivotSelectedElements.Count == 0)
            {
                _pivotSelectedElements.Add(_focusElement);
                RebuildPivot();
            }

            StateHasChanged();
            await Task.Delay(50);

            await LoadSecondaryPlotRowsAsync();
            await RefreshChartsAsync();
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
            await LoadCrmReferenceAsync();
            await LoadCrmSelections();
            LoadExcludedLabelsFromInput();
            await LoadSecondaryPlotRowsAsync();
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

        private async Task LoadCrmReferenceAsync()
        {
            var result = await CrmService.GetCrmListAsync(pageSize: 0);
            if (result.Succeeded && result.Data != null)
            {
                _crmReference = result.Data.Items
                    .Where(x => !string.IsNullOrWhiteSpace(x.CrmId))
                    .GroupBy(x => x.CrmId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
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

        private async Task ToggleIncludedCrmId(string label, bool isIncluded)
        {
            if (isIncluded)
                _includedCrmLabels.Add(label);
            else
                _includedCrmLabels.Remove(label);

            await RenderCalibrationChartAsync();
        }

        private List<string> ParseExcludedLabels()
        {
            if (_excludedLabels.Count > 0)
                return _excludedLabels.ToList();

            if (string.IsNullOrWhiteSpace(_excludedLabelsInput))
                return new List<string>();

            return _excludedLabelsInput
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void LoadExcludedLabelsFromInput()
        {
            if (string.IsNullOrWhiteSpace(_excludedLabelsInput))
                return;

            _excludedLabels = new HashSet<string>(
                _excludedLabelsInput
                    .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);
        }

        private void SyncExcludedLabelsInput()
        {
            _excludedLabelsInput = _excludedLabels.Count == 0
                ? string.Empty
                : string.Join(", ", _excludedLabels.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
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

        private async Task LoadSecondaryPlotRowsAsync()
        {
            if (_projectId == null || string.IsNullOrWhiteSpace(_focusElement))
            {
                _secondaryRows.Clear();
                _blankLabelLines.Clear();
                _excludeLabelRows.Clear();
                await RenderSecondaryChartAsync();
                return;
            }

            var rows = new List<AdvancedPivotRowDto>();
            var page = 1;
            const int pageSize = 2000;

            while (true)
            {
                var request = new AdvancedPivotRequest(
                    ProjectId: _projectId.Value,
                    SearchText: null,
                    SelectedSolutionLabels: null,
                    // Load full row set; element-specific filtering is handled client-side via TryGetElementValue.
                    SelectedElements: null,
                    NumberFilters: null,
                    UseOxide: false,
                    UseInt: false,
                    DecimalPlaces: 4,
                    Page: page,
                    PageSize: pageSize,
                    Aggregation: "First",
                    MergeRepeats: false);

                var result = await PivotService.GetAdvancedPivotTableAsync(request);
                if (!result.Succeeded || result.Data == null)
                {
                    if (!string.IsNullOrWhiteSpace(result.Message))
                    {
                        Snackbar.Add(result.Message, Severity.Warning);
                    }
                    break;
                }

                rows.AddRange(result.Data.Rows);

                if (result.Data.Rows.Count < pageSize)
                    break;

                page++;
            }

            _secondaryRows = rows
                .OrderBy(r => r.OriginalIndex)
                .ThenBy(r => r.SolutionLabel, StringComparer.OrdinalIgnoreCase)
                .ToList();

            UpdateBlankLabels();
            UpdateExcludeLabelRows();
            UpdateCrmLabelOptionsFromRows();
            await RenderSecondaryChartAsync();
        }

        private void UpdateCrmLabelOptionsFromRows()
        {
            var calibrationRows = BuildCalibrationRows();
            if (calibrationRows.Count == 0)
            {
                _crmLabelOptions.Clear();
                _includedCrmLabels.Clear();
                return;
            }

            var labels = calibrationRows
                .Select(r => r.SolutionLabel)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var previousIncluded = new HashSet<string>(_includedCrmLabels, StringComparer.OrdinalIgnoreCase);
            var hadSelection = previousIncluded.Count > 0;
            var labelSet = new HashSet<string>(labels, StringComparer.OrdinalIgnoreCase);

            _crmLabelOptions = labels;
            _includedCrmLabels.RemoveWhere(label => !labelSet.Contains(label));
            foreach (var label in labels)
            {
                if (!hadSelection || previousIncluded.Contains(label))
                    _includedCrmLabels.Add(label);
            }

            if (_includedCrmLabels.Count == 0)
            {
                foreach (var label in labels)
                    _includedCrmLabels.Add(label);
            }
        }

        private void UpdateBlankLabels()
        {
            _blankLabelLines.Clear();
            if (string.IsNullOrWhiteSpace(_focusElement) || !_secondaryRows.Any())
                return;

            foreach (var row in _secondaryRows)
            {
                if (!IsBlankLabel(row.SolutionLabel))
                    continue;

                TryGetElementValue(row.Values, _focusElement, out var value);
                var display = value.HasValue ? value.Value.ToString("0.####") : "---";
                _blankLabelLines.Add($"{row.SolutionLabel}: {display}");
            }
        }

        private void UpdateExcludeLabelRows()
        {
            _excludeLabelRows = _secondaryRows
                .Select(row =>
                {
                    TryGetElementValue(row.Values, _focusElement, out var value);
                    return new ExcludeLabelRow
                    {
                        SolutionLabel = row.SolutionLabel,
                        Value = value
                    };
                })
                .OrderBy(row => row.SolutionLabel, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsBlankLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return false;

            return label.Contains("BLANK", StringComparison.OrdinalIgnoreCase) ||
                   label.Contains("BLNK", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCrmLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return false;

            return label.Contains("CRM", StringComparison.OrdinalIgnoreCase) ||
                   label.Contains("OREAS", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractCrmIdFromLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return string.Empty;

            var match = CrmIdRegex.Match(label);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        // Resolve values for focus element with a fallback on base element key (e.g., "Ag" vs "Ag 338.289").
        private static bool TryGetElementValue(IReadOnlyDictionary<string, decimal?> values, string? element, out decimal? value)
        {
            value = null;
            if (values == null || string.IsNullOrWhiteSpace(element))
                return false;

            if (values.TryGetValue(element, out value))
                return true;

            var trimmed = element.Trim();
            if (!string.Equals(trimmed, element, StringComparison.Ordinal) && values.TryGetValue(trimmed, out value))
                return true;

            var baseElement = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(baseElement))
                return false;

            if (values.TryGetValue(baseElement, out value))
                return true;

            var prefix = baseElement + " ";
            var match = values.FirstOrDefault(kvp => kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Key))
            {
                value = match.Value;
                return true;
            }

            return false;
        }

        private List<CalibrationRow> BuildCalibrationRows()
        {
            var rows = new List<CalibrationRow>();
            // Keep calibration plot stable: prefer full optimization result rows when available.
            // Manual result can be partial and may collapse the chart to a single CRM point.
            var dataSource = (_result?.OptimizedData != null && _result.OptimizedData.Any())
                ? _result.OptimizedData
                : _manualResult?.OptimizedData;

            if (dataSource != null && dataSource.Any())
            {
                foreach (var sample in dataSource)
                {
                    if (string.IsNullOrWhiteSpace(sample.CrmId))
                        continue;

                    if (!TryGetElementValue(sample.CrmValues, _focusElement, out var crmValue) || !crmValue.HasValue)
                        continue;

                    TryGetElementValue(sample.OriginalValues, _focusElement, out var originalValue);
                    TryGetElementValue(sample.OptimizedValues, _focusElement, out var optimizedValue);
                    var displayValue = originalValue ?? optimizedValue;
                    if (!displayValue.HasValue)
                        continue;

                    rows.Add(new CalibrationRow
                    {
                        SolutionLabel = sample.SolutionLabel,
                        CrmId = sample.CrmId,
                        OriginalValue = displayValue,
                        CrmValue = crmValue
                    });
                }

                return rows;
            }

            if (_secondaryRows.Count == 0 || _crmReference.Count == 0)
                return rows;

            foreach (var row in _secondaryRows)
            {
                if (!TryGetElementValue(row.Values, _focusElement, out var rawValue) || !rawValue.HasValue)
                    continue;

                var crmId = ExtractCrmIdFromLabel(row.SolutionLabel);
                if (string.IsNullOrWhiteSpace(crmId))
                    continue;

                if (!_crmReference.TryGetValue(crmId, out var crmItem))
                    continue;

                if (!crmItem.Elements.TryGetValue(_focusElement!, out var certValue))
                    continue;

                rows.Add(new CalibrationRow
                {
                    SolutionLabel = row.SolutionLabel,
                    CrmId = crmId,
                    OriginalValue = rawValue.Value,
                    CrmValue = certValue
                });
            }

            return rows;
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
                StateHasChanged();
                await Task.Delay(150);
                await RenderChartsAsync();
                StateHasChanged();
            }
            else
            {
                Snackbar.Add(result.Message ?? "Failed to get stats", Severity.Error);
            }

            _isLoading = false;
            StateHasChanged();
        }

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
                _resultsTabIndex = 1;

                StateHasChanged();
                await Task.Delay(250);
                await RenderChartsAsync();
                StateHasChanged();

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
                RangeLow = _rangeLow,
                RangeMid = _rangeMid,
                RangeHigh1 = _rangeHigh1,
                RangeHigh2 = _rangeHigh2,
                RangeHigh3 = _rangeHigh3,
                RangeHigh4 = _rangeHigh4,
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
                StateHasChanged();
                await Task.Delay(150);
                await RenderChartsAsync();
                StateHasChanged();
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
                    UpdateManualRows();
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
                StateHasChanged();
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
                await RenderCalibrationChartAsync();
                await RenderSecondaryChartAsync();
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
                _previewBlank = 0m;
                _previewScale = 1.0;
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
            RenderCharts();
        }
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

        private List<OptimizedSampleRow> BuildRows(IEnumerable<OptimizedSampleDto>? data, string? element)
        {
            if (data == null || string.IsNullOrWhiteSpace(element))
                return new List<OptimizedSampleRow>();

            var rows = new List<OptimizedSampleRow>();
            foreach (var sample in data)
            {
                TryGetElementValue(sample.OriginalValues, element, out var original);
                TryGetElementValue(sample.OptimizedValues, element, out var optimized);
                TryGetElementValue(sample.CrmValues, element, out var crmValue);
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

        private void OpenRangesDialog()
        {
            _rangesDialogVisible = true;
        }

        private void CloseRangesDialog()
        {
            _rangesDialogVisible = false;
        }

        private async Task ApplyRangesAsync()
        {
            _rangesDialogVisible = false;
            await RenderCalibrationChartAsync();
            Snackbar.Add("Acceptable ranges updated", Severity.Success);
        }

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

        // ==========================================
        // Chart Rendering Methods
        // ==========================================

        /// <summary>
        /// Renders all charts
        /// </summary>
        private async Task RenderChartsAsync()
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("destroyChart", "calibrationChart");
                await JSRuntime.InvokeVoidAsync("destroyChart", "secondaryChart");

                await Task.Delay(150);

                if (_result != null)
                {
                    await RenderCalibrationChartAsync();
                }

                await RenderSecondaryChartAsync();
                await JSRuntime.InvokeVoidAsync("resizeAllCharts");

                _chartsRendered = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rendering charts: {ex.Message}");
            }
        }

        private async Task RefreshChartsAsync()
        {
            await Task.Delay(10);
            await RenderCalibrationChartAsync();
            await RenderSecondaryChartAsync();
            await JSRuntime.InvokeVoidAsync("resizeAllCharts");
            StateHasChanged();
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
                                beginAtZero = true
                            }
                        }
                    }
                };

                await JSRuntime.InvokeVoidAsync("createChart", "elementChart", chartConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rendering element improvement chart: {ex.Message}");
            }
        }

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

        public object GetCalibrationChartData()
        {
            if (string.IsNullOrWhiteSpace(_focusElement))
                return new { labels = Array.Empty<string>(), datasets = Array.Empty<object>() };

            var excludedLabels = new HashSet<string>(ParseExcludedLabels(), StringComparer.OrdinalIgnoreCase);
            var calibrationRows = BuildCalibrationRows();
            // Apply include filter only when user has actively deselected some items.
            if (_crmLabelOptions.Count > 0 &&
                _includedCrmLabels.Count > 0 &&
                _includedCrmLabels.Count < _crmLabelOptions.Count)
            {
                calibrationRows = calibrationRows
                    .Where(r => _includedCrmLabels.Contains(r.SolutionLabel))
                    .ToList();
            }

            if (!calibrationRows.Any())
                return new { labels = Array.Empty<string>(), datasets = Array.Empty<object>() };

            var crmIds = calibrationRows.Select(r => r.CrmId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            var crmToIndex = crmIds.Select((id, i) => new { id, i })
                .ToDictionary(x => x.id, x => x.i, StringComparer.OrdinalIgnoreCase);

            var datasets = new List<object>();

            if (_showAcceptableRange)
            {
                var rangePoints = new List<object>();

                foreach (var crm in crmIds)
                {
                    var refRow = calibrationRows.FirstOrDefault(r => r.CrmId == crm && r.CrmValue.HasValue);
                    if (refRow == null) continue;

                    var certVal = refRow.CrmValue.Value;
                    var tol = GetToleranceValue(certVal);

                    var lower = (double)(certVal - tol);
                    var upper = (double)(certVal + tol);
                    var x = crmToIndex[crm];

                    rangePoints.Add(new { x = x - 0.25, y = lower });
                    rangePoints.Add(new { x = x + 0.25, y = lower });
                    rangePoints.Add(new { x = (double?)null, y = (double?)null });

                    rangePoints.Add(new { x = x - 0.25, y = upper });
                    rangePoints.Add(new { x = x + 0.25, y = upper });
                    rangePoints.Add(new { x = (double?)null, y = (double?)null });
                }

                if (rangePoints.Count > 0)
                {
                    datasets.Add(new
                    {
                        type = "line",
                        label = "Acceptable Range",
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

            if (_showCertified)
            {
                var certPoints = new List<object>();
                foreach (var crm in crmIds)
                {
                    var refRow = calibrationRows.FirstOrDefault(r => r.CrmId == crm && r.CrmValue.HasValue);
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
                        backgroundColor = "green",
                        borderColor = "green",
                        pointStyle = "circle",
                        pointRadius = 8,
                        showLine = false
                    });
                }
            }

            var samplePoints = new List<object>();
            foreach (var r in calibrationRows)
            {
                if (!r.OriginalValue.HasValue)
                    continue;

                var displayVal = excludedLabels.Contains(r.SolutionLabel)
                    ? (double)r.OriginalValue.Value
                    : (double)GetPreviewValue(r.OriginalValue.Value);

                samplePoints.Add(new
                {
                    x = crmToIndex[r.CrmId],
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
                    backgroundColor = "blue",
                    borderColor = "blue",
                    pointStyle = "triangle",
                    pointRadius = 8,
                    rotation = 0,
                    showLine = false
                });
            }

            var labels = crmIds.Select(id => $"V {id}").ToArray();

            return new
            {
                labels = labels,
                datasets = datasets.ToArray()
            };
        }

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
                        xLabels = ((dynamic)chartData).labels,
                        layout = new
                        {
                            padding = new { bottom = 20, left = 10, right = 10, top = 10 }
                        },
                        plugins = new
                        {
                            legend = new { display = true, position = "top" },
                            tooltip = new { backgroundColor = "rgba(0,0,0,0.7)" }
                        },
                        scales = new
                        {
                            x = new
                            {
                                type = "linear",
                                title = new { display = true, text = "Verification ID" },
                                ticks = new
                                {
                                    display = true,
                                    autoSkip = true,
                                    maxTicksLimit = 20,
                                    color = "#666"
                                },
                                grid = new
                                {
                                    display = true,
                                    drawBorder = true
                                }
                            },
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

        private object GetSecondaryChartData()
        {
            if (string.IsNullOrWhiteSpace(_focusElement) || !_secondaryRows.Any())
                return new { datasets = Array.Empty<object>() };

            var filter = _sampleFilter?.Trim();
            var rows = string.IsNullOrWhiteSpace(filter)
                ? _secondaryRows
                : _secondaryRows.Where(r => r.SolutionLabel.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            var originalPoints = new List<object>();
            var correctedPoints = new List<object>();

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var value = 0m;
                if (TryGetElementValue(row.Values, _focusElement, out var rawValue) && rawValue.HasValue)
                {
                    value = rawValue.Value;
                }

                var x = i;
                var originalVal = (double)value;
                var correctedVal = (double)((value - _previewBlank) * (decimal)_previewScale);

                originalPoints.Add(new { x, y = originalVal, label = row.SolutionLabel });
                correctedPoints.Add(new { x, y = correctedVal, label = row.SolutionLabel });
            }

            return new
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
                        pointRadius = 6,
                        showLine = false
                    },
                    new
                    {
                        label = "Corrected",
                        data = correctedPoints,
                        backgroundColor = "#F44336",
                        borderColor = "#F44336",
                        pointStyle = "cross",
                        pointRadius = 7,
                        showLine = false
                    }
                }
            };
        }

        private async Task RenderSecondaryChartAsync()
        {
            try
            {
                // ابتدا chart موجود را پاک کن
                await JSRuntime.InvokeVoidAsync("destroyChart", "secondaryChart");

                await Task.Delay(50); // کمی تاخیر برای DOM

                var chartData = GetSecondaryChartData();
                var chartConfig = new
                {
                    type = "scatter",
                    data = chartData,
                    options = new
                    {
                        responsive = true,
                        maintainAspectRatio = false, // مهم: aspect ratio را غیرفعال کن
                        layout = new
                        {
                            padding = new { bottom = 20, left = 10, right = 10, top = 10 }
                        },
                        animation = new
                        {
                            duration = 0 // انیمیشن را برای رندر سریع‌تر غیرفعال کن
                        },
                        plugins = new
                        {
                            legend = new
                            {
                                display = true,
                                position = "top",
                                labels = new
                                {
                                    boxWidth = 12,
                                    padding = 10
                                }
                            },
                            tooltip = new
                            {
                                backgroundColor = "rgba(0,0,0,0.8)",
                                titleFont = new { size = 12 },
                                bodyFont = new { size = 12 },
                                padding = 8
                            }
                        },
                        scales = new
                        {
                            x = new
                            {
                                type = "linear",
                                title = new
                                {
                                    display = true,
                                    text = "Index",
                                    font = new { size = 12 }
                                },
                                grid = new
                                {
                                    drawBorder = true,
                                    display = true,
                                    color = "rgba(0,0,0,0.1)"
                                },
                                ticks = new
                                {
                                    display = true,
                                    autoSkip = true,
                                    color = "#666",
                                    font = new { size = 10 },
                                    maxTicksLimit = 20
                                }
                            },
                            y = new
                            {
                                title = new
                                {
                                    display = true,
                                    text = "Value",
                                    font = new { size = 12 }
                                },
                                grid = new
                                {
                                    drawBorder = false,
                                    color = "rgba(0,0,0,0.1)"
                                },
                                ticks = new
                                {
                                    font = new { size = 10 },
                                    maxTicksLimit = 10
                                },
                                beginAtZero = false
                            }
                        },
                        elements = new
                        {
                            point = new
                            {
                                radius = 4, // نقطه‌ها را کوچک‌تر کن
                                hoverRadius = 6
                            }
                        },
                        interaction = new
                        {
                            intersect = false,
                            mode = "nearest"
                        }
                    }
                };

                await JSRuntime.InvokeVoidAsync("createChart", "secondaryChart", chartConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rendering secondary chart: {ex.Message}");
            }
        }

        private decimal GetToleranceValue(decimal crmValue)
        {
            var absVal = Math.Abs(crmValue);

            if (absVal < 10) return _rangeLow;

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
            if (_scaleRangeMin.HasValue && _scaleRangeMax.HasValue)
            {
                if (originalValue < _scaleRangeMin.Value || originalValue > _scaleRangeMax.Value)
                    return originalValue;
            }

            if (_scaleAbove50Only && originalValue <= 50)
                return originalValue;

            return (originalValue - _previewBlank) * (decimal)_previewScale;
        }

        private async Task OnPreviewParamChanged()
        {
            await RenderCalibrationChartAsync();
            await RenderSecondaryChartAsync();
        }

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

        private async Task OnPreviewBlankChanged(decimal? newVal)
        {
            _previewBlank = newVal ?? 0m;
            await OnPreviewParamChanged();
        }

        private async Task OnFilterChanged(string value)
        {
            _sampleFilter = value ?? string.Empty;
            await RenderSecondaryChartAsync();
        }

        private async Task OnShowCertifiedChanged(bool value)
        {
            _showCertified = value;
            await RenderCalibrationChartAsync();
        }

        private async Task OnShowRangeChanged(bool value)
        {
            _showAcceptableRange = value;
            await RenderCalibrationChartAsync();
        }

        private async Task OnScaleAbove50Changed(bool value)
        {
            _scaleAbove50Only = value;
            await OnPreviewParamChanged();
        }

        private void OpenSelectVerificationsDialog()
        {
            _selectVerificationsDialogVisible = true;
        }

        private void CloseSelectVerificationsDialog()
        {
            _selectVerificationsDialogVisible = false;
        }

        private async Task IncludeAllVerifications()
        {
            _includedCrmLabels.Clear();
            foreach (var label in _crmLabelOptions)
                _includedCrmLabels.Add(label);
            await RenderCalibrationChartAsync();
        }

        private async Task ExcludeAllVerifications()
        {
            _includedCrmLabels.Clear();
            await RenderCalibrationChartAsync();
        }

        private void OpenExcludeDialog()
        {
            UpdateExcludeLabelRows();
            _excludeDialogVisible = true;
        }

        private void CloseExcludeDialog()
        {
            _excludeDialogVisible = false;
        }

        private async Task ToggleExcludedLabel(string label, bool isExcluded)
        {
            if (isExcluded)
                _excludedLabels.Add(label);
            else
                _excludedLabels.Remove(label);

            SyncExcludedLabelsInput();
            await RenderCalibrationChartAsync();
        }

        private EventCallback<bool> GetIncludeCallback(string crmId)
        {
            return EventCallback.Factory.Create<bool>(this, (bool v) => ToggleIncludedCrmId(crmId, v));
        }

        private EventCallback<bool> GetExcludeCallback(string label)
        {
            return EventCallback.Factory.Create<bool>(this, (bool v) => ToggleExcludedLabel(label, v));
        }

        private async Task OpenReportDialog()
        {
            if (_result == null && _projectId.HasValue)
            {
                await GetCurrentStats();
            }

            if (TryGetRecommendedModel(out var blank, out var scale))
            {
                _reportBlank = blank;
                _reportScale = scale;
            }
            else
            {
                _reportBlank = _previewBlank;
                _reportScale = (decimal)_previewScale;
            }

            _reportDialogVisible = true;
        }

        private void CloseReportDialog()
        {
            _reportDialogVisible = false;
        }

        private async Task ApplyRecommendedModel()
        {
            if (!TryGetRecommendedModel(out var blank, out var scale))
            {
                Snackbar.Add("No model recommendation available.", Severity.Warning);
                return;
            }

            _previewBlank = blank;
            _previewScale = (double)scale;
            _reportBlank = blank;
            _reportScale = scale;
            _reportDialogVisible = false;
            await OnPreviewParamChanged();
        }

        private bool TryGetRecommendedModel(out decimal blank, out decimal scale)
        {
            blank = _previewBlank;
            scale = (decimal)_previewScale;

            if (_result == null || string.IsNullOrWhiteSpace(_focusElement))
                return false;

            if (!_result.ElementOptimizations.TryGetValue(_focusElement, out var opt))
                return false;

            blank = opt.Blank;
            scale = opt.Scale;
            return true;
        }

        private async Task RenderCharts()
        {
            
            await OnPreviewParamChanged();
        }
    }
}
