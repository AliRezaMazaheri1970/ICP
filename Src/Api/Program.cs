using Application;      // برای دسترسی به AddApplicationServices
using Infrastructure;   // برای دسترسی به AddInfrastructureServices

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// تنظیمات OpenAPI (مخصوص .NET 9 و 10)
builder.Services.AddOpenApi();

// ==================================================================
// اتصال لایه‌های معماری تمیز (Clean Architecture Wiring)
// ==================================================================

// 1. ثبت سرویس‌های لایه Application (مثل MediatR)
builder.Services.AddApplicationServices();

// 2. ثبت سرویس‌های لایه Infrastructure (دیتابیس، فایل، ریپازیتوری)
builder.Services.AddInfrastructureServices(builder.Configuration);

// ==================================================================

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // تولید آدرس /openapi/v1.json
    app.MapOpenApi();

    // نکته: در .NET 9 به بعد، Swagger UI به صورت پیش‌فرض نیست.
    // اگر رابط گرافیکی می‌خواهید، می‌توانید از Scalar استفاده کنید (اختیاری)
    // app.MapScalarApiReference(); 
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();