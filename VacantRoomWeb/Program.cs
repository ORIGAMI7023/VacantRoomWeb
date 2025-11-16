using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.HttpOverrides;
using VacantRoomWeb.Components;
using VacantRoomWeb.Handlers;
using VacantRoomWeb.Middleware;
using VacantRoomWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure forwarded headers to get real client IP from nginx
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = null;
    options.EnableDetailedErrors = true;
});

// Register HttpContextAccessor first (critical for other services)
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

// Register memory cache for Excel file caching
builder.Services.AddMemoryCache();

// Register configuration service first (other services depend on it)
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();

// Register HttpClient for EmailService
builder.Services.AddHttpClient<IEmailService, EmailService>();

// Register existing services
builder.Services.AddSingleton<ConnectionCounterService>();
builder.Services.AddSingleton<ClientConnectionTracker>();

// Register new security and logging services
builder.Services.AddSingleton<EnhancedLoggingService>();
builder.Services.AddSingleton<IStartupLoggingService, StartupLoggingService>();
builder.Services.AddSingleton<ApplicationStartupService>(provider =>
    new ApplicationStartupService(provider.GetRequiredService<IStartupLoggingService>()));

// Register security and other services
builder.Services.AddSingleton<EmailSettingsService>();
builder.Services.AddSingleton<SecurityService>();
builder.Services.AddSingleton<AdminAuthService>();

// Register notification service for component communication
builder.Services.AddSingleton<NotificationService>();

// Register updated services with new dependencies
builder.Services.AddSingleton<VacantRoomService>();
builder.Services.AddSingleton<CircuitHandler, ConnectionLoggingCircuitHandler>();

var app = builder.Build();

// ��¼Ӧ�ó�������
var startupService = app.Services.GetRequiredService<IStartupLoggingService>();
startupService.RecordStart();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseForwardedHeaders();

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