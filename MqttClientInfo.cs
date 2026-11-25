namespace Psxbox.MQTTClient
{
    public struct MqttClientInfo
    {
        public string? Server { get; set; }
        public int Port { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? ClientId { get; set; }

        /// <summary>
        /// true bo'lsa CleanStart=false va <see cref="SessionExpirySeconds"/> bilan ulanadi —
        /// broker uzilish paytidagi QoS 1+ xabarlarni saqlaydi. Barqaror <see cref="ClientId"/> talab qiladi.
        /// </summary>
        public bool PersistentSession { get; set; }

        /// <summary>Persistent session'ning brokerdagi umri (soniya). Faqat <see cref="PersistentSession"/> bilan.</summary>
        public uint SessionExpirySeconds { get; set; }

        public readonly bool IsEqual(MqttClientInfo mqttClientInfo)
        {
            return Server == mqttClientInfo.Server &&
                   Port == mqttClientInfo.Port &&
                   UserName == mqttClientInfo.UserName &&
                   Password == mqttClientInfo.Password &&
                   PersistentSession == mqttClientInfo.PersistentSession &&
                   SessionExpirySeconds == mqttClientInfo.SessionExpirySeconds;
        }
    }
}
