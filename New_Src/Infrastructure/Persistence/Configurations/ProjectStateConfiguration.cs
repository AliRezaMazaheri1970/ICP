using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProjectStateConfiguration : IEntityTypeConfiguration<ProjectState>
{
    public void Configure(EntityTypeBuilder<ProjectState> builder)
    {
        builder.ToTable("ProjectStates");

        builder.HasKey(s => s.StateId);

        builder.Property(s => s.ProjectId)
            .IsRequired();

        builder.Property(s => s.Data)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(s => s.Timestamp)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(s => s.Description)
            .HasMaxLength(500);

        // Explicit FK mapping using navigation props
        builder.HasOne(s => s.Project)
               .WithMany(p => p.ProjectStates)
               .HasForeignKey(s => s.ProjectId)
               .HasPrincipalKey(p => p.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}