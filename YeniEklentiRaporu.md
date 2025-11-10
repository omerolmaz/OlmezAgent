# Yeni* Bileşenleri Eklenti ve Uyumsuzluk Raporu

## 1. Bağımlılık Envanteri

### YeniWeb (Vite + React)
- Uygulama bağımlılıkları: axios ^1.13.1, lucide-react ^0.552.0, react ^19.1.1, react-dom ^19.1.1, react-router-dom ^7.9.5, recharts ^3.3.0, socket.io-client ^4.8.1, zustand ^5.0.8 (YeniWeb/package.json:13).
- Geliştirme bağımlılıkları: @eslint/js ^9.36.0, @types/node ^24.9.2, @types/react ^19.1.16, @types/react-dom ^19.1.9, @vitejs/plugin-react ^5.0.4, autoprefixer ^10.4.21, eslint ^9.36.0, eslint-plugin-react-hooks ^5.2.0, eslint-plugin-react-refresh ^0.4.22, globals ^16.4.0, postcss ^8.5.6, tailwindcss ^3.4.18, typescript ~5.9.3, typescript-eslint ^8.45.0, vite (rolldown-vite@7.1.14) (YeniWeb/package.json:23).
- Build override: vite bağımlılığı rolldown varyantı ile zorlanıyor (YeniWeb/package.json:39).

### YeniAgent (.NET 8)
- Agent.Abstractions: Microsoft.Extensions.Logging.Abstractions 8.0.1 (YeniAgent/Agent.Abstractions/Agent.Abstractions.csproj:10).
- Agent.Modules: System.Management 8.0.0, System.ServiceProcess.ServiceController 8.0.0, System.Drawing.Common 8.0.0, System.Windows.Extensions 8.0.0 (YeniAgent/Agent.Modules/Agent.Modules.csproj:9).
- Agent.Scripting: Microsoft.ClearScript.V8 7.4.3 (YeniAgent/Agent.Scripting/Agent.Scripting.csproj:8).
- Agent.Transport: Microsoft.AspNetCore.SignalR.Client 9.0.10 (YeniAgent/Agent.Transport/Agent.Transport.csproj:8).
- AgentHost: Microsoft.Extensions.Hosting 8.0.1, Microsoft.Extensions.Hosting.WindowsServices 8.0.1, Serilog 4.1.0, Serilog.Extensions.Hosting 8.0.0, Serilog.Sinks.Console 6.0.0, Serilog.Sinks.File 6.0.0, Serilog.Formatting.Compact 3.0.0, Serilog.Enrichers.Environment 3.0.1, Serilog.Enrichers.Thread 4.0.0 (YeniAgent/AgentHost/AgentHost.csproj:26).
- IconGenerator: System.Drawing.Common 8.0.0 (YeniAgent/IconGenerator/IconGenerator.csproj:11).

### YeniServer (.NET 8)
- Server.Api: Microsoft.AspNetCore.OpenApi 8.0.21, Microsoft.AspNetCore.SignalR 1.2.0, Microsoft.EntityFrameworkCore.Design 9.0.10, Swashbuckle.AspNetCore 6.6.2 (YeniServer/Server.Api/Server.Api.csproj:10).
- Server.Application: Microsoft.Extensions.Configuration.Abstractions 9.0.10, System.DirectoryServices 9.0.10, System.DirectoryServices.AccountManagement 9.0.10 (YeniServer/Server.Application/Server.Application.csproj:9).
- Server.Infrastructure: Microsoft.EntityFrameworkCore.SqlServer 9.0.10, Microsoft.EntityFrameworkCore.Tools 9.0.10 (YeniServer/Server.Infrastructure/Server.Infrastructure.csproj:10).

## 2. Özellik Uyumsuzlukları

