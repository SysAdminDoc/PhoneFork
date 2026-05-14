# PhoneFork — OSS Dependencies

Research snapshot 2026-05-14. Stack: C# / .NET 10 / WPF / MVVM (CommunityToolkit.Mvvm) / Catppuccin Mocha. Two USB-connected Samsung phones, S25U → S22U migration. Status legend: **A** = actively maintained, **D** = dormant/archive, **F** = fork-of-fork-only.

---

## 1. ADB client libraries

| Library | NuGet | Latest | License | Status | Verdict |
|---|---|---|---|---|---|
| **AdvancedSharpAdbClient** | `AdvancedSharpAdbClient` 3.6.16 | 2026-01-13 | Apache-2.0 | **A** (346K dl, active) | **PRIMARY — pick this.** Native binary ADB protocol over sockets, multi-device transport (`DeviceData[]` enumeration, per-device `IAdbClient` calls), sync protocol `Push`/`Pull`, `PackageManager` w/ `InstallMultiple` (session install-create/-write/-commit wrapper), `ExecuteShellCommandAsync(... CancellationToken)`. .NET 6/8/9/10/Standard 2.0 multi-target. Still bundles/talks to `adb.exe` server (port 5037) — that's the protocol, not a shellout. |
| **SharpAdbClient** | `SharpAdbClient` 3.3.x | 2022-ish | Apache-2.0 | **D** | Predecessor. Quamotion archived for maintenance; AdvancedSharpAdbClient is the live fork. Skip. |
| **MAdb** (quamotion/madb) | n/a | 2018 | Apache-2.0 | **D** | Original .NET port of `ddmlib`. Skip. |
| **adblib** (Google official) | java only | n/a | Apache-2.0 | n/a | Java/Android-only. Not usable from .NET. |
| **dotnet-adb** / **Mono.Adb** | scattered | 2014-17 | mixed | **D** | Skip. |

Native protocol implementations in .NET: AdvancedSharpAdbClient is the only one that fully implements the binary handshake + transport multiplexing in pure C#. It satisfies all four hard requirements (`pm install-create/-write/-commit` via `PackageManager.InstallMultiple`, multi-device routing via `DeviceData` selector, `SyncService.Push/Pull` for binary file transfer, `ExecuteRemoteCommandAsync` with `CancellationToken` for streamed `logcat -f`).

`adb.exe` server: still required as a localhost daemon. Ship the official `adb.exe` + `AdbWinApi.dll` + `AdbWinUsbApi.dll` from Google `platform-tools` (Apache-2.0) in `tools/` next to the EXE; call `AdbServer.Instance.StartServer(path, restartServerIfNewer: false)` on app start.

---

## 2. APK / AndroidManifest parsers

| Library | NuGet | Latest | License | Status | Verdict |
|---|---|---|---|---|---|
| **AlphaOmega.ApkReader** | `AlphaOmega.ApkReader` 2.0.10 | 2025 | MIT | **A** | **PRIMARY.** Reads APK ZIP + binary AXML manifest + `resources.arsc` + V1/V2 signature blocks. Lazy stream-based, won't OOM on a 4 GB OBB. Strongly-typed `AndroidManifest` model — pulls `application-label`, icon res id, `versionCode`/`versionName`, `<uses-permission>`, splits. |
| **QuestPatcher.Axml** | `QuestPatcher.Axml` 1.0.2 | 2023 | MIT | **A** (low-frequency but used in QuestPatcher releases) | Read+**write** binary AXML. Only needed if PhoneFork ever patches a helper-APK manifest in-flight. Otherwise skip. |
| **AndroidXmlDotNet** | `AndroidXmlDotNet` 0.1.0 | 2018 | MIT | **D** | Zero-dep AXML→`XmlReader`. Lean but read-only and stale. Fallback only. |
| **ApkNet** / **Iteedee.ApkReader** | `ApkNet` 1.0.0 | 2017 | MIT | **D** | Forks of the original Iteedee project. AlphaOmega supersedes both. |

Result: **no need to shell out to `aapt2.exe` for read paths.** AlphaOmega.ApkReader covers everything PhoneFork needs to enumerate the source-phone app inventory. Bundle `aapt2.exe` (from Google `cmdline-tools`, Apache-2.0) only as an optional fallback for icon-PNG extraction from `resources.arsc` densities AlphaOmega doesn't decode — flag as v2 work.

