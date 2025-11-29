using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProcessedDataConfiguration : IEntityTypeConfiguration<ProcessedData>
{
    public void Configure(EntityTypeBuilder<ProcessedData> builder)
    {
        builder.ToTable("ProcessedData");

        builder.HasKey(p => p.Id);
        // DO NOT force ValueGeneratedOnAdd if the existing DB column identity differs.
        // If you need identity on new DBs, configure UseIdentityColumn() only when creating the table.
        // builder.Property(p => p.Id).ValueGeneratedOnAdd(); // removed to avoid altering identity

        builder.Property(p => p.ProjectId).IsRequired();
        builder.Property(p => p.ProcessedId).IsRequired();

        builder.Property(p => p.AnalysisType)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(p => p.Data)
               .IsRequired()
               .HasColumnType("nvarchar(max)");

        builder.Property(p => p.CreatedAt)
               .IsRequired()
               .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(p => new { p.ProjectId, p.ProcessedId }).HasDatabaseName("IX_ProcessedData_Project_ProcessedId");

        builder.HasOne(p => p.Project)
               .WithMany(pr => pr.ProcessedDatas)
               .HasForeignKey(p => p.ProjectId)
               .HasPrincipalKey(pr => pr.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}