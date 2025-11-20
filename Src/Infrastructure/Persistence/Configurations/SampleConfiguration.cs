using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class SampleConfiguration : IEntityTypeConfiguration<Sample>
{
    public void Configure(EntityTypeBuilder<Sample> builder)
    {
        // تنظیم نام جدول (اختیاری)
        builder.ToTable("Samples");

        // تنظیم کلید اصلی
        builder.HasKey(x => x.Id);

        // تنظیم ویژگی‌ها
        builder.Property(x => x.SolutionLabel)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasConversion<string>(); // Enum را به صورت متن ذخیره کن (خوانایی بهتر)

        // تنظیم رابطه One-to-Many با Measurement
        builder.HasMany(s => s.Measurements)
            .WithOne(m => m.Sample)
            .HasForeignKey(m => m.SampleId)
            .OnDelete(DeleteBehavior.Cascade); // اگر نمونه پاک شد، اندازه‌گیری‌هایش هم پاک شوند

        builder.HasOne(s => s.Project)
            .WithMany(p => p.Samples)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}