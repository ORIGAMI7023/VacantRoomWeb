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
builder.Services.AddSingleton<ClientConnectionTracker>();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddSingleton<CircuitHandler, ConnectionLoggingCircuitHandler>();

//builder.WebHost.ConfigureKestrel(options =>
//{
//    options.ListenAnyIP(80); 
//    options.ListenAnyIP(443, listen =>
//    {
//        const string pfxPath = @"C:\Users\Administrator\Desktop\scs1750424505694_origami7023.cn_IIS\scs1750424505694_origami7023.cn_server.pfx";
//        const string pfxPwd = "Kn9?cgGxZ9xxXBEf";      

//        listen.UseHttps(pfxPath, pfxPwd);
//    });

//    options.Limits.MaxConcurrentConnections = 20;
//    options.Limits.MaxConcurrentUpgradedConnections = 20;
//});


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}


app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
