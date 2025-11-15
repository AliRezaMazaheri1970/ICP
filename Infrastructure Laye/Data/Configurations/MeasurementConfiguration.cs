using Core.Icp.Domain.Entities.Samples;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Icp.Data.Configurations;

public class MeasurementConfiguration : IEntityTypeConfiguration<Measurement>
{
    public void Configure(EntityTypeBuilder<Measurement> builder)
    {
        builder.ToTable("Measurements");

        // Key از BaseEntity می‌آید (مثلاً Id)
        builder.HasKey(m => m.Id);

        // FKs
        builder.HasOne(m => m.Sample)
               .WithMany(s => s.Measurements)
               .HasForeignKey(m => m.SampleId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Element)
               .WithMany(e => e.Measurements)
               .HasForeignKey(m => m.ElementId)
               .OnDelete(DeleteBehavior.Restrict);

        // ElementSymbol
        builder.Property(m => m.ElementSymbol)
               .IsRequired()
               .HasMaxLength(10);

        // Isotope
        builder.Property(m => m.Isotope)
               .IsRequired(false);

        // شدت‌ها و غلظت‌ها – حواست به Precision باشه
        builder.Property(m => m.RawIntensity)
               .HasColumnType("decimal(18,6)");

        builder.Property(m => m.NetIntensity)
               .HasColumnType("decimal(18,6)");

        builder.Property(m => m.Concentration)
               .HasColumnType("decimal(18,6)");

        builder.Property(m => m.FinalConcentration)
               .HasColumnType("decimal(18,6)")
               .IsRequired(false);

        builder.Property(m => m.IsValid)
               .HasDefaultValue(true);

        builder.Property(m => m.Notes)
               .HasMaxLength(500);
    }
}
