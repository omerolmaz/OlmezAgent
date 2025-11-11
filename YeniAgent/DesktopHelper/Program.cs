using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.IO.Pipes;
using System.Runtime.InteropServices;

// DesktopHelper - User session desktop capture helper
// Launched by olmez Service when desktop capture is needed
// Communicates via named pipes

// Setup file logging
var logPath = Path.Combine(Path.GetTempPath(), "DesktopHelper.log");
using var logFile = new StreamWriter(logPath, append: true) { AutoFlush = true };

void Log(string message)
{
    var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
    Console.Error.WriteLine(line);
    logFile.WriteLine(line);
}

// Get pipe name from command line argument
if (args.Length == 0)
{
    Log("ERROR: No pipe name provided");
    return 1;
}

var pipeName = args[0];
Log($"DesktopHelper started - User Session Mode");
Log($"Session: {Environment.UserInteractive}");
Log($"User: {Environment.UserName}");
Log($"Pipe: {pipeName}");
Log($"PID: {Environment.ProcessId}");
Log($"Log file: {logPath}");

try
{
    Log($"Creating pipe clients...");
    // Connect to named pipes
    // Helper reads commands from "cmd" pipe
    using var pipeCmd = new NamedPipeClientStream(".", pipeName + "_cmd", PipeDirection.In);
    // Helper writes responses to "resp" pipe  
    using var pipeResp = new NamedPipeClientStream(".", pipeName + "_resp", PipeDirection.Out);
    
    Log($"Connecting to {pipeName}_cmd...");
    pipeCmd.Connect(10000);
    Log($"Connected to cmd pipe!");
    
    Log($"Connecting to {pipeName}_resp...");
    pipeResp.Connect(10000);
    Log($"Connected to resp pipe!");
    
    Log("Setting up streams...");
    using var reader = new StreamReader(pipeCmd);
    using var writer = new StreamWriter(pipeResp) { AutoFlush = true };
    Log("Streams ready, entering command loop...");
    
    // Simple protocol: read command from pipe, write result to pipe
    string? command;
    while ((command = reader.ReadLine()) != null)
    {
        Log($"Received command: {command}");
        
        var parts = command.Split(':', 2);
        var cmd = parts[0];
        
        if (cmd == "CAPTURE")
        {
            var base64Image = CaptureDesktop();
            writer.WriteLine($"IMAGE:{base64Image}");
            Log("Sent image response");
        }
        else if (cmd == "PING")
        {
            writer.WriteLine("PONG");
            Log("Sent PONG");
        }
        else if (cmd == "MOUSEMOVE" && parts.Length == 2)
        {
            var coords = parts[1].Split(',');
            if (coords.Length == 2 && int.TryParse(coords[0], out var x) && int.TryParse(coords[1], out var y))
            {
                SetCursorPos(x, y);
                writer.WriteLine("ACK");
                Log($"Mouse moved to {x},{y}");
            }
        }
        else if (cmd == "MOUSECLICK" && parts.Length == 2)
        {
            if (int.TryParse(parts[1], out var button))
            {
                MouseClick(button);
                writer.WriteLine("ACK");
                Log($"Mouse clicked button {button}");
            }
        }
        else if (cmd == "MOUSEDOWN" && parts.Length == 2)
        {
            if (int.TryParse(parts[1], out var button))
            {
                MouseDown(button);
                writer.WriteLine("ACK");
                Log($"Mouse down button {button}");
            }
        }
        else if (cmd == "MOUSEUP" && parts.Length == 2)
        {
            if (int.TryParse(parts[1], out var button))
            {
                MouseUp(button);
                writer.WriteLine("ACK");
                Log($"Mouse up button {button}");
            }
        }
        else if (cmd == "KEYDOWN" && parts.Length == 2)
        {
            if (byte.TryParse(parts[1], out var key))
            {
                keybd_event(key, 0, 0, 0);
                writer.WriteLine("ACK");
                Log($"Key down {key}");
            }
        }
        else if (cmd == "KEYUP" && parts.Length == 2)
        {
            if (byte.TryParse(parts[1], out var key))
            {
                keybd_event(key, 0, 1, 0);
                writer.WriteLine("ACK");
                Log($"Key up {key}");
            }
        }
        else if (cmd == "KEYPRESS" && parts.Length == 2)
        {
            if (byte.TryParse(parts[1], out var key))
            {
                keybd_event(key, 0, 0, 0);
                keybd_event(key, 0, 1, 0);
                writer.WriteLine("ACK");
                Log($"Key press {key}");
            }
        }
        else if (cmd == "EXIT")
        {
            Log("EXIT command received, exiting...");
            break;
        }
    }
    
    Log("Command loop ended, closing...");
}
catch (Exception ex)
{
    Log($"ERROR:{ex.GetType().Name}: {ex.Message}");
    Log($"Stack: {ex.StackTrace}");
    return 1;
}

Log("DesktopHelper exiting normally");
return 0;

// Native API imports for mouse and keyboard control
[DllImport("user32.dll")]
static extern bool SetCursorPos(int X, int Y);

[DllImport("user32.dll")]
static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

[DllImport("user32.dll")]
static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
const uint MOUSEEVENTF_LEFTUP = 0x0004;
const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
const uint MOUSEEVENTF_RIGHTUP = 0x0010;
const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

static void MouseClick(int button)
{
    uint downFlag = button switch
    {
        1 => MOUSEEVENTF_RIGHTDOWN,
        2 => MOUSEEVENTF_MIDDLEDOWN,
        _ => MOUSEEVENTF_LEFTDOWN
    };

    uint upFlag = button switch
    {
        1 => MOUSEEVENTF_RIGHTUP,
        2 => MOUSEEVENTF_MIDDLEUP,
        _ => MOUSEEVENTF_LEFTUP
    };

    mouse_event(downFlag, 0, 0, 0, 0);
    mouse_event(upFlag, 0, 0, 0, 0);
}

static void MouseDown(int button)
{
    uint flag = button switch
    {
        1 => MOUSEEVENTF_RIGHTDOWN,
        2 => MOUSEEVENTF_MIDDLEDOWN,
        _ => MOUSEEVENTF_LEFTDOWN
    };
    mouse_event(flag, 0, 0, 0, 0);
}

static void MouseUp(int button)
{
    uint flag = button switch
    {
        1 => MOUSEEVENTF_RIGHTUP,
        2 => MOUSEEVENTF_MIDDLEUP,
        _ => MOUSEEVENTF_LEFTUP
    };
    mouse_event(flag, 0, 0, 0, 0);
}

static string CaptureDesktop()
{
    try
    {
        var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Jpeg);
        return Convert.ToBase64String(ms.ToArray());
    }
    catch (Exception ex)
    {
        return $"ERROR:{ex.Message}";
    }
}
