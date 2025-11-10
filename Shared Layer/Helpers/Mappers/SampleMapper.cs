using Core.Icp.Domain.Entities.Samples;
using Shared.Icp.DTOs.Samples;

namespace Shared.Icp.Helpers.Mappers
{
    /// <summary>
    /// Mapper برای Sample
    /// </summary>
    public static class SampleMapper
    {
        /// <summary>
        /// Entity به DTO
        /// </summary>
        public static SampleDto ToDto(this Sample sample)
        {
            return new SampleDto
            {
                Id = sample.Id,
                SampleId = sample.SampleId,
                SampleName = sample.SampleName,
                RunDate = sample.RunDate,
                Status = sample.Status.ToString(),
                ProjectId = sample.ProjectId,
                Weight = sample.Weight,
                Volume = sample.Volume,
                DilutionFactor = sample.DilutionFactor,
                Notes = sample.Notes,
                MeasurementCount = sample.Measurements?.Count ?? 0,
                QualityCheckCount = sample.QualityChecks?.Count ?? 0,
                CreatedAt = sample.CreatedAt,
                UpdatedAt = sample.UpdatedAt
            };
        }

        /// <summary>
        /// لیست Entity به لیست DTO
        /// </summary>
        public static IEnumerable<SampleDto> ToDtoList(this IEnumerable<Sample> samples)
        {
            return samples.Select(s => s.ToDto());
        }

        /// <summary>
        /// CreateDTO به Entity
        /// </summary>
        public static Sample ToEntity(this CreateSampleDto dto)
        {
            return new Sample
            {
                SampleId = dto.SampleId,
                SampleName = dto.SampleName,
                RunDate = dto.RunDate,
                ProjectId = dto.ProjectId,
                Weight = dto.Weight,
                Volume = dto.Volume,
                DilutionFactor = dto.DilutionFactor,
                Notes = dto.Notes
            };
        }

        /// <summary>
        /// به‌روزرسانی Entity از UpdateDTO
        /// </summary>
        public static void UpdateFromDto(this Sample sample, UpdateSampleDto dto)
        {
            sample.SampleName = dto.SampleName;
            sample.RunDate = dto.RunDate;
            sample.Weight = dto.Weight;
            sample.Volume = dto.Volume;
            sample.DilutionFactor = dto.DilutionFactor;
            sample.Notes = dto.Notes;
            sample.UpdatedAt = DateTime.UtcNow;
        }
    }
}