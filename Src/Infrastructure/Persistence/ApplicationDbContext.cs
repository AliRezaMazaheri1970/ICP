using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Reflection.Emit;

namespace Infrastructure.Persistence;

// در دات نت 10 معمولا از Primary Constructor استفاده می‌شود
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Sample> Samples { get; set; }
    public DbSet<Measurement> Measurements { get; set; }
    public DbSet<CalibrationCurve> CalibrationCurves { get; set; }
    public DbSet<CalibrationPoint> CalibrationPoints { get; set; }
    public DbSet<Crm> Crms { get; set; }
    public DbSet<CrmCertifiedValue> CrmCertifiedValues { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // این خط به طور خودکار تمام کلاس‌های Configuration را در این اسمبلی پیدا و اعمال می‌کند
        // (SampleConfiguration و MeasurementConfiguration)
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}