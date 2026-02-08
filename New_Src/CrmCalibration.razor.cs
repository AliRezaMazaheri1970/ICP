//using Microsoft.AspNetCore.Components;
//using Microsoft.AspNetCore.Components.Routing;
//using Microsoft.JSInterop;
//using MudBlazor;
//using System;
//using System.Globalization;
//using System.Text.Json;
//using System.Text.RegularExpressions;
//using WebUI.Services;

//namespace WebUI.Pages.Process
//{
//    public partial class CrmCalibration
//    {
//        [SupplyParameterFromQuery]
//        public Guid? projectId { get; set; }

//        [Inject]
//        private IJSRuntime JSRuntime { get; set; } = default!;

//        // Chart references
//        private ElementReference chart2Canvas;
//        private bool _chartsRendered = false;

//        private Guid? _projectId;
//        private decimal _minDiff = -10m;
//        private decimal _maxDiff = 10m;
//        private int _maxIterations = 100;
//        private int _populationSize = 50;
//        private bool _useMultiModel = true;
//        private IEnumerable<string> _selectedElements = new HashSet<string>();
//        private List<string> _allElements = new();
//        private string? _focusElement;
//        private decimal _previewBlank = 0m;
//        private double _previewScale = 1.0;
//        private string _sampleFilter = "";

//        // فیلدهای مربوط به نمودار پایین (Index vs Value)
//        private List<AdvancedPivotRowDto> _secondaryRows = new();
//        private List<string> _blankLabelLines = new();
//        private string _calibrationRange = "[0 to 0]";
//        private HashSet<string> _excludedLabels = new(StringComparer.OrdinalIgnoreCase);
//        private List<ExcludeLabelRow> _excludeLabelRows = new();
//        private Dictionary<string, CrmListItemDto> _crmReference = new(StringComparer.OrdinalIgnoreCase);
//        private static readonly Regex CrmIdRegex = new(
//            @"(?i)(?:\bCRM\b|\bOREAS\b)?[\s-]*(\d+[a-zA-Z]?)[\s-]*(?:\bpar\b)?",
//            RegexOptions.Compiled);

//        private BlankScaleOptimizationResult? _result;
//        private ManualBlankScaleResult? _manualResult;
//        private List<OptimizedSampleRow> _optimizedRows = new();
//        private List<OptimizedSampleRow> _manualRows = new();
//        private bool _isLoading = false;
//        private string? _projectName;
//        private List<CrmMethodOptionDto> _crmOptions = new();
//        private Dictionary<string, string> _crmSelections = new(StringComparer.OrdinalIgnoreCase);
//        private HashSet<string> _includedCrmIds = new(StringComparer.OrdinalIgnoreCase);
//        private HashSet<string> _includedCrmLabels = new(StringComparer.OrdinalIgnoreCase);
//        private string _excludedLabelsInput = string.Empty;
//        private List<CrmSelectionRowDto> _crmSelectionRows = new();

//        // UI toggles to match Python controls
//        private bool _showCertified = true;
//        private bool _showAcceptableRange = true;

//        // Scale Application Range (Python feature)
//        private decimal? _scaleRangeMin;
//        private decimal? _scaleRangeMax;
//        private bool _scaleAbove50Only = false;

//        // Acceptable Ranges (Python feature - magnitude-based thresholds)
//        private decimal _rangeLow = 2.0m;     // |x| < 10: absolute ±
//        private decimal _rangeMid = 20.0m;    // 10 ≤ |x| < 100: percentage
//        private decimal _rangeHigh1 = 10.0m;  // 100 ≤ |x| < 1000: percentage
//        private decimal _rangeHigh2 = 8.0m;   // 1000 ≤ |x| < 10000: percentage
//        private decimal _rangeHigh3 = 5.0m;   // 10000 ≤ |x| < 100000: percentage
//        private decimal _rangeHigh4 = 3.0m;   // |x| ≥ 100000: percentage
//        private bool _rangesDialogVisible = false;

//        // Details panel UX state
//        private int _detailsTabIndex = 0;
//        private bool _detailsMaximized = false;

//        // Results tabs state
//        private int _resultsTabIndex = 0;

//        // Pivot tab state
//        private PivotValueMode _pivotMode = PivotValueMode.Crm;
//        private HashSet<string> _pivotSelectedElements = new(StringComparer.OrdinalIgnoreCase);
//        private List<PivotRowVm> _pivotRows = new();

//        // Dialog visibility flags
//        private bool _selectVerificationsDialogVisible = false;
//        private bool _excludeDialogVisible = false;
//        private bool _reportDialogVisible = false;

//        // Report values
//        private decimal _reportBlank = 0m;
//        private decimal _reportScale = 1m;

//        // CRM label options for selection
//        private List<string> _crmLabelOptions = new();

//        private enum PivotValueMode
//        {
//            Original,
//            Optimized,
//            Crm,
//            DiffAfter
//        }

//        private int FocusElementIndex => string.IsNullOrWhiteSpace(_focusElement) ? -1 : _allElements.IndexOf(_focusElement);
//        private bool CanPrev => FocusElementIndex > 0;
//        private bool CanNext => FocusElementIndex >= 0 && FocusElementIndex < _allElements.Count - 1;
//        private string ScaleRangeDisplay =>
//            _scaleRangeMin.HasValue && _scaleRangeMax.HasValue
//                ? $"Scale Range: {_scaleRangeMin.Value:0.###} to {_scaleRangeMax.Value:0.###}"
//                : "Scale Range: Not Set";

//        private enum PivotRowType
//        {
//            Sample,      // CRM 258 A
//            CrmRef,      // OREAS 258 ... CRM
//            DiffAfter    // CRM 258 A Diff (%)
//        }

//        private sealed class PivotRowVm
//        {
//            public int Order { get; set; }
//            public string SolutionLabel { get; set; } = "";
//            public PivotRowType RowType { get; set; }
//            public string? CrmId { get; set; }
//            public Dictionary<string, decimal?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
//        }

//        // کلاس‌های کمکی برای نمودار پایین
//        private sealed class ExcludeLabelRow
//        {
//            public string SolutionLabel { get; set; } = "";
//            public decimal? Value { get; set; }
//            public string ValueDisplay => Value.HasValue ? Value.Value.ToString("0.####") : "---";
//        }

//        private sealed class CalibrationRow
//        {
//            public string SolutionLabel { get; set; } = "";
//            public string CrmId { get; set; } = "";
//            public decimal? OriginalValue { get; set; }
//            public decimal? CrmValue { get; set; }
//        }

//        private sealed record OptimizedSampleRow(
//            string SolutionLabel,
//            string CrmId,
//            string Element,
//            decimal? OriginalValue,
//            decimal? OptimizedValue,
//            decimal? CrmValue,
//            decimal DiffBefore,
//            decimal DiffAfter,
//            bool IsPassed);

//        private void ToggleDetailsMaximize()
//        {
//            _detailsMaximized = !_detailsMaximized;
//            StateHasChanged();
//        }

//        private int FilteredManualCount() => FilterRows(_manualRows).Count();
//        private int FilteredOptimizedCount() => FilterRows(_optimizedRows).Count();

//        private static string FormatDec(decimal? v)
//        {
//            if (v == null) return "-";
//            return v.Value.ToString("0.####");
//        }

