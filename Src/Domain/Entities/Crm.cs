using Domain.Common;

namespace Domain.Entities;

public class Crm : BaseEntity
{
    public required string Name { get; set; } // مثلا "OREAS 258"
    public string? Description { get; set; }

    // مقادیر استاندارد تایید شده برای این CRM
    public virtual ICollection<CrmCertifiedValue> CertifiedValues { get; set; } = new List<CrmCertifiedValue>();
}