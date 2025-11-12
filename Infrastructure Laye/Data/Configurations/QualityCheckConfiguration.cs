using Core.Icp.Domain.Entities.QualityControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    /// <summary>
    ///     Configures the entity type <see cref="QualityCheck" /> for Entity Framework Core.
    /// </summary>
    public class QualityCheckConfiguration : IEntityTypeConfiguration<QualityCheck>
    {
        /// <summary>
        ///     Configures the entity of type <see cref="QualityCheck" />.
        /// </summary>
        /// <param name="builder">The builder to be used to configure the entity type.</param>
        public void Configure(EntityTypeBuilder<QualityCheck> builder)
        {
            // Table
            builder.ToTable("QualityChecks");

            // Key
            builder.HasKey(q => q.Id);

            // Properties
            builder.Property(q => q.CheckType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(q => q.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(q => q.Message)
                .HasMaxLength(500);

            builder.Property(q => q.Details)
                .HasMaxLength(2000);

            // Indexes
            builder.HasIndex(q => q.SampleId);
            builder.HasIndex(q => q.CheckType);
            builder.HasIndex(q => q.Status);

            // Relationships
            builder.HasOne(q => q.Sample)
                .WithMany(s => s.QualityChecks)
                .HasForeignKey(q => q.SampleId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}