//        private IEnumerable<string> PivotColumns()
//        {
//            if (_pivotSelectedElements.Count == 0 && !string.IsNullOrWhiteSpace(_focusElement))
//                return new[] { _focusElement! };

//            return _pivotSelectedElements;
//        }

//        private async Task RebuildPivot()
//        {
//            if (!_projectId.HasValue) return;
//            _isLoading = true;
//            StateHasChanged();

//            try
//            {
//                var request = new AdvancedPivotRequest(
//                    ProjectId: _projectId.Value,
//                    SearchText: _sampleFilter,
//                    SelectedElements: _allElements.ToList(),
//                    NumberFilters: null,
//                    UseOxide: false,
//                    UseInt: false,
//                    DecimalPlaces: 4,
//                    Page: 1,
//                    PageSize: 2000,
//                    MergeRepeats: false,
//                    Aggregation: "First"
//                );

//                var result = await PivotService.GetAdvancedPivotTableAsync(request);

//                if (result.Succeeded && result.Data != null)
//                {
//                    var cols = PivotColumns().ToList();
//                    var rows = new List<PivotRowVm>();
//                    int order = 0;

//                    var optimizedData = _manualResult?.OptimizedData ?? _result?.OptimizedData;

//                    foreach (var s in result.Data.Rows)
//                    {
//                        rows.Add(new PivotRowVm
//                        {
//                            Order = order++,
//                            SolutionLabel = s.SolutionLabel,
//                            RowType = PivotRowType.Sample,
//                            Values = s.Values
//                        });

//                        var crmMatch = optimizedData?.FirstOrDefault(x => x.SolutionLabel == s.SolutionLabel);
//                        if (crmMatch != null && !string.IsNullOrEmpty(crmMatch.CrmId))
//                        {
//                            rows.Add(new PivotRowVm
//                            {
//                                Order = order++,
//                                SolutionLabel = $"{crmMatch.CrmId} CRM",
//                                RowType = PivotRowType.CrmRef,
//                                Values = BuildDictValues(crmMatch.CrmValues, cols)
//                            });

//                            rows.Add(new PivotRowVm
//                            {
//                                Order = order++,
//                                SolutionLabel = $"{s.SolutionLabel} Diff (%)",
//                                RowType = PivotRowType.DiffAfter,
//                                Values = BuildDiffValues(crmMatch.DiffPercentAfter, cols)
//                            });
//                        }
//                    }
//                    _pivotRows = rows;
//                }
//            }
//            catch (Exception ex)
//            {
//                Snackbar.Add($"Error rebuilding pivot: {ex.Message}", Severity.Error);
//            }
//            finally
//            {
//                _isLoading = false;
//                StateHasChanged();
//            }
//        }

//        private Dictionary<string, decimal?> BuildSampleValues(OptimizedSampleDto s, List<string> cols)
//        {
//            var dict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);

//            foreach (var el in cols)
//            {
//                decimal? v = null;
//                if (_pivotMode == PivotValueMode.Original)
//                    s.OriginalValues.TryGetValue(el, out v);
//                else
//                    s.OptimizedValues.TryGetValue(el, out v);

//                dict[el] = v;
//            }

//            return dict;
//        }

//        private Dictionary<string, decimal?> BuildDictValues(Dictionary<string, decimal?> source, List<string> cols)
//        {
//            var dict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
//            foreach (var el in cols)
//                dict[el] = source.TryGetValue(el, out var v) ? v : null;
//            return dict;
//        }

//        private Dictionary<string, decimal?> BuildDiffValues(Dictionary<string, decimal> source, List<string> cols)
//        {
//            var dict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
//            foreach (var el in cols)
//                dict[el] = source.TryGetValue(el, out var v) ? v : null;
//            return dict;
//        }

//        private Task OnPivotElementsChanged(IEnumerable<string> values)
//        {
//            _pivotSelectedElements = new HashSet<string>(values ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
//            RebuildPivot();
//            StateHasChanged();
//            return Task.CompletedTask;
//        }

//        private Task OnPivotModeChanged(PivotValueMode mode)
//        {
//            _pivotMode = mode;
//            RebuildPivot();
//            StateHasChanged();
//            return Task.CompletedTask;
//        }

//        /// <summary>
//        /// Called when result tabs change - re-render charts if Tab2 is selected
//        /// </summary>
//        private async Task OnResultsTabChanged(int newTabIndex)
//        {
//            _resultsTabIndex = newTabIndex;

//            if (newTabIndex == 1 && _result != null)
//            {
//                await Task.Delay(100);
//                await RenderChartsAsync();
//            }
//        }

//        private void ResetPivotColumns()
//        {
//            _pivotSelectedElements.Clear();
//            if (!string.IsNullOrWhiteSpace(_focusElement))
//                _pivotSelectedElements.Add(_focusElement!);

//            RebuildPivot();
//        }

//        private void UpdateOptimizedRows()
//        {
//            _optimizedRows = BuildRows(_result?.OptimizedData, _focusElement);
//            RebuildPivot();
//        }

//        private void UpdateManualRows()
//        {
//            _manualRows = BuildRows(_manualResult?.OptimizedData, _focusElement);
//            RebuildPivot();
//        }

//        private async Task SetFocusElement(string? element)
//        {
//            if (string.IsNullOrWhiteSpace(element))
//                return;

//            _focusElement = element;
//            UpdateOptimizedRows();
//            UpdateManualRows();

//            if (_pivotSelectedElements.Count == 0)
//            {
//                _pivotSelectedElements.Add(_focusElement);
//                RebuildPivot();
//            }

//            StateHasChanged();
//            await Task.Delay(50);

//            await LoadSecondaryPlotRowsAsync();
//            await RefreshChartsAsync();
//            StateHasChanged();
//        }

//        protected override async Task OnInitializedAsync()
//        {
//            _projectId = projectId ?? ProjectService.CurrentProjectId;
//            if (!_projectId.HasValue)
//                return;

//            var projectResult = await ProjectService.GetProjectAsync(_projectId.Value);
//            if (projectResult.Succeeded && projectResult.Data != null)
//            {
//                _projectName = projectResult.Data.ProjectName;
//            }
//            else if (!string.IsNullOrWhiteSpace(projectResult.Message))
//            {
//                Snackbar.Add(projectResult.Message, Severity.Warning);
//            }

//            await LoadElements();
//            await LoadCrmOptions();
//            await LoadCrmReferenceAsync();
//            await LoadCrmSelections();
//            LoadExcludedLabelsFromInput();
//            await LoadSecondaryPlotRowsAsync();
//            //await GetCurrentStats();
//        }

//        private async Task LoadCrmOptions()
//        {
//            if (_projectId == null) return;

//            var result = await OptimizationService.GetCrmOptionsAsync(_projectId.Value);
//            if (result.Succeeded && result.Data != null)
//            {
//                _crmOptions = result.Data.Items;
//                _crmSelections.Clear();
//                _includedCrmIds.Clear();

