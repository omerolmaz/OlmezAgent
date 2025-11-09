using Agent.Abstractions;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Agent.Scripting;

public sealed class JavaScriptRuntime : IAsyncDisposable
{
    private readonly ILogger<JavaScriptRuntime> _logger;
    private readonly object _syncRoot = new();
    private readonly string _scriptsDirectory;
    private V8ScriptEngine _engine;
    private bool _disposed;

    public JavaScriptRuntime(ILogger<JavaScriptRuntime> logger)
    {
        _logger = logger;
        _scriptsDirectory = Path.Combine(AppContext.BaseDirectory, "scripts");
        Directory.CreateDirectory(_scriptsDirectory);
        
        try
        {
            _engine = CreateEngine();
            ReloadAllScripts();
            _logger.LogInformation("JavaScript runtime başarıyla yüklendi");
        }
        catch (TypeLoadException ex)
        {
            _logger.LogInformation("JavaScript devre dışı: V8 runtime yüklenemedi. Visual C++ Redistributable gerekebilir.");
            _logger.LogDebug(ex, "V8 TypeLoadException detayı");
            _engine = null!; // JavaScript özellikleri çalışmayacak
        }
        catch (Exception ex)
        {
            _logger.LogInformation("JavaScript devre dışı: {Message}", ex.Message);
            _logger.LogDebug(ex, "JavaScript engine yükleme hatası detayı");
            _engine = null!;
        }
    }

    public string ScriptsDirectory => _scriptsDirectory;

    public void ReloadDefaultScript()
    {
        lock (_syncRoot)
        {
            if (_disposed || _engine == null) return;
            ReloadAllScripts();
        }
    }

