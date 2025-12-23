using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using WebUI.Services;

namespace WebUI.Pages.Process
{
    public partial class CrmCalibration
    {
        [SupplyParameterFromQuery]
        public Guid? projectId { get; set; }

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
            await GetCurrentStats();
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
            }
            else
            {
                Snackbar.Add(result.Message ?? "Failed to get stats", Severity.Error);
            }

            _isLoading = false;
        }

        private async Task RunOptimization()
        {
            if (_projectId == null) return;

            _isLoading = true;
            StateHasChanged();

            var request = new BlankScaleOptimizationRequest
            {
                ProjectId = _projectId.Value,
                MinDiffPercent = _minDiff,
                MaxDiffPercent = _maxDiff,
                MaxIterations = _maxIterations,
                PopulationSize = _populationSize,
                UseMultiModel = _useMultiModel,
                Elements = _selectedElements.Any() ? _selectedElements.ToList() : null
            };

            var result = await OptimizationService.OptimizeAsync(request);

            if (result.Succeeded && result.Data != null)
            {
                _result = result.Data;
                UpdateOptimizedRows();
                Snackbar.Add($"Optimization complete! Improvement: {_result.ImprovementPercent:F1}%", Severity.Success);
            }
            else
            {
                Snackbar.Add(result.Message ?? "Optimization failed", Severity.Error);
            }

            _isLoading = false;
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
                return;

            _isLoading = true;
            StateHasChanged();

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

            _isLoading = false;
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

        private void SetFocusElement(string? element)
        {
            if (string.IsNullOrWhiteSpace(element))
                return;

            _focusElement = element;
            UpdateOptimizedRows();
            UpdateManualRows();
        }

        private void PrevElement()
        {
            if (_allElements.Count == 0 || string.IsNullOrWhiteSpace(_focusElement))
                return;

            var idx = _allElements.IndexOf(_focusElement);
            if (idx > 0)
                SetFocusElement(_allElements[idx - 1]);
        }

        private void NextElement()
        {
            if (_allElements.Count == 0 || string.IsNullOrWhiteSpace(_focusElement))
                return;

            var idx = _allElements.IndexOf(_focusElement);
            if (idx < _allElements.Count - 1)
                SetFocusElement(_allElements[idx + 1]);
        }

        private void UpdateOptimizedRows()
        {
            _optimizedRows = BuildRows(_result?.OptimizedData, _focusElement);
        }

        private void UpdateManualRows()
        {
            _manualRows = BuildRows(_manualResult?.OptimizedData, _focusElement);
        }

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
    }
}
