using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using WebUI.Services;

namespace WebUI.Pages.Process
{
    public partial class CalibrationPro
    {
        // ==========================================
        // Models
        // ==========================================
        public class RMElement
        {
            public string Label { get; set; } = string.Empty;
            public double Orig { get; set; }
            public double Curr { get; set; }
            public int RmGroup { get; set; }
            public int OriginalIndex { get; set; }
        }

        public class SamplePoint
        {
            public string Label { get; set; } = string.Empty;
            public double Orig { get; set; }
            public int OriginalIndex { get; set; }
        }

        // ==========================================
        // Properties & State
        // ==========================================
        private List<RMElement> Elements { get; set; } = new();
        private List<SamplePoint> _allSamplePoints = new();
        private Dictionary<string, bool> IncludedCrms { get; set; } = new();
        private List<int> _rmGroupNumbers { get; set; } = new();
        private List<string> _elements = new();
        private string? _selectedElement;
        private List<string> _files = new();
        private string? _selectedFile = "All Files";

        private int _currentRmIndex = 0;
        private double _previewBlank = 0.0;
        private double _previewScale = 1.0;
        private string _filterSolution = string.Empty;
        private bool _isLoading;

        private int? CurrentRmGroupNumber =>
            (_rmGroupNumbers != null && _currentRmIndex >= 0 && _currentRmIndex < _rmGroupNumbers.Count)
                ? _rmGroupNumbers[_currentRmIndex]
                : null;

        private List<RMElement> VisibleRmPoints =>
            CurrentRmGroupNumber is int g
                ? Elements.Where(e => e.RmGroup == g).OrderBy(e => e.OriginalIndex).ToList()
                : new List<RMElement>();

        private string CurrentRmLabel => CurrentRmGroupNumber is int n ? n.ToString() : "-";

        // ==========================================
        // Table UI Helper Methods (رفع ارورهای شما)
        // ==========================================
        private static int ParseRmGroupFromLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return 1;
            var m = Regex.Match(label.Trim(), @"^RM\s*(\d+)", RegexOptions.IgnoreCase);
            return m.Success && int.TryParse(m.Groups[1].Value, out var num) ? num : 1;
        }

        private static string FormatValue(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) ? "—" : value.ToString("0.00");

        private static string GetRatio(RMElement row) =>
            Math.Abs(row.Orig) < 1e-12 ? "N/A" : (row.Curr / row.Orig).ToString("0.00");

        private string GetNextRmLabel(RMElement context)
        {
            var visible = VisibleRmPoints;
            var idx = visible.IndexOf(context);
            if (idx < 0 || idx >= visible.Count - 1) return "N/A";
            return visible[idx + 1].Label;
        }

