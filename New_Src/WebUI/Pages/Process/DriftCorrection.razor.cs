using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using MudBlazor;
using System.Text.RegularExpressions;
using System.Threading;
using WebUI.Services;

namespace WebUI.Pages.Process
{
    public partial class DriftCorrection : IDisposable
    {
        [SupplyParameterFromQuery]
        public Guid? projectId { get; set; }

        private readonly SemaphoreSlim _loadingLock = new(1, 1);
        private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

        private Guid? _projectId;
        private string? _projectName;

        private DriftMethod _method = DriftMethod.Linear;
        private List<string> _allElements = new();
        private string? _focusElement;
        private bool _useSegmentation = true;
        private DriftCorrectionResult? _analysisResult;

        private bool _isLoading;
        private bool _isApplying;
        private bool _chartRenderPending;

        private string _keyword = "RM";
        private string _solutionFilter = string.Empty;
        private bool _applyStepwiseChanges;
        private bool _globalOptimizeIgnoreChecks;
        private bool _perFileRmReference;

        private decimal _previewSlopeOffset;
        private decimal _currentSlope;
        private decimal _targetSlope;

        private int _selectedRatioIndex;
        private readonly Dictionary<int, decimal> _manualCurrentOverrides = new();
        private readonly List<AdvancedPivotRowDto> _pivotRows = new();
        private readonly List<DriftPlotPointVm> _plotPoints = new();
        private readonly List<RmRatioRowVm> _rmRatioRows = new();
        private readonly List<BetweenRmRowVm> _betweenRows = new();

        private int _currentPage = 1;
        private int _pageSize = 10;
        private int _totalCount;
        private int _totalPages => _totalCount == 0 ? 1 : (int)Math.Ceiling((double)_totalCount / _pageSize);

        private string CurrentRmText =>
            _rmRatioRows.Count == 0
                ? "Current RM: None"
                : $"Current RM: {_selectedRatioIndex + 1} / {_rmRatioRows.Count}";

        private bool HasAnalysis => _analysisResult != null;
        private bool HasPlotData => _plotPoints.Count > 0;
        private bool CanPrevRm => _rmRatioRows.Count > 0 && _selectedRatioIndex > 0;
        private bool CanNextRm => _rmRatioRows.Count > 0 && _selectedRatioIndex < _rmRatioRows.Count - 1;

        protected override async Task OnInitializedAsync()
        {
            _projectId = projectId ?? ProjectService.CurrentProjectId;
            if (!_projectId.HasValue) return;

            var projectResult = await ProjectService.GetProjectAsync(_projectId.Value, includeLatestState: true);
            if (projectResult.Succeeded && projectResult.Data != null)
                _projectName = projectResult.Data.ProjectName;

            await LoadElementsAsync();
            await LoadPivotRowsAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!_chartRenderPending) return;
            _chartRenderPending = false;
            await RenderDriftPlotAsync();
        }

        private async Task LoadElementsAsync()
        {
            if (!_projectId.HasValue) return;
            var result = await PivotService.GetElementsAsync(_projectId.Value);
            if (result.Succeeded && result.Data != null && result.Data.Any())
            {
                _allElements = result.Data;
                _focusElement ??= _allElements[0];
            }
        }

        private async Task LoadPivotRowsAsync()
        {
            if (!_projectId.HasValue) return;

            var req = new AdvancedPivotRequest(
                ProjectId: _projectId.Value,
                SearchText: null,
                SelectedSolutionLabels: null,
                SelectedElements: null,
                NumberFilters: null,
                UseOxide: false,
                UseInt: false,
                DecimalPlaces: 4,
                Page: 1,
                PageSize: 5000,
                Aggregation: "First",
                MergeRepeats: false);

            var result = await PivotService.GetAdvancedPivotTableAsync(req);
            if (!result.Succeeded || result.Data == null) return;

            _pivotRows.Clear();
            _pivotRows.AddRange(result.Data.Rows.OrderBy(r => r.OriginalIndex).ThenBy(r => r.SetIndex));
        }