### 2.1 Agent -> Server Eksikleri
1. **Privacy bar / GDPR kontrolleri** – Agent tarafında PrivacyModule gizlilik çubuğunu açma/kapatma ve veri ihracı sunuyor (YeniAgent/README.md:118). Server kodunda yalnızca privacybarshow / privacybarhide komut sabitleri yer alıyor (YeniServer/Server.Domain/Constants/AgentCommands.cs:119), fakat Server.Api/Controllers altında bu komutları tetikleyecek bir controller bulunmadığı için özellik uzaktan çağrılamıyor.
2. **Dosya izleme oturumları** – Agent FileMonitoringModule gerçek zamanlı dosya değişikliklerini başlat/durdur komutlarıyla sağlıyor (YeniAgent/README.md:121). Server tarafında komut sabitleri tanımlı (startfilemonitor, stopfilemonitor, getfilechanges, listmonitors) (YeniServer/Server.Domain/Constants/AgentCommands.cs:69), ancak SessionsController yalnızca desktop ve console türleri için endpoint sağlıyor (YeniServer/Server.Api/Controllers/SessionsController.cs:32; YeniServer/Server.Api/Controllers/SessionsController.cs:199), bu yüzden file monitor oturumları hiç açılmıyor.
3. **Self-update / bakım komutları** – Agent MaintenanceModule self-update, log toplama ve yeniden kurulum komutlarına sahip (YeniAgent/README.md:123). Server sabitleri gentupdate, gentupdateex, downloadfile, einstall, log gibi komutları listelese de (YeniServer/Server.Domain/Constants/AgentCommands.cs:98), API katmanında bu komutlar için endpoint yok; dolayısıyla agent kendini güncelleme/günlük gönderme isteklerini server üzerinden alamıyor.
4. **JavaScriptBridge script yönetimi** – Agent, ClearScript V8 köprüsü üzerinden scriptdeploy, scriptlist, scriptreload, scriptremove komutlarını destekliyor (YeniAgent/README.md:139). YeniServer/Server.Api/Controllers dizinindeki controller listesinde (ActiveDirectoryController.cs, AgentInstallerController.cs, CommandsController.cs vb.) script yönetimine dair hiçbir uç yok; ayrıca YeniServer/Server.Domain/Constants/AgentCommands.cs dosyasında da bu komutlar tanımlı değil. Bu nedenle agent tarafındaki dinamik script altyapısı server katmanından tetiklenemiyor.

### 2.2 Server -> Web Eksikleri
1. **Kullanıcı mesajlaşması / bildirimler** – Server API, agent mesajı, messagebox, toast ve sohbet göndermek için ayrı endpoint’ler sunuyor (YeniServer/Server.Api/Controllers/MessagingController.cs:33). Web tarafında ise YeniWeb/src/services ve YeniWeb/src/pages dizinlerinde messaging/chat adında bir servis veya sayfa bulunmuyor; mevcut dosyalar ActiveDirectory, bulkOperations, sessions vb. ile sınırlı, dolayısıyla bu API’ler UI’dan tetiklenemiyor.
2. **İleri dosya işlemleri (zip, openurl, Wake-on-LAN)** – Server’daki RemoteOpsController zip, unzip, openurl, wakeonlan gibi sekiz farklı komutu HTTP’ye açıyor (YeniServer/Server.Api/Controllers/RemoteOpsController.cs:78; YeniServer/Server.Api/Controllers/RemoteOpsController.cs:96; YeniServer/Server.Api/Controllers/RemoteOpsController.cs:105). Web istemcisinin remoteOps servisi ise yalnızca dizin listeleme/silme/oluşturma, güç ve servis yönetimi ile pano senkronizasyonunu sarmalıyor (YeniWeb/src/services/remoteOps.service.ts:13), bu nedenle zip arşivleme, link açma veya WOL senaryoları web arayüzünden kullanılamıyor.
3. **Event log izlemede eksik aksiyonlar** – Server API’si event log geçmişini getirmenin yanında izlemeyi başlatma/durdurma ve log temizleme uçları sağlıyor (YeniServer/Server.Api/Controllers/EventLogController.cs:69; YeniServer/Server.Api/Controllers/EventLogController.cs:78; YeniServer/Server.Api/Controllers/EventLogController.cs:87). Web tarafındaki eventLogsService yalnızca get/security/application/system çağrılarını sarmalıyor (YeniWeb/src/services/eventLogs.service.ts:39) ve monitor/clear uçlarına hiç dokunmuyor.
4. **Görsel mesajlaşma bileşeni eksik** – Server’ın MessagingController’ı gentmsg, messagebox, 
otify, 	oast ve chat uçlarını sunuyor (YeniServer/Server.Api/Controllers/MessagingController.cs:33). Ancak YeniWeb/src/pages altındaki sayfa listesinde (BulkOperations.tsx, Commands.tsx, Devices.tsx vb.) chat/messaging ekranı yok; bu nedenle kullanıcıya mesaj gönderme veya sohbet açma gibi özellikler arayüzde bulunmuyor.

## 3. Güncel Durum Notları
- [x] Server tarafında Privacy, FileMonitor, Maintenance ve Scripts controller'ları eklendi (Server.Api/Controllers).
- [x] Agent komut sabitleri Script kategorisiyle güncellendi (Server.Domain/Constants/AgentCommands.cs).
- [x] Web arayüzünde Messaging sekmesi, gelişmiş RemoteOps işlemleri ve EventLog monitor/clear aksiyonları tamamlandı (YeniWeb/src/pages/DeviceDetail.tsx).
- [x] Yeni servis katmanları eklendi: messaging.service.ts, genişletilmiş emoteOps.service.ts, eventLogs.service.ts.
- [x] i18n çevirileri yeni metinlerle güncellendi.
