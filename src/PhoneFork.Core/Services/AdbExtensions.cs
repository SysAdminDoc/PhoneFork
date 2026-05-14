using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Models;
using AdvancedSharpAdbClient.Receivers;

namespace PhoneFork.Core.Services;

/// <summary>
/// Convenience: run a shell command and return its full stdout as a string.
/// AdvancedSharpAdbClient's bare <c>ExecuteShellCommandAsync</c> returns <c>Task</c> (output is discarded),
/// so a receiver must always be wired in. This wraps the boilerplate.
/// </summary>
public static class AdbExtensions
{
    public static async Task<string> ShellAsync(this IAdbClient client, DeviceData device, string command, CancellationToken ct = default)
    {
        var receiver = new ConsoleOutputReceiver();
        await client.ExecuteShellCommandAsync(device, command, receiver, ct);
        return receiver.ToString() ?? "";
    }

    /// <summary>
    /// Synchronous shell — safe to call from the UI dispatcher thread without a sync-over-async deadlock.
    /// </summary>
    public static string Shell(this IAdbClient client, DeviceData device, string command)
    {
        var receiver = new ConsoleOutputReceiver();
        client.ExecuteShellCommand(device, command, receiver);
        return receiver.ToString() ?? "";
    }
}