        private DriftCorrectionRequest BuildRequest(bool includeTargetRm)
        {
            var selectedElements = !string.IsNullOrWhiteSpace(_focusElement)
                ? new List<string> { _focusElement! }
                : null;

            return new DriftCorrectionRequest
            {
                ProjectId = _projectId!.Value,
                Method = _applyStepwiseChanges ? DriftMethod.Stepwise : _method,
                UseSegmentation = _useSegmentation,
                SelectedElements = selectedElements,
                Keyword = string.IsNullOrWhiteSpace(_keyword) ? "RM" : _keyword.Trim(),
                TargetRmNum = includeTargetRm ? GetCurrentTargetRmNumber() : null,
                PreviewOnly = false
            };
        }

        private async Task AnalyzeDrift()
        {
            if (_projectId == null) return;
            if (!await _loadingLock.WaitAsync(0)) return;

            _isLoading = true;
            try
            {
                var result = await DriftService.AnalyzeDriftAsync(BuildRequest(includeTargetRm: false));
                if (!result.Succeeded || result.Data == null)
                {
                    _analysisResult = null;
                    _totalCount = 0;
                    _plotPoints.Clear();
                    _rmRatioRows.Clear();
                    _betweenRows.Clear();
                    RequestChartRender();
                    Snackbar.Add(result.Message ?? "Analysis failed", Severity.Error);
                    return;
                }

                _analysisResult = result.Data;
                _totalCount = _analysisResult.ElementDrifts.Count;
                _currentPage = 1;

                ClearPreviewAdjustments();
                await LoadPivotRowsAsync();
                RebuildDriftUiData();
                RequestChartRender();
                Snackbar.Add($"RM checked: {_analysisResult.SegmentsFound} segments.", Severity.Success);
            }
            finally
            {
                _isLoading = false;
                _loadingLock.Release();
                StateHasChanged();
            }
        }

        private async Task ApplyCorrection()
        {
            if (_projectId == null) return;
            if (!await _loadingLock.WaitAsync(0)) return;

            _isApplying = true;
            try
            {
                var result = await DriftService.ApplyDriftCorrectionAsync(BuildRequest(includeTargetRm: true));
                if (!result.Succeeded || result.Data == null)
                {
                    Snackbar.Add(result.Message ?? "Correction failed", Severity.Error);
                    return;
                }

                _analysisResult = result.Data;
                _totalCount = _analysisResult.ElementDrifts.Count;
                _currentPage = 1;

                await LoadPivotRowsAsync();
                RebuildDriftUiData();
                RequestChartRender();
                Snackbar.Add($"Correction applied: {_analysisResult.CorrectedSamples} rows.", Severity.Success);
            }
            finally
            {
                _isApplying = false;
                _loadingLock.Release();
                StateHasChanged();
            }
        }

        private async Task UndoLastCorrection()
        {
            if (_projectId == null) return;
            if (!await _loadingLock.WaitAsync(0)) return;

            _isLoading = true;
            try
            {
                var undo = await CorrectionService.UndoLastCorrectionAsync(_projectId.Value);
                if (!undo.Succeeded)
                {
                    Snackbar.Add(undo.Message ?? "Undo failed", Severity.Error);
                    return;
                }

                await LoadPivotRowsAsync();
                if (_analysisResult != null)
                    await ReAnalyzeAfterUndoAsync();
                else
                {
                    RebuildDriftUiData();
                    RequestChartRender();
                }

                Snackbar.Add("Undo last correction done.", Severity.Success);
            }
            finally
            {
                _isLoading = false;
                _loadingLock.Release();
                StateHasChanged();
            }
        }

        private async Task ReAnalyzeAfterUndoAsync()
        {
            var result = await DriftService.AnalyzeDriftAsync(BuildRequest(includeTargetRm: false));
            if (!result.Succeeded || result.Data == null)
            {
                _analysisResult = null;
                _totalCount = 0;
                _plotPoints.Clear();
                _rmRatioRows.Clear();
                _betweenRows.Clear();
                RequestChartRender();
                Snackbar.Add(result.Message ?? "Re-analysis after undo failed", Severity.Warning);
                return;
            }

            _analysisResult = result.Data;
            _totalCount = _analysisResult.ElementDrifts.Count;
            _currentPage = 1;
            ClearPreviewAdjustments();
            RebuildDriftUiData();
            RequestChartRender();
        }