//                foreach (var option in _crmOptions)
//                {
//                    if (!string.IsNullOrWhiteSpace(option.DefaultMethod))
//                    {
//                        _crmSelections[option.CrmId] = option.DefaultMethod!;
//                    }
//                    _includedCrmIds.Add(option.CrmId);
//                }
//            }
//            else if (!string.IsNullOrWhiteSpace(result.Message))
//            {
//                Snackbar.Add(result.Message, Severity.Warning);
//            }
//        }

//        private async Task LoadCrmReferenceAsync()
//        {
//            var result = await CrmService.GetCrmListAsync(pageSize: 0);
//            if (result.Succeeded && result.Data != null)
//            {
//                _crmReference = result.Data.Items
//                    .Where(x => !string.IsNullOrWhiteSpace(x.CrmId))
//                    .GroupBy(x => x.CrmId, StringComparer.OrdinalIgnoreCase)
//                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
//            }
//        }

//        private async Task LoadCrmSelections()
//        {
//            if (_projectId == null) return;

//            var result = await OptimizationService.GetCrmSelectionOptionsAsync(_projectId.Value);
//            if (result.Succeeded && result.Data != null)
//            {
//                _crmSelectionRows = result.Data.Items;
//            }
//            else if (!string.IsNullOrWhiteSpace(result.Message))
//            {
//                Snackbar.Add(result.Message, Severity.Warning);
//            }
//        }

//        private List<string> GetRowOptions(CrmSelectionRowDto row)
//        {
//            var options = new List<string>();
//            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

//            foreach (var opt in row.PreferredOptions)
//            {
//                if (seen.Add(opt))
//                    options.Add(opt);
//            }

//            foreach (var opt in row.AllOptions)
//            {
//                if (seen.Add(opt))
//                    options.Add(opt);
//            }

//            if (!string.IsNullOrWhiteSpace(row.SelectedOption) && seen.Add(row.SelectedOption))
//                options.Insert(0, row.SelectedOption);

//            return options;
//        }

//        private EventCallback<string> GetRowSelectionChangedHandler(CrmSelectionRowDto row)
//        {
//            return EventCallback.Factory.Create<string>(this, v => SaveRowSelectionAsync(row, v));
//        }

//        private async Task SaveRowSelectionAsync(CrmSelectionRowDto row, string? selected)
//        {
//            if (_projectId == null || string.IsNullOrWhiteSpace(selected))
//                return;

//            row.SelectedOption = selected;

//            var request = new CrmSelectionSaveRequest
//            {
//                ProjectId = _projectId.Value,
//                Selections = new List<CrmSelectionItemDto>
//                {
//                    new CrmSelectionItemDto
//                    {
//                        SolutionLabel = row.SolutionLabel,
//                        RowIndex = row.RowIndex,
//                        SelectedCrmKey = selected
//                    }
//                }
//            };

//            var result = await OptimizationService.SaveCrmSelectionsAsync(request);
//            if (!result.Succeeded)
//            {
//                Snackbar.Add(result.Message ?? "Failed to save CRM selection", Severity.Error);
//            }
//        }

//        private string? GetCrmSelection(string crmId)
//        {
//            return _crmSelections.TryGetValue(crmId, out var method) ? method : null;
//        }

//        private void SetCrmSelection(string crmId, string? method)
//        {
//            if (string.IsNullOrWhiteSpace(method))
//            {
//                _crmSelections.Remove(crmId);
//                return;
//            }

//            _crmSelections[crmId] = method;
//        }

//        private async Task ToggleIncludedCrmId(string label, bool isIncluded)
//        {
//            if (isIncluded)
//                _includedCrmLabels.Add(label);
//            else
//                _includedCrmLabels.Remove(label);

//            await RenderCalibrationChartAsync();
//        }

//        private List<string> ParseExcludedLabels()
//        {
//            if (_excludedLabels.Count > 0)
//                return _excludedLabels.ToList();

//            if (string.IsNullOrWhiteSpace(_excludedLabelsInput))
//                return new List<string>();

//            return _excludedLabelsInput
//                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
//                .Select(x => x.Trim())
//                .Where(x => !string.IsNullOrWhiteSpace(x))
//                .Distinct(StringComparer.OrdinalIgnoreCase)
//                .ToList();
//        }

//        private void LoadExcludedLabelsFromInput()
//        {
//            if (string.IsNullOrWhiteSpace(_excludedLabelsInput))
//                return;

//            _excludedLabels = new HashSet<string>(
//                _excludedLabelsInput
//                    .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
//                    .Select(x => x.Trim())
//                    .Where(x => !string.IsNullOrWhiteSpace(x)),
//                StringComparer.OrdinalIgnoreCase);
//        }

//        private void SyncExcludedLabelsInput()
//        {
//            _excludedLabelsInput = _excludedLabels.Count == 0
//                ? string.Empty
//                : string.Join(", ", _excludedLabels.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
//        }

//        private async Task LoadElements()
//        {
//            var result = await PivotService.GetElementsAsync(_projectId!.Value);
//            if (result.Succeeded && result.Data != null)
//            {
//                _allElements = result.Data;
//                if (_allElements.Count > 0 && string.IsNullOrWhiteSpace(_focusElement))
//                {
//                    _focusElement = _allElements[0];
//                }
//            }
//            else
//            {
//                Snackbar.Add(result.Message ?? "Failed to load elements", Severity.Error);
//            }
//        }

//        private async Task LoadSecondaryPlotRowsAsync()
//        {
//            if (_projectId == null || string.IsNullOrWhiteSpace(_focusElement))
//            {
//                _secondaryRows.Clear();
//                _blankLabelLines.Clear();
//                _excludeLabelRows.Clear();
//                await RenderSecondaryChartAsync();
//                return;
//            }

//            var rows = new List<AdvancedPivotRowDto>();
//            var page = 1;
//            const int pageSize = 2000;

//            while (true)
//            {
//                var request = new AdvancedPivotRequest(
//                    ProjectId: _projectId.Value,
//                    SearchText: null,
//                    SelectedSolutionLabels: null,
//                    // Load full row set; element-specific filtering is handled client-side via TryGetElementValue.
//                    SelectedElements: null,
//                    NumberFilters: null,
//                    UseOxide: false,
//                    UseInt: false,
//                    DecimalPlaces: 4,
//                    Page: page,
//                    PageSize: pageSize,
//                    Aggregation: "First",
//                    MergeRepeats: false);

//                var result = await PivotService.GetAdvancedPivotTableAsync(request);
//                if (!result.Succeeded || result.Data == null)
//                {
//                    if (!string.IsNullOrWhiteSpace(result.Message))
//                    {
//                        Snackbar.Add(result.Message, Severity.Warning);
//                    }
//                    break;
//                }

//                rows.AddRange(result.Data.Rows);

//                if (result.Data.Rows.Count < pageSize)
//                    break;

//                page++;
//            }

//            _secondaryRows = rows
//                .OrderBy(r => r.OriginalIndex)
//                .ThenBy(r => r.SolutionLabel, StringComparer.OrdinalIgnoreCase)
//                .ToList();

//            UpdateBlankLabels();
//            UpdateExcludeLabelRows();
//            UpdateCrmLabelOptionsFromRows();
//            await RenderSecondaryChartAsync();
//        }

//        private void UpdateCrmLabelOptionsFromRows()
//        {
//            var calibrationRows = BuildCalibrationRows();
//            if (calibrationRows.Count == 0)
//            {
//                _crmLabelOptions.Clear();
//                _includedCrmLabels.Clear();
//                return;
//            }

