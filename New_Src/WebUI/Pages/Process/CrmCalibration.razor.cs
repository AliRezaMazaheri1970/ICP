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
        private List<AdvancedPivotRowDto> _secondaryRows = new();
        private List<RawElementRow> _rawElementRows = new();
        private List<string> _blankLabelLines = new();
        private string _calibrationRange = "[0 to 0]";
        private bool _selectVerificationsDialogVisible = false;
        private bool _excludeDialogVisible = false;
        private bool _reportDialogVisible = false;
        private decimal _reportBlank = 0m;
        private decimal _reportScale = 1m;
        private HashSet<string> _excludedLabels = new(StringComparer.OrdinalIgnoreCase);
        private List<string> _crmIdOptions = new();
        private List<string> _crmLabelOptions = new();
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
        private bool _showCrm = true;
        private bool _showVerification = true;
        private bool _showAcceptableRange = true;

        // Scale Application Range (Python feature)
        private decimal? _scaleRangeMin;
        private decimal? _scaleRangeMax;
        private bool _scaleAbove50Only = false;

        // Acceptable Ranges (Python feature - magnitude-based thresholds)
        private decimal _rangeLow = 2.0m;     // |x| < 10: absolute Â±
        private decimal _rangeMid = 20.0m;    // 10 â‰¤ |x| < 100: percentage
        private decimal _rangeHigh1 = 10.0m;  // 100 â‰¤ |x| < 1000: percentage
        private decimal _rangeHigh2 = 8.0m;   // 1000 â‰¤ |x| < 10000: percentage
        private decimal _rangeHigh3 = 5.0m;   // 10000 â‰¤ |x| < 100000: percentage
        private decimal _rangeHigh4 = 3.0m;   // |x| â‰¥ 100000: percentage
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
            // Ø§Ú¯Ø± Ù…ÛŒâ€ŒØ®ÙˆØ§Ù‡ÛŒ ØªØ¹Ø¯Ø§Ø¯ Ø§Ø¹Ø´Ø§Ø± Ø¯Ù‚ÛŒÙ‚â€ŒØªØ± Ø¨Ø§Ø´Ø¯ØŒ Ø§ÛŒÙ†Ø¬Ø§ ØªÙ†Ø¸ÛŒÙ… Ú©Ù†
            return v.Value.ToString("0.####");
        }

        private IEnumerable<string> PivotColumns()
        {
            // Ø§Ú¯Ø± Ú©Ø§Ø±Ø¨Ø± Ú†ÛŒØ²ÛŒ Ø§Ù†ØªØ®Ø§Ø¨ Ù†Ú©Ø±Ø¯ØŒ Ø­Ø¯Ø§Ù‚Ù„ FocusElement Ø±Ø§ Ù†Ø´Ø§Ù† Ø¨Ø¯Ù‡
            if (_pivotSelectedElements.Count == 0 && !string.IsNullOrWhiteSpace(_focusElement))
                return new[] { _focusElement! };

            // ØªØ¹Ø¯Ø§Ø¯ Ø³ØªÙˆÙ†â€ŒÙ‡Ø§ Ø±Ø§ Ø¨Ø±Ø§ÛŒ UX Ù…Ø­Ø¯ÙˆØ¯ Ú©Ù† (Ù…Ø«Ù„Ø§Ù‹ 12)
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
                // Ù„ÙˆØ¯ Ú©Ù„ Ø¯ÛŒØªØ§ÛŒ Ù¾Ø±ÙˆÚ˜Ù‡ (854 Ø±Ø¯ÛŒÙ) Ø§Ø² Ø³Ø±ÙˆÛŒØ³ Ø§ØµÙ„ÛŒ Ù¾ÛŒÙˆØª
                var request = new AdvancedPivotRequest(
                    ProjectId: _projectId.Value,
                    SearchText: _sampleFilter,
                    SelectedElements: _allElements.ToList(), // Argument 4
                    NumberFilters: null,                    // Argument 5
                    UseOxide: false,                        // Argument 6
                    UseInt: false,                          // Argument 7
                    DecimalPlaces: 4,                       // Argument 8
                    Page: 1,                                // Argument 9
                    PageSize: 2000,                         // Ù„ÙˆØ¯ Ú©Ø§Ù…Ù„ Ø¨Ø±Ø§ÛŒ Ø§Ø³Ú©Ø±ÙˆÙ„ (Argument 10)
                    MergeRepeats: false,                    // Argument 11
                    Aggregation: "First"                    // Argument 12
                );

                var result = await PivotService.GetAdvancedPivotTableAsync(request);

                if (result.Succeeded && result.Data != null)
                {
                    var cols = PivotColumns().ToList();
                    var rows = new List<PivotRowVm>();
                    int order = 0;

                    // Ø¯Ø³ØªØ±Ø³ÛŒ Ø¨Ù‡ Ø¯ÛŒØªØ§ÛŒ Ù…Ø­Ø¯ÙˆØ¯ Ø´Ø¯Ù‡ CRMÙ‡Ø§
                    var optimizedData = _manualResult?.OptimizedData ?? _result?.OptimizedData;

                    foreach (var s in result.Data.Rows)
                    {
                        // 1. Ø§Ø¶Ø§ÙÙ‡ Ú©Ø±Ø¯Ù† Ø±Ø¯ÛŒÙ Ø§ØµÙ„ÛŒ Ù†Ù…ÙˆÙ†Ù‡ (Ø¨Ø±Ø§ÛŒ Ù‡Ù…Ù‡ 854 Ø±Ø¯ÛŒÙ)
                        rows.Add(new PivotRowVm
                        {
                            Order = order++,
                            SolutionLabel = s.SolutionLabel,
                            RowType = PivotRowType.Sample,
                            Values = s.Values
                        });

                        // 2. Ø§Ú¯Ø± Ø§ÛŒÙ† Ø±Ø¯ÛŒÙ Ø¬Ø²Ùˆ CRMÙ‡Ø§ÛŒ Ú©Ø§Ù„ÛŒØ¨Ø±Ø§Ø³ÛŒÙˆÙ† Ø¨ÙˆØ¯ØŒ Ø±Ø¯ÛŒÙâ€ŒÙ‡Ø§ÛŒ Ù…Ø±Ø¬Ø¹ Ùˆ Diff Ø±Ø§ ØªØ²Ø±ÛŒÙ‚ Ú©Ù†
                        var crmMatch = optimizedData?.FirstOrDefault(x => x.SolutionLabel == s.SolutionLabel);
                        if (crmMatch != null && !string.IsNullOrEmpty(crmMatch.CrmId))
                        {
                            // Ø±Ø¯ÛŒÙ Ø²Ø±Ø¯ (Reference)
                            rows.Add(new PivotRowVm
                            {
                                Order = order++,
                                SolutionLabel = $"{crmMatch.CrmId} CRM",
                                RowType = PivotRowType.CrmRef,
                                Values = BuildDictValues(crmMatch.CrmValues, cols)
                            });

                            // Ø±Ø¯ÛŒÙ ØµÙˆØ±ØªÛŒ (Diff %)
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

                // Ø§ÛŒÙ†Ø¬Ø§ ØªØ¹ÛŒÛŒÙ† Ù…ÛŒâ€ŒÚ©Ù†ÛŒÙ… Sample Row Ù…Ù‚Ø¯Ø§Ø± Original Ø¨Ø§Ø´Ø¯ ÛŒØ§ Optimized
                // (Ù¾ÛŒØ´Ù†Ù‡Ø§Ø¯: ÙÙ‚Ø· Original/Optimized Ø±Ø§ Ø¨Ø±Ø§ÛŒ _pivotMode Ù†Ú¯Ù‡ Ø¯Ø§Ø±)
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
            UpdateCalibrationRange();

            // Ø§Ú¯Ø± user Ù‡Ù†ÙˆØ² Ø³ØªÙˆÙ† Ø§Ù†ØªØ®Ø§Ø¨ Ù†Ú©Ø±Ø¯Ù‡ Ø¨ÙˆØ¯ØŒ focus Ø±Ùˆ Ø³ØªÙˆÙ† Ø§ÙˆÙ„ Ú©Ù†
            if (_pivotSelectedElements.Count == 0)
            {
                _pivotSelectedElements.Add(_focusElement);
                RebuildPivot();
            }

            // Ensure UI updates and then refresh charts that depend on focus element
            StateHasChanged();
            await Task.Delay(50);

            // Re-render the calibration scatter and update the element improvement metric
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
            await LoadRawRowsAsync();
            UpdateCalibrationRange();
            await LoadSecondaryPlotRowsAsync();
            await GetCurrentStats();
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

                _crmIdOptions = _crmOptions
                    .Select(x => x.CrmId)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
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

        private async Task LoadRawRowsAsync()
        {
            if (_projectId == null)
                return;

            _rawElementRows.Clear();
            var skip = 0;
            const int take = 2000;

            while (true)
            {
                var result = await ProjectService.GetProjectRawRowsAsync(_projectId.Value, skip, take);
                if (!result.Succeeded || result.Data == null)
                {
                    if (!string.IsNullOrWhiteSpace(result.Message))
                    {
                        Snackbar.Add(result.Message, Severity.Warning);
                    }
                    break;
                }

                foreach (var row in result.Data)
                {
                    var parsed = ParseRawRow(row.ColumnData);
                    if (parsed != null)
                    {
                        _rawElementRows.Add(parsed);
                    }
                }

                if (result.Data.Count < take)
                    break;

                skip += take;
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
                    SelectedElements: new List<string> { _focusElement },
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
                return;

            var labels = calibrationRows
                .Select(r => r.SolutionLabel)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _crmLabelOptions = labels;

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

                row.Values.TryGetValue(_focusElement, out var value);
                var display = value.HasValue ? value.Value.ToString("0.####") : "---";
                _blankLabelLines.Add($"{row.SolutionLabel}: {display}");
            }
        }

        private void UpdateExcludeLabelRows()
        {
            _excludeLabelRows = _secondaryRows
                .Select(row =>
                {
                    row.Values.TryGetValue(_focusElement ?? string.Empty, out var value);
                    return new ExcludeLabelRow
                    {
                        SolutionLabel = row.SolutionLabel,
                        Value = value
                    };
                })
                .OrderBy(row => row.SolutionLabel, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void UpdateCalibrationRange()
        {
            if (string.IsNullOrWhiteSpace(_focusElement) || !_rawElementRows.Any())
            {
                _calibrationRange = "[0 to 0]";
                return;
            }

            var values = _rawElementRows
                .Where(r => r.Element.Equals(_focusElement, StringComparison.OrdinalIgnoreCase))
                .Where(r => r.Type.Equals("Samp", StringComparison.OrdinalIgnoreCase) || r.Type.Equals("Sample", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.SolnConc ?? r.CorrCon)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            if (values.Count == 0)
            {
                _calibrationRange = "[0 to 0]";
                return;
            }

            var min = values.Min();
            var max = values.Max();
            _calibrationRange = $"[{min:0.####} to {max:0.####}]";
        }

        private static bool IsBlankLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return false;

            return label.Contains("BLANK", StringComparison.OrdinalIgnoreCase) ||
                   label.Contains("BLNK", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractCrmIdFromLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return string.Empty;

            var match = CrmIdRegex.Match(label);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static RawElementRow? ParseRawRow(string columnData)
        {
            if (string.IsNullOrWhiteSpace(columnData))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(columnData);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    map[prop.Name] = prop.Value;
                }

                var type = GetString(map, "Type") ?? string.Empty;
                var element = GetString(map, "Element") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(element))
                {
                    return null;
                }

                var solnConc = GetDecimal(map, "Soln Conc", "SolnConc");
                var corrCon = GetDecimal(map, "Corr Con", "CorrCon", "Concentration", "Conc", "Calibrated Conc", "Result");

                return new RawElementRow
                {
                    Type = type.Trim(),
                    Element = element.Trim(),
                    SolnConc = solnConc,
                    CorrCon = corrCon
                };
            }
            catch
            {
                return null;
            }
        }

        private static string? GetString(Dictionary<string, JsonElement> map, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (map.TryGetValue(key, out var value))
                {
                    if (value.ValueKind == JsonValueKind.String)
                    {
                        return value.GetString();
                    }

                    if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var num))
                    {
                        return num.ToString(CultureInfo.InvariantCulture);
                    }
                }
            }

            return null;
        }

        private static decimal? GetDecimal(Dictionary<string, JsonElement> map, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!map.TryGetValue(key, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var num))
                {
                    return num;
                }

                if (value.ValueKind == JsonValueKind.String)
                {
                    var str = value.GetString();
                    if (decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return parsed;
                    }

                    if (decimal.TryParse(str, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
                    {
                        return parsed;
                    }
                }
            }

            return null;
        }

        private sealed class RawElementRow
        {
            public string Type { get; set; } = "";
            public string Element { get; set; } = "";
            public decimal? SolnConc { get; set; }
            public decimal? CorrCon { get; set; }
        }

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

        private List<CalibrationRow> BuildCalibrationRows()
        {
            var rows = new List<CalibrationRow>();
            var dataSource = _manualResult?.OptimizedData ?? _result?.OptimizedData;

            if (dataSource != null && dataSource.Any())
            {
                foreach (var sample in dataSource)
                {
                    if (string.IsNullOrWhiteSpace(sample.CrmId))
                        continue;
                    if (!IsCrmLabel(sample.SolutionLabel))
                        continue;

                    if (!sample.CrmValues.TryGetValue(_focusElement!, out var crmValue) || !crmValue.HasValue)
                        continue;

                    sample.OriginalValues.TryGetValue(_focusElement!, out var originalValue);
                    sample.OptimizedValues.TryGetValue(_focusElement!, out var optimizedValue);
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
                if (!IsCrmLabel(row.SolutionLabel))
                    continue;
                if (!row.Values.TryGetValue(_focusElement!, out var rawValue) || !rawValue.HasValue)
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

        private static bool IsCrmLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return false;

            return label.Contains("CRM", StringComparison.OrdinalIgnoreCase) ||
                   label.Contains("OREAS", StringComparison.OrdinalIgnoreCase);
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
        /// Ø¯Ú©Ù…Ù‡ Calibration - Ø¨Ø±Ø§ÛŒ Ú©Ø§Ù„ÛŒØ¨Ø±ÛŒØ´Ù† ÙÙˆØ±ÛŒ Ùˆ Ù†Ù…Ø§ÛŒØ´ Ù†Ù…ÙˆØ¯Ø§Ø±Ù‡Ø§
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

                // ØªØºÛŒÛŒØ± Ø¨Ù‡ Tab2 Ø¨Ø±Ø§ÛŒ Ù†Ù…Ø§ÛŒØ´ Ù†Ù…ÙˆØ¯Ø§Ø±Ù‡Ø§
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
                    UpdateManualRows(); // Ø§ÛŒÙ† Ù…ØªØ¯ Ù„ÛŒØ³Øª _manualRows Ø±Ø§ Ù¾Ø± Ù…ÛŒâ€ŒÚ©Ù†Ø¯

                    _detailsExpanded = true; // Ø§Ø¬Ø¨Ø§Ø± Ø¨Ù‡ Ø¨Ø§Ø² Ø´Ø¯Ù† Ù¾Ù†Ù„
                    _result = _result ?? new BlankScaleOptimizationResult(); // ÛŒÚ© Ù…Ù‚Ø¯Ø§Ø± ØºÛŒØ± Ù†Ø§Ù„ Ù…ÙˆÙ‚Øª Ø¨Ø±Ø§ÛŒ Ø¹Ø¨ÙˆØ± Ø§Ø² Ø´Ø±Ø· Ù†Ù…Ø§ÛŒØ´
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
                StateHasChanged(); // Ø±Ù†Ø¯Ø± Ù…Ø¬Ø¯Ø¯ ØµÙØ­Ù‡
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
            await RenderCalibrationChartAsync();
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
            try
            {
                // Clear existing charts first
                await JSRuntime.InvokeVoidAsync("destroyChart", "calibrationChart");
                await JSRuntime.InvokeVoidAsync("destroyChart", "secondaryChart");

                // Wait for DOM to be fully ready
                await Task.Delay(150);

                if (_result != null)
                {
                    await RenderCalibrationChartAsync();
                }

                await RenderSecondaryChartAsync();
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
                                // Ø§ØµÙ„Ø§Ø­ Ù…Ù‡Ù…: Ø­Ø°Ù max = 100 Ùˆ ticks Ø«Ø§Ø¨Øª
                                // Ø§ÛŒÙ† Ú©Ø§Ø± Ø¨Ø§Ø¹Ø« Ù…ÛŒâ€ŒØ´ÙˆØ¯ Ù†Ù…ÙˆØ¯Ø§Ø± Ù…Ø«Ù„ Ù¾Ø§ÛŒØªÙˆÙ† Ø¨Ø± Ø§Ø³Ø§Ø³ Ø¯Ø§Ø¯Ù‡â€ŒÙ‡Ø§ Ø§Ø³Ú©ÛŒÙ„ Ø´ÙˆØ¯
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
            if (string.IsNullOrWhiteSpace(_focusElement))
                return new { labels = Array.Empty<string>(), datasets = Array.Empty<object>() };

            var excludedLabels = new HashSet<string>(ParseExcludedLabels(), StringComparer.OrdinalIgnoreCase);
            var calibrationRows = BuildCalibrationRows();
            if (_crmLabelOptions.Count > 0)
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

            if (_showCrm)
            {
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
            }

            var labels = crmIds.Select(id => $"V {id}").ToArray();

            return new
            {
                labels = labels,
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
                if (row.Values.TryGetValue(_focusElement, out var rawValue) && rawValue.HasValue)
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
                var chartData = GetSecondaryChartData();
                var chartConfig = new
                {
                    type = "scatter",
                    data = chartData,
                    options = new
                    {
                        responsive = true,
                        maintainAspectRatio = false,
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
                                title = new { display = true, text = "Index" }
                            },
                            y = new
                            {
                                title = new { display = true, text = "Value" }
                            }
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

        private async Task RefreshChartsAsync()
        {
            await Task.Delay(10);
            await RenderCalibrationChartAsync();
            await RenderSecondaryChartAsync();
            StateHasChanged();
        }


        private decimal GetToleranceValue(decimal crmValue)
        {
            var absVal = Math.Abs(crmValue);

            // Ø·Ø¨Ù‚ Ú©Ø¯ Ù¾Ø§ÛŒØªÙˆÙ†: if abs_value < 10: return self.w.range_low
            // Ø§ÛŒÙ† ÛŒØ¹Ù†ÛŒ Ø¨Ø±Ø§ÛŒ Ø§Ø¹Ø¯Ø§Ø¯ Ú©ÙˆÚ†Ú©ØŒ Ù…Ù‚Ø¯Ø§Ø± rangeLow ÛŒÚ© Ø¹Ø¯Ø¯ Ø«Ø§Ø¨Øª (Absolute) Ø§Ø³ØªØŒ Ù†Ù‡ Ø¯Ø±ØµØ¯.
            if (absVal < 10) return _rangeLow;

            // Ø¨Ø±Ø§ÛŒ Ø³Ø§ÛŒØ± Ø¨Ø§Ø²Ù‡â€ŒÙ‡Ø§ØŒ Ø¯Ø±ØµØ¯ Ù…Ø­Ø§Ø³Ø¨Ù‡ Ù…ÛŒâ€ŒØ´ÙˆØ¯
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

        private async Task UpdateElementImprovementChartAsync()
        {
            var data = _manualResult?.OptimizedData ?? _result?.OptimizedData;
            if (data == null || string.IsNullOrWhiteSpace(_focusElement)) return;

            decimal totalDiff = 0;
            int count = 0;

            foreach (var row in data)
            {
                // Ø¯Ø±ÛŒØ§ÙØª Ù…Ù‚Ø§Ø¯ÛŒØ± Ø§Ø² Ø¯ÛŒÚ©Ø´Ù†Ø±ÛŒ
                if (!row.CrmValues.TryGetValue(_focusElement, out var certified) || certified == null || certified == 0) continue;
                if (!row.OriginalValues.TryGetValue(_focusElement, out var original) || original == null) continue;

                // Ù…Ø­Ø§Ø³Ø¨Ù‡ Ù…Ù‚Ø¯Ø§Ø± Ø¬Ø¯ÛŒØ¯ Ø¨Ø§ ØªÙ†Ø¸ÛŒÙ…Ø§Øª ÙØ¹Ù„ÛŒ
                var calculated = GetPreviewValue(original.Value);

                // Ù…Ø­Ø§Ø³Ø¨Ù‡ Ø¯Ø±ØµØ¯ Ø®Ø·Ø§: (Calc - Cert) / Cert * 100
                var diff = (calculated - certified.Value) / certified.Value * 100m;

                // Ø¯Ø± Ù†Ù…ÙˆØ¯Ø§Ø± "Avg Diff %"ØŒ Ù…Ø¹Ù…ÙˆÙ„Ø§ Ù‚Ø¯Ø±Ù…Ø·Ù„Ù‚ Ø®Ø·Ø§ Ù…ÛŒØ§Ù†Ú¯ÛŒÙ† Ú¯Ø±ÙØªÙ‡ Ù…ÛŒâ€ŒØ´ÙˆØ¯ ØªØ§ Ø®Ø·Ø§Ù‡Ø§ÛŒ Ù…Ø«Ø¨Øª Ùˆ Ù…Ù†ÙÛŒ Ù‡Ù…Ø¯ÛŒÚ¯Ø± Ø±Ø§ Ø®Ù†Ø«ÛŒ Ù†Ú©Ù†Ù†Ø¯
                totalDiff += Math.Abs(diff);
                count++;
            }

            if (count > 0)
            {
                var newMeanDiff = totalDiff / count;

                // Ø¢Ù¾Ø¯ÛŒØª Ù…Ù‚Ø¯Ø§Ø± Ø¯Ø± Ø­Ø§ÙØ¸Ù‡
                if (_result.ElementOptimizations.ContainsKey(_focusElement))
                {
                    _result.ElementOptimizations[_focusElement].MeanDiffAfter = newMeanDiff;
                }

                // Ø±Ù†Ø¯Ø± Ù…Ø¬Ø¯Ø¯ Ù†Ù…ÙˆØ¯Ø§Ø± Ø¨Ø§Ù„Ø§
                await RenderElementImprovementChartAsync();
            }
        }

        private async Task OnPreviewParamChanged()
        {
            // Ø¬Ù„ÙˆÚ¯ÛŒØ±ÛŒ Ø§Ø² Ø±ÙØ±Ø´â€ŒÙ‡Ø§ÛŒ Ø®ÛŒÙ„ÛŒ Ø³Ø±ÛŒØ¹ (Ø§Ø®ØªÛŒØ§Ø±ÛŒ)
            // await Task.Delay(50); 

            // 1. Ø¢Ù¾Ø¯ÛŒØª Ù†Ù…ÙˆØ¯Ø§Ø± Ù¾Ø§ÛŒÛŒÙ† (Calibration Plot)
            await RenderCalibrationChartAsync();

            // 2. Ø¢Ù¾Ø¯ÛŒØª Ù†Ù…ÙˆØ¯Ø§Ø± Ø¨Ø§Ù„Ø§ (Element Improvement)
            await RenderSecondaryChartAsync();
        }

        // Ù‡Ù†Ø¯Ù„Ø±Ù‡Ø§ÛŒ Ø§Ø®ØªØµØ§ØµÛŒ Ø¨Ø±Ø§ÛŒ Ø¨Ø§ÛŒÙ†Ø¯ÛŒÙ†Ú¯
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

        private async Task OnShowCrmChanged(bool value)
        {
            _showCrm = value;
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

        private async Task ResetBlankAndScale()
        {
            _previewBlank = 0m;
            _previewScale = 1.0;
            await OnPreviewParamChanged();
        }

    }
}