        private async Task OnFocusElementChanged(string? value)
        {
            _focusElement = value;
            ClearPreviewAdjustments();
            RebuildDriftUiData();
            RequestChartRender();
            await Task.CompletedTask;
        }

        private async Task OnSolutionFilterChanged(string value)
        {
            _solutionFilter = value ?? string.Empty;
            RebuildDriftUiData();
            RequestChartRender();
            await Task.CompletedTask;
        }

        private Task OnKeywordChanged(string value)
        {
            _keyword = string.IsNullOrWhiteSpace(value) ? "RM" : value.Trim();
            return Task.CompletedTask;
        }

        private async Task PrevRm()
        {
            if (!CanPrevRm) return;
            _selectedRatioIndex--;
            RebuildDriftUiData();
            RequestChartRender();
            await Task.CompletedTask;
        }

        private async Task NextRm()
        {
            if (!CanNextRm) return;
            _selectedRatioIndex++;
            RebuildDriftUiData();
            RequestChartRender();
            await Task.CompletedTask;
        }

        private async Task AutoOptimizeToFlat()
        {
            if (_rmRatioRows.Count == 0)
            {
                Snackbar.Add("No RM ranges available.", Severity.Info);
                return;
            }

            if (_globalOptimizeIgnoreChecks)
            {
                foreach (var range in _rmRatioRows)
                    ApplyFlatToRange(range.StartIndex, range.EndIndex, range.CurrentValue);
            }
            else
            {
                var selected = _rmRatioRows[Math.Clamp(_selectedRatioIndex, 0, _rmRatioRows.Count - 1)];
                ApplyFlatToRange(selected.StartIndex, selected.EndIndex, selected.CurrentValue);
            }

            RebuildDriftUiData();
            RequestChartRender();
            Snackbar.Add("Auto optimize to flat applied (preview).", Severity.Success);
            await Task.CompletedTask;
        }

        private async Task AutoOptimizeSlopeToZero() => await ApplyTargetSlopeAsync(0m);
        private async Task OnTargetSlopeChanged(decimal value) => await ApplyTargetSlopeAsync(value);
        private async Task RotateUp() => await ApplyTargetSlopeAsync(_currentSlope + 0.001m);
        private async Task RotateDown() => await ApplyTargetSlopeAsync(_currentSlope - 0.001m);

        private async Task ResetToOriginal()
        {
            ClearPreviewAdjustments();
            RebuildDriftUiData();
            RequestChartRender();
            await Task.CompletedTask;
        }

        private async Task ApplyTargetSlopeAsync(decimal targetSlope)
        {
            var delta = targetSlope - _currentSlope;
            if (Math.Abs(delta) < 0.0000001m)
            {
                _targetSlope = _currentSlope;
                return;
            }

            _previewSlopeOffset += delta;
            RebuildDriftUiData();
            RequestChartRender();
            Snackbar.Add($"Slope updated to {targetSlope:F6} (preview).", Severity.Info);
            await Task.CompletedTask;
        }

        private void RebuildDriftUiData()
        {
            _plotPoints.Clear();
            _rmRatioRows.Clear();
            _betweenRows.Clear();
            if (string.IsNullOrWhiteSpace(_focusElement) || !_pivotRows.Any()) return;

            var correctedMap = BuildCorrectedLookupForFocusElement();
            foreach (var row in _pivotRows)
            {
                if (!TryGetElementValue(row.Values, _focusElement, out var originalMaybe) || !originalMaybe.HasValue) continue;
                var label = NormalizeSolutionLabel(row.SolutionLabel);
                if (string.IsNullOrWhiteSpace(label)) continue;

                var isRm = IsRmLabel(label);
                var (rmNum, rmType) = ParseRmInfo(label);
                var baseCurrent = correctedMap.TryGetValue(row.OriginalIndex, out var corrected)
                    ? corrected
                    : originalMaybe.Value;

                _plotPoints.Add(new DriftPlotPointVm
                {
                    OriginalIndex = row.OriginalIndex,
                    SolutionLabel = label,
                    OriginalValue = originalMaybe.Value,
                    BaseCurrentValue = baseCurrent,
                    CurrentValue = baseCurrent,
                    IsRm = isRm,
                    RmNum = rmNum,
                    RmType = rmType
                });
            }

            ApplyPreviewAdjustments();
            BuildRmRatioRows();
            BuildBetweenRows();
            UpdateSlopeIndicators();
        }

