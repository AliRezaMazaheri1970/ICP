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

    // --- اضافه شده ---
    public Guid ProjectId { get; set; } // کلید خارجی
    public virtual Project? Project { get; set; } // نویگیشن
    // ----------------

    public virtual ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
}