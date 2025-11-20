using Domain.Common;

namespace Domain.Entities;

public class Project : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }

    // رابطه با نمونه‌ها
    public virtual ICollection<Sample> Samples { get; set; } = new List<Sample>();
}