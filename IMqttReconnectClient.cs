using MQTTnet.Protocol;

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

    /// <summary>
    /// Berilgan QoS bilan obuna bo'ladi. Default implementatsiya QoS'ni e'tiborsiz qoldiradi
    /// (eski implementatsiyalar va test fake'lari buzilmasligi uchun).
    /// </summary>
    Task SubscribeAsync(string topic, MqttQualityOfServiceLevel qos, CancellationToken cancellationToken = default)
        => SubscribeAsync(topic, cancellationToken);
    Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default);
    Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default);
    Task PublishAsync(string topic, byte[] payload, CancellationToken cancellationToken = default);
    Task EnqueueMessageAsync(string topic, string payload, CancellationToken cancellationToken = default);
    Task EnqueueMessageAsync(string topic, byte[] payload, CancellationToken cancellationToken = default);
    Task<bool> WaitForConnectedAsync(TimeSpan timeout);
    Task<bool> WaitForConnectedAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
