using System.Collections.Concurrent;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Higher-level device tracker: wraps <see cref="AdbHostService"/>, hydrates <see cref="PhoneInfo"/> for each
/// connected device, and tracks Source/Destination role assignment.
/// </summary>
public sealed class DeviceService
{
    private readonly AdbHostService _adb;
    private readonly ILogger _log;
    private readonly ConcurrentDictionary<string, PhoneInfo> _phones = new();
    private readonly ConcurrentDictionary<string, DeviceRole> _roles = new();

    public event EventHandler? PhonesChanged;

    public DeviceService(AdbHostService adb, ILogger log)
    {
        _adb = adb;
        _log = log.ForContext<DeviceService>();

        if (_adb.Monitor is not null)
        {
            _adb.Monitor.DeviceConnected += (_, __) => Refresh();
            _adb.Monitor.DeviceDisconnected += (_, __) => Refresh();
            _adb.Monitor.DeviceChanged += (_, __) => Refresh();
        }
    }

    public IReadOnlyCollection<PhoneInfo> Phones => _phones.Values.OrderBy(p => p.Serial).ToArray();

    public DeviceRole RoleOf(string serial) =>
        _roles.TryGetValue(serial, out var r) ? r : DeviceRole.Unassigned;

    public PhoneInfo? RoleHolder(DeviceRole role) =>
        _roles.FirstOrDefault(kv => kv.Value == role) is { Key: { } s } && _phones.TryGetValue(s, out var p)
            ? p
            : null;

    public void AssignRole(string serial, DeviceRole role)
    {
        // Roles are exclusive — assigning a role to one device clears it from any other holder.
        if (role is DeviceRole.Source or DeviceRole.Destination)
        {
            foreach (var kv in _roles.Where(kv => kv.Value == role).ToList())
                _roles[kv.Key] = DeviceRole.Unassigned;
        }
        _roles[serial] = role;
        PhonesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Refresh()
    {
        try
        {
            var current = _adb.GetDevices().ToList();
            var seen = new HashSet<string>();

            foreach (var d in current)
            {
                if (string.IsNullOrWhiteSpace(d.Serial)) continue;
                seen.Add(d.Serial);
                var info = Hydrate(d);
                _phones[d.Serial] = info;
            }

            foreach (var stale in _phones.Keys.Except(seen).ToList())
            {
                _phones.TryRemove(stale, out _);
                _roles.TryRemove(stale, out _);
            }

            PhonesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Refresh failed");
        }
    }

    private PhoneInfo Hydrate(DeviceData d)
    {
        var serial = d.Serial;
        var authorized = d.State == DeviceState.Online;
        if (!authorized)
        {
            return new PhoneInfo(
                Serial: serial,
                Manufacturer: "",
                Model: $"(unauthorized: {d.State})",
                AndroidVersion: "",
                OneUiVersion: "",
                Codename: "",
                IsAuthorized: false);
        }

        try
        {
            var manuf = GetProp(d, "ro.product.manufacturer");
            var model = GetProp(d, "ro.product.model");
            var android = GetProp(d, "ro.build.version.release");
            var oneui = GetProp(d, "ro.build.version.oneui");
            var codename = GetProp(d, "ro.product.device");
            return new PhoneInfo(serial, manuf, model, android, oneui, codename, true);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Hydrate {Serial} failed", serial);
            return new PhoneInfo(serial, "", "(query failed)", "", "", "", false);
        }
    }

    private string GetProp(DeviceData d, string key)
    {
        try
        {
            return _adb.Client.Shell(d, $"getprop {key}").Trim();
        }
        catch
        {
            return "";
        }
    }
}
