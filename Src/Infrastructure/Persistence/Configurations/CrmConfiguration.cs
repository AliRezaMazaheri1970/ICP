using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class CrmConfiguration : IEntityTypeConfiguration<Crm>
{
    public void Configure(EntityTypeBuilder<Crm> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.HasMany(c => c.CertifiedValues)
            .WithOne(v => v.Crm)
            .HasForeignKey(v => v.CrmId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}