# 🔄 Yazılım Yönetimi Karşılaştırması: TacticalRMM vs YeniAgent

**Tarih:** 10 Kasım 2025  
**Analiz Kapsamı:** Software Install, Uninstall, List, Refresh işlemleri  
**Kapsam:** Agent + Server + Web (Full Stack)

---

## 📊 EXECUTIVE SUMMARY

| Özellik | TacticalRMM | YeniAgent | Durum |
|---------|-------------|-----------|-------|
| **Yazılım Listeleme** | ✅ Tam | ✅ Tam | ✅ **EŞIT** |
| **Yazılım Kaldırma** | ✅ Tam | ❌ YOK | ⚠️ **EKSİK** |
| **Yazılım Kurulum** | ✅ Chocolatey | ❌ YOK | ⚠️ **EKSİK** |
| **Chocolatey Integration** | ✅ Var | ❌ YOK | ⚠️ **EKSİK** |
| **Refresh Software List** | ✅ Var | ⚠️ Kısmi | ⚠️ **EKSİK** |
| **Custom Uninstall String** | ✅ Var | ❌ YOK | ⚠️ **EKSİK** |
| **Timeout Control** | ✅ Var | ❌ YOK | ⚠️ **EKSİK** |
| **Run As User** | ✅ Var | ❌ YOK | ⚠️ **EKSİK** |
| **Pending Actions** | ✅ Var | ❌ YOK | ⚠️ **EKSİK** |

**Kritik Fark:** TacticalRMM'de yazılım yönetimi **TAM OTOMASYONLU**, YeniAgent'ta sadece **listeleme** var!

---

## 🎯 TACTICAL RMM MİMARİSİ

### 1. Agent (Go) - Software Management

#### 📦 Dosya Yapısı
```
rmmagent-develop/agent/
├── software_windows_amd64.go  # Software listeleme
├── choco_windows.go            # Chocolatey kurulum/yönetim
└── rpc.go                      # NATS RPC handlers
```

#### 🔍 Software Listeleme (software_windows_amd64.go)

```go
func (a *Agent) GetInstalledSoftware() []trmm.WinSoftwareList {
    ret := make([]trmm.WinSoftwareList, 0)
    
    // Windows API kullanarak registry'den software listesi
    sw, err := wapi.InstalledSoftwareList()
    if err != nil {
        return ret
    }
    
    for _, s := range sw {
        t := s.InstallDate
        ret = append(ret, trmm.WinSoftwareList{
            Name:        CleanString(s.Name()),
            Version:     CleanString(s.Version()),
            Publisher:   CleanString(s.Publisher),
            InstallDate: fmt.Sprintf("%02d-%d-%02d", t.Year(), t.Month(), t.Day()),
            Size:        ByteCountSI(s.EstimatedSize * 1024),
            Source:      CleanString(s.InstallSource),
            Location:    CleanString(s.InstallLocation),
            Uninstall:   CleanString(s.UninstallString),  // ⭐ KRITIK!
        })
    }
    return ret
}
```

**Önemli Detaylar:**
- ✅ `go-win64api` library kullanıyor (registry access)
- ✅ `UninstallString` kaydediliyor (kaldırma için kritik!)
- ✅ `ByteCountSI` ile boyut formatlanıyor
- ✅ `CleanString` ile sanitization

#### 🍫 Chocolatey Integration (choco_windows.go)

```go
// Chocolatey kurulumu
func (a *Agent) InstallChoco() {
    var result rmm.ChocoInstalled
    result.AgentID = a.AgentID
    result.Installed = false
    
    // Chocolatey install script'ini indir
    r, err := rClient.R().Get("https://chocolatey.org/install.ps1")
    if err != nil {
        a.rClient.R().SetBody(result).Post("/api/v3/choco/")
        return
    }
    
    // PowerShell ile çalıştır
    _, _, exitcode, err := a.RunScript(string(r.Body()), "powershell", []string{}, 900, false, []string{}, false, "")
    if exitcode == 0 {
        result.Installed = true
    }
    
    a.rClient.R().SetBody(result).Post("/api/v3/choco/")
}

// Chocolatey ile yazılım kurulumu
func (a *Agent) InstallWithChoco(name string) (string, error) {
    // choco.exe binary'sini bul
    var exe string
    choco, err := exec.LookPath("choco.exe")
    if err != nil || choco == "" {
        exe = filepath.Join(os.Getenv("PROGRAMDATA"), `chocolatey\bin\choco.exe`)
    } else {
        exe = choco
    }
    
    // choco install PACKAGE --yes --force --force-dependencies --no-progress
    out, err := CMD(exe, []string{
        "install", name, 
        "--yes", 
        "--force", 
        "--force-dependencies", 
        "--no-progress"
    }, 1200, false)
    
    if err != nil {
        return err.Error(), err
    }
    return out[0], nil
}
```

