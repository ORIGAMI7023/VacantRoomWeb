using Microsoft.AspNetCore.Components.Server.Circuits;
using VacantRoomWeb;
using VacantRoomWeb.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<VacantRoomService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

//用于处理连接数统计的服务
builder.Services.AddSingleton<ConnectionCounterService>();
builder.Services.AddSingleton<CircuitHandler, ConnectionLoggingCircuitHandler>();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5046); // 明确监听所有网卡 IP 的 HTTP 5046 端口
    options.ListenAnyIP(7152, listen => listen.UseHttps()); // HTTPS

    //  限制最大连接数，避免资源占满导致崩溃
    options.Limits.MaxConcurrentConnections = 20;
    options.Limits.MaxConcurrentUpgradedConnections = 20;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}


//app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
