using Domain.Common;

namespace Domain.Entities;

public class CrmCertifiedValue : BaseEntity
{
    public Guid CrmId { get; set; }
    public virtual Crm Crm { get; set; } = null!;

    public required string ElementName { get; set; } // مثلا "Cu"
    public double Value { get; set; } // مقدار استاندارد (مثلا 230 ppm)
    public double? StandardDeviation { get; set; } // انحراف معیار (SD)
    public string Unit { get; set; } = "ppm";
}