    public void LoadScript(string name, string code)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            if (_engine == null)
            {
                _logger.LogWarning("JavaScript engine yüklü değil. Script yüklenemedi: {Name}", name);
                return;
            }
            var fileName = SanitizeFileName(name);
            var targetPath = Path.Combine(_scriptsDirectory, fileName);
            File.WriteAllText(targetPath, code, Encoding.UTF8);
            ReloadAllScripts();
            _logger.LogInformation("JavaScript script stored & loaded: {Name}", fileName);
        }
    }

    public bool RemoveScript(string name)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            if (_engine == null)
            {
                _logger.LogWarning("JavaScript engine yüklü değil");
                return false;
            }
            var fileName = SanitizeFileName(name);
            var targetPath = Path.Combine(_scriptsDirectory, fileName);
            if (!File.Exists(targetPath))
            {
                return false;
            }

            File.Delete(targetPath);
            ReloadAllScripts();
            _logger.LogInformation("JavaScript script removed: {Name}", fileName);
            return true;
        }
    }

    public string[] ListScripts()
    {
        lock (_syncRoot)
        {
            if (_disposed || _engine == null) return Array.Empty<string>();
            return Directory.EnumerateFiles(_scriptsDirectory, "*.js", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.Equals(name, "agent.js", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
    }

    public string[] ListHandlers()
    {
        lock (_syncRoot)
        {
            if (_disposed || _engine == null) return Array.Empty<string>();
            if (!_engine.Script.HasProperty("bridge")) return Array.Empty<string>();
            dynamic bridge = _engine.Script.bridge;
            try
            {
                if (bridge.list == null)
                {
                    return Array.Empty<string>();
                }

                var result = bridge.list();
                return result switch
                {
                    null => Array.Empty<string>(),
                    string[] arr => arr,
                    object[] objArr => objArr.Select(x => x?.ToString() ?? string.Empty).ToArray(),
                    _ => Array.Empty<string>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JavaScript list() sırasında hata.");
                return Array.Empty<string>();
            }
        }
    }

    public bool CanHandle(string action)
    {
        lock (_syncRoot)
        {
            if (_disposed || _engine == null) return false;
            if (!_engine.Script.HasProperty("bridge")) return false;
            dynamic bridge = _engine.Script.bridge;
            try
            {
                return bridge.canHandle(action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JavaScript canHandle sırasında hata.");
                return false;
            }
        }
    }

    public CommandResult? Execute(AgentCommand command)
    {
        lock (_syncRoot)
        {
            if (_disposed || _engine == null) return null;
            if (!_engine.Script.HasProperty("bridge")) return null;
            dynamic bridge = _engine.Script.bridge;
            try
            {
                var envelope = new
                {
                    action = command.Action,
                    nodeid = command.NodeId,
                    sessionid = command.SessionId,
                    data = JsonDocument.Parse(command.Payload.GetRawText()).RootElement
                };
                var json = JsonSerializer.Serialize(envelope);
                var resultJson = bridge.handle(command.Action, json) as string;
                if (string.IsNullOrWhiteSpace(resultJson))
                {
                    return null;
                }

                var node = JsonNode.Parse(resultJson) as JsonObject;
                if (node == null)
                {
                    return null;
                }

                var payload = node.TryGetPropertyValue("payload", out var payloadNode) && payloadNode is JsonObject obj
                    ? obj
                    : new JsonObject();
                var success = node.TryGetPropertyValue("success", out var successNode) && successNode?.GetValue<bool>() != false;
                var error = node.TryGetPropertyValue("error", out var errorNode) ? errorNode?.GetValue<string>() : null;

                return new CommandResult(
                    command.Action,
                    command.CommandId,
                    command.NodeId,
                    command.SessionId,
                    payload,
                    success,
                    error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JavaScript komutu işlenirken hata oluştu.");
                return null;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_syncRoot)
        {
            if (_disposed) return ValueTask.CompletedTask;
            _engine?.Dispose();
            _disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private V8ScriptEngine CreateEngine()
    {
        var engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableTaskPromiseConversion);
        engine.AddHostType("Console", typeof(Console));
        engine.AddHostObject("log", new Action<string>(message => _logger.LogInformation("JS: {Message}", message)));
        engine.Script.bridge = new ScriptObject();
        return engine;
    }

    private void ReloadAllScripts()
    {
        ResetBridge();
        LoadBridge();
        foreach (var scriptFile in Directory.EnumerateFiles(_scriptsDirectory, "*.js", SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(Path.GetFileName(scriptFile), "agent.js", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var code = File.ReadAllText(scriptFile, Encoding.UTF8);
                ExecuteScript(Path.GetFileName(scriptFile), code);
                _logger.LogInformation("JavaScript plugin yüklendi: {File}", scriptFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JavaScript plugin yüklenirken hata oluştu: {File}", scriptFile);
            }
        }
    }

    private void LoadBridge()
    {
        var scriptPath = Path.Combine(_scriptsDirectory, "agent.js");
        if (!File.Exists(scriptPath))
        {
            _logger.LogWarning("JavaScript bridge scripti bulunamadı: {Path}", scriptPath);
            return;
        }

        try
        {
            var code = File.ReadAllText(scriptPath, Encoding.UTF8);
            ExecuteScript("agent.js", code);
            _logger.LogInformation("JavaScript bridge yüklendi: {Path}", scriptPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JavaScript bridge yüklenirken hata oluştu.");
        }
    }

    private void ExecuteScript(string name, string code)
    {
        var documentInfo = new DocumentInfo(name);
        _engine.Execute(documentInfo, code);
    }

    private void ResetBridge()
    {
        _engine.Dispose();
        _engine = CreateEngine();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(JavaScriptRuntime));
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"script_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.js";
        }

        var clean = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(clean))
        {
            clean = $"script_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.js";
        }

        if (!clean.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            clean += ".js";
        }

        return clean;
    }

    private sealed class ScriptObject
    {
        public bool canHandle(string action) => false;
        public string? handle(string action, string commandJson) => null;
        public object list() => Array.Empty<string>();
    }
}