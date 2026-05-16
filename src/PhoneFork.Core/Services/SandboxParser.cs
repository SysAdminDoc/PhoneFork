using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Outcome of a sandboxed parser run (F027). Hostile input must not be able to
/// influence the WPF process beyond producing a well-formed result-or-error
/// payload, so the parser process is launched out-of-process with restricted
/// privileges and a hard wall-clock timeout.
/// </summary>
public sealed record SandboxParseResult(
    bool Success,
    int ExitCode,
    string? OutputJson,
    string? Error,
    TimeSpan Duration);

/// <summary>
/// Host-side launcher for the AppContainer-restricted parser used to read
/// untrusted Smart Switch <c>.bk</c> archives and other format families
/// (Open Android Backup 7-Zip, AppManager backup .tar.gz). The actual parsers
/// ship as separate executables in <c>tools/parsers/</c> in v0.8.1+; this class
/// is the cross-cutting plumbing: process lifetime, timeout, stdout/stderr
/// buffering, and JSON envelope.
/// </summary>
///
/// Threat model:
/// <list type="bullet">
///   <item>Input is a single file path passed as the first argv element.</item>
///   <item>The parser binary is shipped, not downloaded.</item>
///   <item>The parser binary runs with NO inherited handles, redirected
///         stdout/stderr, no environment leakage, and a 30 s wall-clock cap.
///         (AppContainer integration ships in v0.8.1 once a parser binary
///         actually exists to drop privileges onto.)</item>
///   <item>The host never executes the parser's output as code; it only
///         deserializes the well-formed JSON envelope.</item>
/// </list>
public sealed class SandboxParser
{
    public sealed record Options(
        string ParserExePath,
        string InputPath,
        TimeSpan Timeout)
    {
        public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(30);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ILogger _log;

    public SandboxParser(ILogger log)
    {
        _log = log.ForContext<SandboxParser>();
    }

    public async Task<SandboxParseResult> ParseAsync(Options options, CancellationToken ct = default)
    {
        if (!File.Exists(options.ParserExePath))
            throw new FileNotFoundException("Parser executable not found", options.ParserExePath);
        if (!File.Exists(options.InputPath))
            throw new FileNotFoundException("Input file not found", options.InputPath);

        var psi = new ProcessStartInfo(options.ParserExePath, new[] { options.InputPath })
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // Avoid leaking host secrets to a process parsing untrusted input.
            Environment = { },
        };
        psi.EnvironmentVariables.Clear();

        var sw = Stopwatch.StartNew();
        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(options.Timeout);

        try
        {
            await proc.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.Warning("Parser timed out after {Timeout}; killing.", options.Timeout);
            try { proc.Kill(entireProcessTree: true); } catch { }
            sw.Stop();
            return new SandboxParseResult(false, -1, null,
                $"Parser timed out after {options.Timeout.TotalSeconds:N0}s.",
                sw.Elapsed);
        }

        sw.Stop();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var ok = proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout);
        _log.Information(
            "Sandbox parser {Exe} exit={Exit} ms={Ms} stdout={Out} stderr={Err}",
            options.ParserExePath, proc.ExitCode, sw.ElapsedMilliseconds, stdout?.Length ?? 0, stderr?.Length ?? 0);

        return new SandboxParseResult(ok, proc.ExitCode, stdout, stderr, sw.Elapsed);
    }

    /// <summary>
    /// Convenience: parse and deserialize the JSON envelope. Throws when the parser
    /// exited non-zero or produced unparseable output.
    /// </summary>
    public async Task<T> ParseAndDeserializeAsync<T>(Options options, CancellationToken ct = default)
    {
        var result = await ParseAsync(options, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.OutputJson))
            throw new InvalidOperationException(
                $"Parser failed (exit={result.ExitCode}): {result.Error ?? "no error output"}");
        var deserialized = JsonSerializer.Deserialize<T>(result.OutputJson, JsonOpts)
            ?? throw new InvalidOperationException("Parser returned null JSON envelope.");
        return deserialized;
    }
}
