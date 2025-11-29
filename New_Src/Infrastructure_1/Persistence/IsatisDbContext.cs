using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Infrastructure.Persistence;

public class IsatisDbContext : DbContext
{
    public IsatisDbContext(DbContextOptions<IsatisDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<RawDataRow> RawDataRows => Set<RawDataRow>();
    public DbSet<ProjectState> ProjectStates => Set<ProjectState>();
    public DbSet<ProcessedData> ProcessedDatas => Set<ProcessedData>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Projects
        modelBuilder.Entity<Project>(b =>
        {
            b.HasKey(p => p.ProjectId);
            b.Property(p => p.ProjectName).IsRequired().HasMaxLength(250);
            b.Property(p => p.Owner).HasMaxLength(200);
            b.HasMany(p => p.RawDataRows).WithOne(r => r.Project).HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.ProjectStates).WithOne(s => s.Project).HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.ProcessedDatas).WithOne(pd => pd.Project).HasForeignKey(pd => pd.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        // RawDataRow
        modelBuilder.Entity<RawDataRow>(b =>
        {
            b.HasKey(r => r.DataId);
            b.Property(r => r.ColumnData).IsRequired();
            b.Property(r => r.SampleId).HasMaxLength(200);
            b.HasIndex(r => r.ProjectId);
        });

        // ProjectState
        modelBuilder.Entity<ProjectState>(b =>
        {
            b.HasKey(s => s.StateId);
            b.Property(s => s.Data).IsRequired();
            b.Property(s => s.Description).HasMaxLength(500);
            b.HasIndex(s => new { s.ProjectId, s.Timestamp });
        });

        // ProcessedData
        modelBuilder.Entity<ProcessedData>(b =>
        {
            b.HasKey(p => p.ProcessedId);
            b.Property(p => p.AnalysisType).HasMaxLength(100);
            b.Property(p => p.Data).IsRequired();
            b.HasIndex(p => p.ProjectId);
        });
    }
}