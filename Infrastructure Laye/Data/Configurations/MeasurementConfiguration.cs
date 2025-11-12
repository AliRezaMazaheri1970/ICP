using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Core.Icp.Domain.Entities.Samples;

namespace Infrastructure.Icp.Data.Configurations
{
    /// <summary>
    ///     Configures the entity type <see cref="Measurement" /> for Entity Framework Core.
    /// </summary>
    public class MeasurementConfiguration : IEntityTypeConfiguration<Measurement>
    {
        /// <summary>
        ///     Configures the entity of type <see cref="Measurement" />.
        /// </summary>
        /// <param name="builder">The builder to be used to configure the entity type.</param>
        public void Configure(EntityTypeBuilder<Measurement> builder)
        {
            // Table
            builder.ToTable("Measurements");

            // Key
            builder.HasKey(m => m.Id);

            // Properties
            builder.Property(m => m.Notes)
                .HasMaxLength(1000);

            builder.Property(m => m.CreatedBy)
                .HasMaxLength(100);

            builder.Property(m => m.UpdatedBy)
                .HasMaxLength(100);

            // Indexes
            builder.HasIndex(m => m.SampleId)
                .HasDatabaseName("IX_Measurement_SampleId");

            builder.HasIndex(m => m.ElementId)
                .HasDatabaseName("IX_Measurement_ElementId");

            builder.HasIndex(m => new { m.SampleId, m.ElementId })
                .HasDatabaseName("IX_Measurement_Sample_Element");

            // Relationships
            builder.HasOne(m => m.Sample)
                .WithMany(s => s.Measurements)
                .HasForeignKey(m => m.SampleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(m => m.Element)
                .WithMany()
                .HasForeignKey(m => m.ElementId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}