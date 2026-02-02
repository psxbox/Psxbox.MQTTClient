using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using System.Buffers;

namespace Psxbox.MQTTClient;

[Obsolete("This class is deprecated, please use MqttAutoReconnectClient instead.")]
public partial class MqttManagedClient : IDisposable
{
    private MqttClientInfo _mqttClientInfo;
    private readonly ILogger? _logger;
    private readonly IMqttClient _mqttClient = new MqttClientFactory().CreateMqttClient();
    private bool _disposedValue;

    public event Func<string, byte[], Task>? OnMessage;

    public event Func<Task>? OnConnected;
    public event Func<Task>? OnDisconnected;

    public IMqttClient Client => _mqttClient;
    public MqttClientInfo ClientInfo => _mqttClientInfo;

    public MqttManagedClient(IConfiguration configuration, ILogger? logger = null)
    {
        MqttClientInfo mqttClientInfo = new()
        {
            Server = configuration["MqttBroker:Server"],
            Port = int.Parse(configuration["MqttBroker:Port"] ?? "0"),
            UserName = configuration["MqttBroker:UserName"],
            Password = configuration["MqttBroker:Password"],
            ClientId = configuration["MqttBroker:ClientId"]
        };

        _mqttClientInfo = mqttClientInfo;
        _logger = logger;

        SetupMqttClientHandlers();
    }

    private void SetupMqttClientHandlers()
    {
        _mqttClient.ConnectedAsync += MqttClient_ConnectedAsync;
        _mqttClient.DisconnectedAsync += MqttClient_DisconnectedAsync;

        _mqttClient.ApplicationMessageReceivedAsync += (han) =>
        {
            try
            {
                var topic = han.ApplicationMessage.Topic;
                var data = han.ApplicationMessage.Payload.ToArray();

                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                    _logger.LogDebug("<< TOPIC=\"{topic}\" PAYLOAD=\"{payload}\"", topic, BitConverter.ToString(data));

                if (OnMessage?.GetInvocationList().Length > 0)
                {
                    return OnMessage.Invoke(topic!, data);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in ApplicationMessageReceivedAsync");
            }

            return Task.CompletedTask;
        };
    }

    public MqttManagedClient(MqttClientInfo mqttClientInfo, ILogger? logger = null)
    {
        _mqttClientInfo = mqttClientInfo;
        _logger = logger;

        SetupMqttClientHandlers();
    }

    public MqttManagedClient(ILogger? logger = null)
    {
        _logger = logger;

        SetupMqttClientHandlers();
    }

    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private TaskCompletionSource<bool>? _connectionTaskCompletionSource;

    public async Task<bool> WaitForConnection(int waitTimeMs)
    {
        if (_mqttClient.IsConnected)
        {
            return true;
        }

        await _connectionSemaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_mqttClient.IsConnected)
            {
                return true;
            }

            _connectionTaskCompletionSource = new TaskCompletionSource<bool>();

            await ConnectMqttClientAsync().ConfigureAwait(false);

            var completedTask = await Task.WhenAny(_connectionTaskCompletionSource.Task, Task.Delay(waitTimeMs)).ConfigureAwait(false);

            if (completedTask == _connectionTaskCompletionSource.Task)
            {
                return _connectionTaskCompletionSource.Task.Result;
            }
            else
            {
                _logger?.LogWarning("Timeout while waiting for connection");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while waiting for connection");
            return false;
        }
        finally
        {
            _connectionTaskCompletionSource = null;
            _connectionSemaphore.Release();
        }
    }

    public bool IsConnected => _mqttClient.IsConnected;

    public async Task SubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (!await WaitForConnection(5000).ConfigureAwait(false))
        {
            throw new Exception("Client not connected");
        }
        
        await _mqttClient.SubscribeAsync(topic, cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger?.LogDebug("Subscribed to topic: {topic}", topic);
    }

    public async Task DisconnectAsync()
    {
        OnDisconnected?.Invoke();

        await _mqttClient.DisconnectAsync().ConfigureAwait(false);
    }

    public async Task UnsubscribeAsync(string topic)
    {
        await _mqttClient.UnsubscribeAsync(topic).ConfigureAwait(false);
        _logger?.LogDebug("Unsubscribed from topic: {topic}", topic);
    }

    public async ValueTask ConnectMqttClientAsync()
    {
        if (IsConnected) return;

        var options = new MqttClientOptionsBuilder()
                .WithClientId(_mqttClientInfo.ClientId)
                .WithTcpServer(_mqttClientInfo.Server, _mqttClientInfo.Port)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
                .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                .WithCredentials(_mqttClientInfo.UserName, _mqttClientInfo.Password)
                .Build();

        await _mqttClient.ConnectAsync(options).ConfigureAwait(false);
    }

    public async Task ConnectMqttClientAsync(MqttClientInfo mqttClientInfo)
    {
        if (_mqttClientInfo.IsEqual(mqttClientInfo))
        {
            await ConnectMqttClientAsync().ConfigureAwait(false);
        }
        else
        {
            _mqttClientInfo = mqttClientInfo;
            await DisconnectAsync().ConfigureAwait(false);
            await ConnectMqttClientAsync().ConfigureAwait(false);
        }
        await ConnectMqttClientAsync().ConfigureAwait(false);
    }

    private Task MqttClient_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
    {
        _logger?.LogInformation("MQTT {server} DAN UZILDI! EXEPTION:{ex}", _mqttClientInfo.Server, arg.Exception);
        OnDisconnected?.Invoke();
        return Task.CompletedTask;
    }

    private Task MqttClient_ConnectedAsync(MqttClientConnectedEventArgs arg)
    {
        _logger?.LogInformation("MQTT {server} GA ULANDI!", _mqttClientInfo.Server);
        OnConnected?.Invoke();

        _connectionTaskCompletionSource?.SetResult(true);

        return Task.CompletedTask;
    }

    public async Task PublishAsync(string topic, byte[] payload)
    {
        if (!await WaitForConnection(5000).ConfigureAwait(false))
        {
            throw new Exception("Client not connected");
        }

        await _mqttClient.PublishBinaryAsync(topic, payload).ConfigureAwait(false);

        if (_logger?.IsEnabled(LogLevel.Debug) == true)
            _logger.LogDebug(">> TOPIC=\"{topic}\" PAYLOAD=\"{payload}\"", topic, BitConverter.ToString(payload));
    }


    public async Task PublishAsync(string topic, string payload)
    {
        if (!await WaitForConnection(5000).ConfigureAwait(false))
        {
            throw new Exception("Client not connected");
        }

        await _mqttClient.PublishStringAsync(topic, payload).ConfigureAwait(false);

        if (_logger?.IsEnabled(LogLevel.Debug) == true)
            _logger?.LogDebug(">> TOPIC=\"{topic}\" PAYLOAD=\"{payload}\"", topic, payload);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                if (_mqttClient is not null)
                {
                    if (_mqttClient.IsConnected)
                        DisconnectAsync().Wait(3000);

                    _mqttClient.ConnectedAsync -= MqttClient_ConnectedAsync;
                    _mqttClient.DisconnectedAsync -= MqttClient_DisconnectedAsync;

                    _mqttClient.Dispose();
                }

                OnMessage = null;
                OnConnected = null;
                OnDisconnected = null;
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        _logger?.LogInformation("Disposing the service");

        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}