# O'zgarishlar Tarixi

## [3.0.0] - 2026-04-22

### 🗑️ O'chirildi

- Eskirgan `MqttManagedClient` sinfi olib tashlandi

### ✨ Qo'shildi

- `MqttAutoReconnectClient` da kutayotgan xabarlar sonini boshqarish imkoniyati qo'shildi
- MQTT client ulanishida timeout qo'shildi (5 soniyadan 10 soniyagacha oshirildi)
- Kutayotgan xabarlarni yozishni to'xtatish (`CompleteWriting`) client to'xtaganda qo'shildi
- `WithCleanStart` parametri MQTT ulanishida qo'shildi

### 🔄 O'zgartirildi

- `MqttAutoReconnectClient` da xabarlarni qayta ishlash soddalashtiri va bekor qilish boshqaruvi yaxshilandi
- Kutayotgan xabarlar kanali hajmi 10 000 ga oshirildi
- `Microsoft.Extensions.Configuration.Abstractions` va `Microsoft.Extensions.Logging.Abstractions` paketlari `10.0.7` versiyasiga yangilandi

### 🐛 Tuzatildi

- `MqttAutoReconnectClient` da ko'p tarmoqlilik muammolari bartaraf etildi; uzilishlarni boshqarish yaxshilandi
- Kutayotgan xabarlarni asenkron kutish va xatolarni qayta ishlash yaxshilandi
- `StartAsync` metodida obyekt yo'q qilinganligini tekshirish soddalashtiri

---
