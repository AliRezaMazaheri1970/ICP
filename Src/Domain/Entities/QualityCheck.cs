using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class QualityCheck : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid SampleId { get; set; }
    public virtual Sample Sample { get; set; } = null!;

    public CheckType CheckType { get; set; }
    public CheckStatus Status { get; set; }
    public string? Message { get; set; }
    public string? Details { get; set; }
}