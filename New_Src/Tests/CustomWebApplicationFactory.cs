using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;

namespace Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the app's DbContext registration (SQL Server) if present
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<IsatisDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // If TEST_SQL_CONNECTION env var is set, use that SQL Server for integration tests.
            var sqlConn = System.Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION");
            if (!string.IsNullOrWhiteSpace(sqlConn))
            {
                services.AddDbContext<IsatisDbContext>(options =>
                {
                    options.UseSqlServer(sqlConn, sqlOptions =>
                    {
                        // Optional: increase command timeout for CI if necessary
                        sqlOptions.CommandTimeout(180);
                    });
                });

                // build provider and apply migrations so DB schema exists for tests
                var sp = services.BuildServiceProvider();
                using (var scope = sp.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IsatisDbContext>();
                    // Ensure DB is created and migrations applied
                    db.Database.Migrate();
                }

                return;
            }

            // Default: use InMemory for fast/local tests
            services.AddDbContext<IsatisDbContext>(options =>
            {
                options.UseInMemoryDatabase("Isatis_TestDb");
            });

            var provider = services.BuildServiceProvider();
            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IsatisDbContext>();
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();
            }
        });
    }
}