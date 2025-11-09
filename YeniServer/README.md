# OlmezServer - Remote Management Server

**YeniAgent** için tasarlanmış profesyonel uzaktan yönetim sunucusu.

## 🎯 Özellikler

### Backend (Tamamlandı)
- ✅ **SQL Server LocalDB** - Embedded database (kurulum gerektirmez)
- ✅ **SignalR WebSocket Hub** - Gerçek zamanlı agent bağlantıları
- ✅ **REST API** - Web UI için tam CRUD endpoints
- ✅ **Lisans Sistemi** - Community (50 cihaz) + Enterprise (sınırsız)
- ✅ **4 Ana Servis**:
  - License Service (key generation, validation)
  - Device Service (registration, status management)
  - User Service (authentication, user management)
  - Command Service (remote command execution)

### Agent Uyumluluğu
- ✅ **YeniAgent 100% uyumlu**
- ✅ SignalR connection handling
- ✅ Command execution pipeline
- ✅ Heartbeat monitoring
- ✅ Device registration & status tracking

## 🏗️ Mimari

```
OlmezServer/
├── Server.Domain/          # Entities + Enums
│   ├── Entities/           # 9 entity (User, Device, Group, License, etc.)
│   └── Enums/              # 4 enum (LicenseEdition, EnterpriseFeature, etc.)
├── Server.Application/     # Business Logic
│   ├── Services/           # 4 service implementation
│   ├── Interfaces/         # Service contracts
│   ├── DTOs/               # Data transfer objects
│   └── Common/             # Result pattern
├── Server.Infrastructure/  # Data Access
│   ├── Data/               # DbContext
│   └── Migrations/         # EF Core migrations
└── Server.Api/             # Web API
    ├── Controllers/        # 4 REST controllers
    ├── Hubs/               # SignalR AgentHub
    └── Program.cs          # App configuration
```

## 🚀 Kurulum

### Gereksinimler
- .NET 8.0 SDK
- SQL Server LocalDB (Windows ile birlikte gelir)

### Adımlar

1. **Projeyi klonlayın:**
```bash
git clone https://github.com/omerolmaz/OlmezAgent.git
cd YeniServer
```

2. **Uygulamayı başlatın:**
```powershell
cd Server.Api
dotnet run
```

3. **Database otomatik oluşturulur** (ilk çalıştırmada)
   - Admin user: `admin` / `Admin123!`
   - Community license aktif

4. **Swagger UI'a erişin:**
```
https://localhost:5001/swagger
```

## 📡 API Endpoints

### Authentication
- `POST /api/users/login` - Kullanıcı girişi

### Devices
- `GET /api/devices` - Tüm cihazları listele
- `GET /api/devices/{id}` - Cihaz detayı
- `POST /api/devices/register` - Cihaz kaydı
- `DELETE /api/devices/{id}` - Cihaz sil

### Commands
- `GET /api/commands/{id}` - Komut detayı
- `GET /api/commands/device/{deviceId}` - Cihaz komutları
- `POST /api/commands/execute` - Komut çalıştır

### Users
- `GET /api/users` - Kullanıcı listesi
- `POST /api/users` - Yeni kullanıcı
- `PUT /api/users/{id}` - Kullanıcı güncelle
- `DELETE /api/users/{id}` - Kullanıcı sil

### License
- `GET /api/license` - Aktif lisans bilgisi
- `POST /api/license/validate` - Lisans doğrula
- `POST /api/license/generate` - Yeni lisans oluştur
- `GET /api/license/capacity` - Kapasite kontrolü

## 🔌 SignalR Hub

**Endpoint:** `wss://localhost:5001/hub/agent`

### Agent Methods
```csharp
// Agent → Server
await hub.InvokeAsync("RegisterDevice", new {
    hostname = "PC001",
    osVersion = "Windows 11",
    agentVersion = "1.0.0"
});

await hub.InvokeAsync("Heartbeat", deviceId);

await hub.InvokeAsync("CommandResult", commandId, "Completed", result);
```

### Server → Agent
```csharp
// Server sends command to agent
hub.SendAsync("ExecuteCommand", new {
    commandId = Guid.NewGuid(),
    commandType = "GetSystemInfo",
    parameters = null
});
```

## 📊 Database Schema

### Core Tables
1. **Users** - Kullanıcı hesapları
2. **Devices** - Agent cihazları
3. **Groups** - Cihaz grupları
4. **Licenses** - Lisans bilgileri
5. **Sessions** - Aktif bağlantılar
6. **Commands** - Komut geçmişi
7. **Events** - Sistem olayları
8. **AuditLogs** - Denetim kayıtları
9. **Files** - Dosya metadata

### Connection String
```json
"Server=(localdb)\\mssqllocaldb;Database=OlmezServer;Trusted_Connection=true;TrustServerCertificate=true"
```

## 🔐 Lisans Sistemi

### Community Edition (Ücretsiz)
- ✅ Maksimum 50 cihaz
- ✅ Temel özellikler
- ❌ Ticari kullanım yasak
- ❌ Enterprise özellikler yok

### Enterprise Edition (Ücretli)
- ✅ Sınırsız cihaz
- ✅ Tüm özellikler
- ✅ Ticari kullanım
- ✅ Multi-user, RBAC, AD entegrasyonu
- ✅ Öncelikli destek

**Lisans Key Format:** `OLMEZ-{EDITION}-{RANDOM}-{CHECKSUM}`

## 🧪 Test

### Swagger ile Test
1. Uygulamayı başlatın: `dotnet run`
2. Swagger UI: `https://localhost:5001/swagger`
3. `/api/users/login` ile giriş yapın
4. Token'ı kopyalayın
5. "Authorize" butonuna tıklayın
6. API'leri test edin

### YeniAgent ile Test
1. YeniAgent'ı derleyin
2. `ConnectionDetails` ayarlarını güncelleyin:
```json
{
  "ServerUrl": "wss://localhost:5001/hub/agent",
  "DeviceId": "your-device-id",
  "Hostname": "TEST-PC"
}
```
3. Agent'ı çalıştırın
4. Swagger'da `/api/devices` ile cihazı görün

## 🔧 Yapılandırma

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=OlmezServer;..."
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

### CORS Policy
Varsayılan: **AllowAll** (geliştirme için)
Prod için `AllowedOrigins` ekleyin.

## 📝 Sıradaki Adımlar

- [ ] Web UI (React/Blazor)
- [ ] JWT Authentication
- [ ] Agent Installer Generator
- [ ] File Upload/Download
- [ ] Real-time Dashboard
- [ ] Email notifications
- [ ] High Availability setup

## 📞 İletişim

**Geliştirici:** Ömer Ölmez  
**Email:** omer.olmez@sitetelekom.com.tr  
**Şirket:** Site Telekom  

## 📄 Lisans

Dual License:
- Community Edition: GPL v3
- Enterprise Edition: Commercial License

Detaylar için: [LICENSE.md](../YeniAgent/LICENSE.md)

---

**Not:** Bu sunucu YeniAgent ile tam uyumludur. Her iki proje de birlikte geliştirilmiştir.
