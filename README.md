# PhoneFork

[![Version](https://img.shields.io/badge/version-0.6.5-blue.svg)](https://github.com/SysAdminDoc/PhoneFork/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue.svg)](#)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)

**Dual-Samsung Android migration tool for Windows.** Drives two USB-connected Galaxy phones at the same time and copies apps, media, settings, Wi-Fi, default app roles, and applies a debloat profile — from a new device to a freshly-reset old one. No root, no Samsung account, no cloud.

Built because Samsung Smart Switch is sequential (one phone at a time), one-direction-blessed (old→new), and refuses to debloat.

## What it does

| Domain | What gets copied | Mechanism |
|---|---|---|
| **Apps** | All `-3` user apps + their split APKs | `pm path` → `adb pull` → `pm install-create/-write/-commit -i com.android.vending --install-reason 4` |
| **Media** | DCIM, Pictures, Movies, Download, Documents, Music, Ringtones, Notifications, Alarms | Incremental manifest diff + `adb pull/push` |
| **Settings** | AOSP `secure`/`system`/`global` + Samsung One UI keys (AOD, edge panels, refresh rate, font scale, status-bar tweaks) | `settings list` snapshot diff + cherry-picked `settings put` |
| **Debloat** | Apply [AppManagerNG](https://github.com/SysAdminDoc/AppManagerNG)'s 5,481-entry curated bloat list | `pm disable-user --user 0` (reversible) |
| **Wi-Fi** | Saved networks + PSKs (via optional Shizuku helper) or QR-bridge fallback | `WifiManager.getPrivilegedConfiguredNetworks` over Shizuku, or `WIFI:T=WPA;S=…;P=…;;` QR |
| **Roles** | Default dialer, SMS, browser, launcher, assistant | `cmd role add-role-holder` |

## What it cannot do (and why)

Third-party app **private data** (banking, messengers, game saves, login sessions) does not transfer. Android's security model prevents reading `/data/data/<pkg>/` without root, and `adb backup` was effectively neutered in Android 12. For app-data migration, run Samsung Smart Switch alongside PhoneFork as a complementary step.

Knox-bound data (Secure Folder, Samsung Wallet payment tokens, enterprise containers) is intentionally inaccessible by design — re-set those up on the destination.

## Install

Download the latest release: [Releases](https://github.com/SysAdminDoc/PhoneFork/releases) — `PhoneFork-vX.Y.Z.zip`. Extract, double-click `PhoneFork.exe`. No installer.

Requires the **.NET 10 Desktop Runtime** ([download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)) — the zip is framework-dependent (~10 MB).

The bundled `tools/adb.exe` ships with the app — no Android SDK needed on your PC.

## Usage

1. Plug **both** phones in via USB. Accept the "Allow USB debugging?" prompt on each.
2. Open PhoneFork. Both devices appear in the top bar; pick which is **Source** and which is **Destination**.
3. Open the tab for whatever you want to migrate. Each tab has a **dry-run** preview before **Apply**.
4. Audit log writes one NDJSON line per operation to `%LOCALAPPDATA%\PhoneFork\logs\audit-YYYY-MM-DD.log`.

## CLI

```bash
phonefork devices                          # list connected
phonefork apps list --device R5CY34G070L   # enumerate user apps
phonefork apps migrate --from <src> --to <dst> [--dry-run]
phonefork media sync   --from <src> --to <dst>
phonefork settings dump --device <serial> --out settings.json
phonefork settings apply --device <serial> --plan plan.json
phonefork debloat apply --device <serial> --profile aggressive
```

## Build from source

```bash
git clone https://github.com/SysAdminDoc/PhoneFork.git
cd PhoneFork
dotnet build -c Release
dotnet run --project src/PhoneFork.App
```

Requires **.NET 10 SDK** (10.0.202+).

## Tech

- **Stack**: C# / .NET 10 / WPF / MVVM (CommunityToolkit.Mvvm 8.4.2)
- **ADB**: [AdvancedSharpAdbClient](https://github.com/SharpAdb/AdvancedSharpAdbClient) — native binary protocol, no shellout
- **APK parsing**: [AlphaOmega.ApkReader](https://www.nuget.org/packages/AlphaOmega.ApkReader)
- **UI**: [MaterialDesignInXamlToolkit](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) + [HandyControl](https://github.com/HandyOrg/HandyControl), Catppuccin Mocha theme
- **Logging**: Serilog + CompactJsonFormatter (NDJSON)
- **QR**: QRCoder
- **CLI**: Spectre.Console.Cli

## Credits

- [AppManagerNG](https://github.com/SysAdminDoc/AppManagerNG) — the 5,481-entry debloat dataset PhoneFork applies
- [Universal Android Debloater Next Generation](https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation) — upstream of the debloat dataset
- [Shizuku](https://shizuku.rikka.app/) — the no-root Wireless-ADB elevation model
- [scrcpy](https://github.com/Genymobile/scrcpy) — the `app_process` push-and-run helper pattern
- [Muntashir's App Manager](https://github.com/MuntashirAkon/AppManager) — backup-format compatibility target

## License

MIT — see [LICENSE](LICENSE).

Third-party redistributable binaries (`tools/adb.exe`, `AdbWinApi.dll`, `AdbWinUsbApi.dll`, `libwinpthread-1.dll`) are Apache-2.0 (Google `platform-tools`) — see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
