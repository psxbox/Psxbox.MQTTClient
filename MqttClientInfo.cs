namespace Psxbox.MQTTClient
{
    public struct MqttClientInfo
    {
        public string? Server { get; set; }
        public int Port { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? ClientId { get; set; }

        public readonly bool IsEqual(MqttClientInfo mqttClientInfo)
        {
            return Server == mqttClientInfo.Server &&
                   Port == mqttClientInfo.Port &&
                   UserName == mqttClientInfo.UserName &&
                   Password == mqttClientInfo.Password;
        }
    }
}
