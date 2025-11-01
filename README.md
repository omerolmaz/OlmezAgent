# YeniAgent

## Calistirma
```bash
cd YeniAgent
dotnet build
dotnet run --project AgentHost
```

## Konfigurasyon
- `AgentHost/appsettings.json` dosyasindan `Agent.ServerEndpoint` alanini hedef Mesh sunucusuna gore guncelleyin.
- `EnableJavascriptModules` ileride ClearScript tabanli modul calistirmak icin kullanilacak.

## Modul Haritasi
| Modul | Sorumluluk | Durum |
| --- | --- | --- |
| CoreDiagnosticsModule | ping/status, temel saglik | taslak |
| InventoryModule | donanim/yazilim/envanter toplama | aktif (WMI & Windows Update) |
| SoftwareDistributionModule | yazilim kur/kaldir, patch planlama | aktif (MSI/EXE + schedule) |
| RemoteOperationsModule | uzak konsol, dosya, servis, power | aktif (terminal/dosya) |
| MessagingModule | sohbet, bildirim, WebRTC sinyalleri | aktif (state/ack, WebRTC beklemede) |
| MaintenanceModule | self-update, log toplama | log/versiyon raporu aktif |


## JavaScript K"oprusu
- `AgentHost/scripts/agent.js` uzerinden ClearScript (V8) ile gelen komutlar JS tarafina devredilebilir.
- JS kontrol komutlari: `scriptdeploy` (kod/base64), `scriptlist`, `scriptreload`, `scriptremove`.
- Scriptler `AgentHost/scripts` klasorunde saklanir; publish sirasinda otomatik kopyalanir.
- JS tarafinda `bridge.canHandle(action)` ve `bridge.handle(action, commandJson)` fonksiyonlari tanimlanarak yeni aksiyonlar desteklenebilir.
- Varsayilan ornek WebRTC SDP/ICE ve sohbet mesajlarini ack eder; referans `meshcore.js` fonksiyonlari buraya tasinabilir.
## Sonraki Adimlar
- ClearScript veya Node benzeri runtime baglanti koprusu entegre edilecek.
- Windows Update API, WMI, PDH baglamalari icin altyapi projeleri eklenecek.
- WebRTC icin natif/managed baglantilar hazirlanacak.

## Yayim Talimatlari
- Boyutu minimum tutmak icin framework-bagimli yayinlayin:
  ```bash
  dotnet publish AgentHost -c Release -r win-x64 --self-contained false
  ```
- Bu komut tek dosya (PublishSingleFile) ve trimming sayesinde ~12 MB boyutlu `AgentHost.exe` uretir.
- Hedef makinada .NET 8 Runtime yüklü degilse tek seferlik kurulum gerekir.

