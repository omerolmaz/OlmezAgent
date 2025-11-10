# YeniAgent - Tüm Özellikler ve Komutlar

Bu dokümanda agent'in desteklediği tüm modüller ve komutlar listelenmiştir.

## 1. ProtocolModule (Protokol Yönetimi)
**Actions:**
- `serverhello` - Server merhaba mesajını işler
- `registered` - Kayıt onay mesajını işler
- `error` - Hata mesajlarını işler

## 2. CoreDiagnosticsModule (Temel Diagnostik)
**Actions:**
- `ping` - Basit ping/pong testi
- `status` - Agent durumu (bağlantı, uptime)
- `agentinfo` - Agent bilgileri (versiyon, platform, OS)
- `versions` - Versiyon detayları
- `connectiondetails` - Bağlantı detayları

## 3. InventoryModule (Envanter ve Sistem Bilgisi)
**Actions:**
- `getfullinventory` - Tam sistem envanteri
- `getinstalledsoftware` - Yüklü yazılımlar
- `getinstalledpatches` - Yüklü yamalar
- `getpendingupdates` - Bekleyen güncellemeler
- `sysinfo` - Sistem bilgisi
- `cpuinfo` - CPU bilgisi
- `netinfo` - Ağ bilgisi
- `smbios` - SMBIOS bilgisi
- `vm` - Sanal makine tespiti
- `wifiscan` - WiFi ağları taraması
- `perfcounters` - Performans sayaçları

## 4. RemoteOperationsModule (Uzaktan İşlemler)
**Actions:**
- `console` - Konsol/terminal erişimi
- `power` - Güç yönetimi (restart, shutdown)
- `service` - Windows servisleri yönetimi
- `ls` - Dosya/klasör listeleme
- `download` - Dosya indirme (agent'ten server'a)
- `upload` - Dosya yükleme (server'dan agent'a)
- `mkdir` - Klasör oluşturma
- `rm` - Dosya/klasör silme
- `zip` - Dosya sıkıştırma
- `unzip` - Dosya açma
- `openurl` - URL açma
- `wallpaper` - Duvar kağıdı değiştirme (TODO)
- `kvmmode` - KVM modu (TODO)
- `wakeonlan` - Wake on LAN
- `clipboardget` - Pano içeriği okuma
- `clipboardset` - Pano içeriği yazma

## 5. DesktopModule (Uzak Masaüstü)
**Actions:**
- `desktopstart` - Uzak masaüstü başlat
- `desktopstop` - Uzak masaüstü durdur
- `desktopframe` - Ekran görüntüsü al
- `desktopmousemove` - Fare hareketi
- `desktopmouseclick` - Fare tıklama
- `desktopmousedown` - Fare basılı tutma
- `desktopmouseup` - Fare bırakma
- `desktopkeydown` - Tuş basma
- `desktopkeyup` - Tuş bırakma
- `desktopkeypress` - Tuşa basma

## 6. FileMonitoringModule (Dosya İzleme)
**Actions:**
- `startfilemonitor` - Dosya izlemeyi başlat
- `stopfilemonitor` - Dosya izlemeyi durdur
- `getfilechanges` - Dosya değişikliklerini al
- `listmonitors` - Aktif monitörleri listele

## 7. SecurityMonitoringModule (Güvenlik İzleme)
**Actions:**
- `getsecuritystatus` - Genel güvenlik durumu
- `getantivirusstatus` - Antivirüs durumu
- `getfirewallstatus` - Firewall durumu
- `getdefenderstatus` - Windows Defender durumu
- `getuacstatus` - UAC durumu
- `getencryptionstatus` - Disk şifreleme durumu

## 8. EventLogModule (Olay Günlükleri)
**Actions:**
- `geteventlogs` - Event log kayıtları
- `getsecurityevents` - Güvenlik olayları
- `getapplicationevents` - Uygulama olayları
- `getsystemevents` - Sistem olayları
- `starteventmonitor` - Event log izlemeyi başlat
- `stopeventmonitor` - Event log izlemeyi durdur
- `cleareventlog` - Event log'u temizle

## 9. SoftwareDistributionModule (Yazılım Dağıtımı)
**Actions:**
- `installsoftware` - Yazılım kurulumu
- `uninstallsoftware` - Yazılım kaldırma
- `installupdates` - Windows güncellemelerini yükle
- `schedulepatch` - Yama planla

## 10. MaintenanceModule (Bakım)
**Actions:**
- `agentupdate` - Agent güncelleme
- `agentupdateex` - Gelişmiş agent güncelleme
- `downloadfile` - Dosya indirme
- `reinstall` - Agent'i yeniden kur
- `log` - Log mesajı gönder
- `versions` - Versiyon bilgisi

## 11. MessagingModule (Mesajlaşma)
**Actions:**
- `agentmsg` - Agent mesajı
- `messagebox` - Windows message box göster
- `notify` - Bildirim göster
- `toast` - Toast bildirimi
- `chat` - Chat mesajı
- `webrtcsdp` - WebRTC SDP
- `webrtcice` - WebRTC ICE

## 12. HealthCheckModule (Sağlık Kontrolü)
**Actions:**
- `health` - Sağlık durumu
- `metrics` - Metrikler
- `uptime` - Çalışma süresi

## 13. PrivacyModule (Gizlilik)
**Actions:**
- `privacybarshow` - Gizlilik çubuğunu göster
- `privacybarhide` - Gizlilik çubuğunu gizle

## 14. AuditModule (Denetim)
**Actions:**
- `getauditlogs` - Denetim kayıtları
- `clearauditlogs` - Denetim kayıtlarını temizle

---

## TOPLAM: 84 Farklı Komut/Action

### Kategoriler:
- **Protokol:** 3 komut
- **Diagnostik:** 5 komut  
- **Envanter:** 11 komut
- **Uzaktan İşlemler:** 16 komut
- **Masaüstü:** 11 komut
- **Dosya İzleme:** 4 komut
- **Güvenlik:** 6 komut
- **Event Log:** 7 komut
- **Yazılım:** 4 komut
- **Bakım:** 6 komut
- **Mesajlaşma:** 7 komut
- **Sağlık:** 3 komut
- **Gizlilik:** 2 komut
- **Denetim:** 2 komut
