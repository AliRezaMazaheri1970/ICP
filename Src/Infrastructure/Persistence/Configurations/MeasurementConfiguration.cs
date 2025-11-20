using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Diagnostics.Metrics;

namespace Infrastructure.Persistence.Configurations;

public class MeasurementConfiguration : IEntityTypeConfiguration<Measurement>
{
    public void Configure(EntityTypeBuilder<Measurement> builder)
    {
        builder.ToTable("Measurements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ElementName)
            .HasMaxLength(10) // مثلا "Cu", "Zn" بیشتر از ۱۰ کاراکتر نیستند
            .IsRequired();

        builder.Property(x => x.Unit)
            .HasMaxLength(20)
            .HasDefaultValue("ppm");

        // ایندکس گذاری برای سرعت جستجو روی ElementName (چون زیاد فیلتر می‌کنید)
        builder.HasIndex(x => x.ElementName);
    }
}