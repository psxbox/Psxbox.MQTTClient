using Microsoft.Extensions.Logging;
using MQTTnet;
using System.Buffers;
using System.Threading.Channels;

namespace Psxbox.MQTTClient;

/// <summary>
/// Lightweight managed MQTT client for MQTTnet 5.x (ManagedClient removed in v5).
/// Provides auto-reconnect with backoff, race-condition-safe connect, and simple API.
/// </summary>
public sealed class MqttAutoReconnectClient : IDisposable
{
    private readonly ILogger? _logger;
    private readonly IMqttClient _client = new MqttClientFactory().CreateMqttClient();
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();

    private MqttClientInfo _info;
    private Task? _reconnectTask;
    private Task? _pendingMessageProcessorTask;
    private volatile bool _disposed;
    private readonly Channel<(string topic, byte[] payload)> _pendingMessages =
        Channel.CreateBounded<(string topic, byte[] payload)>(10_000);


    public event Func<string, byte[], Task>? OnMessage;
    public event Func<Task>? OnConnected;
    public event Func<Task>? OnDisconnected;

    public bool IsConnected => _client.IsConnected;

    public MqttAutoReconnectClient(MqttClientInfo info, ILogger? logger = null)
    {
        _info = info;
        _logger = logger;

        _client.ApplicationMessageReceivedAsync += e =>
        {
            try
            {
                var topic = e.ApplicationMessage.Topic;
                var seq = e.ApplicationMessage.Payload; // ReadOnlySequence<byte>
                byte[] payload;
                if (seq.IsEmpty)
                {
                    payload = Array.Empty<byte>();
                }
                else
                {
                    var len = checked((int)seq.Length);
                    payload = new byte[len];
                    var dest = payload.AsSpan();
                    int written = 0;
                    foreach (var segment in seq)
                    {
                        segment.Span.CopyTo(dest.Slice(written));
                        written += segment.Length;
                    }
                }
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug("<< TOPIC=\"{topic}\" PAYLOAD=\"{payload}\"", topic, BitConverter.ToString(payload));
                }
                var d = OnMessage;
                if (d != null)
                {
                    return d.Invoke(topic, payload);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in ApplicationMessageReceivedAsync");
            }

            return Task.CompletedTask;
        };

        _client.ConnectedAsync += async _ =>
        {
            _logger?.LogInformation("MQTT {server} GA ULANDI!", _info.Server);
            try { await (OnConnected?.Invoke() ?? Task.CompletedTask).ConfigureAwait(false); }
            catch (Exception ex) { _logger?.LogError(ex, "OnConnected handler error"); }
        };

        _client.DisconnectedAsync += async args =>
        {
            _logger?.LogWarning(args.Exception, "MQTT {server} DAN UZILDI!", _info.Server);
            try { await (OnDisconnected?.Invoke() ?? Task.CompletedTask).ConfigureAwait(false); }
            catch (Exception ex) { _logger?.LogError(ex, "OnDisconnected handler error"); }
        };
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _reconnectTask ??= Task.Run(() => ReconnectLoopAsync(_lifetime.Token), _lifetime.Token);
        _pendingMessageProcessorTask ??= Task.Run(() => ProcessPendingMessagesAsync(_lifetime.Token), _lifetime.Token);
        
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _lifetime.CancelAsync().ConfigureAwait(false);
            if (_reconnectTask != null)
            {
                try { await _reconnectTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping MqttAutoReconnectClient");
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Pending message processor started");

        while (!cancellationToken.IsCancellationRequested)
        {
            // TryPeek orqali xabar mavjudligini va ulanishni tekshirish
            if (!_pendingMessages.Reader.TryPeek(out _) || !_client.IsConnected)
            {
                try { await Task.Delay(100, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            // Xabarni o'qish va yuborish
            if (_pendingMessages.Reader.TryRead(out var msg))
            {
                try
                {
                    await PublishAsync(msg.topic, msg.payload, cancellationToken).ConfigureAwait(false);
                    _logger?.LogDebug("Pending message sent TOPIC=\"{topic}\"", msg.topic);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error publishing pending message TOPIC=\"{topic}\"", msg.topic);
                }
            }
        }

        _logger?.LogInformation("Pending message processor stopped");
    }

    public async Task<bool> WaitForConnectedAsync(TimeSpan timeout)
    {
        if (_client.IsConnected) return true;

        using var timeoutCts = new CancellationTokenSource(timeout);
        while (!timeoutCts.IsCancellationRequested)
        {
            if (_client.IsConnected) return true;
            try { await Task.Delay(200, timeoutCts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
        return _client.IsConnected;
    }

    public async Task SubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        await _client.SubscribeAsync(topic, cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger?.LogDebug("Subscribed: {topic}", topic);
    }

    public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        await _client.UnsubscribeAsync(topic, cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger?.LogDebug("Unsubscribed: {topic}", topic);
    }

    public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(payload);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        await _client.PublishStringAsync(topic, payload, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (_logger?.IsEnabled(LogLevel.Debug) == true)
            _logger.LogDebug(">> TOPIC=\"{topic}\" PAYLOAD=\"{payload}\"", topic, payload);
    }

    public async Task PublishAsync(string topic, byte[] payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(payload);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        await _client.PublishBinaryAsync(topic, payload, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (_logger?.IsEnabled(LogLevel.Debug) == true)
            _logger.LogDebug(">> TOPIC=\"{topic}\" PAYLOAD=\"{payload}\"", topic, BitConverter.ToString(payload));
    }

    // Enqueue payload methods to be sent when connected
    public async Task EnqueueMessageAsync(string topic, byte [] payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(payload);
        await _pendingMessages.Writer.WriteAsync((topic, payload), cancellationToken).ConfigureAwait(false);
    }

    public async Task EnqueueMessageAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(payload);
        var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        await _pendingMessages.Writer.WriteAsync((topic, bytes), cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateOptionsAsync(MqttClientInfo info, CancellationToken cancellationToken = default)
    {
        _info = info;
        if (_client.IsConnected)
        {
            try { await _client.DisconnectAsync(cancellationToken: cancellationToken).ConfigureAwait(false); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Error disconnecting before options update"); }
        }
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected) return;

        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client.IsConnected) return; // double-check
            var options = new MqttClientOptionsBuilder()
                .WithClientId(_info.ClientId ?? ("MYGATEWAY_" + Guid.NewGuid().ToString("N").Substring(0, 6)))
                .WithTcpServer(_info.Server, _info.Port)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
                .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                .WithCredentials(_info.UserName, _info.Password)
                .WithTimeout(TimeSpan.FromSeconds(10))
                .WithCleanStart(true)
                .Build();

            await _client.ConnectAsync(options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task ReconnectLoopAsync(CancellationToken token)
    {
        var delay = TimeSpan.FromSeconds(1);
        var maxDelay = TimeSpan.FromSeconds(30);

        while (!token.IsCancellationRequested)
        {
            if (!_client.IsConnected)
            {
                try
                {
                    await EnsureConnectedAsync(token).ConfigureAwait(false);
                    delay = TimeSpan.FromSeconds(1); // reset after success
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Reconnect attempt failed");
                    try { await Task.Delay(delay, token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                    var nextMs = Math.Min((int)delay.TotalMilliseconds * 2, (int)maxDelay.TotalMilliseconds);
                    delay = TimeSpan.FromMilliseconds(nextMs);
                }
            }

            try { await Task.Delay(1000, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _lifetime.Cancel();
            _reconnectTask?.Wait(3000);
            _client.Dispose();
            _connectLock.Dispose();
        }
        catch { /* ignore */ }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }
}
