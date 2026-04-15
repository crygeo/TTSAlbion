using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using TTSAlbion.Infrastructure;
using TTSAlbion.Interfaces;

namespace TTSAlbion.Services.Audio;

/// <summary>
/// Local bridge sink: converts WAV to PCM and forwards it to a Python Discord bot
/// over localhost. The Python side owns the Discord connection and playback queue.
/// </summary>
public sealed class DiscordAudioSink : IAudioSink, IAsyncDisposable
{
    private const string PythonDependencyInstallCommand = "python -m pip install \"discord.py[voice]>=2.4,<3.0\"";
    private readonly IWavToPcmConverter _converter = new ResamplingWavToPcmConverter();
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private Process? _pythonProcess;
    private ulong _trackedUserId;
    private int _port;
    private string? _pythonScriptPath;
    private string? _pythonExecutable;

    public async Task<DiscordConnectionInfo> StartAsync(
        DiscordBotConfig config,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.Token))
            throw new ArgumentException("El token del bot es obligatorio.", nameof(config));

        if (config.UserId == 0)
            throw new ArgumentException("El UserId a observar es obligatorio.", nameof(config));

        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_pythonProcess is not null && !_pythonProcess.HasExited)
                throw new InvalidOperationException("El bot Python ya está iniciado.");

            _trackedUserId = config.UserId;
            _port = GetBridgePort(config.UserId);
            _pythonExecutable = ResolvePythonExecutable();
            await EnsurePythonDependenciesAsync(_pythonExecutable, ct).ConfigureAwait(false);
            _pythonScriptPath = ResolvePythonScriptPath();

            StartPythonBridgeProcess(config.Token, config.UserId, _port, _pythonScriptPath, _pythonExecutable);
            var status = await WaitForBridgeReadyAsync(ct).ConfigureAwait(false);

            Console.WriteLine($"[DiscordAudioSink] Python bridge ready. Port={_port} User={status.UserName ?? "Unknown"}");
            return new DiscordConnectionInfo(status.UserName ?? "Unknown");
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task SendAsync(byte[] wav, CancellationToken ct = default)
    {
        var pcm = _converter.Convert(wav);
        if (pcm.Length == 0)
            return;

        var response = await SendCommandAsync(
            new BridgeRequest("enqueue_audio", _trackedUserId, pcm.Length),
            pcm,
            ct).ConfigureAwait(false);

        if (!response.Ok)
            throw new InvalidOperationException(response.Error ?? "Python bridge rejected audio.");
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_pythonProcess is null)
                return;

            try
            {
                using var shutdownTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                shutdownTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                await SendCommandAsync(
                    new BridgeRequest("shutdown", _trackedUserId, 0),
                    null,
                    shutdownTimeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DiscordAudioSink] Shutdown command failed: {ex.Message}");
            }

            try
            {
                if (!_pythonProcess.HasExited)
                {
                    using var stopTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    stopTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                    await _pythonProcess.WaitForExitAsync(stopTimeout.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Console.WriteLine("[DiscordAudioSink] Python bridge did not stop in time. Killing process.");
                if (!_pythonProcess.HasExited)
                    _pythonProcess.Kill(entireProcessTree: true);
            }
            catch
            {
                if (!_pythonProcess.HasExited)
                    _pythonProcess.Kill(entireProcessTree: true);
            }
            finally
            {
                try
                {
                    if (!_pythonProcess.HasExited)
                    {
                        using var exitTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        exitTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                        await _pythonProcess.WaitForExitAsync(exitTimeout.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    Console.WriteLine("[DiscordAudioSink] Python bridge still alive after kill timeout.");
                }
                catch
                {
                    /* best-effort */
                }

                _pythonProcess.Dispose();
                _pythonProcess = null;
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _lifecycleLock.Dispose();
    }

    private void StartPythonBridgeProcess(string token, ulong userId, int port, string scriptPath, string pythonExecutable)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "discord-python-bridge.log");

        var psi = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = $"\"{scriptPath}\" --port {port} --token \"{token}\" --user-id {userId} --log-file \"{logPath}\"",
            WorkingDirectory = Path.GetDirectoryName(scriptPath)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = new Process
        {
            StartInfo = psi,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                Console.WriteLine($"[PyBridge] {args.Data}");
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                Console.WriteLine($"[PyBridge:ERR] {args.Data}");
        };
        process.Exited += (_, _) => Console.WriteLine($"[DiscordAudioSink] Python bridge exited code={process.ExitCode}");

        if (!process.Start())
            throw new InvalidOperationException("No se pudo iniciar el bot Python.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _pythonProcess = process;
    }

    private async Task<BridgeResponse> WaitForBridgeReadyAsync(CancellationToken ct)
    {
        var startedAt = DateTime.UtcNow;
        Exception? lastError = null;

        while (DateTime.UtcNow - startedAt < TimeSpan.FromSeconds(30))
        {
            ct.ThrowIfCancellationRequested();

            if (_pythonProcess is { HasExited: true })
                throw new InvalidOperationException($"El bot Python terminó con código {_pythonProcess.ExitCode}.");

            try
            {
                var status = await SendCommandAsync(new BridgeRequest("status", _trackedUserId, 0), null, ct).ConfigureAwait(false);
                if (status.Ok && status.BotReady)
                    return status;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(400, ct).ConfigureAwait(false);
        }

        throw new TimeoutException($"El bot Python no respondió a tiempo. Último error: {lastError?.Message}");
    }

    private async Task<BridgeResponse> SendCommandAsync(
        BridgeRequest request,
        byte[]? payload,
        CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", _port, ct).ConfigureAwait(false);

        await using var stream = client.GetStream();

        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(request, _jsonOptions);
        var lengthBuffer = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, headerBytes.Length);
        await stream.WriteAsync(lengthBuffer, ct).ConfigureAwait(false);
        await stream.WriteAsync(headerBytes, ct).ConfigureAwait(false);

        if (payload is not null && payload.Length > 0)
            await stream.WriteAsync(payload, ct).ConfigureAwait(false);

        await stream.FlushAsync(ct).ConfigureAwait(false);

        await ReadExactlyAsync(stream, lengthBuffer, ct).ConfigureAwait(false);
        var responseHeaderLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
        var responseBytes = new byte[responseHeaderLength];
        await ReadExactlyAsync(stream, responseBytes, ct).ConfigureAwait(false);

        var response = JsonSerializer.Deserialize<BridgeResponse>(responseBytes, _jsonOptions);
        return response ?? new BridgeResponse(false, "Respuesta inválida del bot Python.", false, null);
    }

    private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct).ConfigureAwait(false);
            if (read == 0)
                throw new IOException("Conexión cerrada por el bot Python.");
            offset += read;
        }
    }

    private static string ResolvePythonScriptPath()
    {
        if (TryResolvePythonScriptPath(out var scriptPath, out _))
            return scriptPath;

        throw new FileNotFoundException(
            "No se encontró el script del bot Python. Verifica la carpeta 'Python' junto al ejecutable.");
    }

    private static int GetBridgePort(ulong userId)
        => 39000 + (int)(userId % 1000);

    private static string ResolvePythonExecutable()
    {
        if (TryResolvePythonExecutable(out var pythonExecutable, out _))
            return pythonExecutable;

        throw new FileNotFoundException(
            "No se encontró Python 3. Instálalo y asegúrate de que 'python.exe' esté disponible en PATH.");
    }

    public static bool TryResolvePythonExecutable(out string pythonExecutable, out string? reason)
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "WindowsApps",
                "PythonSoftwareFoundation.Python.3.13_qbz5n2kfra8p0",
                "python.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "WindowsApps",
                "python.exe")
        };

        foreach (var candidate in candidates)
        {
            if (CanRunPython(candidate, out reason))
            {
                pythonExecutable = candidate;
                return true;
            }
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var candidate = Path.Combine(directory.Trim(), "python.exe");
                    if (CanRunPython(candidate, out reason))
                    {
                        pythonExecutable = candidate;
                        return true;
                    }
                }
                catch
                {
                    // Ignore malformed PATH entries and keep searching.
                }
            }
        }

        pythonExecutable = string.Empty;
        reason = "Requiere Python 3 instalado y disponible como 'python.exe'.";
        return false;
    }

    public static bool TryResolvePythonScriptPath(out string scriptPath, out string? reason)
    {
        try
        {
            scriptPath = ExtractEmbeddedPythonScript();
            reason = null;
            return true;
        }
        catch (Exception ex)
        {
            scriptPath = string.Empty;
            reason = $"No se pudo preparar el script del bot Python: {ex.Message}";
            return false;
        }
    }

    private static bool CanRunPython(string candidate, out string? reason)
    {
        if (!File.Exists(candidate))
        {
            reason = null;
            return false;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "-c \"import sys; raise SystemExit(0 if sys.version_info.major == 3 else 1)\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };

            if (!process.Start())
            {
                reason = "No se pudo iniciar Python 3.";
                return false;
            }

            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore kill failures; the candidate is still invalid for our purposes.
                }

                reason = "Python 3 no respondió a tiempo.";
                return false;
            }

            if (process.ExitCode == 0)
            {
                reason = null;
                return true;
            }

            reason = "Requiere Python 3 instalado y disponible como 'python.exe'.";
            return false;
        }
        catch
        {
            reason = null;
            return false;
        }
    }

    private static string ExtractEmbeddedPythonScript()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("discord_bridge_bot.py", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new FileNotFoundException("No se encontró el recurso embebido 'discord_bridge_bot.py'.");

        var extractionDirectory = Path.Combine(Path.GetTempPath(), "TTSAlbion", "PythonBridge");
        Directory.CreateDirectory(extractionDirectory);

        var scriptPath = Path.Combine(extractionDirectory, "discord_bridge_bot.py");
        using var resourceStream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"No se pudo abrir el recurso '{resourceName}'.");
        using var memoryStream = new MemoryStream();
        resourceStream.CopyTo(memoryStream);
        var resourceBytes = memoryStream.ToArray();

        if (File.Exists(scriptPath))
        {
            var currentBytes = File.ReadAllBytes(scriptPath);
            if (currentBytes.AsSpan().SequenceEqual(resourceBytes))
                return scriptPath;
        }

        File.WriteAllBytes(scriptPath, resourceBytes);
        return scriptPath;
    }

    private static async Task EnsurePythonDependenciesAsync(string pythonExecutable, CancellationToken ct)
    {
        if (await ArePythonDependenciesInstalledAsync(pythonExecutable, ct).ConfigureAwait(false))
            return;

        Console.WriteLine("[DiscordAudioSink] Instalando dependencias de Python para Discord bot...");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = "-m pip install \"discord.py[voice]>=2.4,<3.0\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        var output = new StringBuilder();
        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                output.AppendLine(args.Data);
                Console.WriteLine($"[PySetup] {args.Data}");
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                output.AppendLine(args.Data);
                Console.WriteLine($"[PySetup:ERR] {args.Data}");
            }
        };

        if (!process.Start())
            throw new InvalidOperationException($"No se pudo ejecutar: {PythonDependencyInstallCommand}");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"No se pudieron instalar las dependencias de Python. Ejecuta manualmente: {PythonDependencyInstallCommand}");

        if (!await ArePythonDependenciesInstalledAsync(pythonExecutable, ct).ConfigureAwait(false))
            throw new InvalidOperationException(
                $"Las dependencias de Python siguen sin estar disponibles. Ejecuta manualmente: {PythonDependencyInstallCommand}");
    }

    private static async Task<bool> ArePythonDependenciesInstalledAsync(string pythonExecutable, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = "-c \"import importlib.util; raise SystemExit(0 if importlib.util.find_spec('discord') is not None else 1)\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        if (!process.Start())
            return false;

        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return process.ExitCode == 0;
    }

    private sealed record BridgeRequest(string Type, ulong UserId, int PayloadLength);

    private sealed record BridgeResponse(bool Ok, string? Error, bool BotReady, string? UserName);
}

public sealed record DiscordConnectionInfo(string UserName);

public sealed record DiscordBotConfig(string Token, ulong UserId);