**Özellikler:**
- ✅ Otomatik Chocolatey kurulumu
- ✅ 1200 saniye timeout (20 dakika)
- ✅ Force install parametreleri
- ✅ PROGRAMDATA fallback

---

### 2. Server (Django/Python) - API Backend

#### 📦 Dosya Yapısı
```
api/tacticalrmm/software/
├── models.py           # Database models
├── views.py            # API endpoints
├── serializers.py      # JSON serialization
├── urls.py             # URL routing
├── permissions.py      # Permission checks
└── chocos.json         # Chocolatey package list cache
```

#### 🗄️ Database Models (models.py)

```python
class ChocoSoftware(models.Model):
    """Chocolatey paket listesi cache"""
    chocos = models.JSONField()  # Tüm chocolatey paketleri
    added = models.DateTimeField(auto_now_add=True)
    
    def __str__(self):
        return f"{len(self.chocos)} - {self.added}"

class InstalledSoftware(models.Model):
    """Agent'tan gelen software listesi"""
    objects = PermissionQuerySet.as_manager()
    
    id = models.BigAutoField(primary_key=True)
    agent = models.ForeignKey(Agent, on_delete=models.CASCADE)
    software = models.JSONField()  # Software array as JSON
    
    def __str__(self):
        return self.agent.hostname
```

**Özellikler:**
- ✅ JSONField kullanımı (PostgreSQL native)
- ✅ Permission-based queryset
- ✅ Chocolatey cache mekanizması

#### 🔌 API Endpoints (views.py)

```python
# 1. CHOCOLATEY PACKAGE LİSTESİ
@api_view(["GET"])
def chocos(request):
    """Chocolatey paket listesi"""
    chocos = ChocoSoftware.objects.last()
    if not chocos:
        return Response({})
    return Response(chocos.chocos)

# 2. SOFTWARE LİSTELEME VE KURULUM
class GetSoftware(APIView):
    permission_classes = [IsAuthenticated, SoftwarePerms]
    
    def get(self, request, agent_id=None):
        """Software listesini getir"""
        if agent_id:
            agent = get_object_or_404(Agent, agent_id=agent_id)
            try:
                software = InstalledSoftware.objects.filter(agent=agent).get()
                return Response(InstalledSoftwareSerializer(software).data)
            except Exception:
                return Response([])
        else:
            # Tüm agent'ların software'leri
            software = InstalledSoftware.objects.filter_by_role(request.user)
            return Response(InstalledSoftwareSerializer(software, many=True).data)
    
    def post(self, request, agent_id):
        """CHOCOLATEY ile software kurulumu"""
        agent = get_object_or_404(Agent, agent_id=agent_id)
        
        if agent.is_posix:
            return notify_error(f"Not available for {agent.plat}")
        
        name = request.data["name"]
        
        # Pending action oluştur
        action = PendingAction.objects.create(
            agent=agent,
            action_type=PAAction.CHOCO_INSTALL,
            details={"name": name, "output": None, "installed": False}
        )
        
        # Agent'a NATS mesajı gönder
        nats_data = {
            "func": "installwithchoco",
            "choco_prog_name": name,
            "pending_action_pk": action.pk
        }
        
        r = asyncio.run(agent.nats_cmd(nats_data, timeout=2))
        if r != "ok":
            action.delete()
            return notify_error("Unable to contact the agent")
        
        return Response(
            f"{name} will be installed shortly on {agent.hostname}. "
            "Check the Pending Actions menu to see the status/output"
        )
    
    def put(self, request, agent_id):
        """SOFTWARE LİSTESİNİ REFRESH ET"""
        agent = get_object_or_404(Agent, agent_id=agent_id)
        
        if agent.is_posix:
            return notify_error(f"Not available for {agent.plat}")
        
        # Agent'a software listesi isteği gönder
        r = asyncio.run(agent.nats_cmd({"func": "softwarelist"}, timeout=15))
        if r in ("timeout", "natsdown"):
            return notify_error("Unable to contact the agent")
        
        # Database'e kaydet veya güncelle
        if not InstalledSoftware.objects.filter(agent=agent).exists():
            InstalledSoftware(agent=agent, software=r).save()
        else:
            s = agent.installedsoftware_set.first()
            s.software = r
            s.save(update_fields=["software"])
        
        return Response("ok")

# 3. SOFTWARE KALDIRMA
class UninstallSoftware(APIView):
    permission_classes = [IsAuthenticated, UninstallSoftwarePerms]
    
    def post(self, request, agent_id):
        """Software kaldırma"""
        agent = get_object_or_404(Agent, agent_id=agent_id)
        
        if agent.is_posix:
            return notify_error(f"Not available for {agent.plat}")
        
        name = request.data["name"]
        uninstall_cmd = request.data["command"]
        
        # Tactical Agent'ı kaldırmayı engelle
        if all(i in uninstall_cmd.lower() for i in ("tacticalagent", "unins")):
            return notify_error(
                "The Tactical RMM Agent cannot be uninstalled from here."
            )
        
        # Command execution data
        data = {
            "func": "rawcmd",
            "timeout": request.data["timeout"],
            "payload": {
                "command": uninstall_cmd,
                "shell": "cmd",
            },
            "run_as_user": request.data["run_as_user"]
        }
        
        # History kaydı
        hist = AgentHistory.objects.create(
            agent=agent,
            type=AgentHistoryType.CMD_RUN,
            command=uninstall_cmd,
            username=request.user.username[:50]
        )
        data["id"] = hist.pk
        
        # Audit log
        AuditLog.audit_raw_command(
            username=request.user.username,
            agent=agent,
            cmd=uninstall_cmd,
            shell="cmd",
            debug_info={"ip": request._client_ip}
        )
        
        # Async olarak çalıştır (wait=False)
        asyncio.run(agent.nats_cmd(data, wait=False))
        
        return Response(f"{name} will now be uninstalled on {agent.hostname}.")
```

