using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using WebUI.Services;

namespace WebUI.Pages
{
    public partial class Elements
    {
        [SupplyParameterFromQuery]
        public Guid? projectId { get; set; }

        private Guid? _projectId;
        private bool _isLoading;
        private string? _selectedElement;
        private string _selectedWavelength = "All Wavelengths";
        private List<string> _elementButtons = new();
        private List<string> _availableWavelengths = new() { "All Wavelengths" };
        private readonly List<RawElementRow> _rawRows = new();
        private readonly List<ElementDetailRow> _detailRows = new();

        protected override async Task OnParametersSetAsync()
        {
            var nextProjectId = projectId ?? ProjectService.CurrentProjectId;
            if (nextProjectId == _projectId)
            {
                return;
            }

            _projectId = nextProjectId;
            _rawRows.Clear();
            _detailRows.Clear();
            _elementButtons = new List<string>();
            _availableWavelengths = new List<string> { "All Wavelengths" };
            _selectedElement = null;
            _selectedWavelength = "All Wavelengths";

            if (_projectId.HasValue)
            {
                await LoadRawRowsAsync();
                BuildElementButtons();
                if (_elementButtons.Count > 0)
                {
                    SelectElement(_elementButtons[0]);
                }
            }
        }

        private async Task LoadRawRowsAsync()
        {
            _isLoading = true;
            StateHasChanged();

            try
            {
                var allRows = new List<RawDataDto>();
                const int pageSize = 2000;
                var skip = 0;

                while (true)
                {
                    var result = await ProjectService.GetProjectRawRowsAsync(_projectId!.Value, skip, pageSize);
                    if (!result.Succeeded || result.Data == null)
                    {
                        Snackbar.Add(result.Message ?? "Failed to load raw rows", Severity.Error);
                        break;
                    }

                    allRows.AddRange(result.Data);
                    if (result.Data.Count < pageSize)
                    {
                        break;
                    }

                    skip += result.Data.Count;
                }

                foreach (var row in allRows)
                {
                    var parsed = ParseRawRow(row.ColumnData);
                    if (parsed != null)
                    {
                        _rawRows.Add(parsed);
                    }
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void BuildElementButtons()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _elementButtons = new List<string>();

            foreach (var row in _rawRows)
            {
                if (!IsBlankRow(row))
                {
                    continue;
                }

                var baseElement = ExtractBaseElement(row.Element);
                if (string.IsNullOrWhiteSpace(baseElement))
                {
                    continue;
                }

                if (seen.Add(baseElement))
                {
                    _elementButtons.Add(baseElement);
                }
            }
        }

        private void SelectElement(string element)
        {
            _selectedElement = element;
            BuildWavelengthOptions();
            _selectedWavelength = "All Wavelengths";
            UpdateDetailsRows();
        }

        private Task OnWavelengthChanged(string value)
        {
            _selectedWavelength = value;
            UpdateDetailsRows();
            return Task.CompletedTask;
        }

        private void BuildWavelengthOptions()
        {
            _availableWavelengths = new List<string> { "All Wavelengths" };
            if (string.IsNullOrWhiteSpace(_selectedElement))
            {
                return;
            }

            var wavelengths = _rawRows
                .Where(row => IsStdRow(row) && ElementMatchesBase(row.Element, _selectedElement))
                .Select(row => ExtractWavelength(row.Element, _selectedElement))
                .Where(wl => !string.IsNullOrWhiteSpace(wl))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(wl => wl)
                .ToList();

            _availableWavelengths.AddRange(wavelengths);
        }

        private void UpdateDetailsRows()
        {
            _detailRows.Clear();
            if (string.IsNullOrWhiteSpace(_selectedElement))
            {
                return;
            }

            IEnumerable<RawElementRow> stdRows;
            if (string.Equals(_selectedWavelength, "All Wavelengths", StringComparison.OrdinalIgnoreCase))
            {
                stdRows = _rawRows.Where(row => IsStdRow(row) && ElementMatchesBase(row.Element, _selectedElement));
            }
            else
            {
                var fullElement = $"{_selectedElement} {_selectedWavelength}".Trim();
                stdRows = _rawRows.Where(row =>
                    IsStdRow(row) && string.Equals(row.Element, fullElement, StringComparison.OrdinalIgnoreCase));
            }

            if (!stdRows.Any())
            {
                var message = string.Equals(_selectedWavelength, "All Wavelengths", StringComparison.OrdinalIgnoreCase)
                    ? "No STD data found"
                    : $"No data for {_selectedWavelength}";
                _detailRows.Add(ElementDetailRow.Message(message));
                return;
            }

            foreach (var row in stdRows)
            {
                var solnConc = row.SolnConc ?? row.CorrCon;
                var wavelength = ExtractWavelength(row.Element, _selectedElement);

                _detailRows.Add(new ElementDetailRow
                {
                    SolutionLabel = row.SolutionLabel,
                    Element = _selectedElement,
                    SolnConc = solnConc,
                    Intensity = row.Intensity,
                    Wavelength = wavelength
                });
            }
        }

        private static bool IsStdRow(RawElementRow row)
            => string.Equals(row.Type, "Std", StringComparison.OrdinalIgnoreCase);

        private static bool IsBlankRow(RawElementRow row)
        {
            if (string.Equals(row.Type, "Blk", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return row.SolutionLabel?.Contains("BLANK", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static string ExtractBaseElement(string elementText)
        {
            if (string.IsNullOrWhiteSpace(elementText))
            {
                return string.Empty;
            }

            var parts = elementText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0].Trim() : string.Empty;
        }

        private static bool ElementMatchesBase(string elementText, string baseElement)
        {
            if (string.IsNullOrWhiteSpace(elementText) || string.IsNullOrWhiteSpace(baseElement))
            {
                return false;
            }

            return elementText.Equals(baseElement, StringComparison.OrdinalIgnoreCase)
                   || elementText.StartsWith($"{baseElement} ", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractWavelength(string elementText, string baseElement)
        {
            if (string.IsNullOrWhiteSpace(elementText) || string.IsNullOrWhiteSpace(baseElement))
            {
                return string.Empty;
            }

            if (elementText.StartsWith($"{baseElement} ", StringComparison.OrdinalIgnoreCase))
            {
                return elementText.Substring(baseElement.Length).Trim();
            }

            return string.Empty;
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

                var solutionLabel = GetString(map, "Solution Label", "SolutionLabel", "Sample ID", "SampleId", "Sample", "Label", "Name") ?? string.Empty;
                var solnConc = GetDecimal(map, "Soln Conc", "SolnConc");
                var corrCon = GetDecimal(map, "Corr Con", "CorrCon", "Concentration", "Conc", "Calibrated Conc", "Result");
                var intensity = GetDecimal(map, "Int", "Intensity", "Net Intensity", "CPS", "Counts", "Signal");

                return new RawElementRow
                {
                    Type = type.Trim(),
                    Element = element.Trim(),
                    SolutionLabel = solutionLabel.Trim(),
                    SolnConc = solnConc,
                    CorrCon = corrCon,
                    Intensity = intensity
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
            public string SolutionLabel { get; set; } = "";
            public decimal? SolnConc { get; set; }
            public decimal? CorrCon { get; set; }
            public decimal? Intensity { get; set; }
        }

        public sealed class ElementDetailRow
        {
            public string SolutionLabel { get; set; } = "";
            public string Element { get; set; } = "";
            public decimal? SolnConc { get; set; }
            public decimal? Intensity { get; set; }
            public string Wavelength { get; set; } = "";
            public bool IsMessage { get; set; }

            public decimal SortSolnConc => SolnConc ?? -1m;
            public decimal SortInt => Intensity ?? -1m;

            public string SolnConcDisplay => SolnConc.HasValue ? SolnConc.Value.ToString("F2") : "---";
            public string IntDisplay => Intensity.HasValue ? Intensity.Value.ToString("F2") : "---";

            public static ElementDetailRow Message(string message)
            {
                return new ElementDetailRow
                {
                    SolutionLabel = message,
                    Element = string.Empty,
                    SolnConc = null,
                    Intensity = null,
                    Wavelength = string.Empty,
                    IsMessage = true
                };
            }
        }
    }
}
