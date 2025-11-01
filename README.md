# YeniAgent (olmez Agent)

Modern Remote Management Agent - Windows için enterprise-grade uzaktan yönetim aracı.

## Özellikler

### ✅ Temel Özellikler
- **Remote Desktop & Screen Sharing** - Gerçek zamanlı ekran paylaşımı
- **File Management** - Dosya yükleme/indirme/düzenleme
- **Remote Terminal** - PowerShell/CMD komut çalıştırma
- **Service Management** - Windows servis kontrolü
- **Software Distribution** - MSI/EXE kurulum ve kaldırma

### ✅ Güvenlik & İzleme
- **Security Monitoring** - Antivirus, Firewall, Defender, UAC, BitLocker izleme
- **Event Log Collection** - Real-time Windows event monitoring
- **File System Monitoring** - Dosya değişiklik izleme
- **Audit Logging** - Tüm işlemlerin denetim kaydı

### ✅ Enterprise Özellikler
- **GDPR Compliance** - Privacy controls ve data export
- **Granular Permissions** - AgentRights ile detaylı yetkilendirme
- **Health Monitoring** - System metrics ve uptime tracking
- **Structured Logging** - Serilog ile JSON ve text logging

### ✅ Deployment Options
- **Console Mode** - Standalone çalıştırma
- **Windows Service** - Arka plan servisi olarak kurulum
- **Flexible Configuration** - appsettings.json ile kolay yapılandırma

## Kurulum & Kullanım

### Console Mode (Standalone)
```bash
# Build
cd YeniAgent
dotnet build --configuration Release

# Çalıştır
dotnet run --project AgentHost

# veya doğrudan
cd AgentHost/bin/Release/net8.0-windows
olmez.exe
```

### Windows Service Mode
```bash
# Administrator olarak çalıştırın

# Service olarak kur
olmez.exe --install-service

# Service'i başlat
sc start olmezAgent

# Service durumunu kontrol et
sc query olmezAgent

# Service'i durdur
sc stop olmezAgent

# Service'i kaldır
olmez.exe --uninstall-service
```

### Otomatik Başlatma
```bash
# Windows başlangıcında otomatik başlat
sc config olmezAgent start=auto

# Manuel başlatma
sc config olmezAgent start=demand
```

### Yardım
```bash
olmez.exe --help
```

## Konfigurasyon

**Dosya:** `AgentHost/appsettings.json`

```json
{
  "Agent": {
    "ServerEndpoint": "wss://your-server.com:443/agent",
    "NodeId": "node-001",
    "ReconnectInterval": "00:00:05",
    "EnableJavascriptModules": false
  }
}
```