//            var labels = calibrationRows
//                .Select(r => r.SolutionLabel)
//                .Where(label => !string.IsNullOrWhiteSpace(label))
//                .Distinct(StringComparer.OrdinalIgnoreCase)
//                .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
//                .ToList();

//            var previousIncluded = new HashSet<string>(_includedCrmLabels, StringComparer.OrdinalIgnoreCase);
//            var hadSelection = previousIncluded.Count > 0;
//            var labelSet = new HashSet<string>(labels, StringComparer.OrdinalIgnoreCase);

//            _crmLabelOptions = labels;
//            _includedCrmLabels.RemoveWhere(label => !labelSet.Contains(label));
//            foreach (var label in labels)
//            {
//                if (!hadSelection || previousIncluded.Contains(label))
//                    _includedCrmLabels.Add(label);
//            }

//            if (_includedCrmLabels.Count == 0)
//            {
//                foreach (var label in labels)
//                    _includedCrmLabels.Add(label);
//            }
//        }

//        private void UpdateBlankLabels()
//        {
//            _blankLabelLines.Clear();
//            if (string.IsNullOrWhiteSpace(_focusElement) || !_secondaryRows.Any())
//                return;

//            foreach (var row in _secondaryRows)
//            {
//                if (!IsBlankLabel(row.SolutionLabel))
//                    continue;

//                TryGetElementValue(row.Values, _focusElement, out var value);
//                var display = value.HasValue ? value.Value.ToString("0.####") : "---";
//                _blankLabelLines.Add($"{row.SolutionLabel}: {display}");
//            }
//        }

//        private void UpdateExcludeLabelRows()
//        {
//            _excludeLabelRows = _secondaryRows
//                .Select(row =>
//                {
//                    TryGetElementValue(row.Values, _focusElement, out var value);
//                    return new ExcludeLabelRow
//                    {
//                        SolutionLabel = row.SolutionLabel,
//                        Value = value
//                    };
//                })
//                .OrderBy(row => row.SolutionLabel, StringComparer.OrdinalIgnoreCase)
//                .ToList();
//        }

//        private static bool IsBlankLabel(string label)
//        {
//            if (string.IsNullOrWhiteSpace(label))
//                return false;

//            return label.Contains("BLANK", StringComparison.OrdinalIgnoreCase) ||
//                   label.Contains("BLNK", StringComparison.OrdinalIgnoreCase);
//        }

//        private static bool IsCrmLabel(string label)
//        {
//            if (string.IsNullOrWhiteSpace(label))
//                return false;

//            return label.Contains("CRM", StringComparison.OrdinalIgnoreCase) ||
//                   label.Contains("OREAS", StringComparison.OrdinalIgnoreCase);
//        }

//        private static string ExtractCrmIdFromLabel(string label)
//        {
//            if (string.IsNullOrWhiteSpace(label))
//                return string.Empty;

//            var match = CrmIdRegex.Match(label);
//            return match.Success ? match.Groups[1].Value : string.Empty;
//        }

//        // Resolve values for focus element with a fallback on base element key (e.g., "Ag" vs "Ag 338.289").
//        private static bool TryGetElementValue(IReadOnlyDictionary<string, decimal?> values, string? element, out decimal? value)
//        {
//            value = null;
//            if (values == null || string.IsNullOrWhiteSpace(element))
//                return false;

//            if (values.TryGetValue(element, out value))
//                return true;

//            var trimmed = element.Trim();
//            if (!string.Equals(trimmed, element, StringComparison.Ordinal) && values.TryGetValue(trimmed, out value))
//                return true;

//            var baseElement = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
//            if (string.IsNullOrWhiteSpace(baseElement))
//                return false;

//            if (values.TryGetValue(baseElement, out value))
//                return true;

//            var prefix = baseElement + " ";
//            var match = values.FirstOrDefault(kvp => kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
//            if (!string.IsNullOrWhiteSpace(match.Key))
//            {
//                value = match.Value;
//                return true;
//            }

//            return false;
//        }

//        private List<CalibrationRow> BuildCalibrationRows()
//        {
//            var rows = new List<CalibrationRow>();
//            // Keep calibration plot stable: prefer full optimization result rows when available.
//            // Manual result can be partial and may collapse the chart to a single CRM point.
//            var dataSource = (_result?.OptimizedData != null && _result.OptimizedData.Any())
//                ? _result.OptimizedData
//                : _manualResult?.OptimizedData;

//            if (dataSource != null && dataSource.Any())
//            {
//                foreach (var sample in dataSource)
//                {
//                    if (string.IsNullOrWhiteSpace(sample.CrmId))
//                        continue;

//                    if (!TryGetElementValue(sample.CrmValues, _focusElement, out var crmValue) || !crmValue.HasValue)
//                        continue;

//                    TryGetElementValue(sample.OriginalValues, _focusElement, out var originalValue);
//                    TryGetElementValue(sample.OptimizedValues, _focusElement, out var optimizedValue);
//                    var displayValue = originalValue ?? optimizedValue;
//                    if (!displayValue.HasValue)
//                        continue;

//                    rows.Add(new CalibrationRow
//                    {
//                        SolutionLabel = sample.SolutionLabel,
//                        CrmId = sample.CrmId,
//                        OriginalValue = displayValue,
//                        CrmValue = crmValue
//                    });
//                }

//                return rows;
//            }

//            if (_secondaryRows.Count == 0 || _crmReference.Count == 0)
//                return rows;

//            foreach (var row in _secondaryRows)
//            {
//                if (!TryGetElementValue(row.Values, _focusElement, out var rawValue) || !rawValue.HasValue)
//                    continue;

//                var crmId = ExtractCrmIdFromLabel(row.SolutionLabel);
//                if (string.IsNullOrWhiteSpace(crmId))
//                    continue;

//                if (!_crmReference.TryGetValue(crmId, out var crmItem))
//                    continue;

//                if (!crmItem.Elements.TryGetValue(_focusElement!, out var certValue))
//                    continue;

//                rows.Add(new CalibrationRow
//                {
//                    SolutionLabel = row.SolutionLabel,
//                    CrmId = crmId,
//                    OriginalValue = rawValue.Value,
//                    CrmValue = certValue
//                });
//            }

//            return rows;
//        }

//        private async Task GetCurrentStats()
//        {
//            if (_projectId == null) return;

//            _isLoading = true;
//            StateHasChanged();

//            var result = await OptimizationService.GetCurrentStatsAsync(_projectId.Value, _minDiff, _maxDiff);

//            if (result.Succeeded && result.Data != null)
//            {
//                _result = result.Data;
//                UpdateOptimizedRows();
//                StateHasChanged();
//                await Task.Delay(150);
//                await RenderChartsAsync();
//                StateHasChanged();
//            }
//            else
//            {
//                Snackbar.Add(result.Message ?? "Failed to get stats", Severity.Error);
//            }

//            _isLoading = false;
//            StateHasChanged();
//        }

//        private async Task RunCalibration()
//        {
//            if (_projectId == null) return;

//            _isLoading = true;
//            StateHasChanged();

//            Snackbar.Add("Starting Calibration...", Severity.Info);

