using Domain.Common;

namespace Domain.Entities;

public class Measurement : BaseEntity
{
    // کلید خارجی برای ارتباط با Sample
    public Guid SampleId { get; set; }

    // نام عنصر شیمیایی مثل "Cu", "Au"
    public required string ElementName { get; set; }

    // مقدار خوانده شده (Int یا Corr Con)
    public double Value { get; set; }

    public string Unit { get; set; } = "ppm";

    // Property برای Navigation (اختیاری ولی در EF Core مفید است)
    public virtual Sample? Sample { get; set; }
}