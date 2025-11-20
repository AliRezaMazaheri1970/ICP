using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Sample : BaseEntity
{
    // نام یا لیبل محلول
    public required string SolutionLabel { get; set; }

    public SampleType Type { get; set; }

    public double Weight { get; set; }

    public double Volume { get; set; }

    public double DilutionFactor { get; set; }

    // رابطه One-to-Many با اندازه‌گیری‌ها
    public virtual ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
}