---

## 3. WPF UI toolkits

| Library | NuGet | Latest | License | Status | Verdict for PhoneFork |
|---|---|---|---|---|---|
| **MaterialDesignInXamlToolkit** | `MaterialDesignThemes` 5.3.2 | 2026 | MIT | **A** | Gold standard. `BundledTheme BaseTheme=Dark` + Catppuccin palette swap is well-trodden. Good for the side-by-side phone panels + category tree. |
| **HandyControl** | `HandyControl` 3.5+ | 2026 | MIT | **A** | More raw controls (TreeView w/ checkboxes, TabControl variants, SideMenu) than MDIX. Catppuccin-friendly via `ThemeResources.AccentColor` override. **Pair with MDIX** — they coexist (HandyOrg/HandyControl Discussion #841 confirms). |
| **WPF-UI (lepoco/wpfui)** | `WPF-UI` 3.x | 2026-04 | MIT | **A** | WinUI-3 look. Fluent navigation view fits a two-phone left-rail UI. But theme is locked to Light/Dark/HighContrast — Catppuccin would require overriding all `DynamicResource` brushes. Heavier lift than MDIX swap. |
| **ModernWpf** | `ModernWpfUI` 0.9.6 | 2022 | MIT | **D** | Abandoned in favor of WPF-UI. Skip. |
| **HelixToolkit** | various | 2026 | MIT | **A** | 3D scene graph. Skip — irrelevant to PhoneFork. |
| **ModernPropertyGrid** / `Hardcodet.WpfPropertyGrid` | various | 2023 | MIT | **A** | Use for the "Settings cherry-pick" tab — bind diffed `settings list secure` rows. Optional. |

**Recommendation:** **MaterialDesignInXamlToolkit + HandyControl side-by-side.** MDIX for theming/cards/dialogs/progress; HandyControl for the dual TreeView (apps category tree, per-phone) and dock-style layout. Both MIT, both .NET 10 compatible.

---

## 4. Catppuccin theme for WPF

**No official `catppuccin/wpf` port as of 2026-05.** The Catppuccin org has 361 repos but XAML is absent. Community: a handful of gists, no NuGet.

**Action:** hand-roll `Themes/CatppuccinMocha.xaml` as in OrganizeContacts/Snapture. 26 swatches as `Color` + matching `SolidColorBrush` (gotcha: `Foreground` Setters need the Brush, not the Color — caught in OrganizeContacts v0.1.0). Override MDIX `PrimaryHueMidBrush` + `SecondaryHueMidBrush` and HandyControl `AccentColor`/`PrimaryBrush` to map to Mocha `Mauve #CBA6F7` + `Blue #89B4FA`. Base/Mantle/Crust → MDIX `MaterialDesign.Brush.Background`/`Card.Background`/`Background.Header`.

Plan to upstream as `catppuccin/wpf` once stable — fills a real gap.

---

## 5. QR-code generation (Wi-Fi QR bridge)

| Library | NuGet | Latest | License | Status | Verdict |
|---|---|---|---|---|---|
| **QRCoder** | `QRCoder` 1.6.x | 2026 | MIT | **A** | **PRIMARY.** `XamlQRCode` returns a WPF `DrawingImage` directly (no GDI+, vector, scales crisply at any zoom). `SvgQRCode` for export. Zero deps. ~6× faster than ZXing for encode-only. |
| **Net.Codecrete.QrCodeGenerator** | `Net.Codecrete.QrCodeGenerator` 2.1.0 | 2024 | MIT | **A** | Solid alt; ships a WPF demo. Slightly cleaner SVG output. Fallback. |
| **ZXing.Net** | `ZXing.Net` 0.16.11 | 2024 | Apache-2.0 | **A** | Overkill — only worth it if PhoneFork ever **reads** a QR (e.g. importing Wi-Fi config from a screenshot). Needs `ZXing.Net.Bindings.Windows.Compatibility` for WPF rasterization. **Not thread-safe** — instantiate one reader per parallel scan. |

Wi-Fi payload is the standard `WIFI:T:WPA;S:<ssid>;P:<psk>;H:false;;` string — QRCoder + `PayloadGenerator.WiFi` covers it natively.

---

## 6. NDJSON logging

**Use `Serilog` + `Serilog.Sinks.File` + `Serilog.Formatting.Compact.CompactJsonFormatter`** (the de facto NDJSON path). One JSON object per line — `jq`/Seq/Vector ready. `RollingInterval.Day`, `fileSizeLimitBytes: 50*1024*1024`, `retainedFileCountLimit: 30`. Wire `LogContext.PushProperty("device", serial)` per phone so a single audit file demuxes by device.

Alternatives:
- `NReco.Logging.File` (MIT, active) — simpler but no NDJSON formatter; you'd hand-roll `JsonSerializer.Serialize` per line. Use only if you want zero Serilog dependency.
- Pure `System.Text.Json` + `StreamWriter` writeline — fine for a sub-1k-LOC tool, but PhoneFork's audit log spans push/pull/install/role-grant/permission per app per phone; you'll want Serilog enrichers (timestamp, level, device, op-id) for free.

---

## 7. CommunityToolkit.Mvvm

`CommunityToolkit.Mvvm` **8.4.2** (2026-02) — fixes the .NET 10 / C# 14 source-generator break that 8.4.0 had (MVVMTK0041/CS9248/CS8050). **Use 8.4.2, not 8.4.0.** Roslyn 5.0 analyzers, partial-property `[ObservableProperty]` works without `<LangVersion>preview</LangVersion>`. Same pattern as FileOrganizer.UI / OrganizeContacts. MIT.

---

## 8. Settings parsing / diffing

No mainstream library worth bringing in. `settings list secure` / `system` / `global` is plain `key=value\n`. Parse to `Dictionary<string,string>` per phone, then `LINQ Except/Intersect` for the three diff buckets (S25-only, S22-only, both-but-different). Render with `DataGrid` + checkboxes for cherry-pick. For a richer 3-way merge (e.g. preserving S22 device-specific values), look at `DiffPlex` 1.7.x (Apache-2.0, active) — line-level diff with `IDiffer`/`InlineDiffBuilder`, ~1.2M dl/mo. Add only if the simple set-diff UI proves insufficient.

---

## 9. Process supervision / async-stream

**`CliWrap` 3.10.1** (2026-03-21, MIT, 20.4M dl) — already standard. `Cli.Wrap("adb").WithArguments(...).ListenAsync()` yields `StandardOutputCommandEvent`/`StandardErrorCommandEvent`/`ExitedCommandEvent` as an `IAsyncEnumerable` — perfect for piping `adb logcat` lines into a WPF `ObservableCollection<LogLine>` on the dispatcher. Cancellation via `CancellationToken` passed to `ExecuteAsync()`.

**Most ADB calls should go through AdvancedSharpAdbClient (no shellout).** Reserve CliWrap for `adb.exe` lifecycle (`start-server`/`kill-server`/`devices -l` verification) and any `aapt2.exe`/`apksigner.bat` build-time invocations.

---

## 10. JSON-schema validation (AppManagerNG debloat lists)

| Library | NuGet | Latest | License | Verdict |
|---|---|---|---|---|
| **JsonSchema.Net** | `JsonSchema.Net` 7.x | 2026 | MIT | **PRIMARY.** Greg Dennis, Draft 2020-12 compliant, fastest of the .NET options, ~1.5M dl. `System.Text.Json`-native (no `Newtonsoft` drag-in). |
| **NJsonSchema** | `NJsonSchema` 11.x | 2026 | MIT | A. Heavier (Newtonsoft + code-gen surface). Use only if you also need C# class generation from a schema. |

Pick `JsonSchema.Net` — STJ alignment matches the rest of .NET 10's surface.

---

## 11. Auto-update

**Velopack** 0.0.x → 1.x (MIT, **A**, github.com/velopack/velopack). Successor to Squirrel/Clowd.Squirrel — Rust-core, delta packages, ~2s update+relaunch, no UAC, GitHub Releases as the update source out of the box. Works fine with WPF. `Squirrel.Windows` and `Clowd.Squirrel` are both EOL — don't pick them.

**Recommendation for PhoneFork v0.1:** **skip auto-update**, ship via GitHub Releases manual download (matches OrganizeContacts/Snapture/Devicer). Add Velopack at v0.5 once the release cadence justifies it. PhoneFork is a "run it twice when you upgrade your phone" tool, not a daily driver.

---

## 12. Single-file packaging

`dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=false`. Framework-dependent per stack convention (.NET 10 desktop runtime on the box). Manifest layout:

```
PhoneFork.exe
PhoneFork.dll  (extracted on first run to %LOCALAPPDATA%\PhoneFork\)
tools\
  adb.exe
  AdbWinApi.dll
  AdbWinUsbApi.dll
  aapt2.exe                (optional, v2)
  apksigner.bat + lib\     (optional, only if shipping helper-APK)
assets\
  helper.apk               (optional)
  catppuccin-mocha.xaml    (embedded ResourceDictionary, also fine)
```

`adb.exe` + DLLs as **content/CopyToOutput** (`<None Include="tools\**" CopyToOutputDirectory="PreserveNewest" />`). Do NOT bundle inside the single-file blob — AdbServer needs a real on-disk path to spawn. Self-contained (~150 MB) only if you want zero-install — not worth the bloat.

---

## 13. Helper-APK signing/packaging

If PhoneFork ships a companion APK (suggested for SMS-DB query, wallpaper write via system signature, Shizuku binder):

- Build via separate Gradle project under `helper-apk/` (Android Studio or `./gradlew assembleRelease` in CI).
- Sign with `apksigner.jar` (Apache-2.0, from Android SDK build-tools) — `apksigner sign --ks helper.jks --ks-pass file:keystore.pass --v1-signing-enabled true --v2-signing-enabled true --v3-signing-enabled true helper-release-unsigned.apk`.
- Keystore lives at `~/.android/phonefork-helper/phonefork-helper.jks` (same pattern as SwiftFloris). Keystore properties file co-located, gitignored.
- Embed the signed APK as `EmbeddedResource` or copy to `assets/helper.apk` and install via `PackageManager.InstallMultiple` over ADB at runtime.
- CI: GitHub Actions matrix step builds the APK first, then the .NET publish copies the artifact into `assets/`.

If the helper only needs **normal** permissions (no `signature`/`signatureOrSystem`), a self-signed cert is fine — no Samsung firmware key needed.

---

## 14. CLI / scriptability

**Yes — ship `phonefork.cli` alongside the WPF EXE.** `Spectre.Console.Cli` 0.55.0 (2026-04-03, MIT, Apache-licensed examples). Same `PhoneForkCore` class library underneath both `PhoneFork.App` (WPF) and `PhoneFork.Cli` (Spectre). Power users + sysadmins want `phonefork backup --device <serial> --out backup.zip` and `phonefork apply --plan plan.json` for unattended bench-rebuild workflows. Three-project layout — matches OrganizeContacts.Core split.

---

## 15. Background-task UI patterns

No library needed beyond what CommunityToolkit.Mvvm provides:

- `[RelayCommand(IncludeCancelCommand = true)]` auto-generates `<Cmd>` + `<Cmd>CancelCommand` from `Task RunAsync(CancellationToken)`.
- `IAsyncRelayCommand.IsRunning` binds straight to `Button.IsEnabled` / `ProgressBar.Visibility`.
- `IProgress<T>` callbacks marshal to dispatcher; bind `Progress` (double 0-1) + `Eta` + `CurrentOp` to a status row per device.
- Pause/resume: `ManualResetEventSlim` inside the long-running loop, checked between each `pm install-write` chunk.
- Two devices = two independent VMs, each with its own queue + cancel token; UI just renders both. No `Microsoft.Toolkit.Uwp.Notifications` / `TaskScheduler` overkill.

Optional polish: `Microsoft.Xaml.Behaviors.Wpf` 1.1.135 (MIT) for `EventTrigger`→command bindings.

---

## 16. WPF vs WinUI 3 vs WinForms

**Stick with WPF.** Reasons:

- WinUI 3 packaging is still painful in 2026: MSIX-only by default, self-contained unpackaged works but `Microsoft.UI.Xaml.dll` won't resolve without WindowsAppRuntime installer or a Win10 LTSC-incompatible self-contained bundle. Real friction on the LTSC IoT boxes PhoneFork's users (sysadmins migrating fleet phones) will run it on.
- WPF on .NET 10 picked up Grid shorthand syntax + Roslyn 5.0 source-gen + IL trimming improvements; the gap to WinUI 3 is mostly the Fluent control library, which MaterialDesign/HandyControl/WPF-UI fill.
- Every recent C# project — PatientImage (now C++, but the WPF v0.3 ran fine), FileOrganizer.UI, OrganizeContacts, Snapture, Devicer, TeamStation — picked WPF and shipped without packaging drama.
- WinForms is a non-starter for a Catppuccin Mocha custom-themed shell.

If a future PhoneFork v2 wants drag-to-MAUI for macOS reach, the WPF→Avalonia port is ~4× easier than WPF→WinUI 3.

---

## Known .NET 10 compatibility flags

- **`CommunityToolkit.Mvvm` < 8.4.2** breaks on .NET 10 default LangVersion. Pin **≥ 8.4.2**.
- **`AdvancedSharpAdbClient` 3.6.16** — clean on .NET 10, multi-target TFM list includes `net9.0`/`net10.0`-compatible netstandard 2.0.
- **`MaterialDesignThemes` 5.3.2** — fine on .NET 10; check `LangVersion` if you also pull `MaterialDesignThemes.MahApps`.
- **`HandyControl` 3.5.x** — fine on .NET 10.
- **`Serilog.Sinks.File` 6.x / `Serilog.Formatting.Compact` 3.x** — fine on .NET 10.
- **`CliWrap` 3.10.1** — fine on .NET 10 (`Microsoft.Bcl.AsyncInterfaces` ≥ 10.0.3 transitive).
- **`QRCoder` 1.6.x** — fine on .NET 10. `XamlQRCode` requires `PresentationCore`, which is implicit in WPF projects.
- **`Spectre.Console` 0.55.0** — fine on .NET 10 via `net8.0`/`netstandard2.0` MTM.
- **`JsonSchema.Net` 7.x** — fine, STJ-native.
- **`Velopack`** — fine; Rust core is per-RID.

---

## Final shopping list

```xml
<!-- src/PhoneFork.Core/PhoneFork.Core.csproj -->
<PackageReference Include="AdvancedSharpAdbClient"      Version="3.6.16" />
<PackageReference Include="AlphaOmega.ApkReader"        Version="2.0.10" />
<PackageReference Include="CliWrap"                     Version="3.10.1" />
<PackageReference Include="Serilog"                     Version="4.2.0" />
<PackageReference Include="Serilog.Sinks.File"          Version="6.0.0" />
<PackageReference Include="Serilog.Formatting.Compact"  Version="3.0.0" />
<PackageReference Include="JsonSchema.Net"              Version="7.3.0" />
<PackageReference Include="Microsoft.Data.Sqlite"       Version="9.0.0" />

<!-- src/PhoneFork.App/PhoneFork.App.csproj (WPF) -->
<PackageReference Include="CommunityToolkit.Mvvm"       Version="8.4.2" />
<PackageReference Include="MaterialDesignThemes"        Version="5.3.2" />
<PackageReference Include="HandyControl"                Version="3.5.1" />
<PackageReference Include="QRCoder"                     Version="1.6.0" />
<PackageReference Include="Microsoft.Xaml.Behaviors.Wpf" Version="1.1.135" />

<!-- src/PhoneFork.Cli/PhoneFork.Cli.csproj -->
<PackageReference Include="Spectre.Console"             Version="0.55.0" />
<PackageReference Include="Spectre.Console.Cli"         Version="0.55.0" />
```

Bundled binaries (Apache-2.0, redistributable per Google ToS):
- `tools/adb.exe` + `AdbWinApi.dll` + `AdbWinUsbApi.dll` from latest Google `platform-tools`.
- (optional v2) `tools/aapt2.exe` from `cmdline-tools`.
- (optional, helper-APK only) `tools/apksigner.bat` + `lib/apksigner.jar` from `build-tools`.

Document each bundled binary's license in `THIRD-PARTY-NOTICES.md` at repo root.
