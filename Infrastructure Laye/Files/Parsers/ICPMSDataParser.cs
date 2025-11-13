using Core.Icp.Domain.Entities.Samples;
using Infrastructure.Icp.Files.Models;

namespace Infrastructure.Icp.Files.Parsers
{
    /// <summary>
    /// Parser برای داده‌های ICP-MS
    /// </summary>
    public class ICPMSDataParser
    {
        /// <summary>
        /// Parse کردن یک ردیف CSV/Excel به Sample
        /// </summary>
        public Sample ParseSample(Dictionary<string, string> row, int rowNumber)
        {
            try
            {
                var sample = new Sample
                {
                    SampleId = GetValue(row, "SampleId", "Sample_ID", "ID"),
                    SampleName = GetValue(row, "SampleName", "Sample_Name", "Name"),
                    Weight = ParseDecimal(GetValue(row, "Weight", "Wt", "Sample_Weight")),
                    Volume = ParseDecimal(GetValue(row, "Volume", "Vol", "Sample_Volume")),
                    DilutionFactor = ParseInt(GetValue(row, "DilutionFactor", "DF", "Dilution")),
                    RunDate = ParseDateTime(GetValue(row, "RunDate", "Run_Date", "Date"))
                };

                // Parse Measurements (عناصر)
                ParseMeasurements(row, sample);

                return sample;
            }
            catch (Exception ex)
            {
                throw new Exception($"خطا در parse کردن ردیف {rowNumber}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parse کردن Measurements از ستون‌های عنصری
        /// </summary>
        private void ParseMeasurements(Dictionary<string, string> row, Sample sample)
        {
            var measurements = new List<Measurement>();

            foreach (var column in row.Keys)
            {
                // اگر ستون یک عنصر شیمیایی است
                if (IsElementColumn(column))
                {
                    var elementSymbol = ExtractElementSymbol(column);
                    var isotope = ExtractIsotope(column);

                    if (decimal.TryParse(row[column], out var intensity))
                    {
                        var measurement = new Measurement
                        {
                            ElementSymbol = elementSymbol,
                            Isotope = isotope,
                            NetIntensity = intensity,
                            RawIntensity = intensity,
                            IsValid = true
                        };

                        measurements.Add(measurement);
                    }
                }
            }

            sample.Measurements = measurements;
        }

        /// <summary>
        /// بررسی اینکه آیا ستون یک عنصر شیمیایی است
        /// </summary>
        private bool IsElementColumn(string columnName)
        {
            var elementPattern = @"^[A-Z][a-z]?(\d+)?(-\d+)?$";
            return System.Text.RegularExpressions.Regex.IsMatch(columnName, elementPattern);
        }

        /// <summary>
        /// استخراج نماد عنصر
        /// </summary>
        private string ExtractElementSymbol(string columnName)
        {
            var match = System.Text.RegularExpressions.Regex.Match(columnName, @"^([A-Z][a-z]?)");
            return match.Success ? match.Groups[1].Value : columnName;
        }

        /// <summary>
        /// استخراج شماره ایزوتوپ
        /// </summary>
        private int? ExtractIsotope(string columnName)
        {
            var match = System.Text.RegularExpressions.Regex.Match(columnName, @"\d+");
            return match.Success && int.TryParse(match.Value, out var isotope) ? isotope : null;
        }

        /// <summary>
        /// Helper برای گرفتن مقدار با چند نام ممکن
        /// </summary>
        private string GetValue(Dictionary<string, string> row, params string[] possibleKeys)
        {
            foreach (var key in possibleKeys)
            {
                if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            return string.Empty;
        }

        private decimal ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            value = value.Trim().Replace(",", "");

            return decimal.TryParse(value, out var result) ? result : 0;
        }

        private int ParseInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 1;

            value = value.Trim();
            return int.TryParse(value, out var result) ? result : 1;
        }

        private DateTime ParseDateTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DateTime.UtcNow;

            return DateTime.TryParse(value, out var result) ? result : DateTime.UtcNow;
        }
    }
}