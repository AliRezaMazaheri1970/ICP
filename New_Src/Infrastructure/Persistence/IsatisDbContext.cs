using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class IsatisDbContext : DbContext
{
    public IsatisDbContext(DbContextOptions<IsatisDbContext> options) : base(options) { }

    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<RawDataRow> RawDataRows { get; set; } = null!;
    public DbSet<ProjectState> ProjectStates { get; set; } = null!;
    public DbSet<ProjectImportJob> ProjectImportJobs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IsatisDbContext).Assembly);
    }
}