**Kritik Özellikler:**
- ✅ **Pending Actions** mekanizması
- ✅ **Audit Log** her işlem için
- ✅ **Agent History** tracking
- ✅ **NATS** async messaging
- ✅ **Timeout** kontrolü (default 1800 saniye)
- ✅ **Run as user** desteği
- ✅ **Security check** (Tactical Agent'ı kaldırmayı engelle)

#### 🛣️ URL Routing (urls.py)

```python
urlpatterns = [
    path("chocos/", views.chocos),                              # GET /software/chocos/
    path("", views.GetSoftware.as_view()),                      # GET /software/
    path("<agent:agent_id>/", views.GetSoftware.as_view()),     # GET/POST/PUT /software/{agent_id}/
    path("<agent:agent_id>/uninstall/", views.UninstallSoftware.as_view()),  # POST /software/{agent_id}/uninstall/
]
```

---

### 3. Web (Vue.js + Quasar) - Frontend

#### 📦 Dosya Yapısı
```
tacticalrmm-web-develop/src/
├── api/software.js                          # API client
├── components/agents/SoftwareTab.vue        # Ana tab
├── components/software/
│   ├── InstallSoftware.vue                 # Chocolatey kurulum modal
│   └── UninstallSoftware.vue               # Kaldırma modal
```

#### 🔌 API Client (api/software.js)

```javascript
const baseUrl = "/software";

// Chocolatey paket listesi
export async function fetchChocosSoftware(params = {}) {
  const { data } = await axios.get(`${baseUrl}/chocos/`, { params: params });
  return data;
}

// Agent'ın yüklü software'leri
export async function fetchAgentSoftware(agent_id, params = {}) {
  const { data } = await axios.get(`${baseUrl}/${agent_id}/`, { params: params });
  return data.software;
}

// Chocolatey ile kurulum
export async function installAgentSoftware(agent_id, payload) {
  const { data } = await axios.post(`${baseUrl}/${agent_id}/`, payload);
  return data;
}

// Software kaldırma
export async function uninstallAgentSoftware(agent_id, payload) {
  const { data } = await axios.post(`${baseUrl}/${agent_id}/uninstall/`, payload);
  return data;
}

// Software listesini refresh et
export async function refreshAgentSoftware(agent_id) {
  const { data } = await axios.put(`${baseUrl}/${agent_id}/`);
  return data;
}
```

#### 🎨 Software Tab Component (SoftwareTab.vue)

**Özellikler:**
- ✅ **Quasar Table** ile listeleme
- ✅ **Virtual scroll** (performance)
- ✅ **Search/Filter**
- ✅ **Export to Excel**
- ✅ **Install buton** → Chocolatey modal
- ✅ **Uninstall buton** → Her software için
- ✅ **Refresh buton** → Agent'tan güncel liste
- ✅ **Loading state**
- ✅ **Empty state**
- ✅ **Platform check** (sadece Windows)

**Kolonlar:**
```javascript
const columns = [
  { name: "name", label: "Name", field: "name", sortable: true },
  { name: "publisher", label: "Publisher", field: "publisher", sortable: true },
  { name: "install_date", label: "Installed On", field: "install_date", sortable: false },
  { name: "size", label: "Size", field: "size", sortable: false },
  { name: "version", label: "Version", field: "version", sortable: false },
  { name: "uninstall", label: "", field: "uninstall", sortable: false }  // Action button
];
```

#### 🍫 Install Software Modal (InstallSoftware.vue)

**Özellikler:**
- ✅ Chocolatey paket listesi (10,000+ paket)
- ✅ Searchable table
- ✅ Package link → Chocolatey.org
- ✅ Confirm dialog
- ✅ Success notification (5 saniye)
- ✅ Pending Actions mesajı

**Workflow:**
```
1. User clicks "Install Software"
2. Modal opens with Chocolatey packages
3. User searches for package (e.g., "vlc")
4. User clicks "Add" icon
5. Confirm dialog: "Install vlc?"
6. User confirms
7. API call: POST /software/{agent_id}/ {name: "vlc"}
8. Server creates PendingAction
9. Server sends NATS message to agent
10. Agent installs via Chocolatey
11. User sees: "vlc will be installed shortly. Check Pending Actions."
```

#### ❌ Uninstall Software Modal (UninstallSoftware.vue)

**Özellikler:**
- ✅ Uninstall string editable (pre-filled)
- ✅ Timeout control (default 1800s)
- ✅ "Run as user" checkbox
- ✅ Confirm/Cancel buttons

**Pre-processing:**
```javascript
// Eğer MSI uninstaller ise /qn /norestart ekle
initialUninstallString: software.uninstall + 
  (software.uninstall.toLowerCase().includes("msiexec") 
    ? " /qn /norestart" 
    : "")
```

**Workflow:**
```
1. User clicks "Uninstall" button on software row
2. Modal opens with uninstall command
3. User can edit command, set timeout, check "run as user"
4. User clicks "Uninstall"
5. API call: POST /software/{agent_id}/uninstall/ {
     name: "VLC",
     command: "msiexec /x {GUID} /qn /norestart",
     timeout: 1800,
     run_as_user: false
   }
6. Server logs to AgentHistory and AuditLog
7. Server sends rawcmd via NATS
8. Agent executes uninstall command
9. User sees: "VLC will now be uninstalled on HOSTNAME"
```

---

## 🆚 YENİAGENT MİMARİSİ (MEVCUT DURUM)

### 1. Agent (C# / .NET 8) - Software Management

#### 📦 Dosya Yapısı
```
YeniAgent/Agent.Modules/
└── InventoryModule.cs        # Sadece listeleme
```

#### 🔍 Software Listeleme (InventoryModule.cs)

```csharp
private async Task HandleInstalledSoftwareAsync(AgentCommand command, AgentContext context)
{
    var software = await Task.Run(GetInstalledSoftware).ConfigureAwait(false);
    var payload = new JsonObject
    {
        ["software"] = software
    };
    await SendSuccessAsync(command, context, payload).ConfigureAwait(false);
}

private static JsonArray GetInstalledSoftware()
{
    var arr = new JsonArray();
    const string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    
    // Registry'den software listesi
    foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using var key = baseKey.OpenSubKey(uninstallKey);
        
        if (key == null) continue;
        
        foreach (var subkeyName in key.GetSubKeyNames())
        {
            using var subkey = key.OpenSubKey(subkeyName);
            if (subkey == null) continue;
            
            var displayName = subkey.GetValue("DisplayName") as string;
            if (string.IsNullOrWhiteSpace(displayName)) continue;
            
            var obj = new JsonObject
            {
                ["name"] = displayName,
                ["publisher"] = subkey.GetValue("Publisher") as string ?? "",
                ["version"] = subkey.GetValue("DisplayVersion") as string ?? "",
                ["installLocation"] = subkey.GetValue("InstallLocation") as string ?? "",
                ["uninstallString"] = subkey.GetValue("UninstallString") as string ?? "",  // ⭐ VAR!
                
                // Son eklenen
                ["installDate"] = ParseInstallDate(subkey.GetValue("InstallDate") as string),
                ["sizeInBytes"] = ParseSize(subkey.GetValue("EstimatedSize"))
            };
            arr.Add(obj);
        }
    }
    return arr;
}
```

**Mevcut Durum:**
- ✅ Registry'den listeleme
- ✅ 32-bit ve 64-bit desteği
- ✅ `UninstallString` toplanıyor (ama kullanılmıyor!)
- ✅ InstallDate ve Size parsing eklendi
- ❌ **Kaldırma fonksiyonu YOK**
- ❌ **Kurulum fonksiyonu YOK**
- ❌ **Chocolatey integration YOK**

---

### 2. Server (ASP.NET Core 8 / C#) - API Backend

#### 📦 Dosya Yapısı
```
YeniServer/
├── Server.Domain/Entities/InstalledSoftware.cs     # Entity
├── Server.Application/Services/InventoryService.cs # Business logic
├── Server.Api/Middleware/AgentWebSocketMiddleware.cs  # WebSocket handler
└── Server.Api/Controllers/InventoryController.cs   # HTTP API (if exists)
```

#### 🗄️ Database Entity (InstalledSoftware.cs)

```csharp
public class InstalledSoftware
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public string UninstallString { get; set; } = string.Empty;  // ⭐ VAR ama kullanılmıyor!
    
    // Yeni eklenen
    public DateTime? InstallDate { get; set; }
    public long? SizeInBytes { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**Mevcut Durum:**
- ✅ Entity Framework Core
- ✅ `UninstallString` field'ı var
- ✅ InstallDate ve SizeInBytes eklendi
- ❌ **Kaldırma endpoint'i YOK**
- ❌ **Kurulum endpoint'i YOK**

#### 🔌 WebSocket Handler (AgentWebSocketMiddleware.cs)

```csharp
// Software inventory kaydetme
case "getinstalledsoftware":
    var softwareList = new List<Server.Domain.Entities.InstalledSoftware>();
    
    if (payload.software is JsonArray softwareArray)
    {
        foreach (var item in softwareArray)
        {
            if (item is not JsonObject softObj) continue;
            
            var soft = new Server.Domain.Entities.InstalledSoftware
            {
                DeviceId = command.DeviceId,
                Name = softObj["name"]?.GetValue<string>() ?? "",
                Publisher = softObj["publisher"]?.GetValue<string>() ?? "",
                Version = softObj["version"]?.GetValue<string>() ?? "",
                InstallLocation = softObj["installLocation"]?.GetValue<string>() ?? "",
                UninstallString = softObj["uninstallString"]?.GetValue<string>() ?? "",
                
                // Yeni alanlar
                InstallDate = ParseInstallDate(softObj["installDate"]),
                SizeInBytes = ParseSizeInBytes(softObj["sizeInBytes"]),
                
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            softwareList.Add(soft);
        }
    }
    
    if (softwareList.Count > 0)
    {
        await inventoryService.SaveInstalledSoftwareAsync(command.DeviceId, softwareList);
    }
    break;
```

**Mevcut Durum:**
- ✅ WebSocket ile data reception
- ✅ Bulk insert
- ❌ **Uninstall endpoint YOK**
- ❌ **Install endpoint YOK**
- ❌ **Refresh endpoint YOK**

#### ⚠️ Eksik API Endpoints

```csharp
// ❌ BUNLAR YOK!

// Software kaldırma endpoint'i
[HttpPost("software/{deviceId}/uninstall")]
public async Task<IActionResult> UninstallSoftware(Guid deviceId, [FromBody] UninstallRequest request)
{
    // TODO: Implement
    return NotImplemented();
}

// Chocolatey ile kurulum endpoint'i
[HttpPost("software/{deviceId}/install")]
public async Task<IActionResult> InstallSoftware(Guid deviceId, [FromBody] InstallRequest request)
{
    // TODO: Implement
    return NotImplemented();
}

// Software listesini refresh et
[HttpPut("software/{deviceId}/refresh")]
public async Task<IActionResult> RefreshSoftware(Guid deviceId)
{
    // TODO: Implement
    return NotImplemented();
}

// Chocolatey paket listesi
[HttpGet("software/chocolatey/packages")]
public async Task<IActionResult> GetChocoPackages()
{
    // TODO: Implement
    return NotImplemented();
}
```

---

### 3. Web (React + TypeScript) - Frontend

#### 📦 Dosya Yapısı
```
YeniWeb/src/
├── services/software.service.ts         # API client (stub)
├── pages/DeviceDetail.tsx               # Device detay sayfası
└── components/inventory/InventoryOverview.tsx  # Inventory component
```

#### 🔌 API Client (software.service.ts)

```typescript
// MEVCUT KOD (sadece placeholder)
export const softwareService = {
  installSoftware(deviceId: string, payload: InstallPayload) {
    return executeAndTrack(`/api/software/install/${deviceId}`, payload);
  },
  uninstallSoftware(deviceId: string, payload: UninstallPayload) {
    return executeAndTrack(`/api/software/uninstall/${deviceId}`, payload);
  },
  // ...
};
```

**Mevcut Durum:**
- ⚠️ Fonksiyonlar tanımlı ama **backend yok**
- ❌ **UI component YOK**
- ❌ **Software Tab YOK**
- ❌ **Install modal YOK**
- ❌ **Uninstall modal YOK**

---

## 🔥 FARKLAR VE EKSİKLER (DETAYLI)

### 1. ❌ YAZILIM KALDIRMA (UNINSTALL)

| Özellik | TacticalRMM | YeniAgent |
|---------|-------------|-----------|
| **Uninstall String Kullanımı** | ✅ Var | ❌ Yok |
| **Custom Command Edit** | ✅ Var | ❌ Yok |
| **Timeout Control** | ✅ Var (1800s default) | ❌ Yok |
| **Run As User** | ✅ Var | ❌ Yok |
| **MSI Auto-params** | ✅ `/qn /norestart` | ❌ Yok |
| **Security Check** | ✅ Tactical Agent engelleme | ❌ Yok |
| **Audit Logging** | ✅ Var | ❌ Yok |
| **Agent History** | ✅ Var | ❌ Yok |
| **Async Execution** | ✅ NATS async | ❌ Yok |

**Eksik:**
```csharp
// YeniAgent'ta eklenmesi gereken

// Agent.Modules/SoftwareModule.cs
private async Task HandleUninstallSoftwareAsync(AgentCommand command, AgentContext context)
{
    var name = command.Data["name"]?.GetValue<string>();
    var uninstallCmd = command.Data["command"]?.GetValue<string>();
    var timeout = command.Data["timeout"]?.GetValue<int>() ?? 1800;
    var runAsUser = command.Data["runAsUser"]?.GetValue<bool>() ?? false;
    
    // Security check
    if (uninstallCmd.Contains("olmezagent", StringComparison.OrdinalIgnoreCase))
    {
        await SendErrorAsync(command, context, "Cannot uninstall agent from here");
        return;
    }
    
    // Execute uninstall command
    var result = await ExecuteCommandAsync(uninstallCmd, timeout, runAsUser);
    
    await SendSuccessAsync(command, context, new JsonObject
    {
        ["output"] = result.Output,
        ["exitCode"] = result.ExitCode
    });
}
```

---

### 2. ❌ CHOCOLATEY INTEGRATION

| Özellik | TacticalRMM | YeniAgent |
|---------|-------------|-----------|
| **Chocolatey Auto-Install** | ✅ Var | ❌ Yok |
| **Chocolatey Package List** | ✅ 10,000+ cached | ❌ Yok |
| **Package Search** | ✅ Var | ❌ Yok |
| **Install via Choco** | ✅ Var | ❌ Yok |
| **Force Install** | ✅ Var | ❌ Yok |
| **Dependency Resolution** | ✅ --force-dependencies | ❌ Yok |

**Eksik:**
```csharp
// Agent.Modules/ChocolateyModule.cs (YENİ DOSYA)

public sealed class ChocolateyModule : AgentModuleBase
{
    public override string Name => "ChocolateyModule";
    
    public override IReadOnlyCollection<string> SupportedActions => new[]
    {
        "installchoco",
        "installwithchoco",
        "chocolist"
    };
    
    public async Task<bool> InstallChocolatey()
    {
        // 1. Install script'i indir
        var client = new HttpClient();
        var script = await client.GetStringAsync("https://chocolatey.org/install.ps1");
        
        // 2. PowerShell ile çalıştır
        var result = await ExecutePowerShellAsync(script, timeout: 900);
        
        return result.ExitCode == 0;
    }
    
    public async Task<CommandResult> InstallWithChoco(string packageName)
    {
        // choco.exe binary'sini bul
        var chocoPath = FindChocolateyExe();
        
        // choco install PACKAGE --yes --force --force-dependencies --no-progress
        var args = new[]
        {
            "install", packageName,
            "--yes",
            "--force",
            "--force-dependencies",
            "--no-progress"
        };
        
        return await ExecuteCommandAsync(chocoPath, args, timeout: 1200);
    }
    
    private string FindChocolateyExe()
    {
        // 1. PATH'te ara
        var chocoInPath = Environment.GetEnvironmentVariable("PATH")
            ?.Split(';')
            .Select(p => Path.Combine(p, "choco.exe"))
            .FirstOrDefault(File.Exists);
            
        if (chocoInPath != null) return chocoInPath;
        
        // 2. PROGRAMDATA fallback
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, @"chocolatey\bin\choco.exe");
    }
}
```

---

### 3. ❌ PENDING ACTIONS

| Özellik | TacticalRMM | YeniAgent |
|---------|-------------|-----------|
| **Pending Actions Table** | ✅ Var | ❌ Yok |
| **Status Tracking** | ✅ Var | ❌ Yok |
| **Output Storage** | ✅ Var | ❌ Yok |
| **User Notification** | ✅ Var | ❌ Yok |
| **Action History** | ✅ Var | ❌ Yok |

**Eksik:**
```csharp
// Server.Domain/Entities/PendingAction.cs (YENİ DOSYA)

public class PendingAction
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    
    public PendingActionType ActionType { get; set; }  // ChocoInstall, Uninstall, etc.
    public PendingActionStatus Status { get; set; }    // Pending, Running, Completed, Failed
    
    public string Details { get; set; } = "{}";  // JSON details
    public string? Output { get; set; }
    public int? ExitCode { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum PendingActionType
{
    ChocoInstall = 1,
    SoftwareUninstall = 2,
    ScriptExecution = 3
}

public enum PendingActionStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Timeout = 4
}
```

---

### 4. ❌ AUDIT LOGGING

| Özellik | TacticalRMM | YeniAgent |
|---------|-------------|-----------|
| **Command Audit Log** | ✅ Var | ❌ Yok |
| **User Tracking** | ✅ Var | ❌ Yok |
| **IP Tracking** | ✅ Var | ❌ Yok |
| **Agent History** | ✅ Var | ❌ Yok |

**Eksik:**
```csharp
// Server.Domain/Entities/AuditLog.cs (YENİ DOSYA)

public class AuditLog
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public Guid? DeviceId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = "{}";
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Server.Domain/Entities/AgentHistory.cs (YENİ DOSYA)

public class AgentHistory
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public AgentHistoryType Type { get; set; }
    public string Command { get; set; } = string.Empty;
    public string? Output { get; set; }
    public int? ExitCode { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public enum AgentHistoryType
{
    CommandRun = 1,
    ScriptExecution = 2,
    SoftwareInstall = 3,
    SoftwareUninstall = 4
}
```

---

### 5. ❌ WEB UI COMPONENTS

| Component | TacticalRMM | YeniAgent |
|-----------|-------------|-----------|
| **Software Tab** | ✅ Full-featured | ❌ Yok |
| **Install Modal** | ✅ Chocolatey search | ❌ Yok |
| **Uninstall Modal** | ✅ Editable command | ❌ Yok |
| **Refresh Button** | ✅ Var | ❌ Yok |
| **Export Button** | ✅ Excel export | ❌ Yok |
| **Loading States** | ✅ Var | ❌ Yok |
| **Virtual Scroll** | ✅ Performance | ❌ Yok |

**Eksik:**
```typescript
// YeniWeb/src/components/software/SoftwareTab.tsx (YENİ DOSYA)

export function SoftwareTab({ deviceId }: { deviceId: string }) {
  const [software, setSoftware] = useState<Software[]>([]);
  const [loading, setLoading] = useState(false);
  const [showInstallModal, setShowInstallModal] = useState(false);
  
  const refreshSoftware = async () => {
    setLoading(true);
    try {
      await softwareService.refreshSoftware(deviceId);
      const data = await inventoryService.getInstalledSoftware(deviceId);
      setSoftware(data);
    } finally {
      setLoading(false);
    }
  };
  
  const handleUninstall = async (software: Software) => {
    const confirmed = await confirm({
      title: `Uninstall ${software.name}?`,
      message: software.uninstallString
    });
    
    if (!confirmed) return;
    
    await softwareService.uninstallSoftware(deviceId, {
      name: software.name,
      command: software.uninstallString,
      timeout: 1800,
      runAsUser: false
    });
  };
  
  return (
    <div className="software-tab">
      <div className="toolbar">
        <button onClick={refreshSoftware}>Refresh</button>
        <button onClick={() => setShowInstallModal(true)}>Install Software</button>
      </div>
      
      <table>
        <thead>
          <tr>
            <th>Name</th>
            <th>Publisher</th>
            <th>Version</th>
            <th>Installed On</th>
            <th>Size</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {software.map(s => (
            <tr key={s.id}>
              <td>{s.name}</td>
              <td>{s.publisher}</td>
              <td>{s.version}</td>
              <td>{s.installDate}</td>
              <td>{formatBytes(s.sizeInBytes)}</td>
              <td>
                {s.uninstallString && (
                  <button onClick={() => handleUninstall(s)}>
                    Uninstall
                  </button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      
      {showInstallModal && (
        <InstallSoftwareModal
          deviceId={deviceId}
          onClose={() => setShowInstallModal(false)}
        />
      )}
    </div>
  );
}
```

---

## 📋 İMPLEMENTASYON PLANI

### 🎯 Priority 1: Uninstall Software (1 hafta)

#### Agent (C#)
```
✅ 1. SoftwareModule.cs oluştur
✅ 2. HandleUninstallSoftwareAsync implement et
✅ 3. Security check ekle (agent uninstall engelle)
✅ 4. Timeout support ekle
✅ 5. Run as user support ekle
```

#### Server (C#)
```
✅ 1. PendingAction entity ekle
✅ 2. AuditLog entity ekle
✅ 3. AgentHistory entity ekle
✅ 4. UninstallSoftware API endpoint
✅ 5. WebSocket handler güncelle
```

#### Web (React/TypeScript)
```
✅ 1. SoftwareTab component
✅ 2. UninstallSoftwareModal component
✅ 3. API client güncellemeleri
✅ 4. Confirmation dialog
✅ 5. Success/error notifications
```

---

### 🎯 Priority 2: Chocolatey Integration (1.5 hafta)

#### Agent (C#)
```
✅ 1. ChocolateyModule.cs oluştur
✅ 2. InstallChocolatey method
✅ 3. InstallWithChoco method
✅ 4. FindChocolateyExe helper
✅ 5. Timeout ve progress handling
```

#### Server (C#)
```
✅ 1. ChocoPackage entity/cache
✅ 2. InstallSoftware API endpoint
✅ 3. GetChocoPackages API endpoint
✅ 4. Package list cache mekanizması
✅ 5. PendingAction integration
```

#### Web (React/TypeScript)
```
✅ 1. InstallSoftwareModal component
✅ 2. Chocolatey package search
✅ 3. Package link to chocolatey.org
✅ 4. Install confirmation
✅ 5. Pending actions notification
```

---

### 🎯 Priority 3: Refresh & Advanced Features (1 hafta)

```
✅ 1. Refresh software list endpoint
✅ 2. Pending actions UI
✅ 3. Agent history viewer
✅ 4. Audit log viewer
✅ 5. Export to Excel
✅ 6. Virtual scroll (performance)
✅ 7. Platform filtering (Windows only for now)
```

---

## 📊 KARŞILAŞTIRMA TAB LOSU (SON DURUM)

| Kategori | TacticalRMM | YeniAgent | Fark |
|----------|-------------|-----------|------|
| **Software Listeleme** | ✅ | ✅ | ✅ EŞIT |
| **UninstallString** | ✅ Kullanılıyor | ✅ Toplanan ama kullanılmıyor | ⚠️ |
| **Uninstall Feature** | ✅ Full | ❌ YOK | 🔴 **KRİTİK** |
| **Chocolatey** | ✅ Full | ❌ YOK | 🔴 **KRİTİK** |
| **Pending Actions** | ✅ Var | ❌ YOK | 🔴 **KRİTİK** |
| **Audit Logging** | ✅ Var | ❌ YOK | 🟡 **ÖNEMLI** |
| **Agent History** | ✅ Var | ❌ YOK | 🟡 **ÖNEMLI** |
| **Timeout Control** | ✅ Var | ❌ YOK | 🟡 **ÖNEMLI** |
| **Run As User** | ✅ Var | ❌ YOK | 🟡 **ÖNEMLI** |
| **Security Checks** | ✅ Var | ❌ YOK | 🟡 **ÖNEMLI** |
| **Web UI** | ✅ Full-featured | ❌ Minimal | 🔴 **KRİTİK** |

---

## 🎯 ÖNERİLER VE NEXT STEPS

### 🔥 Acil Eklemeler (Bu Sprint)
1. ✅ **Uninstall Software** - En kritik eksik
2. ✅ **PendingAction mekanizması** - Async tracking için
3. ✅ **UninstallSoftwareModal** - UI component

### 🚀 Hızlı Kazançlar (Sonraki Sprint)
1. ✅ **Chocolatey Integration** - Büyük değer
2. ✅ **InstallSoftwareModal** - UI component
3. ✅ **Refresh Software** endpoint

### 📈 İyileştirmeler (Orta Vadeli)
1. ✅ **Audit Logging** - Security ve compliance
2. ✅ **Agent History** - Troubleshooting
3. ✅ **Export to Excel** - Reporting

### 🌟 Bonus Features (Uzun Vadeli)
1. ⭐ **Bulk Operations** - Çoklu uninstall
2. ⭐ **Software Policies** - Auto-remove/install
3. ⭐ **Software Inventory Reports**
4. ⭐ **License Management** - Software lisans tracking
5. ⭐ **Software Update Detection**

---

## 💡 KRİTİK NOTLAR

### ✅ YeniAgent'ın Güçlü Yönleri
1. ✅ Modern stack (C# .NET 8, React TypeScript)
2. ✅ Clean architecture
3. ✅ UninstallString zaten toplanıyor
4. ✅ InstallDate ve SizeInBytes eklendi
5. ✅ WebSocket real-time communication

### ⚠️ YeniAgent'ın Zayıf Yönleri
1. ❌ Uninstall feature yok (TacticalRMM'de var)
2. ❌ Chocolatey integration yok (büyük dezavantaj)
3. ❌ Pending actions tracking yok
4. ❌ Audit logging yok
5. ❌ Web UI minimal (TacticalRMM çok gelişmiş)

### 🎯 Strateji
1. **Hızlı kazanç:** Uninstall feature ekle (1 hafta)
2. **Büyük değer:** Chocolatey ekle (1.5 hafta)
3. **Fark yaratma:** Pending actions + audit log (1 hafta)
4. **Liderlik:** Bulk operations + policies (2 hafta)

**Toplam süre:** ~5-6 hafta ile TacticalRMM'i yakalayıp geçebiliriz!

---

## 🚀 SONUÇ

**TacticalRMM:** Mature, full-featured, production-ready software management  
**YeniAgent:** Modern stack ama **yazılım yönetimi eksik**  

**Aksiyon:** Bu karşılaştırmadaki tüm eksiklikleri 5-6 haftada kapatarak **TacticalRMM'den daha iyi** hale getirebiliriz!

---

**Hazırlayan:** GitHub Copilot  
**Tarih:** 10 Kasım 2025  
**Versiyon:** 1.0  
**Durum:** COMPREHENSIVE ANALYSIS COMPLETE ✅