        private void ApplyPreviewAdjustments()
        {
            var anchorX = GetSlopeAnchorX();
            foreach (var point in _plotPoints)
            {
                var current = _manualCurrentOverrides.TryGetValue(point.OriginalIndex, out var manual)
                    ? manual
                    : point.BaseCurrentValue;
                point.CurrentValue = current + (_previewSlopeOffset * (point.OriginalIndex - anchorX));
            }
        }

        private void BuildRmRatioRows()
        {
            var rms = _plotPoints.Where(p => p.IsRm).OrderBy(p => p.OriginalIndex).ToList();
            for (var i = 0; i < rms.Count - 1; i++)
            {
                var start = rms[i];
                var end = rms[i + 1];
                var ratio = start.CurrentValue == 0m ? 1m : end.CurrentValue / start.CurrentValue;
                _rmRatioRows.Add(new RmRatioRowVm
                {
                    StartIndex = start.OriginalIndex,
                    EndIndex = end.OriginalIndex,
                    RmLabel = start.SolutionLabel,
                    NextRmLabel = end.SolutionLabel,
                    Type = start.RmType,
                    StartRmNum = start.RmNum,
                    OriginalValue = start.OriginalValue,
                    CurrentValue = start.CurrentValue,
                    Ratio = ratio
                });
            }

            if (_rmRatioRows.Count == 0) _selectedRatioIndex = 0;
            else _selectedRatioIndex = Math.Clamp(_selectedRatioIndex, 0, _rmRatioRows.Count - 1);
        }

        private void BuildBetweenRows()
        {
            if (_rmRatioRows.Count == 0) return;

            var range = _rmRatioRows[_selectedRatioIndex];
            var samples = _plotPoints
                .Where(p => !p.IsRm && p.OriginalIndex > range.StartIndex && p.OriginalIndex < range.EndIndex)
                .Where(MatchesFilter)
                .OrderBy(p => p.OriginalIndex)
                .ToList();

            _betweenRows.AddRange(samples.Select(p => new BetweenRmRowVm
            {
                OriginalIndex = p.OriginalIndex,
                SolutionLabel = p.SolutionLabel,
                OriginalValue = p.OriginalValue,
                CorrectedValue = p.CurrentValue
            }));
        }

        private Dictionary<int, decimal> BuildCorrectedLookupForFocusElement()
        {
            var map = new Dictionary<int, decimal>();
            if (_analysisResult == null || string.IsNullOrWhiteSpace(_focusElement)) return map;

            foreach (var row in _analysisResult.CorrectedData)
            {
                if (TryGetElementValue(row.CorrectedValues, _focusElement, out var corrected) && corrected.HasValue)
                    map[row.OriginalIndex] = corrected.Value;
            }

            return map;
        }

        private void ApplyFlatToRange(int startIndex, int endIndex, decimal targetValue)
        {
            var anchorX = GetSlopeAnchorX();
            foreach (var point in _plotPoints.Where(p => !p.IsRm && p.OriginalIndex > startIndex && p.OriginalIndex < endIndex))
                _manualCurrentOverrides[point.OriginalIndex] = targetValue - (_previewSlopeOffset * (point.OriginalIndex - anchorX));
        }

        private int GetSlopeAnchorX()
            => _plotPoints.Where(p => p.IsRm).OrderBy(p => p.OriginalIndex).Select(p => p.OriginalIndex).FirstOrDefault();

        private void UpdateSlopeIndicators()
        {
            var rmPoints = _plotPoints.Where(p => p.IsRm).OrderBy(p => p.OriginalIndex).ToList();
            _currentSlope = CalculateSlope(rmPoints);
            _targetSlope = _currentSlope;
        }

