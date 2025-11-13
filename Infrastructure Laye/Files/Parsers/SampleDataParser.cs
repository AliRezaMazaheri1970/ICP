using Core.Icp.Domain.Entities.Samples;
using Core.Icp.Domain.Enums;

namespace Infrastructure.Icp.Files.Parsers
{
    /// <summary>
    /// Parser اختصاصی برای Sample ها
    /// </summary>
    public class SampleDataParser
    {
        private readonly ICPMSDataParser _icpmsParser;

        public SampleDataParser()
        {
            _icpmsParser = new ICPMSDataParser();
        }

        /// <summary>
        /// Parse کردن لیستی از ردیف‌ها به Sample ها
        /// </summary>
        public List<Sample> ParseSamples(List<Dictionary<string, string>> rows)
        {
            var samples = new List<Sample>();
            var rowNumber = 1;

            foreach (var row in rows)
            {
                try
                {
                    var sample = _icpmsParser.ParseSample(row, rowNumber);

                    sample.Status = SampleStatus.Pending;
                    sample.CreatedAt = DateTime.UtcNow;

                    samples.Add(sample);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"خطا در پردازش ردیف {rowNumber}: {ex.Message}");
                }

                rowNumber++;
            }

            return samples;
        }

        /// <summary>
        /// Validate کردن Sample
        /// </summary>
        public bool ValidateSample(Sample sample, out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(sample.SampleId))
                errors.Add("SampleId الزامی است");

            if (string.IsNullOrWhiteSpace(sample.SampleName))
                errors.Add("SampleName الزامی است");

            if (sample.Weight <= 0)
                errors.Add("Weight باید بزرگتر از صفر باشد");

            if (sample.Volume <= 0)
                errors.Add("Volume باید بزرگتر از صفر باشد");

            if (sample.DilutionFactor < 1)
                errors.Add("DilutionFactor نمی‌تواند کمتر از 1 باشد");

            if (!sample.Measurements.Any())
                errors.Add("حداقل یک Measurement الزامی است");

            return !errors.Any();
        }
    }
}