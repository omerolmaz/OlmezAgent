using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows;

namespace NotificationHelper;

/// <summary>
/// NotificationHelper - Session 1'de çalışan bildirim helper'ı
/// </summary>
public partial class App : Application
{
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private bool _isRunning;
    private readonly string _logFile;
    private string _pipeName = "olmez_notification";
    private ChatWindow? _chatWindow;

    public App()
    {
        _logFile = Path.Combine(Path.GetTempPath(), "NotificationHelper.log");
        Log("NotificationHelper başlatılıyor...");
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Komut satırından pipe name al
        if (e.Args.Length > 0)
        {
            _pipeName = e.Args[0];
            Log($"Pipe name from args: {_pipeName}");
        }

        // Ana pencere yok - sadece sistem tray'de olacak
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        
        Log($"Uygulama başlatıldı. Named Pipe: {_pipeName}");
        
        // Named pipe client olarak bağlan
        ConnectToPipes();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        Log("Uygulama kapatılıyor...");
        _isRunning = false;
        _reader?.Dispose();
        _writer?.Dispose();
    }

    private async void ConnectToPipes()
    {
        _isRunning = true;

        try
        {
            // Connect to command pipe (agent writes, we read)
            var pipeCmd = new NamedPipeClientStream(".", _pipeName + "_cmd", PipeDirection.In, PipeOptions.Asynchronous);
            Log("Connecting to command pipe...");
            await pipeCmd.ConnectAsync(10000);
            Log("Connected to command pipe");
            
            // Connect to response pipe (we write, agent reads)
            var pipeResp = new NamedPipeClientStream(".", _pipeName + "_resp", PipeDirection.Out, PipeOptions.Asynchronous);
            Log("Connecting to response pipe...");
            await pipeResp.ConnectAsync(10000);
            Log("Connected to response pipe");

            _reader = new StreamReader(pipeCmd);
            _writer = new StreamWriter(pipeResp) { AutoFlush = true };

            Log("Pipes connected successfully!");

            // Read commands
            while (_isRunning && pipeCmd.IsConnected)
            {
                var command = await _reader.ReadLineAsync();
                if (string.IsNullOrEmpty(command))
                    break;

                Log($"Komut alındı: {command}");
                
                if (command == "PING")
                {
                    await _writer.WriteLineAsync("PONG");
                }
                else
                {
                    ProcessCommand(command);
                }
            }

            Log("Agent bağlantısı kesildi.");
        }
        catch (Exception ex)
        {
            Log($"Pipe connection error: {ex.Message}");
        }
        finally
        {
            _reader?.Dispose();
            _writer?.Dispose();
        }
    }

    private void ProcessCommand(string command)
    {
        try
        {
            var parts = command.Split(':', 2);
            if (parts.Length < 2)
            {
                Log($"Geçersiz komut formatı: {command}");
                return;
            }

            var commandType = parts[0].ToUpperInvariant();
            var data = parts[1];

            switch (commandType)
            {
                case "TOAST":
                case "NOTIFY":
                    HandleToast(data);
                    break;

                case "MESSAGEBOX":
                    HandleMessageBox(data);
                    break;

                case "CHAT":
                    HandleChat(data);
                    break;

                case "SHUTDOWN":
                    Log("Kapatma komutu alındı.");
                    Dispatcher.Invoke(() => Shutdown());
                    break;

                default:
                    Log($"Bilinmeyen komut: {commandType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"Komut işleme hatası: {ex.Message}");
        }
    }

    private void HandleToast(string data)
    {
        // Format: title|message|type
        var parts = data.Split('|');
        var title = parts.Length > 0 ? parts[0] : "Bildirim";
        var message = parts.Length > 1 ? parts[1] : "";
        var typeStr = parts.Length > 2 ? parts[2] : "Info";

        var type = Enum.TryParse<NotificationType>(typeStr, true, out var parsed)
            ? parsed
            : NotificationType.Info;

        Log($"Toast gösteriliyor: {title} - {message}");
        ToastManager.ShowToast(title, message, type);
    }

    private void HandleMessageBox(string data)
    {
        // Format: title|message|buttons
        var parts = data.Split('|');
        var title = parts.Length > 0 ? parts[0] : "Mesaj";
        var message = parts.Length > 1 ? parts[1] : "";
        var buttons = parts.Length > 2 ? parts[2] : "OK";

        MessageBoxButton buttonType = buttons.ToUpperInvariant() switch
        {
            "OKCANCEL" => MessageBoxButton.OKCancel,
            "YESNO" => MessageBoxButton.YesNo,
            "YESNOCANCEL" => MessageBoxButton.YesNoCancel,
            _ => MessageBoxButton.OK
        };

        Log($"MessageBox gösteriliyor: {title}");
        
        Dispatcher.Invoke(() =>
        {
            var result = MessageBox.Show(message, title, buttonType, MessageBoxImage.Information);
            Log($"MessageBox sonucu: {result}");
        });
    }

    private void HandleChat(string data)
    {
        // Format: sender|message
        var parts = data.Split('|', 2);
        var sender = parts.Length > 0 ? parts[0] : "Server";
        var message = parts.Length > 1 ? parts[1] : "";

        Log($"Chat mesajı: {sender} - {message}");

        Dispatcher.Invoke(() =>
        {
            if (_chatWindow == null || !_chatWindow.IsLoaded)
            {
                // Chat penceresi yoksa oluştur
                _chatWindow = new ChatWindow(message, sender, SendChatResponse);
                _chatWindow.Show();
            }
            else
            {
                // Varsa mesajı ekle ve göster
                _chatWindow.AddMessage(message, sender, false);
                if (!_chatWindow.IsVisible)
                    _chatWindow.Show();
                _chatWindow.Activate();
            }
        });
    }

    private async void SendChatResponse(string message)
    {
        try
        {
            // Agent'a geri gönder
            await _writer?.WriteLineAsync($"CHAT_RESPONSE:{message}")!;
            Log($"Chat yanıtı gönderildi: {message}");
        }
        catch (Exception ex)
        {
            Log($"Chat yanıt gönderme hatası: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        try
        {
            var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}";
            File.AppendAllText(_logFile, logMessage + Environment.NewLine);
        }
        catch
        {
            // Ignore log errors
        }
    }
}