**Ayarlar:**
- `ServerEndpoint` - WebSocket sunucu adresi (wss:// veya ws://)
- `NodeId` - Agent'ın benzersiz kimliği
- `ReconnectInterval` - Bağlantı koptuğunda tekrar deneme süresi
- `EnableJavascriptModules` - ClearScript V8 modül desteği (opsiyonel)

**Log Dosyaları:**
- `logs/agent-{Date}.log` - Text formatında loglar
- `logs/agent-{Date}.json` - JSON formatında structured logs
- Retention: 7 gün (otomatik temizleme)

## Modül Haritası

| Modül | Sorumluluk | Komut Sayısı | Durum |
| --- | --- | --- | --- |
| CoreDiagnosticsModule | ping/status, temel sağlık | 3 | ✅ Aktif |
| HealthCheckModule | Sistem metrikleri, uptime | 3 | ✅ Aktif |
| InventoryModule | Donanım/yazılım/envanter | 5 | ✅ Aktif |
| SoftwareDistributionModule | Yazılım kur/kaldır, patch | 4 | ✅ Aktif |
| RemoteOperationsModule | Uzak konsol, dosya, servis | 10+ | ✅ Aktif |
| DesktopModule | Remote desktop, screen sharing | 5 | ✅ Aktif |
| MessagingModule | Sohbet, bildirim | 4 | ✅ Aktif |
| PrivacyModule | GDPR compliance, data export | 5 | ✅ Aktif |
| SecurityMonitoringModule | Güvenlik durumu izleme | 6 | ✅ Aktif |
| EventLogModule | Windows event log toplama | 7 | ✅ Aktif |
| FileMonitoringModule | Dosya sistemi izleme | 3 | ✅ Aktif |
| AuditModule | Denetim günlüğü | 3 | ✅ Aktif |
| MaintenanceModule | Self-update, log toplama | 4 | ✅ Aktif |
| JavaScriptBridgeModule | ClearScript V8 entegrasyonu | 4 | ✅ Aktif |

**Toplam:** 14 modül, 70+ komut


## JavaScript Köprüsü

**ClearScript V8 Runtime** - Dinamik JavaScript modül desteği

### Özellikler
- `AgentHost/scripts/agent.js` üzerinden ClearScript (V8) ile komutları JavaScript'e devredebilirsiniz
- Scriptler `AgentHost/scripts` klasöründe saklanır
- Publish sırasında otomatik kopyalanır

### Kontrol Komutları
- `scriptdeploy` - Yeni script yükle (kod veya base64)
- `scriptlist` - Kurulu scriptleri listele
- `scriptreload` - Script'i yeniden yükle
- `scriptremove` - Script'i kaldır

### JavaScript API
```javascript
// agent.js içinde
bridge.canHandle = function(action) {
    return action === 'customcommand';
};

bridge.handle = function(action, commandJson) {
    const cmd = JSON.parse(commandJson);
    // İşlem yap
    return JSON.stringify({ success: true });
};
```

### Kullanım Senaryoları
- WebRTC SDP/ICE mesaj işleme
- Custom protokol implementasyonları
- MeshCore.js fonksiyonlarının taşınması
- Dinamik komut uzantıları

## Yayımlama (Publishing)

### Framework-Dependent Deployment (Önerilen)
```bash
# Boyutu minimum tutmak için
dotnet publish AgentHost -c Release -r win-x64 --self-contained false
```
- Çıktı: ~8-12 MB `olmez.exe`
- Hedef makinada .NET 8 Runtime gereklidir

### Self-Contained Deployment
```bash
# .NET Runtime dahil
dotnet publish AgentHost -c Release -r win-x64 --self-contained true
```
- Çıktı: ~60-80 MB (runtime dahil)
- Hedef makinada .NET kurulu olmasına gerek yoktur

### Publish Ayarları
`AgentHost.csproj` içinde:
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <PublishSingleFile>true</PublishSingleFile>
  <PublishTrimmed>false</PublishTrimmed>
  <SelfContained>false</SelfContained>
  <PublishReadyToRun>true</PublishReadyToRun>
</PropertyGroup>
```

## Geliştirme

### Gereksinimler
- .NET 8.0 SDK
- Windows 10/11 veya Windows Server 2019+
- Administrator yetkisi (Service kurulumu için)

### Proje Yapısı
```
YeniAgent/
├── Agent.Abstractions/    # Core interfaces & models
├── Agent.Modules/          # 14 modül implementasyonu
├── Agent.Scripting/        # ClearScript V8 runtime
├── Agent.Transport/        # WebSocket transport layer
├── AgentHost/              # Main executable
│   ├── olmez.ico          # Application icon
│   ├── ServiceInstaller.cs # Service management
│   └── scripts/           # JavaScript modules
├── IconGenerator/          # Icon generator tool
└── md/                     # Documentation
    └── gelismis_ozellikler_raporu.md
```

### Build
```bash
# Debug build
dotnet build

# Release build
dotnet build --configuration Release

# Run tests (eğer varsa)
dotnet test
```

## Dokümantasyon

- **[Gelişmiş Özellikler Raporu](md/gelismis_ozellikler_raporu.md)** - Detaylı özellik listesi ve kullanım örnekleri
- **[Next Agent Mimari](next_agent_mimari.md)** - Mimari tasarım dökümanı
- **[Agent Referans Notları](agent_referans_notlari.md)** - Geliştirme notları

## Lisans

Copyright © 2025 olmez

## Katkıda Bulunma

Pull request'ler kabul edilir. Büyük değişiklikler için lütfen önce bir issue açın.

## Destek

- GitHub Issues: https://github.com/omerolmaz/OlmezAgent/issues
- Email: (contact info)
