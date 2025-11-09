using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace AgentHost;

/// <summary>
/// Windows Service kurulum ve yönetim işlemleri
/// </summary>
public static class ServiceInstaller
{
    private const string ServiceName = "olmezAgent";
    private const string ServiceDisplayName = "olmez Agent";
    private const string ServiceDescription = "olmez - Modern Remote Management Agent";

    public static async Task<int> InstallServiceAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("❌ Windows Service sadece Windows üzerinde desteklenir.");
            return 1;
        }

        if (!IsAdministrator())
        {
            Console.WriteLine("❌ Service kurulumu için Administrator yetkisi gereklidir.");
            Console.WriteLine("   Lütfen uygulamayı 'Yönetici olarak çalıştır' ile başlatın.");
            return 1;
        }

        try
        {
            var exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Process path alınamadı.");
            
            Console.WriteLine($"🔧 {ServiceDisplayName} kuruluyor...");
            Console.WriteLine($"   Çalıştırılabilir: {exePath}");

            // Service zaten var mı kontrol et
            if (await IsServiceInstalledAsync())
            {
                Console.WriteLine("⚠️  Service zaten kurulu. Önce kaldırın: olmez.exe --uninstall-service");
                return 1;
            }

            // sc.exe ile service oluştur
            var createResult = await RunCommandAsync(
                "sc.exe",
                $"create {ServiceName} binPath=\"{exePath}\" start=auto DisplayName=\"{ServiceDisplayName}\""
            );

            if (createResult != 0)
            {
                Console.WriteLine("❌ Service oluşturulamadı.");
                return createResult;
            }

            // Service açıklaması ekle
            await RunCommandAsync(
                "sc.exe",
                $"description {ServiceName} \"{ServiceDescription}\""
            );

            // Service'i başlat
            Console.WriteLine("🚀 Service başlatılıyor...");
            var startResult = await RunCommandAsync("sc.exe", $"start {ServiceName}");

            if (startResult == 0)
            {
                Console.WriteLine("✅ Service başarıyla kuruldu ve başlatıldı!");
                Console.WriteLine($"   Service adı: {ServiceName}");
                Console.WriteLine("   Durum kontrol: sc query olmezAgent");
                Console.WriteLine("   Durdurmak için: sc stop olmezAgent");
            }
            else
            {
                Console.WriteLine("⚠️  Service kuruldu ancak başlatılamadı. Manuel başlatın: sc start olmezAgent");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hata: {ex.Message}");
            return 1;
        }
    }

    public static async Task<int> UninstallServiceAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("❌ Windows Service sadece Windows üzerinde desteklenir.");
            return 1;
        }

        if (!IsAdministrator())
        {
            Console.WriteLine("❌ Service kaldırma için Administrator yetkisi gereklidir.");
            Console.WriteLine("   Lütfen uygulamayı 'Yönetici olarak çalıştır' ile başlatın.");
            return 1;
        }

        try
        {
            Console.WriteLine($"🗑️  {ServiceDisplayName} kaldırılıyor...");

            // Service var mı kontrol et
            if (!await IsServiceInstalledAsync())
            {
                Console.WriteLine("⚠️  Service kurulu değil.");
                return 1;
            }

            // Service'i durdur
            Console.WriteLine("⏹️  Service durduruluyor...");
            await RunCommandAsync("sc.exe", $"stop {ServiceName}");
            await Task.Delay(2000); // Service durması için bekle

            // Service'i sil
            var deleteResult = await RunCommandAsync("sc.exe", $"delete {ServiceName}");

            if (deleteResult == 0)
            {
                Console.WriteLine("✅ Service başarıyla kaldırıldı!");
            }
            else
            {
                Console.WriteLine("❌ Service kaldırılamadı.");
            }

            return deleteResult;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hata: {ex.Message}");
            return 1;
        }
    }

    public static void ShowHelp()
    {
        Console.WriteLine(@"
olmez Agent - Modern Remote Management Agent
===========================================

Kullanım:
  olmez.exe                      Console modunda çalıştır (standalone)
  olmez.exe --install-service    Windows Service olarak kur
  olmez.exe --uninstall-service  Windows Service'i kaldır
  olmez.exe --help               Bu yardımı göster

Service Yönetimi (Administrator gerektirir):
  sc start olmezAgent            Service'i başlat
  sc stop olmezAgent             Service'i durdur
  sc query olmezAgent            Service durumunu kontrol et
  sc config olmezAgent start=auto    Otomatik başlatmayı etkinleştir
  sc config olmezAgent start=demand  Manuel başlatma

Log Dosyaları:
  logs/agent-{Date}.log          Text formatında loglar
  logs/agent-{Date}.json         JSON formatında loglar

Yapılandırma:
  appsettings.json               Ana yapılandırma dosyası
  appsettings.Development.json   Geliştirme ortamı ayarları

Örnekler:
  # Console modunda çalıştır
  olmez.exe

  # Service olarak kur (Administrator)
  olmez.exe --install-service

  # Service'i başlat (Administrator)
  sc start olmezAgent

  # Service'i kaldır (Administrator)
  olmez.exe --uninstall-service

Daha fazla bilgi: https://github.com/omerolmaz/OlmezAgent
");
    }

    private static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static async Task<bool> IsServiceInstalledAsync()
    {
        try
        {
            var result = await RunCommandAsync("sc.exe", $"query {ServiceName}", suppressOutput: true);
            return result == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<int> RunCommandAsync(string fileName, string arguments, bool suppressOutput = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException($"Process başlatılamadı: {fileName} {arguments}");
        }

        if (!suppressOutput)
        {
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (!string.IsNullOrWhiteSpace(output))
                Console.WriteLine(output);
            if (!string.IsNullOrWhiteSpace(error))
                Console.Error.WriteLine(error);
        }

        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
