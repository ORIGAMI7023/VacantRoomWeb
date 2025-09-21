using Microsoft.AspNetCore.Components.Server.Circuits;
using VacantRoomWeb;
using VacantRoomWeb.Components;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = null;
    options.EnableDetailedErrors = true;
});

// Register existing services
builder.Services.AddSingleton<ConnectionCounterService>();
builder.Services.AddSingleton<ClientConnectionTracker>();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

// Register new security and logging services
builder.Services.AddSingleton<EnhancedLoggingService>();
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<SecurityService>();
builder.Services.AddSingleton<AdminAuthService>();
builder.Services.AddSingleton<ApplicationStartupService>();

// Register notification service for component communication
builder.Services.AddSingleton<NotificationService>();

// Register updated services with new dependencies
builder.Services.AddSingleton<VacantRoomService>();
builder.Services.AddSingleton<CircuitHandler, ConnectionLoggingCircuitHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Add domain routing middleware FIRST
app.UseMiddleware<DomainRoutingMiddleware>();

// Add security middleware AFTER domain routing
app.UseMiddleware<SecurityMiddleware>();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();