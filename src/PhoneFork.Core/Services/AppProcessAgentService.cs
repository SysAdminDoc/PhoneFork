using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Push-and-run JAR pattern (F011) lifted from scrcpy. The host pushes
/// <c>phonefork-agent.jar</c> to <c>/data/local/tmp/</c>, then runs
/// <c>CLASSPATH=&lt;path&gt; app_process / com.sysadmindoc.phonefork.helper.Agent &lt;args&gt;</c>
/// as the shell user. No install, no Settings entry, no launcher icon.
///
/// Used for read-side operations that don't need a manifest permission
/// (settings list, role queries, dumpsys parsing, etc.).
/// </summary>
public sealed class AppProcessAgentService
{
    public const string AgentClass = "com.sysadmindoc.phonefork.helper.Agent";
    public const string RemoteJarPath = "/data/local/tmp/phonefork-agent.jar";

    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public AppProcessAgentService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<AppProcessAgentService>();
    }

    /// <summary>
    /// Push the agent JAR to <see cref="RemoteJarPath"/> if not already present
    /// (size-based equality is enough — the JAR is content-addressed at build).
    /// </summary>
    public async Task PushAgentAsync(DeviceData device, string localJarPath, CancellationToken ct = default)
    {
        if (!File.Exists(localJarPath))
            throw new FileNotFoundException("Agent JAR not found", localJarPath);

        var localSize = new FileInfo(localJarPath).Length;
        var remoteSize = await ProbeRemoteSizeAsync(device, ct);
        if (remoteSize == localSize)
        {
            _log.Information("Agent JAR already present on {Device} (size={Size}).", device.Serial, localSize);
            return;
        }

        await using var stream = File.OpenRead(localJarPath);
        using var sync = new SyncService(_client, device);
        await sync.PushAsync(stream, RemoteJarPath, UnixFileStatus.DefaultFileMode, DateTimeOffset.UtcNow,
            callback: null, useV2: false, cancellationToken: ct);
        _log.Information("Agent JAR pushed to {Device}: {Path} ({Size} bytes).", device.Serial, RemoteJarPath, localSize);
    }

    /// <summary>
    /// Invoke the agent with the given arguments and return its stdout. The JAR's main
    /// class must accept JSON-shaped args (single argv element) for forward-compat.
    /// </summary>
    public async Task<string> InvokeAsync(DeviceData device, string requestJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requestJson))
            throw new ArgumentException("request required", nameof(requestJson));

        var cmd = $"CLASSPATH={AdbShell.Arg(RemoteJarPath)} app_process / {AgentClass} {AdbShell.Arg(requestJson)}";
        var output = await _client.ShellAsync(device, cmd, ct);
        _log.Information("Agent invoke on {Device}: ok bytes={Bytes}", device.Serial, output?.Length ?? 0);
        return output ?? "";
    }

    /// <summary>
    /// Remove the agent JAR from the device (F019). Cheap; the JAR is the only artifact
    /// the push-and-run model ever leaves behind.
    /// </summary>
    public async Task<bool> RemoveAgentAsync(DeviceData device, CancellationToken ct = default)
    {
        var output = await _client.ShellAsync(device, $"rm -f {AdbShell.Arg(RemoteJarPath)}", ct);
        return string.IsNullOrWhiteSpace(output) || !(output ?? "").Contains("denied", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<long> ProbeRemoteSizeAsync(DeviceData device, CancellationToken ct)
    {
        try
        {
            var output = await _client.ShellAsync(device,
                $"stat -c %s {AdbShell.Arg(RemoteJarPath)} 2>/dev/null", ct);
            return long.TryParse((output ?? "").Trim(), out var n) ? n : -1;
        }
        catch
        {
            return -1;
        }
    }
}
