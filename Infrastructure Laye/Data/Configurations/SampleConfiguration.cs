using Core.Icp.Domain.Entities.Samples;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    /// <summary>
    ///     Configures the entity type <see cref="Sample" /> for Entity Framework Core.
    /// </summary>
    public class SampleConfiguration : IEntityTypeConfiguration<Sample>
    {
        /// <summary>
        ///     Configures the entity of type <see cref="Sample" />.
        /// </summary>
        /// <param name="builder">The builder to be used to configure the entity type.</param>
        public void Configure(EntityTypeBuilder<Sample> builder)
        {
            // Table
            builder.ToTable("Samples");

            // Key
            builder.HasKey(s => s.Id);

            // Properties
            builder.Property(s => s.SampleId)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(s => s.SampleName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(s => s.RunDate)
                .IsRequired();

            // Enum to string conversion
            builder.Property(s => s.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(s => s.Weight)
                .HasPrecision(18, 4)
                .IsRequired();

            builder.Property(s => s.Volume)
                .HasPrecision(18, 4)
                .IsRequired();

            builder.Property(s => s.DilutionFactor)
                .IsRequired();

            // Indexes
            builder.HasIndex(s => s.SampleId).IsUnique();
            builder.HasIndex(s => s.ProjectId);
            builder.HasIndex(s => s.Status);
            builder.HasIndex(s => s.RunDate);

            // Relationships
            builder.HasOne(s => s.Project)
                .WithMany(p => p.Samples)
                .HasForeignKey(s => s.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(s => s.Measurements)
                .WithOne(m => m.Sample)
                .HasForeignKey(m => m.SampleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(s => s.QualityChecks)
                .WithOne(q => q.Sample)
                .HasForeignKey(q => q.SampleId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}