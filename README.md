# Psxbox.MQTTClient

**Psxbox.MQTTClient** - MQTT brokerga avtomatik qayta ulanish va ulanishni boshqarish funksiyalarini ta'minlovchi kutubxona.

## Xususiyatlari

- ?? **Avtomatik qayta ulanish** - ulanish uzilganda avtomatik reconnect
- ?? **Holat monitornigi** - ulanish holatini real-time kuzatish
- ?? **Autentifikatsiya** - username/password bilan xavfsiz ulanish
- ?? **Sozlanuvchi** - `IConfiguration` orqali konfiguratsiya
- ?? **Logging** - `ILogger` integratsiyasi
- ?? **Boshqariluvchan mijoz** - pub/sub operatsiyalari uchun qulay API

## O'rnatish

```bash
dotnet add reference Shared/Psxbox.MQTTClient/Psxbox.MQTTClient.csproj
```

## Bog'liqliklar

- `MQTTnet` (v5.0.1.1416) - MQTT protokol kutubxonasi
- `Microsoft.Extensions.Configuration.Abstractions` (v10.0.0)
- `Microsoft.Extensions.Logging.Abstractions` (v10.0.0)

## Foydalanish

### MqttMangedClient - Asosiy Mijoz

```csharp
using Psxbox.MQTTClient;
using Microsoft.Extensions.Logging;

var clientInfo = new MqttClientInfo
{
    Server = "broker.hivemq.com",
    Port = 1883,
    UserName = "myuser",
    Password = "mypassword",
    ClientId = "gateway-001"
};

var logger = loggerFactory.CreateLogger<MqttMangedClient>();
var client = new MqttMangedClient(clientInfo, logger);

await client.StartAsync();
```

### Subscribe va Xabar Qabul Qilish

```csharp
// Subscribe qilish
await client.SubscribeAsync("sensors/temperature");
await client.SubscribeAsync("sensors/pressure");

// Xabarlarni qabul qilish
client.MessageReceived += (sender, message) =>
{
    string topic = message.Topic;
    string payload = Encoding.UTF8.GetString(message.Payload);
    Console.WriteLine($"Topic: {topic}, Payload: {payload}");
};
```

### Publish - Xabar Yuborish

```csharp
// String xabar yuborish
await client.PublishAsync("actuators/valve", "open");

// Binary ma'lumot yuborish
byte[] data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
await client.PublishAsync("devices/plc/data", data);

// Retain va QoS bilan yuborish
await client.PublishAsync("status/online", "true", retain: true, qos: 1);
```

### MqttAutoReconnectClient - Avtomatik Qayta Ulanish

```csharp
var autoClient = new MqttAutoReconnectClient(clientInfo, logger);

// Ulanish holati o'zgarganda event
autoClient.Connected += (sender, args) =>
{
    Console.WriteLine("MQTT broker bilan ulanish o'rnatildi!");
};

autoClient.Disconnected += (sender, args) =>
{
    Console.WriteLine("MQTT brokerdan uzildi. Qayta ulanish...");
};

await autoClient.StartAsync();
```

## MqttClientInfo Konfiguratsiyasi

```csharp
public struct MqttClientInfo
{
    public string? Server { get; set; }      // MQTT broker manzili
    public int Port { get; set; }            // Port raqami (default: 1883)
    public string? UserName { get; set; }    // Foydalanuvchi nomi
    public string? Password { get; set; }    // Parol
    public string? ClientId { get; set; }    // Yagona mijoz identifikatori
}
```

### appsettings.json orqali konfiguratsiya

```json
{
  "Mqtt": {
    "Server": "broker.hivemq.com",
    "Port": 1883,
    "UserName": "myuser",
    "Password": "mypassword",
    "ClientId": "gateway-001"
  }
}
```

```csharp
var config = configuration.GetSection("Mqtt");
var clientInfo = new MqttClientInfo
{
    Server = config["Server"],
    Port = int.Parse(config["Port"]),
    UserName = config["UserName"],
    Password = config["Password"],
    ClientId = config["ClientId"]
};
```

## Arxitektura

```
MqttMangedClient
    ?
MqttAutoReconnectClient
    ?
MQTTnet.MqttClient
```

## MyGateway Loyihasida Foydalanish

MyGateway tizimida ushbu kutubxona quyidagilar uchun ishlatiladi:

- ?? **Telemetriya yuborish** - sanoat qurilmalaridan o'lchangan ma'lumotlarni cloud ga yuborish
- ?? **Buyruqlarni qabul qilish** - markaziy tizimdan boshqaruv buyruqlarini olish
- ?? **Voqealar xabarnomasi** - real-time hodisalar haqida xabar berish
- ?? **Line type: MQTT** - MQTT transport orqali qurilmalar bilan integratsiya

## Xavfsizlik

- TLS/SSL ulanishlarini qo'llab-quvvatlaydi
- Username/password autentifikatsiya
- Client sertifikatlari (opsional)

## Litsenziya

MIT License