//            var result = await OptimizationService.GetCurrentStatsAsync(_projectId.Value, _minDiff, _maxDiff);

//            if (result.Succeeded && result.Data != null)
//            {
//                _result = result.Data;
//                UpdateOptimizedRows();
//                _resultsTabIndex = 1;

//                StateHasChanged();
//                await Task.Delay(250);
//                await RenderChartsAsync();
//                StateHasChanged();

//                Snackbar.Add($"Calibration Complete! Improvement: {_result.ImprovementPercent:F1}%", Severity.Success);
//            }
//            else
//            {
//                Snackbar.Add(result.Message ?? "Calibration failed", Severity.Error);
//            }

//            _isLoading = false;
//            StateHasChanged();
//        }

//        private async Task RunOptimization()
//        {
//            if (_projectId == null) return;

//            _isLoading = true;
//            StateHasChanged();

//            if (_crmSelectionRows.Any(r => string.IsNullOrWhiteSpace(r.SelectedOption)))
//            {
//                Snackbar.Add("Please select CRM method for all CRM rows before optimization.", Severity.Warning);
//                _isLoading = false;
//                return;
//            }

//            var request = new BlankScaleOptimizationRequest
//            {
//                ProjectId = _projectId.Value,
//                MinDiffPercent = _minDiff,
//                MaxDiffPercent = _maxDiff,
//                MaxIterations = _maxIterations,
//                PopulationSize = _populationSize,
//                UseMultiModel = _useMultiModel,
//                Elements = _selectedElements.Any() ? _selectedElements.ToList() : null,
//                RangeLow = _rangeLow,
//                RangeMid = _rangeMid,
//                RangeHigh1 = _rangeHigh1,
//                RangeHigh2 = _rangeHigh2,
//                RangeHigh3 = _rangeHigh3,
//                RangeHigh4 = _rangeHigh4,
//                ScaleRangeMin = _scaleRangeMin,
//                ScaleRangeMax = _scaleRangeMax,
//                ScaleAbove50Only = _scaleAbove50Only,
//                CrmSelections = _crmSelections.Count > 0 ? new Dictionary<string, string>(_crmSelections) : null,
//                IncludedCrmIds = _includedCrmIds.Count > 0 ? _includedCrmIds.ToList() : null,
//                ExcludedSolutionLabels = ParseExcludedLabels()
//            };

//            var result = await OptimizationService.OptimizeAsync(request);

//            if (result.Succeeded && result.Data != null)
//            {
//                _result = result.Data;
//                UpdateOptimizedRows();
//                StateHasChanged();
//                await Task.Delay(150);
//                await RenderChartsAsync();
//                StateHasChanged();
//                Snackbar.Add($"Optimization complete! Improvement: {_result.ImprovementPercent:F1}%", Severity.Success);
//            }
//            else
//            {
//                Snackbar.Add(result.Message ?? "Optimization failed", Severity.Error);
//            }

//            _isLoading = false;
//            StateHasChanged();
//        }

//        private async Task OnBeforeNavigation(LocationChangingContext context)
//        {
//            if (_isLoading)
//            {
//                context.PreventNavigation();
//            }
//        }

//        private async Task PreviewManualAsync()
//        {
//            if (_projectId == null || string.IsNullOrWhiteSpace(_focusElement))
//            {
//                Snackbar.Add("Please select a Project and Focus Element first.", Severity.Warning);
//                return;
//            }

//            _isLoading = true;
//            StateHasChanged();

//            try
//            {
//                var result = await OptimizationService.PreviewManualDetailsAsync(
//                    _projectId.Value,
//                    _focusElement,
//                    _previewBlank,
//                    (decimal)_previewScale);

//                if (result.Succeeded && result.Data != null)
//                {
//                    _manualResult = result.Data;
//                    UpdateManualRows();
//                }
//                else
//                {
//                    Snackbar.Add(result.Message ?? "Preview failed", Severity.Error);
//                }
//            }
//            catch (Exception ex)
//            {
//                Snackbar.Add($"Error: {ex.Message}", Severity.Error);
//            }
//            finally
//            {
//                _isLoading = false;
//                StateHasChanged();
//            }
//        }

//        private async Task ApplyManualAsync()
//        {
//            if (_projectId == null || string.IsNullOrWhiteSpace(_focusElement))
//                return;

//            _isLoading = true;
//            StateHasChanged();

//            var result = await OptimizationService.ApplyManualAsync(
//                _projectId.Value,
//                _focusElement,
//                _previewBlank,
//                (decimal)_previewScale);

//            if (result.Succeeded && result.Data != null)
//            {
//                _manualResult = result.Data;
//                UpdateManualRows();
//                Snackbar.Add("Manual blank/scale applied.", Severity.Success);
//                await RenderCalibrationChartAsync();
//                await RenderSecondaryChartAsync();
//            }
//            else
//            {
//                Snackbar.Add(result.Message ?? "Apply failed", Severity.Error);
//            }

//            _isLoading = false;
//        }

//        private async Task UndoManualAsync()
//        {
//            if (_projectId == null)
//                return;

//            _isLoading = true;
//            StateHasChanged();

//            var result = await CorrectionService.UndoLastCorrectionAsync(_projectId.Value);
//            if (result.Succeeded)
//            {
//                Snackbar.Add("Undo applied.", Severity.Success);
//                _previewBlank = 0m;
//                _previewScale = 1.0;
//                await GetCurrentStats();
//            }
//            else
//            {
//                Snackbar.Add(result.Message ?? "Undo failed", Severity.Error);
//            }

//            _isLoading = false;
//        }

//        private void ResetPreview()
//        {
//            _previewBlank = 0m;
//            _previewScale = 1.0;
//        }

//        private void ResetAll()
//        {
//            _minDiff = -10m;
//            _maxDiff = 10m;
//            //use multi-model
//            //filter element _selectedElements
//            _previewBlank = 0m;
//            _previewScale = 1.0;
//            _scaleRangeMin = null;
//            _scaleRangeMax = null;
//            // > 50 only
//            //_scaleAbove50Only = false;
//            ResetRanges();
//            RenderCharts();
//        }
//        private async Task PrevElement()
//        {
//            if (_allElements.Count == 0 || string.IsNullOrWhiteSpace(_focusElement))
//                return;

//            var idx = _allElements.IndexOf(_focusElement);
//            if (idx > 0)
//                await SetFocusElement(_allElements[idx - 1]);
//        }

//        private async Task NextElement()
//        {
//            if (_allElements.Count == 0 || string.IsNullOrWhiteSpace(_focusElement))
//                return;

//            var idx = _allElements.IndexOf(_focusElement);
//            if (idx < _allElements.Count - 1)
//                await SetFocusElement(_allElements[idx + 1]);
//        }

//        private List<OptimizedSampleRow> BuildRows(IEnumerable<OptimizedSampleDto>? data, string? element)
//        {
//            if (data == null || string.IsNullOrWhiteSpace(element))
//                return new List<OptimizedSampleRow>();

