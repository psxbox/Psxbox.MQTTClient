namespace Psxbox.MQTTClient;

public interface IMqttReconnectClient : IDisposable
{
    event Func<string, byte[], Task>? OnMessage;
    event Func<Task>? OnConnected;
    event Func<Task>? OnDisconnected;

    bool IsConnected { get; }
    int PendingCount { get; }

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SubscribeAsync(string topic, CancellationToken cancellationToken = default);
    Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default);
    Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default);
    Task PublishAsync(string topic, byte[] payload, CancellationToken cancellationToken = default);
    Task EnqueueMessageAsync(string topic, string payload, CancellationToken cancellationToken = default);
    Task EnqueueMessageAsync(string topic, byte[] payload, CancellationToken cancellationToken = default);
    Task<bool> WaitForConnectedAsync(TimeSpan timeout);
    Task<bool> WaitForConnectedAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
