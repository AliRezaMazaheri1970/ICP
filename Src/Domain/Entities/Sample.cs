using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Sample : BaseEntity
{
    public required string SolutionLabel { get; set; }
    public SampleType Type { get; set; }
    public double Weight { get; set; }
    public double Volume { get; set; }
    public double DilutionFactor { get; set; }

    public Guid ProjectId { get; set; }
    public virtual Project? Project { get; set; }

    public virtual ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();

    // --- اضافه شده برای QC ---
    public virtual ICollection<QualityCheck> QualityChecks { get; set; } = new List<QualityCheck>();
}