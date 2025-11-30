using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Register application and infrastructure services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Optional explicit override (safe) - keep only if you need to override the default mapping.
// If AddInfrastructureServices already registered this, you can remove this line.
// builder.Services.AddScoped<Application.Services.IProjectPersistenceService, Infrastructure.Services.ProjectPersistenceService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();
app.Run();