//            var rows = new List<OptimizedSampleRow>();
//            foreach (var sample in data)
//            {
//                TryGetElementValue(sample.OriginalValues, element, out var original);
//                TryGetElementValue(sample.OptimizedValues, element, out var optimized);
//                TryGetElementValue(sample.CrmValues, element, out var crmValue);
//                sample.DiffPercentBefore.TryGetValue(element, out var diffBefore);
//                sample.DiffPercentAfter.TryGetValue(element, out var diffAfter);
//                var passed = sample.PassStatusAfter.TryGetValue(element, out var p) && p;

//                if (original == null && optimized == null && crmValue == null)
//                    continue;

//                rows.Add(new OptimizedSampleRow(
//                    sample.SolutionLabel,
//                    sample.CrmId,
//                    element,
//                    original,
//                    optimized,
//                    crmValue,
//                    diffBefore,
//                    diffAfter,
//                    passed));
//            }

//            return rows;
//        }

//        private IEnumerable<OptimizedSampleRow> FilterRows(IEnumerable<OptimizedSampleRow> rows)
//        {
//            if (string.IsNullOrWhiteSpace(_sampleFilter))
//                return rows;

//            return rows.Where(r =>
//                r.SolutionLabel.Contains(_sampleFilter, StringComparison.OrdinalIgnoreCase));
//        }

//        private void OpenRangesDialog()
//        {
//            _rangesDialogVisible = true;
//        }

//        private void CloseRangesDialog()
//        {
//            _rangesDialogVisible = false;
//        }

//        private async Task ApplyRangesAsync()
//        {
//            _rangesDialogVisible = false;
//            await RenderCalibrationChartAsync();
//            Snackbar.Add("Acceptable ranges updated", Severity.Success);
//        }

//        private void ResetRanges()
//        {
//            _rangeLow = 2.0m;
//            _rangeMid = 20.0m;
//            _rangeHigh1 = 10.0m;
//            _rangeHigh2 = 8.0m;
//            _rangeHigh3 = 5.0m;
//            _rangeHigh4 = 3.0m;
//        }

//        private string GetSampleDetailsTitle()
//        {
//            if (_manualRows.Any())
//                return "Manual Preview Details";
//            return "Optimized Sample Details";
//        }

//        // ==========================================
//        // Chart Rendering Methods
//        // ==========================================

//        /// <summary>
//        /// Renders all charts
//        /// </summary>
//        private async Task RenderChartsAsync()
//        {
//            try
//            {
//                await JSRuntime.InvokeVoidAsync("destroyChart", "calibrationChart");
//                await JSRuntime.InvokeVoidAsync("destroyChart", "secondaryChart");

//                await Task.Delay(150);

//                if (_result != null)
//                {
//                    await RenderCalibrationChartAsync();
//                }

//                await RenderSecondaryChartAsync();
//                await JSRuntime.InvokeVoidAsync("resizeAllCharts");

//                _chartsRendered = true;
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error rendering charts: {ex.Message}");
//            }
//        }

//        private async Task RefreshChartsAsync()
//        {
//            await Task.Delay(10);
//            await RenderCalibrationChartAsync();
//            await RenderSecondaryChartAsync();
//            await JSRuntime.InvokeVoidAsync("resizeAllCharts");
//            StateHasChanged();
//        }

//        /// <summary>
//        /// Renders the Element-wise improvement chart
//        /// </summary>
//        private async Task RenderElementImprovementChartAsync()
//        {
//            try
//            {
//                var chartData = GetElementImprovementChartData();

//                var chartConfig = new
//                {
//                    type = "line",
//                    data = chartData,
//                    options = new
//                    {
//                        responsive = true,
//                        maintainAspectRatio = false,
//                        animation = new
//                        {
//                            duration = 750,
//                            easing = "easeInOutQuart"
//                        },
//                        plugins = new
//                        {
//                            legend = new { display = true, position = "top" },
//                            tooltip = new { backgroundColor = "rgba(0,0,0,0.7)" }
//                        },
//                        scales = new
//                        {
//                            y = new
//                            {
//                                beginAtZero = true
//                            }
//                        }
//                    }
//                };

//                await JSRuntime.InvokeVoidAsync("createChart", "elementChart", chartConfig);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error rendering element improvement chart: {ex.Message}");
//            }
//        }

//        public object GetElementImprovementChartData()
//        {
//            if (_result?.ElementOptimizations == null || !_result.ElementOptimizations.Any())
//                return new { labels = Array.Empty<string>(), datasets = Array.Empty<object>() };

//            var elements = _result.ElementOptimizations.Keys.OrderBy(x => x).ToList();
//            var diffBeforeList = new List<decimal>();
//            var diffAfterList = new List<decimal>();

//            foreach (var element in elements)
//            {
//                var opt = _result.ElementOptimizations[element];
//                diffBeforeList.Add(opt.MeanDiffBefore);
//                diffAfterList.Add(opt.MeanDiffAfter);
//            }

//            return new
//            {
//                labels = elements.ToArray(),
//                datasets = new object[]
//                {
//                    new
//                    {
//                        label = "Avg Diff % (Before)",
//                        data = diffBeforeList.Select(x => (double)x).ToArray(),
//                        borderColor = "#ff9800",
//                        backgroundColor = "rgba(255, 152, 0, 0.1)",
//                        borderWidth = 2,
//                        tension = 0.3,
//                        fill = false
//                    },
//                    new
//                    {
//                        label = "Avg Diff % (After)",
//                        data = diffAfterList.Select(x => (double)x).ToArray(),
//                        borderColor = "#4caf50",
//                        backgroundColor = "rgba(76, 175, 80, 0.1)",
//                        borderWidth = 2,
//                        tension = 0.3,
//                        fill = false
//                    }
//                }
//            };
//        }

//        public object GetCalibrationChartData()
//        {
//            if (string.IsNullOrWhiteSpace(_focusElement))
//                return new { labels = Array.Empty<string>(), datasets = Array.Empty<object>() };

//            var excludedLabels = new HashSet<string>(ParseExcludedLabels(), StringComparer.OrdinalIgnoreCase);
//            var calibrationRows = BuildCalibrationRows();
//            // Apply include filter only when user has actively deselected some items.
//            if (_crmLabelOptions.Count > 0 &&
//                _includedCrmLabels.Count > 0 &&
//                _includedCrmLabels.Count < _crmLabelOptions.Count)
//            {
//                calibrationRows = calibrationRows
//                    .Where(r => _includedCrmLabels.Contains(r.SolutionLabel))
//                    .ToList();
//            }

//            if (!calibrationRows.Any())
//                return new { labels = Array.Empty<string>(), datasets = Array.Empty<object>() };

//            var crmIds = calibrationRows.Select(r => r.CrmId)
//                .Distinct(StringComparer.OrdinalIgnoreCase)
//                .OrderBy(x => x)
//                .ToList();

//            var crmToIndex = crmIds.Select((id, i) => new { id, i })
//                .ToDictionary(x => x.id, x => x.i, StringComparer.OrdinalIgnoreCase);

//            var datasets = new List<object>();

//            if (_showAcceptableRange)
//            {
//                var rangePoints = new List<object>();

//                foreach (var crm in crmIds)
//                {
//                    var refRow = calibrationRows.FirstOrDefault(r => r.CrmId == crm && r.CrmValue.HasValue);
//                    if (refRow == null) continue;

//                    var certVal = refRow.CrmValue.Value;
//                    var tol = GetToleranceValue(certVal);

