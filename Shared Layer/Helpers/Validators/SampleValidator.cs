using Shared.Icp.DTOs.Samples;
using Shared.Icp.Exceptions;

namespace Shared.Icp.Helpers.Validators
{
    /// <summary>
    /// اعتبارسنجی Sample
    /// </summary>
    public static class SampleValidator
    {
        public static void ValidateCreate(CreateSampleDto dto)
        {
            var errors = new Dictionary<string, List<string>>();

            if (string.IsNullOrWhiteSpace(dto.SampleId))
                errors.Add(nameof(dto.SampleId), new List<string> { "شناسه نمونه الزامی است" });

            if (string.IsNullOrWhiteSpace(dto.SampleName))
                errors.Add(nameof(dto.SampleName), new List<string> { "نام نمونه الزامی است" });

            if (dto.Weight.HasValue && dto.Weight.Value < 0)
                errors.Add(nameof(dto.Weight), new List<string> { "وزن نمی‌تواند منفی باشد" });

            if (dto.Volume.HasValue && dto.Volume.Value < 0)
                errors.Add(nameof(dto.Volume), new List<string> { "حجم نمی‌تواند منفی باشد" });

            if (dto.DilutionFactor.HasValue && dto.DilutionFactor.Value < 1)
                errors.Add(nameof(dto.DilutionFactor), new List<string> { "ضریب رقت باید حداقل 1 باشد" });

            if (errors.Any())
                throw new ValidationException(errors);
        }

        public static void ValidateUpdate(UpdateSampleDto dto)
        {
            var errors = new Dictionary<string, List<string>>();

            if (dto.Id <= 0)
                errors.Add(nameof(dto.Id), new List<string> { "شناسه نامعتبر است" });

            if (string.IsNullOrWhiteSpace(dto.SampleName))
                errors.Add(nameof(dto.SampleName), new List<string> { "نام نمونه الزامی است" });

            if (dto.Weight.HasValue && dto.Weight.Value < 0)
                errors.Add(nameof(dto.Weight), new List<string> { "وزن نمی‌تواند منفی باشد" });

            if (errors.Any())
                throw new ValidationException(errors);
        }
    }
}