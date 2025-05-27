using Microsoft.AspNetCore.Components.Server.Circuits;
using VacantRoomWeb;

/// <summary>
/// 用于处理连接数统计和获取客户端IP的核心事件处理器
/// </summary>
public class ConnectionLoggingCircuitHandler : CircuitHandler
{
    private readonly ConnectionCounterService _counter;
    private readonly ClientConnectionTracker _ipTracker;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ConnectionLoggingCircuitHandler(
        ConnectionCounterService counter,
        ClientConnectionTracker ipTracker,
        IHttpContextAccessor httpContextAccessor)
    {
        _counter = counter;
        _ipTracker = ipTracker;
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "未知IP";

        _ipTracker.SetClientIp(circuit.Id, ip);

        var count = _counter.Increment();
        Console.WriteLine($"{DateTime.Now:yyyy/M/d HH:mm:ss} IP: {ip,-16} 已连接 当前连接数：{count}");

        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var ip = _ipTracker.GetClientIp(circuit.Id);

        // 只有记录过 IP（即该连接是我们关注的）才执行移除和递减
        if (ip != null)
        {
            _ipTracker.Remove(circuit.Id);
            var count = _counter.Decrement();
            Console.WriteLine($"{DateTime.Now:yyyy/M/d HH:mm:ss} IP: {ip,-16} 已断开 当前连接数：{count}");
        }
        else
        {
            // 跳过无效断开事件的递减，但仍可记录日志
            Console.WriteLine($"{DateTime.Now:yyyy/M/d HH:mm:ss} IP: 未知IP        忽略断开事件");
        }

        return Task.CompletedTask;
    }


}