//                    var lower = (double)(certVal - tol);
//                    var upper = (double)(certVal + tol);
//                    var x = crmToIndex[crm];

//                    rangePoints.Add(new { x = x - 0.25, y = lower });
//                    rangePoints.Add(new { x = x + 0.25, y = lower });
//                    rangePoints.Add(new { x = (double?)null, y = (double?)null });

//                    rangePoints.Add(new { x = x - 0.25, y = upper });
//                    rangePoints.Add(new { x = x + 0.25, y = upper });
//                    rangePoints.Add(new { x = (double?)null, y = (double?)null });
//                }

//                if (rangePoints.Count > 0)
//                {
//                    datasets.Add(new
//                    {
//                        type = "line",
//                        label = "Acceptable Range",
//                        data = rangePoints,
//                        borderColor = "#FF0000",
//                        borderWidth = 2,
//                        showLine = true,
//                        pointRadius = 0,
//                        pointHoverRadius = 0,
//                        fill = false
//                    });
//                }
//            }

//            if (_showCertified)
//            {
//                var certPoints = new List<object>();
//                foreach (var crm in crmIds)
//                {
//                    var refRow = calibrationRows.FirstOrDefault(r => r.CrmId == crm && r.CrmValue.HasValue);
//                    if (refRow != null)
//                    {
//                        certPoints.Add(new { x = crmToIndex[crm], y = (double)refRow.CrmValue.Value });
//                    }
//                }
//                if (certPoints.Any())
//                {
//                    datasets.Add(new
//                    {
//                        label = "Certificate Value",
//                        data = certPoints,
//                        backgroundColor = "green",
//                        borderColor = "green",
//                        pointStyle = "circle",
//                        pointRadius = 8,
//                        showLine = false
//                    });
//                }
//            }

//            var samplePoints = new List<object>();
//            foreach (var r in calibrationRows)
//            {
//                if (!r.OriginalValue.HasValue)
//                    continue;

//                var displayVal = excludedLabels.Contains(r.SolutionLabel)
//                    ? (double)r.OriginalValue.Value
//                    : (double)GetPreviewValue(r.OriginalValue.Value);

//                samplePoints.Add(new
//                {
//                    x = crmToIndex[r.CrmId],
//                    y = displayVal,
//                    label = r.SolutionLabel
//                });
//            }

//            if (samplePoints.Any())
//            {
//                datasets.Add(new
//                {
//                    label = "Sample Value",
//                    data = samplePoints,
//                    backgroundColor = "blue",
//                    borderColor = "blue",
//                    pointStyle = "triangle",
//                    pointRadius = 8,
//                    rotation = 0,
//                    showLine = false
//                });
//            }

//            var labels = crmIds.Select(id => $"V {id}").ToArray();

//            return new
//            {
//                labels = labels,
//                datasets = datasets.ToArray()
//            };
//        }

//        private async Task RenderCalibrationChartAsync()
//        {
//            try
//            {
//                var chartData = GetCalibrationChartData();

//                var chartConfig = new
//                {
//                    type = "scatter",
//                    data = chartData,
//                    options = new
//                    {
//                        responsive = true,
//                        maintainAspectRatio = false,
//                        xLabels = ((dynamic)chartData).labels,
//                        layout = new
//                        {
//                            padding = new { bottom = 20, left = 10, right = 10, top = 10 }
//                        },
//                        plugins = new
//                        {
//                            legend = new { display = true, position = "top" },
//                            tooltip = new { backgroundColor = "rgba(0,0,0,0.7)" }
//                        },
//                        scales = new
//                        {
//                            x = new
//                            {
//                                type = "linear",
//                                title = new { display = true, text = "Verification ID" },
//                                ticks = new
//                                {
//                                    display = true,
//                                    autoSkip = true,
//                                    maxTicksLimit = 20,
//                                    color = "#666"
//                                },
//                                grid = new
//                                {
//                                    display = true,
//                                    drawBorder = true
//                                }
//                            },
//                            y = new { beginAtZero = false }
//                        }
//                    }
//                };

//                await JSRuntime.InvokeVoidAsync("createChart", "calibrationChart", chartConfig);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error rendering calibration chart: {ex.Message}");
//            }
//        }

//        private object GetSecondaryChartData()
//        {
//            if (string.IsNullOrWhiteSpace(_focusElement) || !_secondaryRows.Any())
//                return new { datasets = Array.Empty<object>() };

//            var filter = _sampleFilter?.Trim();
//            var rows = string.IsNullOrWhiteSpace(filter)
//                ? _secondaryRows
//                : _secondaryRows.Where(r => r.SolutionLabel.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

//            var originalPoints = new List<object>();
//            var correctedPoints = new List<object>();

//            for (var i = 0; i < rows.Count; i++)
//            {
//                var row = rows[i];
//                var value = 0m;
//                if (TryGetElementValue(row.Values, _focusElement, out var rawValue) && rawValue.HasValue)
//                {
//                    value = rawValue.Value;
//                }

//                var x = i;
//                var originalVal = (double)value;
//                var correctedVal = (double)((value - _previewBlank) * (decimal)_previewScale);

//                originalPoints.Add(new { x, y = originalVal, label = row.SolutionLabel });
//                correctedPoints.Add(new { x, y = correctedVal, label = row.SolutionLabel });
//            }

//            return new
//            {
//                datasets = new object[]
//                {
//                    new
//                    {
//                        label = "Original",
//                        data = originalPoints,
//                        backgroundColor = "#2196F3",
//                        borderColor = "#2196F3",
//                        pointStyle = "circle",
//                        pointRadius = 6,
//                        showLine = false
//                    },
//                    new
//                    {
//                        label = "Corrected",
//                        data = correctedPoints,
//                        backgroundColor = "#F44336",
//                        borderColor = "#F44336",
//                        pointStyle = "cross",
//                        pointRadius = 7,
//                        showLine = false
//                    }
//                }
//            };
//        }

//        private async Task RenderSecondaryChartAsync()
//        {
//            try
//            {
//                // ابتدا chart موجود را پاک کن
//                await JSRuntime.InvokeVoidAsync("destroyChart", "secondaryChart");

//                await Task.Delay(50); // کمی تاخیر برای DOM

