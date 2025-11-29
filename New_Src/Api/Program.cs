using Infrastructure;
using Application;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Register application and infrastructure services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Optional explicit override (safe): ensure EF impl is used
builder.Services.AddScoped<Application.Services.IProjectPersistenceService, Infrastructure.Services.ProjectPersistenceService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();
app.Run();