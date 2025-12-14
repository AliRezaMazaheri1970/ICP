var builder = WebApplication.CreateBuilder(args);

// ============================================
// YARP Reverse Proxy
// ============================================

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Health Checks
builder.Services.AddHealthChecks();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Request timeouts (برای Route-level TimeoutPolicy)
builder.Services.AddRequestTimeouts(options =>
{
    options.AddPolicy("Default", TimeSpan.FromSeconds(60));  // برای اکثر API ها
    options.AddPolicy("Long", TimeSpan.FromMinutes(5));      // برای Undo/Export
});

var app = builder.Build();

// ============================================
// Middleware
// ============================================

// Logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("→ {Method} {Path}", context.Request.Method, context.Request.Path);

    await next();

    logger.LogInformation("← {StatusCode}", context.Response.StatusCode);
});

// CORS
app.UseCors("AllowAll");

// Health check
app.MapHealthChecks("/health");

// Gateway info
app.MapGet("/", () => new
{
    Name = "Isatis ICP Gateway",
    Version = "1.0.0",
    Status = "Running",
    Timestamp = DateTime.UtcNow,
    Endpoints = new
    {
        Health = "/health",
        Api = "/api/*"
    }
});

// ✅ YARP Reverse Proxy + Apply request timeouts to proxy pipeline
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseRequestTimeouts();
});

app.Run();
