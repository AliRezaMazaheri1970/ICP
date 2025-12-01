using Application;
using Infrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using Web.Components;
using Web.Interface;
using Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Blazor Services
// ============================================

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

// ============================================
// Authentication (Blazor Server Style)
// ============================================

builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AddScoped<IAuthStateService, AuthStateService>();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore();

// ============================================
// Application & Infrastructure Services
// ============================================

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// ============================================
// Build & Run
// ============================================

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();