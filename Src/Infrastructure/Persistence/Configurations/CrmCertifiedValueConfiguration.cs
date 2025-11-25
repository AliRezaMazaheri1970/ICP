using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class CrmCertifiedValueConfiguration : IEntityTypeConfiguration<CrmCertifiedValue>
{
    public void Configure(EntityTypeBuilder<CrmCertifiedValue> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.ElementName).HasMaxLength(10).IsRequired();
        // ایجاد ایندکس ترکیبی برای جلوگیری از تکرار عنصر در یک CRM
        builder.HasIndex(v => new { v.CrmId, v.ElementName }).IsUnique();
    }
}