//                var chartData = GetSecondaryChartData();
//                var chartConfig = new
//                {
//                    type = "scatter",
//                    data = chartData,
//                    options = new
//                    {
//                        responsive = true,
//                        maintainAspectRatio = false, // مهم: aspect ratio را غیرفعال کن
//                        layout = new
//                        {
//                            padding = new { bottom = 20, left = 10, right = 10, top = 10 }
//                        },
//                        animation = new
//                        {
//                            duration = 0 // انیمیشن را برای رندر سریع‌تر غیرفعال کن
//                        },
//                        plugins = new
//                        {
//                            legend = new
//                            {
//                                display = true,
//                                position = "top",
//                                labels = new
//                                {
//                                    boxWidth = 12,
//                                    padding = 10
//                                }
//                            },
//                            tooltip = new
//                            {
//                                backgroundColor = "rgba(0,0,0,0.8)",
//                                titleFont = new { size = 12 },
//                                bodyFont = new { size = 12 },
//                                padding = 8
//                            }
//                        },
//                        scales = new
//                        {
//                            x = new
//                            {
//                                type = "linear",
//                                title = new
//                                {
//                                    display = true,
//                                    text = "Index",
//                                    font = new { size = 12 }
//                                },
//                                grid = new
//                                {
//                                    drawBorder = true,
//                                    display = true,
//                                    color = "rgba(0,0,0,0.1)"
//                                },
//                                ticks = new
//                                {
//                                    display = true,
//                                    autoSkip = true,
//                                    color = "#666",
//                                    font = new { size = 10 },
//                                    maxTicksLimit = 20
//                                }
//                            },
//                            y = new
//                            {
//                                title = new
//                                {
//                                    display = true,
//                                    text = "Value",
//                                    font = new { size = 12 }
//                                },
//                                grid = new
//                                {
//                                    drawBorder = false,
//                                    color = "rgba(0,0,0,0.1)"
//                                },
//                                ticks = new
//                                {
//                                    font = new { size = 10 },
//                                    maxTicksLimit = 10
//                                },
//                                beginAtZero = false
//                            }
//                        },
//                        elements = new
//                        {
//                            point = new
//                            {
//                                radius = 4, // نقطه‌ها را کوچک‌تر کن
//                                hoverRadius = 6
//                            }
//                        },
//                        interaction = new
//                        {
//                            intersect = false,
//                            mode = "nearest"
//                        }
//                    }
//                };

//                await JSRuntime.InvokeVoidAsync("createChart", "secondaryChart", chartConfig);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error rendering secondary chart: {ex.Message}");
//            }
//        }

//        private decimal GetToleranceValue(decimal crmValue)
//        {
//            var absVal = Math.Abs(crmValue);

//            if (absVal < 10) return _rangeLow;

//            decimal percentage = 0;
//            if (absVal < 100) percentage = _rangeMid;
//            else if (absVal < 1000) percentage = _rangeHigh1;
//            else if (absVal < 10000) percentage = _rangeHigh2;
//            else if (absVal < 100000) percentage = _rangeHigh3;
//            else percentage = _rangeHigh4;

//            return absVal * (percentage / 100m);
//        }

//        private decimal GetPreviewValue(decimal originalValue)
//        {
//            if (_scaleRangeMin.HasValue && _scaleRangeMax.HasValue)
//            {
//                if (originalValue < _scaleRangeMin.Value || originalValue > _scaleRangeMax.Value)
//                    return originalValue;
//            }

//            if (_scaleAbove50Only && originalValue <= 50)
//                return originalValue;

//            return (originalValue - _previewBlank) * (decimal)_previewScale;
//        }

//        private async Task OnPreviewParamChanged()
//        {
//            await RenderCalibrationChartAsync();
//            await RenderSecondaryChartAsync();
//        }

//        private async Task OnPreviewScaleChanged(double newVal)
//        {
//            _previewScale = newVal;
//            await OnPreviewParamChanged();
//        }

//        private async Task OnRangeMinChanged(decimal? newVal)
//        {
//            _scaleRangeMin = newVal;
//            await OnPreviewParamChanged();
//        }

//        private async Task OnRangeMaxChanged(decimal? newVal)
//        {
//            _scaleRangeMax = newVal;
//            await OnPreviewParamChanged();
//        }

//        private async Task OnPreviewBlankChanged(decimal? newVal)
//        {
//            _previewBlank = newVal ?? 0m;
//            await OnPreviewParamChanged();
//        }

//        private async Task OnFilterChanged(string value)
//        {
//            _sampleFilter = value ?? string.Empty;
//            await RenderSecondaryChartAsync();
//        }

//        private async Task OnShowCertifiedChanged(bool value)
//        {
//            _showCertified = value;
//            await RenderCalibrationChartAsync();
//        }

//        private async Task OnShowRangeChanged(bool value)
//        {
//            _showAcceptableRange = value;
//            await RenderCalibrationChartAsync();
//        }

//        private async Task OnScaleAbove50Changed(bool value)
//        {
//            _scaleAbove50Only = value;
//            await OnPreviewParamChanged();
//        }

//        private void OpenSelectVerificationsDialog()
//        {
//            _selectVerificationsDialogVisible = true;
//        }

//        private void CloseSelectVerificationsDialog()
//        {
//            _selectVerificationsDialogVisible = false;
//        }

//        private async Task IncludeAllVerifications()
//        {
//            _includedCrmLabels.Clear();
//            foreach (var label in _crmLabelOptions)
//                _includedCrmLabels.Add(label);
//            await RenderCalibrationChartAsync();
//        }

//        private async Task ExcludeAllVerifications()
//        {
//            _includedCrmLabels.Clear();
//            await RenderCalibrationChartAsync();
//        }

//        private void OpenExcludeDialog()
//        {
//            UpdateExcludeLabelRows();
//            _excludeDialogVisible = true;
//        }

//        private void CloseExcludeDialog()
//        {
//            _excludeDialogVisible = false;
//        }

//        private async Task ToggleExcludedLabel(string label, bool isExcluded)
//        {
//            if (isExcluded)
//                _excludedLabels.Add(label);
//            else
//                _excludedLabels.Remove(label);

//            SyncExcludedLabelsInput();
//            await RenderCalibrationChartAsync();
//        }

//        private EventCallback<bool> GetIncludeCallback(string crmId)
//        {
//            return EventCallback.Factory.Create<bool>(this, (bool v) => ToggleIncludedCrmId(crmId, v));
//        }

//        private EventCallback<bool> GetExcludeCallback(string label)
//        {
//            return EventCallback.Factory.Create<bool>(this, (bool v) => ToggleExcludedLabel(label, v));
//        }

//        private async Task OpenReportDialog()
//        {
//            if (_result == null && _projectId.HasValue)
//            {
//                await GetCurrentStats();
//            }

//            if (TryGetRecommendedModel(out var blank, out var scale))
//            {
//                _reportBlank = blank;
//                _reportScale = scale;
//            }
//            else
//            {
//                _reportBlank = _previewBlank;
//                _reportScale = (decimal)_previewScale;
//            }

//            _reportDialogVisible = true;
//        }

//        private void CloseReportDialog()
//        {
//            _reportDialogVisible = false;
//        }

//        private async Task ApplyRecommendedModel()
//        {
//            if (!TryGetRecommendedModel(out var blank, out var scale))
//            {
//                Snackbar.Add("No model recommendation available.", Severity.Warning);
//                return;
//            }

//            _previewBlank = blank;
//            _previewScale = (double)scale;
//            _reportBlank = blank;
//            _reportScale = scale;
//            _reportDialogVisible = false;
//            await OnPreviewParamChanged();
//        }

//        private bool TryGetRecommendedModel(out decimal blank, out decimal scale)
//        {
//            blank = _previewBlank;
//            scale = (decimal)_previewScale;

//            if (_result == null || string.IsNullOrWhiteSpace(_focusElement))
//                return false;

//            if (!_result.ElementOptimizations.TryGetValue(_focusElement, out var opt))
//                return false;

//            blank = opt.Blank;
//            scale = opt.Scale;
//            return true;
//        }

//        private async Task RenderCharts()
//        {

//            await OnPreviewParamChanged();
//        }
//    }
//}
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
