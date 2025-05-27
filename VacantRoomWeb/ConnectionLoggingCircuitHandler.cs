using Microsoft.AspNetCore.Components.Server.Circuits;
using VacantRoomWeb;

public class ConnectionLoggingCircuitHandler : CircuitHandler
{
    private readonly ConnectionCounterService _counter;

    public ConnectionLoggingCircuitHandler(ConnectionCounterService counter)
    {
        _counter = counter;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var count = _counter.Increment();
        Console.WriteLine(DateTime.Now + $"用户连接，当前连接数：{count}");
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var count = _counter.Decrement();
        Console.WriteLine(DateTime.Now + $"用户断开，当前连接数：{count}");
        return Task.CompletedTask;
    }
}