        // ==========================================
        // Lifecycle & Data Loading
        // ==========================================
        protected override async Task OnInitializedAsync()
        {
            _isLoading = true;
            try
            {
                if (ProjectService.CurrentProjectId is Guid projectId)
                {
                    var request = new AdvancedPivotRequest(ProjectId: projectId, Page: 1, PageSize: 5000);
                    var pivotResult = await PivotService.GetAdvancedPivotTableAsync(request);
                    if (pivotResult.Succeeded && pivotResult.Data is not null)
                    {
                        _elements = pivotResult.Data.Metadata?.AllElements ?? new List<string>();
                        _selectedElement ??= _elements.FirstOrDefault();
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

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await UpdateChartsAsync();
            }
        }

        private async Task LoadRmTableForCurrentElementAsync(Guid projectId)
        {
            if (string.IsNullOrWhiteSpace(_selectedElement)) return;

            var request = new AdvancedPivotRequest(ProjectId: projectId, SelectedElements: new List<string> { _selectedElement }, Page: 1, PageSize: 5000);
            var pivotResult = await PivotService.GetAdvancedPivotTableAsync(request);

            if (!pivotResult.Succeeded || pivotResult.Data is null) return;


            var allRows = pivotResult.Data.Rows.ToList();

            var crmLabels = allRows
                .Where(r => !string.IsNullOrEmpty(r.SolutionLabel) &&
                            r.SolutionLabel.Trim().StartsWith("CRM", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.SolutionLabel)
                .Distinct()
                .ToList();

            foreach (var label in crmLabels)
            {
                if (!IncludedCrms.ContainsKey(label))
                    IncludedCrms[label] = true;
            }

            //var rmRows = allRows
            //    .Where(r => !string.IsNullOrEmpty(r.SolutionLabel) &&
            //        r.SolutionLabel.Trim().StartsWith("RM", StringComparison.OrdinalIgnoreCase) &&
            //        !r.SolutionLabel.Trim().StartsWith("CRM", StringComparison.OrdinalIgnoreCase))
            //    .OrderBy(r => r.OriginalIndex).ToList();

            // این خط جایگزین منطق قبلی در متد LoadRmTableForCurrentElementAsync شود
            var rmRows = allRows
                .Where(r => !string.IsNullOrEmpty(r.SolutionLabel) &&
                            r.SolutionLabel.Trim().StartsWith(_rmKeyword, StringComparison.OrdinalIgnoreCase) && // استفاده از کیورد داینامیک
                            !r.SolutionLabel.Trim().StartsWith("CRM", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.OriginalIndex).ToList();


            _allSamplePoints = allRows
                .Where(r => !string.IsNullOrEmpty(r.SolutionLabel) &&
                    !r.SolutionLabel.Trim().StartsWith("RM", StringComparison.OrdinalIgnoreCase) &&
                    !r.SolutionLabel.Trim().StartsWith("CRM", StringComparison.OrdinalIgnoreCase) &&
                    !r.SolutionLabel.Trim().StartsWith("Blank", StringComparison.OrdinalIgnoreCase))
                .Select(r => {
                    double value = 0;
                    if (r.Values != null && r.Values.TryGetValue(_selectedElement, out var v) && v.HasValue)
                        value = Convert.ToDouble(v.Value);
                    return new SamplePoint { Label = r.SolutionLabel, Orig = value, OriginalIndex = r.OriginalIndex };
                }).Where(s => s.Orig > 0).ToList();

            if (rmRows.Count == 0)
            {
                Elements = new List<RMElement>();
                _rmGroupNumbers = new List<int>();
                _currentRmIndex = 0;
                await UpdateChartsAsync();
                return;
            }

            Elements = rmRows.Select(r =>
            {
                double value = 0;
                if (r.Values != null && r.Values.TryGetValue(_selectedElement, out var v) && v.HasValue)
                    value = Convert.ToDouble(v.Value);

                return new RMElement
                {
                    Label = r.SolutionLabel,
                    Orig = value,
                    Curr = value,
                    RmGroup = ParseRmGroupFromLabel(r.SolutionLabel),
                    OriginalIndex = r.OriginalIndex
                };
            }).ToList();

            _rmGroupNumbers = Elements.Select(e => e.RmGroup).Distinct().OrderBy(x => x).ToList();
            _currentRmIndex = _rmGroupNumbers.Count > 0 ? 0 : -1;

            await UpdateChartsAsync();
        }

        // ==========================================
        // Event Handlers
        // ==========================================
        private async Task OnFileChanged(string? value) { _selectedFile = value; await UpdateChartsAsync(); }
        private async Task OnElementChanged(string? value)
        {
            _selectedElement = value;
            if (ProjectService.CurrentProjectId is Guid projectId && !string.IsNullOrWhiteSpace(_selectedElement))
            {
                await LoadRmTableForCurrentElementAsync(projectId);
                StateHasChanged();
            }
        }
        private async Task OnFilterSolutionChanged(string value) { _filterSolution = value ?? string.Empty; await UpdateChartsAsync(); }
        private async Task PrevRm() { if (_rmGroupNumbers != null && _currentRmIndex > 0) { _currentRmIndex--; await UpdateChartsAsync(); } }
        private async Task NextRm() { if (_rmGroupNumbers != null && _currentRmIndex < _rmGroupNumbers.Count - 1) { _currentRmIndex++; await UpdateChartsAsync(); } }
        private async Task ResetAll() { foreach (var el in Elements) el.Curr = el.Orig; _previewBlank = 0.0; _previewScale = 1.0; _filterSolution = string.Empty; await UpdateChartsAsync(); }
        private async Task RunCalibrationAsync() { await Task.Yield(); Snackbar.Add("Calibration run is not implemented on the server yet.", Severity.Warning); }
        private async Task UpdatePreviewBlank(string value) { if (!double.TryParse(value, out _previewBlank)) _previewBlank = 0.0; await UpdateChartsAsync(); }
        private async Task UpdatePreviewScale(double value) { _previewScale = value; await UpdateChartsAsync(); }
        private async Task ResetBlankAndScale() { _previewBlank = 0.0; _previewScale = 1.0; await UpdateChartsAsync(); }

        // ==========================================
        // Chart Generation Logic (Exact Python Logic)
        // ==========================================
        private async Task UpdateChartsAsync()
        {
            await UpdateDriftChartAsync();
            await UpdateVerificationChartAsync();
        }

        private async Task UpdateDriftChartAsync()
        {
            var rmPointsToDraw = VisibleRmPoints.Any() ? VisibleRmPoints : Elements;
            if (!rmPointsToDraw.Any())
            {
                await JS.InvokeVoidAsync("destroyChart", "fullDriftChart");
                return;
            }

            var rmData = rmPointsToDraw.Select(e => new { x = (double?)e.OriginalIndex, y = (double?)e.Curr, label = e.Label }).ToList();

            var minIndex = rmPointsToDraw.Min(r => r.OriginalIndex);
            var maxIndex = rmPointsToDraw.Max(r => r.OriginalIndex);
            var relevantSamples = _allSamplePoints.Where(s => s.OriginalIndex >= minIndex && s.OriginalIndex <= maxIndex).ToList();

            var originalData = relevantSamples.Select(e => new { x = (double?)e.OriginalIndex, y = (double?)e.Orig, label = e.Label }).ToList();

            var correctedData = new List<object>();
            for (int i = 0; i < rmPointsToDraw.Count - 1; i++)
            {
                var prevRm = rmPointsToDraw[i];
                var nextRm = rmPointsToDraw[i + 1];

                var segmentSamples = relevantSamples.Where(s => s.OriginalIndex > prevRm.OriginalIndex && s.OriginalIndex < nextRm.OriginalIndex).ToList();

                double prevRatio = prevRm.Orig != 0 ? prevRm.Curr / prevRm.Orig : 1.0;
                double currentRatio = nextRm.Orig != 0 ? nextRm.Curr / nextRm.Orig : 1.0;

                int n = segmentSamples.Count;
                if (n > 0)
                {
                    double z = (currentRatio - prevRatio) / n;
                    for (int j = 0; j < n; j++)
                    {
                        double ratio = (z * (j + 1)) + prevRatio;
                        double adjusted = segmentSamples[j].Orig - _previewBlank;
                        double scaled = adjusted * _previewScale;
                        double correctedY = scaled * ratio;

                        correctedData.Add(new { x = (double?)segmentSamples[j].OriginalIndex, y = (double?)correctedY, label = segmentSamples[j].Label });
                    }
                }
            }

            var chartConfig = new
            {
                type = "scatter",
                data = new
                {
                    datasets = new object[]
                    {
                        new { label = "RM Points", data = rmData, backgroundColor = "#2E7D32", pointStyle = "circle", pointRadius = 6 },
                        new { label = "Original Values", data = originalData, backgroundColor = "#F44336", borderColor = "#D32F2F", pointStyle = "crossRot", pointRadius = 6, borderWidth = 2 },
                        new { label = "Corrected Values", data = correctedData, backgroundColor = "#2196F3", borderColor = "#1976D2", pointStyle = "circle", pointRadius = 4 }
                    }
                },
                options = new
                {
                    responsive = true,
                    maintainAspectRatio = false,
                    plugins = new
                    {
                        title = new { display = true, text = $"Title of your chart" },
                        // ------- این بلوک زوم را اضافه کنید -------
                        zoom = new
                        {
                            zoom = new
                            {
                                wheel = new { enabled = true }, 
                                pinch = new { enabled = true }, 
                                mode = "xy" 
                            },
                            pan = new
                            {
                                enabled = true, 
                                mode = "xy" 
                            }
                        }
                        // ---------------------------------------------
                    },
                    scales = new { /* اسکیل های قبلی شما */ }
                }
            };

            try { await JS.InvokeVoidAsync("destroyChart", "fullDriftChart"); await JS.InvokeVoidAsync("createChart", "fullDriftChart", chartConfig); }
            catch (Exception ex) { _logger.LogError(ex, "Error rendering Drift chart"); }
        }

        private async Task UpdateVerificationChartAsync()
        {
            if (ProjectService.CurrentProjectId is not Guid projectId || string.IsNullOrWhiteSpace(_selectedElement)) return;

            // Load fresh pivot data to get all solution labels per CRM (like Python)
            var request = new AdvancedPivotRequest(ProjectId: projectId, SelectedElements: new List<string> { _selectedElement }, Page: 1, PageSize: 5000);
            var pivotResult = await PivotService.GetAdvancedPivotTableAsync(request);

            if (!pivotResult.Succeeded || pivotResult.Data?.Rows == null || !pivotResult.Data.Rows.Any())
            {
                await JS.InvokeVoidAsync("destroyChart", "verificationChart");
                return;
            }

            var pivotRows = pivotResult.Data.Rows.ToList();
            _logger.LogInformation($"[VerificationChart] Loaded {pivotRows.Count} pivot rows, element: {_selectedElement}");

            // Extract all CRM solution labels and their values
            var crmRows = pivotRows.Where(r => !string.IsNullOrEmpty(r.SolutionLabel) && r.SolutionLabel.Contains("CRM", StringComparison.OrdinalIgnoreCase)).ToList();
            _logger.LogInformation($"[VerificationChart] Found {crmRows.Count} CRM rows");

            if (!crmRows.Any())
            {
                await JS.InvokeVoidAsync("destroyChart", "verificationChart");
                return;
            }

            // Helper to extract numeric ID
            string ExtractCrmId(string label) => System.Text.RegularExpressions.Regex.Match(label ?? "", @"(?i)(?:\bCRM\b|\bOREAS\b)?[\s-]*(\d+[a-zA-Z]?)").Groups[1].Value is string id && !string.IsNullOrEmpty(id) ? id : label ?? "";

            // Group by CRM ID preserving insertion order
            var crmIdToLabels = new Dictionary<string, List<string>>();
            var crmIdOrder = new List<string>();

            foreach (var row in crmRows)
            {
                string id = ExtractCrmId(row.SolutionLabel);
                if (!crmIdToLabels.ContainsKey(id))
                {
                    crmIdToLabels[id] = new List<string>();
                    crmIdOrder.Add(id);
                }
                if (!crmIdToLabels[id].Contains(row.SolutionLabel))
                    crmIdToLabels[id].Add(row.SolutionLabel);
            }

            _logger.LogInformation($"[VerificationChart] CRM IDs: {string.Join(", ", crmIdOrder.Select(id => $"{id}({crmIdToLabels[id].Count})"))}");

            var xLabels = crmIdOrder.Select(id => $"V {id}").ToArray();
            var certPoints = new List<object>();
            var samplePoints = new List<object>();
            var rangeData = new List<object>();
            var allY = new List<double>();

            // Get certificate values from API (once)
            var diffResult = await CrmService.CalculateDiffAsync(projectId, null, -10m, 10m);
            var crmDiffMap = new Dictionary<string, (double CertVal, double LowerBound, double UpperBound)>();
            if (diffResult.Succeeded && diffResult.Data != null)
            {
                foreach (var crmDiff in diffResult.Data)
                {
                    string id = ExtractCrmId(crmDiff.SolutionLabel);
                    var elemDiff = crmDiff.Differences.FirstOrDefault(d => d.Element.Equals(_selectedElement, StringComparison.OrdinalIgnoreCase));
                    if (elemDiff != null)
                    {
                        double certVal = Convert.ToDouble(elemDiff.CrmValue);
                        double rangeVal = Math.Abs(certVal) * 0.1;
                        crmDiffMap[id] = (certVal, certVal - rangeVal, certVal + rangeVal);
                    }
                }
            }

            // For each CRM ID, for each solution label in that group, extract sample value and plot with certificate
            for (int xi = 0; xi < crmIdOrder.Count; xi++)
            {
                string crmId = crmIdOrder[xi];
                var labelList = crmIdToLabels[crmId];

                // Get certificate values for this CRM once
                double? certVal = null;
                double? low = null;
                double? up = null;
                if (crmDiffMap.TryGetValue(crmId, out var cert))
                {
                    certVal = cert.CertVal;
                    low = cert.LowerBound;
                    up = cert.UpperBound;
                }

                // For each solution label under this CRM ID, plot its sample value
                foreach (var solLabel in labelList)
                {
                    var row = crmRows.FirstOrDefault(r => r.SolutionLabel == solLabel);
                    if (row == null) continue;

                    // Get sample value from pivot
                    double? sampleVal = null;
                    if (row.Values != null && row.Values.TryGetValue(_selectedElement, out var val) && val.HasValue)
                    {
                        try { sampleVal = Convert.ToDouble(val.Value); } catch { }
                    }

                    // Add certificate point (same for all solution labels under this CRM ID)
                    if (certVal.HasValue)
                    {
                        certPoints.Add(new { x = (double?)xi, y = (double?)certVal.Value });
                        allY.Add(certVal.Value);
                    }

                    // Add sample point (one per solution label)
                    if (sampleVal.HasValue)
                    {
                        samplePoints.Add(new { x = (double?)xi, y = (double?)sampleVal.Value });
                        allY.Add(sampleVal.Value);
                    }

                    // Track bounds for y-range
                    if (low.HasValue) allY.Add(low.Value);
                    if (up.HasValue) allY.Add(up.Value);
                }

                // Add range lines once per CRM ID
                if (low.HasValue && up.HasValue)
                {
                    rangeData.Add(new { x = (double?)(xi - 0.2), y = (double?)low.Value });
                    rangeData.Add(new { x = (double?)(xi + 0.2), y = (double?)low.Value });
                    rangeData.Add(new { x = (double?)null, y = (double?)null });
                    rangeData.Add(new { x = (double?)(xi - 0.2), y = (double?)up.Value });
                    rangeData.Add(new { x = (double?)(xi + 0.2), y = (double?)up.Value });
                    rangeData.Add(new { x = (double?)null, y = (double?)null });
                }
            }

            double? yMin = allY.Any() ? allY.Min() : (double?)null;
            double? yMax = allY.Any() ? allY.Max() : (double?)null;

            _logger.LogInformation($"[VerificationChart] Data: cert={certPoints.Count}, sample={samplePoints.Count}, range={rangeData.Count}, yMin={yMin}, yMax={yMax}");

            var chartConfig = new
            {
                type = "scatter",
                data = new { datasets = new object[] { new { label = "Certificate Value", data = certPoints, backgroundColor = "green", pointStyle = "circle", pointRadius = 6 }, new { label = "Sample Value", data = samplePoints, backgroundColor = "blue", pointStyle = "triangle", rotation = 180, pointRadius = 7 }, new { label = "Acceptable Range", data = rangeData, borderColor = "red", borderWidth = 2, showLine = true, pointRadius = 0, fill = false, spanGaps = false } } },
                options = new { responsive = true, maintainAspectRatio = false, plugins = new { title = new { display = true, text = $"Verification Values for {_selectedElement}" }, zoom = new { zoom = new { wheel = new { enabled = true }, pinch = new { enabled = true }, mode = "xy" }, pan = new { enabled = true, mode = "xy" } }, legend = new { display = true } }, xLabels = xLabels, scales = (yMin.HasValue && yMax.HasValue) ? new { x = new { min = -0.5, max = (double?)(crmIdOrder.Count - 0.5) }, y = new { min = (double?)(yMin.Value - (yMax.Value - yMin.Value) * 0.1), max = (double?)(yMax.Value + (yMax.Value - yMin.Value) * 0.1) } } : null }
            };

            try { await JS.InvokeVoidAsync("destroyChart", "verificationChart"); await JS.InvokeVoidAsync("createChart", "verificationChart", chartConfig); }
            catch (Exception ex) { _logger.LogError(ex, "Error rendering Verification chart"); }
        }


        private decimal _rangeLow = 2.0m;
        private decimal _rangeMid = 20.0m;
        private decimal _rangeHigh1 = 10.0m;
        private decimal _rangeHigh2 = 8.0m;
        private decimal _rangeHigh3 = 5.0m;
        private decimal _rangeHigh4 = 3.0m;

        private async Task OpenRangesDialogAsync()
        {
            var parameters = new DialogParameters
            {
                { "RangeLow", _rangeLow },
                { "RangeMid", _rangeMid },
                { "RangeHigh1", _rangeHigh1 },
                { "RangeHigh2", _rangeHigh2 },
                { "RangeHigh3", _rangeHigh3 },
                { "RangeHigh4", _rangeHigh4 }
            };

            var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };

            var dialog = await DialogService.ShowAsync<RangesDialog>("Acceptable Ranges", parameters, options);
            var result = await dialog.Result;

            if (!result.Canceled && result.Data is decimal[] newRanges)
            {
                
                _rangeLow = newRanges[0];
                _rangeMid = newRanges[1];
                _rangeHigh1 = newRanges[2];
                _rangeHigh2 = newRanges[3];
                _rangeHigh3 = newRanges[4];
                _rangeHigh4 = newRanges[5];

                Snackbar.Add("Acceptable ranges updated.", Severity.Success);
             
                await UpdateChartsAsync();
            }
        }
        private async Task OpenExcludeDialog()
        {
            var allItems = new List<WebUI.Pages.ExcludeItemModel>();

            if (Elements != null)
            {
                allItems.AddRange(Elements.Select(e => new WebUI.Pages.ExcludeItemModel
                {
                    Id = $"RM_{e.OriginalIndex}", 
                    Name = e.Label ?? "Unknown",
                    Value = Math.Round(e.Orig, 4),
                    IsExcluded = false 
                }));
            }

            if (_allSamplePoints != null)
            {
                allItems.AddRange(_allSamplePoints.Select(s => new WebUI.Pages.ExcludeItemModel
                {
                    Id = $"Sample_{s.OriginalIndex}",
                    Name = s.Label ?? "Unknown",
                    Value = Math.Round(s.Orig, 4),
                    IsExcluded = false
                }));
            }

            var excludeItems = allItems.OrderBy(x => x.Name).ToList();

            var parameters = new DialogParameters<WebUI.Pages.ExcludeDialog>
    {
        { x => x.Items, excludeItems }
    };

            var options = new DialogOptions
            {
                CloseOnEscapeKey = true,
                MaxWidth = MaxWidth.Small,
                FullWidth = true,
                NoHeader = true,
                CloseButton = false
            };

            var dialog = await DialogService.ShowAsync<WebUI.Pages.ExcludeDialog>("", parameters, options);
            var result = await dialog.Result;

            if (result != null && !result.Canceled && result.Data is WebUI.Pages.ExcludeResult excludeResult)
            {
                if (excludeResult.ExcludedItems != null && excludeResult.ExcludedItems.Any())
                {
                    foreach (var item in excludeResult.ExcludedItems)
                    {
                        if (item.Id.StartsWith("RM_"))
                        {
                            var originalId = item.Id.Replace("RM_", "");
                            var elementToRemove = Elements?.FirstOrDefault(e => e.OriginalIndex.ToString() == originalId);
                            if (elementToRemove != null) Elements?.Remove(elementToRemove);
                        }
                        else if (item.Id.StartsWith("Sample_"))
                        {
                            var originalId = item.Id.Replace("Sample_", "");
                            var sampleToRemove = _allSamplePoints?.FirstOrDefault(s => s.OriginalIndex.ToString() == originalId);
                            if (sampleToRemove != null) _allSamplePoints?.Remove(sampleToRemove);
                        }
                    }


                    UpdateVisibleRmPoints();
                    StateHasChanged();

                    Snackbar.Add($"{excludeResult.ExcludedItems.Count} آیتم از محاسبات حذف شد.", Severity.Success);
                }
            }
        }

        private async Task OpenSelectCrmsDialog()
        {
            var crmItems = new List<WebUI.Pages.CrmSelectionModel>();

            foreach (var crm in IncludedCrms.OrderBy(x => x.Key))
            {
                var displayLabel = crm.Key.Contains('_')
                    ? crm.Key.Substring(0, crm.Key.LastIndexOf('_'))
                    : crm.Key;

                crmItems.Add(new WebUI.Pages.CrmSelectionModel
                {
                    Id = crm.Key,
                    Label = displayLabel,
                    IsIncluded = crm.Value
                });
            }

            var onSelectionChanged = EventCallback.Factory.Create(this, async () =>
            {
             
                foreach (var crm in crmItems)
                {
                    if (IncludedCrms.ContainsKey(crm.Id))
                    {
                        IncludedCrms[crm.Id] = crm.IsIncluded;
                    }
                }
                await UpdateChartsAsync();
                StateHasChanged();
            });

            var parameters = new DialogParameters<WebUI.Pages.SelectCrmsDialog>
    {
        { x => x.Items, crmItems },
        { x => x.OnSelectionChanged, onSelectionChanged } 
    };

            var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true, NoHeader = true, CloseButton = false };

            var dialog = await DialogService.ShowAsync<WebUI.Pages.SelectCrmsDialog>("", parameters, options);

            await dialog.Result;
        }


        private double GetToleranceValue(double value)
        {
            double absVal = Math.Abs(value);
            if (absVal < 10) return (double)_rangeLow;
            if (absVal < 100) return value * ((double)_rangeMid / 100.0);
            if (absVal < 1000) return value * ((double)_rangeHigh1 / 100.0);
            if (absVal < 10000) return value * ((double)_rangeHigh2 / 100.0);
            if (absVal < 100000) return value * ((double)_rangeHigh3 / 100.0);
            return value * ((double)_rangeHigh4 / 100.0);
        }

        private RMElement? _selectedRmRow;
        private List<SamplePoint> _selectedSegmentSamples = new();

        
        private void OnRmSelected(RMElement? selected)
        {
            if (selected == null)
            {
                _selectedSegmentSamples.Clear();
                return;
            }

            _selectedRmRow = selected;

            var allOrdered = Elements
                .OrderBy(e => e.OriginalIndex)
                .ToList();

            var index = allOrdered.IndexOf(selected);

            if (index < 0 || index >= allOrdered.Count - 1)
            {
                _selectedSegmentSamples.Clear();
                return;
            }

            var currentRm = allOrdered[index];
            var nextRm = allOrdered[index + 1];

            _selectedSegmentSamples = _allSamplePoints
                .Where(s => s.OriginalIndex > currentRm.OriginalIndex &&
                            s.OriginalIndex < nextRm.OriginalIndex)
                .OrderBy(s => s.OriginalIndex)
                .ToList();
        }
        private double GetCorrectedValue(SamplePoint sample)
        {
            if (_selectedRmRow == null)
                return sample.Orig;

            var visible = VisibleRmPoints;
            var index = visible.IndexOf(_selectedRmRow);

            if (index < 0 || index >= visible.Count - 1)
                return sample.Orig;

            var prevRm = visible[index];
            var nextRm = visible[index + 1];

            double prevRatio = prevRm.Orig != 0 ? prevRm.Curr / prevRm.Orig : 1.0;
            double nextRatio = nextRm.Orig != 0 ? nextRm.Curr / nextRm.Orig : 1.0;

            var segmentSamples = _selectedSegmentSamples;
            int n = segmentSamples.Count;
            if (n == 0) return sample.Orig;

            int sampleIndex = segmentSamples.IndexOf(sample);
            if (sampleIndex < 0) return sample.Orig;

            double z = (nextRatio - prevRatio) / n;
            double ratio = (z * (sampleIndex + 1)) + prevRatio;

            double adjusted = sample.Orig - _previewBlank;
            double scaled = adjusted * _previewScale;

            return scaled * ratio;
        }

        private List<RMElement> _visibleRmPoints = new();

        private void UpdateVisibleRmPoints()
        {
            if (CurrentRmGroupNumber is int g)
                _visibleRmPoints = Elements
                    .Where(e => e.RmGroup == g)
                    .OrderBy(e => e.OriginalIndex)
                    .ToList();
            else
                _visibleRmPoints = new();
            UpdateCurrentSlope();
        }
        // ==========================================
        // RM Drift Correction State & Methods
        // ==========================================
        private string _rmKeyword = "RM";
        private bool _perFileRmReference = true;
        private bool _globalOptimize = false;
        private bool _stepwiseChanges = false;
        private double _manualSlope = 0.0;
        private double _currentSlope = 0.0;

        // استک برای پیاده‌سازی قابلیت Undo مشابه پایتون
        private Stack<string> _undoStack = new();

        // متد ذخیره وضعیت فعلی برای Undo
        private void SaveStateForUndo()
        {
            // استفاده از JSON برای کپی عمیق (Deep Copy) از لیست تا در صورت تغییر مقادیر مرجع خراب نشوند
            var serialized = System.Text.Json.JsonSerializer.Serialize(Elements);
            _undoStack.Push(serialized);
        }

        // محاسبه شیب خط فعلی نقاط RM بر اساس فرمول رگرسیون خطی
        private void UpdateCurrentSlope()
        {
            var pts = VisibleRmPoints;
            int n = pts.Count;

            if (n < 2)
            {
                _currentSlope = 0.0;
                return;
            }

            double sumX = 0.0;
            double sumY = 0.0;

            // ۱. ابتدا میانگین‌ها را حساب می‌کنیم
            foreach (var p in pts)
            {
                sumX += p.OriginalIndex;
                sumY += p.Curr;
            }

            double meanX = sumX / n;
            double meanY = sumY / n;

            // ۲. محاسبه کوواریانس و واریانس (روش دقیق آماری مشابه numpy)
            double numerator = 0.0;
            double denominator = 0.0;

            foreach (var p in pts)
            {
                double xDiff = p.OriginalIndex - meanX;
                double yDiff = p.Curr - meanY;

                numerator += xDiff * yDiff;
                denominator += xDiff * xDiff;
            }

            // جلوگیری از تقسیم بر صفر در مواردی که x ها یکسان هستند
            if (Math.Abs(denominator) < 1e-12)
            {
                _currentSlope = 0.0;
            }
            else
            {
                _currentSlope = numerator / denominator;
            }
        }

        // دکمه Check RM (بر اساس کیوردی که در اینپوت تایپ شده لیست را فیلتر می‌کند)
        private async Task CheckRmAsync()
        {
            if (string.IsNullOrWhiteSpace(_rmKeyword)) return;

            if (ProjectService.CurrentProjectId is Guid projectId)
            {
                Snackbar.Add($"Reloading and filtering RM points by keyword: {_rmKeyword}...", Severity.Info);
                await LoadRmTableForCurrentElementAsync(projectId);
            }
        }

        // دکمه Reset to Original
        private async Task ResetToOriginalAsync()
        {
            SaveStateForUndo();
            if (Elements != null && Elements.Any())
            {
                foreach (var el in Elements)
                {
                    el.Curr = el.Orig; // مقادیر تصحیح شده به حالت اول برمی‌گردند
                }
                UpdateCurrentSlope();
                await UpdateChartsAsync();
                StateHasChanged();
                Snackbar.Add("Reset to original applied.", Severity.Success);
            }
        }

        // دکمه Apply Slope (دقیقاً معادل apply_slope_from_spin در پایتون)
        private async Task ApplySlopeAsync()
        {
            var pts = VisibleRmPoints;
            if (pts.Count < 2) return;

            SaveStateForUndo();
            UpdateCurrentSlope();

            // این بخش دقیقاً معادل کدهای پایتون شماست:
            // slope, _ = np.polyfit(x_n, y_n, 1)
            // first_x = x_n[0]
            // y_n -= slope * (x_n - first_x)  --> برای صفر کردن
            // اینجا ما به شیب هدف (ManualSlope) می‌رسیم

            double deltaSlope = _currentSlope - _manualSlope;
            double firstX = pts[0].OriginalIndex; // چرخش حول اولین نقطه RM

            foreach (var p in pts)
            {
                // فرمول اصلاح نقطه‌ها بر اساس شیب جدید
                p.Curr = p.Curr - (deltaSlope * (p.OriginalIndex - firstX));
            }

            // آپدیت مجدد شیب برای نمایش در UI
            UpdateCurrentSlope();

            await UpdateChartsAsync();
            StateHasChanged();

            Snackbar.Add($"Target slope applied: {_manualSlope:F8}", Severity.Success);
        }

        // دکمه Auto Zero Slope
        private async Task AutoZeroSlopeAsync()
        {
            var pts = VisibleRmPoints;
            if (pts.Count < 2) return;

            SaveStateForUndo();
            UpdateCurrentSlope();

            double slopeToRemove = _currentSlope;
            double firstX = pts[0].OriginalIndex;

            // کپی مستقیم از لاجیک auto_optimize_slope_to_zero پایتون
            foreach (var p in pts)
            {
                p.Curr = p.Curr - (slopeToRemove * (p.OriginalIndex - firstX));
            }

            _manualSlope = 0.0;
            UpdateCurrentSlope();
            await UpdateChartsAsync();
            StateHasChanged();

            Snackbar.Add("Slope successfully set to zero.", Severity.Success);
        }

        // دکمه Auto Flat
        private async Task AutoFlatAsync()
        {
            var pts = VisibleRmPoints;
            if (pts.Count == 0) return;

            SaveStateForUndo();

            // تمام نقاط هم‌ارز مقدار اولین نقطه می‌شوند تا کاملاً صاف شوند
            double firstVal = pts[0].Curr;
            foreach (var p in pts)
            {
                p.Curr = firstVal;
            }

            UpdateCurrentSlope();
            await UpdateChartsAsync();
            StateHasChanged();
            Snackbar.Add("Optimized to flat.", Severity.Success);
        }

        // دکمه Undo RM
        private async Task UndoRmAsync()
        {
            if (_undoStack.Count > 0)
            {
                var previousState = _undoStack.Pop();
                Elements = System.Text.Json.JsonSerializer.Deserialize<List<RMElement>>(previousState) ?? new();

                UpdateVisibleRmPoints();
                UpdateCurrentSlope();
                await UpdateChartsAsync();
                StateHasChanged();

                Snackbar.Add("Last RM changes undone.", Severity.Success);
            }
            else
            {
                Snackbar.Add("No changes to undo.", Severity.Warning);
            }
        }
    }
}