        private static decimal CalculateSlope(IReadOnlyList<DriftPlotPointVm> points)
        {
            if (points.Count < 2) return 0m;
            var x = points.Select(p => (double)p.OriginalIndex).ToArray();
            var y = points.Select(p => (double)p.CurrentValue).ToArray();
            var meanX = x.Average();
            var meanY = y.Average();

            var numerator = 0d;
            var denominator = 0d;
            for (var i = 0; i < x.Length; i++)
            {
                var dx = x[i] - meanX;
                numerator += dx * (y[i] - meanY);
                denominator += dx * dx;
            }

            return Math.Abs(denominator) < 1e-12 ? 0m : (decimal)(numerator / denominator);
        }

        private async Task RenderDriftPlotAsync()
        {
            if (!HasAnalysis || !HasPlotData)
            {
                await JSRuntime.InvokeVoidAsync("destroyChart", "driftChart");
                return;
            }

            var filtered = _plotPoints.Where(p => p.IsRm || MatchesFilter(p)).OrderBy(p => p.OriginalIndex).ToList();
            if (filtered.Count == 0)
            {
                await JSRuntime.InvokeVoidAsync("destroyChart", "driftChart");
                return;
            }

            var originalSamples = filtered.Where(p => !p.IsRm).Select(p => new { x = p.OriginalIndex, y = (double)p.OriginalValue }).ToList<object>();
            var correctedSamples = filtered.Where(p => !p.IsRm).Select(p => new { x = p.OriginalIndex, y = (double)p.CurrentValue }).ToList<object>();
            var rmCurrent = filtered.Where(p => p.IsRm).Select(p => new { x = p.OriginalIndex, y = (double)p.CurrentValue }).ToList<object>();

            var yValues = filtered.SelectMany(p => new[] { (double)p.OriginalValue, (double)p.CurrentValue }).ToList();
            var minY = yValues.Min();
            var maxY = yValues.Max();
            var span = maxY - minY;
            var margin = span > 0 ? span * 0.08 : Math.Max(1.0, Math.Abs(maxY) * 0.08);

            var minX = filtered.Min(p => p.OriginalIndex) - 2;
            var maxX = filtered.Max(p => p.OriginalIndex) + 2;

            var config = new
            {
                type = "scatter",
                data = new
                {
                    datasets = new object[]
                    {
                        new { label = "Original", data = originalSamples, showLine = false, pointStyle = "crossRot", pointRadius = 4, backgroundColor = "#F44336", borderColor = "#D32F2F" },
                        new { label = "Corrected", data = correctedSamples, showLine = false, pointStyle = "circle", pointRadius = 3, backgroundColor = "#2196F3", borderColor = "#1976D2" },
                        new { label = "RM Points", data = rmCurrent, showLine = false, pointStyle = "rect", pointRadius = 5, backgroundColor = "#2E7D32", borderColor = "#1B5E20" }
                    }
                },
                options = new
                {
                    responsive = true,
                    maintainAspectRatio = false,
                    animation = false,
                    scales = new
                    {
                        x = new { type = "linear", min = minX, max = maxX, title = new { display = true, text = "Sample Index" }, grid = new { color = "rgba(0,0,0,0.10)" } },
                        y = new { min = minY - margin, max = maxY + margin, title = new { display = true, text = $"{_focusElement} Value" }, grid = new { color = "rgba(0,0,0,0.10)" } }
                    },
                    plugins = new { legend = new { display = true, position = "top", labels = new { usePointStyle = true } } }
                }
            };

            await JSRuntime.InvokeVoidAsync("createChart", "driftChart", config);
            await JSRuntime.InvokeVoidAsync("resizeAllCharts");
        }

        private bool MatchesFilter(DriftPlotPointVm point)
            => string.IsNullOrWhiteSpace(_solutionFilter) || point.SolutionLabel.Contains(_solutionFilter, StringComparison.OrdinalIgnoreCase);

        private bool IsRmLabel(string? label)
        {
            if (string.IsNullOrWhiteSpace(label)) return false;
            var keyword = string.IsNullOrWhiteSpace(_keyword) ? "RM" : _keyword.Trim();
            return label.Trim().StartsWith(keyword, StringComparison.OrdinalIgnoreCase);
        }

