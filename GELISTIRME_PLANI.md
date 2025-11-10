# 🚀 YeniAgent Geliştirme Planı - MeshCentral'ı Geçmek İçin

**Hedef:** YeniAgent'ı MeshCentral'dan daha iyi hale getirmek  
**Başlangıç Puanı:** 65/100 → **Hedef:** 95/100  
**Süre:** 3-6 ay (Aşamalı)

---

## 📊 Mevcut Durum Analizi

### ✅ Güçlü Yönlerimiz (Koruyalım)
1. Modern Clean Architecture
2. Yüksek performans (daha az CPU/RAM)
3. Modüler plugin sistemi
4. Type-safe (C# + TypeScript)
5. Structured logging (Serilog)
6. GDPR compliance
7. Modern UI (React + Tailwind)
8. Test edilebilir kod

### ❌ Kritik Eksiklikler (Öncelik!)
1. **2FA yok** (MeshCentral'da var)
2. **Email notifications yok**
3. **Rate limiting yok**
4. **Multi-platform yok** (sadece Windows)
5. **Intel AMT desteği yok**
6. **Docker support zayıf**
7. **CLI tool yok**
8. **Webhook integration yok**
9. **Mobile app yok**
10. **Process management yok**

---

## 🎯 FAZ 1: Kritik Eksiklikleri Kapatma (2-3 hafta)

### A) Güvenlik ve Kimlik Doğrulama

#### 1. Two-Factor Authentication (2FA) ⭐⭐⭐⭐⭐
**Öncelik:** YÜKSEK  
**Durum:** ❌ Yok

**Özellikler:**
- ✅ TOTP (Time-based OTP) - Google Authenticator, Authy
- ✅ Email-based OTP (6 digit code)
- ✅ SMS OTP (Twilio integration)
- ✅ Backup codes (10 adet one-time codes)
- ✅ QR Code generation
- ✅ Remember device (30 gün)
- ✅ Force 2FA for admins

**Teknik:**
```csharp
// YeniServer/Server.Application/Services/TwoFactorAuthService.cs
- GenerateTotpSecret()
- GenerateQrCode()
- ValidateTotp()
- GenerateBackupCodes()
- SendEmailOtp()
- SendSmsOtp()
```

**Entity:**
```csharp
// Server.Domain/Entities/UserTwoFactor.cs
public class UserTwoFactor {
    public Guid UserId { get; set; }
    public bool IsTotpEnabled { get; set; }
    public string? TotpSecret { get; set; }
    public List<string> BackupCodes { get; set; }
    public bool IsEmailOtpEnabled { get; set; }
    public bool IsSmsOtpEnabled { get; set; }
    public string? PhoneNumber { get; set; }
}
```

---

#### 2. Rate Limiting & Brute Force Protection ⭐⭐⭐⭐⭐
**Öncelik:** YÜKSEK  
**Durum:** ❌ Yok

**Özellikler:**
- ✅ Login attempts limit (5 tries / 15 min)
- ✅ IP-based rate limiting
- ✅ User-based rate limiting
- ✅ Progressive delays
- ✅ Account lockout (30 min after 5 fails)
- ✅ Admin notification
- ✅ IP blacklist/whitelist

**Teknik:**
```csharp
// ASP.NET Core middleware
// Server.Api/Middleware/RateLimitingMiddleware.cs
- AspNetCoreRateLimit NuGet package
- MemoryCache for tracking
- Redis for distributed rate limiting
```

---

#### 3. Advanced RBAC (Role-Based Access Control) ⭐⭐⭐⭐
**Öncelik:** ORTA  
**Durum:** ⚠️ Basic var, geliştirilmeli

**Özellikler:**
- ✅ Predefined roles (Admin, Manager, Operator, Viewer)
- ✅ Custom roles
- ✅ Granular permissions (60+ permission)
- ✅ Device-level permissions
- ✅ Group-level permissions
- ✅ Time-based access (schedule)
- ✅ Approval workflow

**Permissions:**
```
Device.View, Device.Edit, Device.Delete
Device.RemoteDesktop, Device.Terminal, Device.FileAccess
Device.ServiceManagement, Device.SoftwareInstall
Device.Reboot, Device.Shutdown
User.View, User.Create, User.Edit, User.Delete
Group.View, Group.Create, Group.Edit, Group.Delete
Reports.View, Reports.Export
Settings.View, Settings.Edit
Audit.View, Audit.Export
```

---

### B) İletişim ve Bildirimler

#### 4. Email Notification System ⭐⭐⭐⭐⭐
**Öncelik:** YÜKSEK  
**Durum:** ❌ Yok

**Özellikler:**
- ✅ SMTP configuration
- ✅ Email templates (Razor)
- ✅ Alert notifications
  - Device offline > 5 min
  - Agent update available
  - Security alert (AV disabled, Firewall off)
  - Disk space warning (>90%)
  - High CPU/RAM usage
  - Failed login attempts
- ✅ Scheduled reports (daily, weekly, monthly)
- ✅ Email queue (background job)
- ✅ Retry mechanism
- ✅ Unsubscribe link

**Teknik:**
```csharp
// Server.Application/Services/EmailService.cs
- MailKit for SMTP
- Hangfire for background jobs
- Razor templates
```

**Templates:**
```
emails/
├── device-offline.cshtml
├── security-alert.cshtml
├── disk-space-warning.cshtml
├── weekly-report.cshtml
└── login-notification.cshtml
```

---

#### 5. SMS Notification System ⭐⭐⭐
**Öncelik:** DÜŞÜK  
**Durum:** ❌ Yok

**Özellikler:**
- ✅ Twilio integration
- ✅ Critical alerts only
- ✅ SMS templates
- ✅ Cost tracking

---

#### 6. Webhook Integration ⭐⭐⭐⭐
**Öncelik:** ORTA  
**Durum:** ❌ Yok

**Özellikler:**
- ✅ Custom webhooks (POST to URL)
- ✅ Event triggers:
  - Device connected/disconnected
  - Command executed
  - Security alert
  - Software installed/uninstalled
  - User login/logout
- ✅ Webhook templates
- ✅ Retry mechanism
- ✅ Webhook logs
- ✅ Signature validation (HMAC)

**Teknik:**
```csharp
// Server.Application/Services/WebhookService.cs
public class WebhookEvent {
    public string EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public object Payload { get; set; }
    public string Signature { get; set; } // HMAC-SHA256
}
```

---

### C) Yönetim ve Monitoring

#### 7. Process Management ⭐⭐⭐⭐⭐
**Öncelik:** YÜKSEK  
**Durum:** ❌ Yok (MeshCentral'da var)

**Agent'a Eklenecek:**
```csharp
// Agent.Modules/ProcessModule.cs
- getprocesslist - Tüm prosesleri listele
- killprocess - Proses sonlandır
- startprocess - Proses başlat
- processinfo - Proses detayı (CPU, RAM, threads)
- setpriority - Proses önceliği ayarla
```

---

#### 8. Advanced Logging & Monitoring ⭐⭐⭐⭐
**Öncelik:** ORTA  
**Durum:** ⚠️ Basic var, geliştirilmeli

**Özellikler:**
- ✅ Centralized logging (Seq, Elasticsearch)
- ✅ Real-time log streaming
- ✅ Log retention policies
- ✅ Log search and filtering
- ✅ Performance metrics (Prometheus)
- ✅ Health check endpoint (/health)
- ✅ Grafana dashboards

**Teknik:**
```csharp
// Serilog sinks
- Seq (structured logs)
- Elasticsearch (search)
- Application Insights (Azure)
```

---

#### 9. Backup & Recovery ⭐⭐⭐⭐
**Öncelik:** ORTA  
**Durum:** ⚠️ SQL backup var, automated yok

**Özellikler:**
- ✅ Automated SQL backups (daily, weekly)
- ✅ Configuration backup
- ✅ Agent installer backup
- ✅ Backup retention (30 days)
- ✅ One-click restore
- ✅ Backup encryption
- ✅ Backup verification

---

### D) Deployment ve DevOps

#### 10. Docker Support ⭐⭐⭐⭐⭐
**Öncelik:** YÜKSEK  
**Durum:** ❌ Yok

**Deliverables:**
```dockerfile
# YeniServer/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
# Multi-stage build
```

```yaml
# docker-compose.yml
version: '3.8'
services:
  yeniserver:
    build: ./YeniServer
    ports:
      - "5001:443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
  redis:
    image: redis:alpine
```

**Kubernetes:**
```yaml
# k8s/deployment.yaml
# k8s/service.yaml
# k8s/ingress.yaml
```

---

#### 11. CLI Tool (olmezctl) ⭐⭐⭐⭐
**Öncelik:** ORTA  
**Durum:** ❌ Yok (MeshCentral'da var: meshctrl)

**Özellikler:**
```bash
olmezctl login --server https://server.com --username admin
olmezctl devices list
olmezctl devices info <device-id>
olmezctl command exec <device-id> "ipconfig"
olmezctl users add --username john --role operator
olmezctl groups create "Production Servers"
olmezctl backup create
olmezctl logs tail --device <device-id>
```

**Teknik:**
- .NET 8 Console App
- System.CommandLine package
- REST API client

---

#### 12. High Availability (HA) ⭐⭐⭐
**Öncelik:** DÜŞÜK  
**Durum:** ❌ Yok

**Özellikler:**
- ✅ Multi-server support
- ✅ Load balancing (NGINX, HAProxy)
- ✅ SQL Server AlwaysOn
- ✅ Redis for distributed cache
- ✅ Sticky sessions (SignalR)
- ✅ Health checks
- ✅ Failover automation

---

## 🎯 FAZ 2: Özellik Zenginleştirme (4-6 hafta)

### E) Cross-Platform Expansion

#### 13. Linux Agent ⭐⭐⭐⭐⭐
**Öncelik:** ÇOK YÜKSEK  
**Durum:** ❌ Yok (MeshCentral'da var)

**Platform Hedefi:**
- Ubuntu 20.04+, 22.04+
- Debian 11+, 12+
- CentOS/RHEL 8+, 9+
- Fedora 38+
- openSUSE Leap 15+

**Teknik:**
```csharp
// .NET 8 Linux-x64 publish
// Runtime: linux-x64, linux-arm64
// Systemd service integration
```

**Modüller:**
- CoreDiagnostics (tamamı)
- Inventory (SMBIOS hariç)
- RemoteOperations (bash terminal)
- Desktop (X11/Wayland screen capture)
- Security (linux-specific)
- EventLog (syslog)

---

#### 14. macOS Agent ⭐⭐⭐⭐
**Öncelik:** YÜKSEK  
**Durum:** ❌ Yok

**Platform:**
- macOS 12+ (Monterey)
- macOS 13+ (Ventura)
- macOS 14+ (Sonoma)
- Apple Silicon (ARM64) + Intel (x64)

**Teknik:**
```csharp
// .NET 8 osx-x64, osx-arm64
// LaunchAgent for auto-start
```

---

### F) Mobile ve Remote Access

#### 15. Mobile App (iOS + Android) ⭐⭐⭐⭐
**Öncelik:** ORTA  
**Durum:** ❌ Yok (MeshCentral'da Android var)

**Özellikler:**
- Device list & status
- Real-time notifications
- Quick commands (reboot, shutdown)
- Terminal access (mobile keyboard)
- File browser
- View-only remote desktop
- Biometric authentication

**Teknik:**
- React Native veya Flutter
- SignalR client library
- Push notifications (FCM, APNS)

---

#### 16. Desktop App (Windows/Mac/Linux) ⭐⭐⭐
**Öncelik:** DÜŞÜK  
**Durum:** ❌ Yok

**Özellikler:**
- Native tray icon
- Quick access to devices
- Offline mode (read-only)
- Better performance (native)

**Teknik:**
- Electron + React
- veya Avalonia (C# cross-platform)

---

### G) Intel AMT & Hardware Management

#### 17. Intel AMT (vPro) Support ⭐⭐⭐⭐
**Öncelik:** ORTA (Enterprise için önemli)  
**Durum:** ❌ Yok (MeshCentral'ın killer feature'ı)

**Özellikler:**
- AMT discovery (LAN scan)
- AMT activation (CCM/ACM)
- Out-of-band KVM (BIOS level)
- Power management (hardware)
- IDE redirection (ISO mount)
- Serial over LAN (SOL)

**Teknik:**
- WS-Management protocol
- SOAP API calls
- C# WS-Management library

**Not:** Bu çok spesifik ve karmaşık. MeshCentral'ın 10 yıllık tecrübesi var. Alternatif: MeshCommander entegrasyonu.

---

### H) Raporlama ve Analytics

#### 18. Advanced Reporting ⭐⭐⭐⭐
**Öncelik:** ORTA  
**Durum:** ❌ Yok

**Raporlar:**
- Device inventory report
- Software inventory report
- Security compliance report
- Uptime report
- Bandwidth usage report
- User activity report
- Command execution report
- Audit log report

**Export:**
- PDF (professional)
- Excel (.xlsx)
- CSV
- JSON

**Teknik:**
- QuestPDF for PDF generation
- ClosedXML for Excel
- Scheduled reports (Hangfire)

---

#### 19. Dashboard & Analytics ⭐⭐⭐⭐
**Öncelik:** ORTA  
**Durum:** ⚠️ Basic var, geliştirilmeli

**Widgets:**
- Real-time device status
- CPU/RAM/Disk usage charts
- Security alerts
- Recent commands
- Top 10 devices by resource usage
- Geographic map (device locations)
- Trend analysis (7 days, 30 days)

**Teknik:**
- Chart.js / Recharts
- SignalR for real-time updates
- Leaflet for maps

---

### I) Gelişmiş Özellikler

#### 20. Script Repository ⭐⭐⭐⭐
**Öncelik:** ORTA  
**Durum:** ⚠️ Basic var (single script)

**Özellikler:**
- Script library (PowerShell, Bash, JavaScript)
- Script versioning
- Script parameters
- Scheduled script execution
- Script output history
- Community scripts (marketplace)

---

#### 21. Software Repository ⭐⭐⭐⭐
**Öncelik:** ORTA  
**Durum:** ❌ Yok

**Özellikler:**
- Internal software repository
- Approved software catalog
- One-click deployment
- Version management
- Auto-update software
- Chocolatey integration (Windows)
- apt/yum integration (Linux)

---

#### 22. Network Tools ⭐⭐⭐
**Öncelik:** DÜŞÜK  
**Durum:** ⚠️ Basic (WakeOnLan var)

**Agent'a Eklenecek:**
```csharp
- ping - Network connectivity test
- traceroute - Route tracing
- portscan - Port scanning
- speedtest - Bandwidth test
- dnslookup - DNS query
- whois - Domain info
```

---

#### 23. Bulk Operations ⭐⭐⭐⭐⭐
**Öncelik:** YÜKSEK  
**Durum:** ❌ Yok

**Özellikler:**
- Multi-select devices
- Bulk command execution
- Bulk software install/uninstall
- Bulk reboot/shutdown
- Progress tracking
- Rollback on failure

**UI:**
```
☑️ Device 1 (✅ Success)
☑️ Device 2 (⏳ In progress)
☑️ Device 3 (❌ Failed - rollback)
```

---

## 🎯 FAZ 3: Ekstra İnovasyon (2-3 ay)

### J) AI ve Otomasyon

#### 24. AI-Powered Features ⭐⭐⭐⭐⭐
**Öncelik:** YÜKSEK (Fark yaratır!)  
**Durum:** ❌ Yok (MeshCentral'da da yok - İLK OLURUZ!)

**Özellikler:**

**a) AI Assistant (Chatbot)**
- Natural language commands
- "Restart all production servers"
- "Show devices with high CPU usage"
- "Install Chrome on all marketing computers"
- ChatGPT/Claude API integration

**b) Anomaly Detection**
- ML models for normal behavior
- Alert on unusual activity
- Predictive maintenance

**c) Auto-Remediation**
- AI suggests fix for issues
- Auto-apply approved fixes
- Learning from past incidents

**d) Smart Grouping**
- Auto-categorize devices by usage pattern
- Suggest device retirement

**Teknik:**
- Azure OpenAI / OpenAI API
- ML.NET for local models
- TensorFlow.NET

---

#### 25. Automation Workflows ⭐⭐⭐⭐
**Öncelik:** YÜKSEK  
**Durum:** ❌ Yok

**Özellikler:**
- Visual workflow designer (drag-drop)
- Triggers:
  - Time-based (cron)
  - Event-based (device offline)
  - Condition-based (CPU > 80%)
- Actions:
  - Run command
  - Send email
  - Call webhook
  - Execute script
- If/Else logic
- Loops
- Variables

**Example:**
```
IF device.cpu > 80% FOR 5min
THEN
  1. Send email to admin
  2. Run script: "cleanup-temp-files.ps1"
  3. If still high, restart service
```

**Teknik:**
- Workflow engine (Elsa Workflows)
- Visual designer (React Flow)

---

#### 26. Chatops Integration ⭐⭐⭐
**Öncelik:** DÜŞÜK  
**Durum:** ❌ Yok

**Platforms:**
- Slack integration
- Microsoft Teams
- Discord
- Telegram

**Commands:**
```
/olmez devices list
/olmez device <id> status
/olmez device <id> reboot
/olmez alerts
```

---

### K) Compliance ve Security

#### 27. Compliance Reporting ⭐⭐⭐⭐
**Öncelik:** ORTA (Enterprise için önemli)  
**Durum:** ❌ Yok

**Standards:**
- ISO 27001
- SOC 2
- NIST Cybersecurity Framework
- CIS Benchmarks
- GDPR

**Reports:**
- Compliance score (%)
- Non-compliant devices
- Remediation suggestions
- Audit trail

---

#### 28. Vulnerability Scanning ⭐⭐⭐⭐
**Öncelik:** ORTA  
**Durum:** ❌ Yok

**Özellikler:**
- Windows Update status
- Missing patches
- Vulnerable software (CVE database)
- Configuration issues
- Open ports
- Weak passwords

**Integration:**
- Windows Update API
- NVD (National Vulnerability Database)

---

#### 29. Encryption & Data Protection ⭐⭐⭐⭐
**Öncelik:** ORTA  
**Durum:** ⚠️ Basic (TLS var)

**Özellikler:**
- End-to-end encryption (E2EE)
- Database encryption at rest
- File encryption during transfer
- Encrypted backup
- Key rotation
- HSM support (enterprise)

---

### L) Multi-Tenancy & SaaS

#### 30. Multi-Tenant Architecture ⭐⭐⭐⭐⭐
**Öncelik:** YÜKSEK (SaaS için gerekli)  
**Durum:** ❌ Yok

**Özellikler:**
- Tenant isolation (data, users)
- Per-tenant configuration
- Per-tenant branding
- Per-tenant billing
- Tenant admin portal
- Tenant analytics

**Database:**
- Shared database, separate schema
- Row-level security
- Tenant ID in all queries

---

#### 31. Billing & Subscription ⭐⭐⭐⭐
**Öncelik:** ORTA (SaaS için)  
**Durum:** ❌ Yok

**Plans:**
- Free (5 devices)
- Starter ($9/mo, 25 devices)
- Professional ($49/mo, 100 devices)
- Enterprise ($199/mo, unlimited)

**Integrations:**
- Stripe
- PayPal
- Invoice generation
- Usage tracking

---

#### 32. Public API & SDK ⭐⭐⭐⭐⭐
**Öncelik:** YÜKSEK  
**Durum:** ⚠️ REST API var, SDK yok

**SDKs:**
- C# / .NET SDK
- Python SDK
- JavaScript/TypeScript SDK
- Go SDK

**API:**
- OpenAPI 3.0 spec
- API versioning (v1, v2)
- API rate limiting
- API key management
- API documentation (Swagger + custom)

---

## 🎁 BONUS: İnovatif Özellikler (MeshCentral'da YOK)

### 33. AR/VR Remote Support ⭐⭐⭐⭐⭐
**Durum:** ❌ Yok (DÜNYADA İLK!)

**Konsept:**
- AR glasses ile remote support
- Technician sees through user's camera
- Draw on screen (AR overlay)
- 3D object placement
- Voice guidance

**Platform:**
- Microsoft HoloLens
- Apple Vision Pro
- Meta Quest

---

### 34. Blockchain Audit Log ⭐⭐⭐⭐
**Durum:** ❌ Yok (İLK!)

**Konsept:**
- Tamper-proof audit logs
- Blockchain timestamping
- Verifiable command history
- Immutable compliance trail

---

### 35. Quantum-Safe Encryption ⭐⭐⭐⭐
**Durum:** ❌ Yok (GELECEK!)

**Konsept:**
- Post-quantum cryptography
- Future-proof encryption
- NIST PQC algorithms

---

## 📊 Geliştirme Öncelik Matrisi

### 🔴 Kritik (Hemen başla)
1. **2FA** (1 hafta)
2. **Rate Limiting** (3 gün)
3. **Email Notifications** (1 hafta)
4. **Process Management** (3 gün)
5. **Docker Support** (3 gün)
6. **Bulk Operations** (1 hafta)

### 🟠 Yüksek Öncelik (2-4 hafta)
7. Advanced RBAC (1 hafta)
8. Webhook Integration (3 gün)
9. Linux Agent (3 hafta)
10. CLI Tool (1 hafta)
11. AI Assistant (2 hafta)
12. Multi-Tenant (2 hafta)

### 🟡 Orta Öncelik (1-2 ay)
13. macOS Agent
14. Advanced Reporting
15. Script Repository
16. Software Repository
17. Mobile App
18. Compliance Reporting
19. Automation Workflows

### 🟢 Düşük Öncelik (2-3 ay)
20. SMS Notifications
21. Intel AMT
22. Desktop App
23. Network Tools
24. Vulnerability Scanning
25. Chatops

---

## 🎯 Hedef: 6 Ay Sonra

### Puan Karşılaştırması

| Kategori | Şu An | 6 Ay Sonra | MeshCentral |
|----------|-------|------------|-------------|
| **Özellik Zenginliği** | 7/10 | 10/10 | 10/10 |
| **Platform Desteği** | 3/10 | 9/10 | 10/10 |
| **Kod Kalitesi** | 10/10 | 10/10 | 5/10 |
| **Performans** | 9/10 | 10/10 | 7/10 |
| **Güvenlik** | 8/10 | 10/10 | 9/10 |
| **AI & Innovation** | 0/10 | 9/10 | 0/10 |
| **Enterprise** | 6/10 | 10/10 | 8/10 |
| **SaaS Ready** | 2/10 | 10/10 | 3/10 |
| **TOPLAM** | 65/100 | **95/100** | 85/100 |

---

## 💰 Tahmini Maliyet

### Development Time
- Solo developer: 6-9 ay (full-time)
- Small team (3 dev): 3-4 ay
- Full team (5+ dev): 2-3 ay

### Infrastructure
- Dev/Test: $200/mo
- Production: $500-2000/mo (scale'e göre)

---

## 🚀 Başlangıç Adımları

### Hafta 1-2: Kritik Güvenlik
1. ✅ 2FA implementation
2. ✅ Rate limiting
3. ✅ Email service

### Hafta 3-4: Yönetim
4. ✅ Process management
5. ✅ Bulk operations
6. ✅ Docker images

### Hafta 5-6: API & Tools
7. ✅ CLI tool (olmezctl)
8. ✅ Webhook system
9. ✅ Advanced RBAC

### Hafta 7-10: Cross-Platform
10. ✅ Linux agent (beta)
11. ✅ macOS agent (beta)

### Hafta 11-14: AI & Innovation
12. ✅ AI assistant (MVP)
13. ✅ Automation workflows
14. ✅ Advanced reporting

### Hafta 15-20: Mobile & Enterprise
15. ✅ Mobile app (beta)
16. ✅ Multi-tenant
17. ✅ Compliance reporting

### Hafta 21-24: Polish & Launch
18. ✅ Performance optimization
19. ✅ Security audit
20. ✅ Documentation
21. ✅ Beta testing
22. ✅ Public launch

---

## 📈 Başarı Metrikleri

### 6 Ay Sonra Hedefler:
- ✅ 95/100 puan (MeshCentral: 85)
- ✅ 3 platform desteği (Windows, Linux, macOS)
- ✅ 100+ özellik
- ✅ <50ms response time
- ✅ %99.9 uptime
- ✅ 1000+ GitHub stars
- ✅ 100+ production users
- ✅ 10+ contributors

---

## 🎬 Sonuç

Bu planı takip ederseniz:
- ✅ MeshCentral'ın tüm özelliklerini yakalarsınız
- ✅ AI ile fark yaratırsınız
- ✅ Modern mimari ile sürdürülebilirsiniz
- ✅ SaaS olarak satabilirsiniz
- ✅ Enterprise müşteri kazanırsınız

**İLK 6 ÖZELLİK İLE BAŞLAYALIM MI? (2FA, Rate Limit, Email, Process, Docker, Bulk)**

---

**Hazırlayan:** GitHub Copilot  
**Tarih:** 10 Kasım 2025  
**Durum:** Plan Hazır - Implementation Bekliyor! 🚀
