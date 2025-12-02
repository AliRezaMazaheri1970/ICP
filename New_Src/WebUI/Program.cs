using MudBlazor.Services;
using WebUI.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// 1.  Blazor Server Services
// ============================================
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
// در Program.cs بعد از AuthService
// ============================================
// 2. MudBlazor Services
// ============================================
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
    config.SnackbarConfiguration.ShowTransitionDuration = 300;
    config.SnackbarConfiguration.HideTransitionDuration = 300;
});

// ============================================
// 3. HttpClient for API Connection
// ============================================
// خواندن آدرس API از appsettings.json
var apiBaseUrl = builder.Configuration.GetValue<string>("ApiSettings:BaseUrl") ?? "http://192.168.0.103:5000/api/";

builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ============================================
// 4. Application Services
// ============================================
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ImportService>();
// ============================================
// Build Application
// ============================================
var app = builder.Build();

// ============================================
// Middleware Pipeline
// ============================================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    // فقط در صورت استفاده از HTTPS
    // app.UseHsts();
}

// اگه پشت Nginx با SSL هستی، این خط رو کامنت کن
// app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// ============================================
// Log startup info
// ============================================
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("WebUI started.  API Base URL: {ApiUrl}", apiBaseUrl);

app.Run();