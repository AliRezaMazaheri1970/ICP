using Core.Icp.Domain.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    /// <summary>
    ///     Configures the entity type <see cref="Project" /> for Entity Framework Core.
    /// </summary>
    public class ProjectConfiguration : IEntityTypeConfiguration<Project>
    {
        /// <summary>
        ///     Configures the entity of type <see cref="Project" />.
        /// </summary>
        /// <param name="builder">The builder to be used to configure the entity type.</param>
        public void Configure(EntityTypeBuilder<Project> builder)
        {
            // Table
            builder.ToTable("Projects");

            // Key
            builder.HasKey(p => p.Id);

            // Properties
            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(p => p.Description)
                .HasMaxLength(1000);

            builder.Property(p => p.SourceFileName)
                .HasMaxLength(500);

            // Enum to string conversion
            builder.Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            // Indexes
            builder.HasIndex(p => p.Name);
            builder.HasIndex(p => p.Status);
            builder.HasIndex(p => p.CreatedAt);

            // Relationships
            builder.HasMany(p => p.Samples)
                .WithOne(s => s.Project)
                .HasForeignKey(s => s.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(p => p.CalibrationCurves)
                .WithOne(c => c.Project)
                .HasForeignKey(c => c.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}