        private (int RmNum, string RmType) ParseRmInfo(string label)
        {
            var keyword = string.IsNullOrWhiteSpace(_keyword) ? "RM" : _keyword.Trim();
            var text = label.Trim();
            var afterKeyword = text.StartsWith(keyword, StringComparison.OrdinalIgnoreCase) ? text[keyword.Length..].Trim() : text;
            var numberMatch = Regex.Match(afterKeyword, @"\d+");
            var rmNum = numberMatch.Success && int.TryParse(numberMatch.Value, out var n) ? n : 0;
            var rmType = Regex.IsMatch(text, "cone", RegexOptions.IgnoreCase) ? "Cone"
                : Regex.IsMatch(text, "check|chek", RegexOptions.IgnoreCase) ? "Check"
                : "Base";
            return (rmNum, rmType);
        }

        private int? GetCurrentTargetRmNumber()
            => _rmRatioRows.Count == 0 ? null : _rmRatioRows[_selectedRatioIndex].StartRmNum > 0 ? _rmRatioRows[_selectedRatioIndex].StartRmNum : null;

        private void ClearPreviewAdjustments()
        {
            _previewSlopeOffset = 0m;
            _currentSlope = 0m;
            _targetSlope = 0m;
            _selectedRatioIndex = 0;
            _manualCurrentOverrides.Clear();
        }

        private static string NormalizeSolutionLabel(string? raw)
            => string.IsNullOrWhiteSpace(raw) ? string.Empty : MultiWhitespaceRegex.Replace(raw.Trim(), " ");

        private static string NormalizeElement(string raw)
            => raw.Split(new[] { ' ', '_', '.' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim().ToLowerInvariant();

        private static bool TryGetElementValue(IReadOnlyDictionary<string, decimal?> values, string? element, out decimal? value)
        {
            value = null;
            if (values == null || string.IsNullOrWhiteSpace(element)) return false;
            if (values.TryGetValue(element, out value)) return true;
            var normalized = NormalizeElement(element);
            var match = values.FirstOrDefault(k => NormalizeElement(k.Key) == normalized || NormalizeElement(k.Key).StartsWith(normalized));
            if (match.Key == null) return false;
            value = match.Value;
            return true;
        }

        private void RequestChartRender() => _chartRenderPending = true;

        private IEnumerable<ElementDriftInfo> GetPagedElementDrifts()
        {
            if (_analysisResult == null || !_analysisResult.ElementDrifts.Any())
                return Enumerable.Empty<ElementDriftInfo>();

            return _analysisResult.ElementDrifts.Values
                .OrderBy(x => x.Element, StringComparer.OrdinalIgnoreCase)
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize);
        }

        private Color GetDriftColor(decimal drift)
        {
            var abs = Math.Abs(drift);
            if (abs < 2) return Color.Success;
            if (abs < 5) return Color.Warning;
            return Color.Error;
        }

        private void OnPageChanged(int page)
        {
            _currentPage = page;
            StateHasChanged();
        }

        private void OnPageSizeChanged(int size)
        {
            _pageSize = size;
            _currentPage = 1;
            StateHasChanged();
        }

        private Task OnBeforeNavigation(LocationChangingContext context)
        {
            if (_isLoading || _isApplying)
                context.PreventNavigation();
            return Task.CompletedTask;
        }

        public void Dispose() => _loadingLock.Dispose();

        private sealed class DriftPlotPointVm
        {
            public int OriginalIndex { get; set; }
            public string SolutionLabel { get; set; } = "";
            public decimal OriginalValue { get; set; }
            public decimal BaseCurrentValue { get; set; }
            public decimal CurrentValue { get; set; }
            public bool IsRm { get; set; }
            public int RmNum { get; set; }
            public string RmType { get; set; } = "";
        }

        private sealed class RmRatioRowVm
        {
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
            public string RmLabel { get; set; } = "";
            public string NextRmLabel { get; set; } = "";
            public string Type { get; set; } = "";
            public int StartRmNum { get; set; }
            public decimal OriginalValue { get; set; }
            public decimal CurrentValue { get; set; }
            public decimal Ratio { get; set; }
        }

        private sealed class BetweenRmRowVm
        {
            public int OriginalIndex { get; set; }
            public string SolutionLabel { get; set; } = "";
            public decimal OriginalValue { get; set; }
            public decimal CorrectedValue { get; set; }
        }
    }
}
