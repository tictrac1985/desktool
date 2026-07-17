# DeskMate — رفيق سطح المكتب — Design

**Date:** 2026-07-17 · **Status:** Approved (autonomous session — decisions delegated to Claude by the user's brief, including visual design: "وصممها انت")

## 1. Goal

A Windows desktop widget that lives *on* the desktop (always below open apps), showing a beautiful clock/date in the center with a row of quick-tool icons beneath it. Bilingual (Arabic default / English), draggable anywhere, near-zero resource usage. Delivered as **two final files**: `DeskMate.exe` (portable) and `DeskMateSetup.exe` (permanent installer).

## 2. Features (from the user's brief)

1. **Clock & date** — multiple styles designed here: *Digital* (large glass digits + Gregorian & Hijri date), *Analog dial* (drawn dial with hands), *Minimal*. 12/24h, optional seconds.
2. **Quick clean** — deletes Windows leftovers: `%TEMP%`, `C:\Windows\Temp` (accessible files only), INetCache; optional Recycle Bin empty. Reports MB freed. Skips locked files silently.
3. **Calculator** — simple keypad, one click switches to *Pro* (sin/cos/tan/log/ln/√/^/π/e, parentheses). Own shunting-yard evaluator.
4. **Wallpaper changer** — pick image → `SystemParametersInfo(SPI_SETDESKWALLPAPER)` with fit style (fill/fit/stretch/tile/center).
5. **Sleep timeout** — presets (never/5/10/15/30/60 min) for AC & battery via `powercfg /change`.
6. **Clipboard keeper** — listens via `AddClipboardFormatListener`; keeps recent text snippets (temporary), click to re-copy, pin/delete.
7. **Weather** — Open-Meteo (free, keyless) + auto location by IP (ipapi.co → ip-api.com fallback) or manual city (Open-Meteo geocoding, Arabic supported). Current + 3-day. 30-min refresh.
8. **App launcher** — scans Start Menu `.lnk` (user + common), searchable dropdown; per app: **فتح** or **إضافة لسطح المكتب** (copies shortcut to Desktop).
9. **Prayer times** — fully local astronomical calculation (Fajr/Sunrise/Dhuhr/Asr/Maghrib/Isha), methods: MWL, Egyptian, Umm al-Qura, ISNA, Karachi (auto-picked by country, changeable). Uses device timezone & widget location. Icon click → next prayer + full day list; topmost toast + sound at prayer time, optional pre-alert.

**Shell requirements:** every icon can be hidden/shown from settings; AR/EN with RTL; draggable (position persisted); always below apps (feels part of the wallpaper); desktop icon after install to open it; autostart on login (toggleable); single instance (second launch just shows it); tray icon (show/hide, settings, exit).

## 3. Stack decision

Constraint: no .NET SDK on the build machine; available: VS 2022 Build Tools **Roslyn csc 4.14** (modern C#), .NET Framework **4.8.1** runtime (built into Windows 10/11), Inno Setup absent.

| Option | Verdict |
|---|---|
| **WPF on .NET Framework 4.8, compiled directly with Roslyn csc (no XAML, code-only UI)** | ✅ Chosen. Tiny exe (~0.5 MB), zero runtime install, per-pixel transparency, vector clock faces, low RAM (~40–60 MB). |
| .NET 8 self-contained WPF | ❌ Needs SDK download; 150+ MB portable exe contradicts "final product = one small portable file". |
| Electron/Tauri/Python | ❌ Heavy or missing toolchains; violates "no resource consumption". |

**Installer:** no Inno Setup → a second small C# WinForms exe embedding `DeskMate.exe` as a resource. Installs to `%LocalAppData%\DeskMate`, creates Desktop + Start Menu shortcuts, optional autostart, HKCU uninstall entry, copies itself as `Uninstall.exe` (uninstall mode: kill app, remove dir/shortcuts/registry, optionally keep settings).

## 4. Architecture

Single project, code-only WPF, one exe. Units:

- `App.cs` — entry, mutex `DeskMate.Single`, show-event for second instance.
- `Core/Settings.cs` + `Core/MiniJson.cs` — flat settings persisted to `%AppData%\DeskMate\settings.json` (works for portable & installed).
- `Core/L.cs` — localization table (ar/en), `L.T(key)`; FlowDirection driven by language.
- `Core/Win32.cs` — all P/Invoke (z-order, clipboard listener, wallpaper, recycle bin, Progman parenting).
- `UI/MainWindow.cs` — transparent widget shell: clock area + date + optional next-prayer line + icon bar; drag; bottom z-order via `WM_WINDOWPOSCHANGING` (hwndInsertAfter=HWND_BOTTOM) with optional *pin-to-desktop* (SetParent → Progman, fallback safe); tray `NotifyIcon`.
- `UI/ClockViews.cs` — the 3 clock styles (DispatcherTimer 1 s; seconds hand only when enabled).
- `UI/PanelWindow.cs` — shared rounded dark "glass" popup near the widget, Topmost while open, closes on deactivate; one at a time.
- `Features/*.cs` — one file per panel: Cleaner, Calculator (+`ExpressionEvaluator`), Wallpaper, Power, ClipboardKeeper, Weather (+`WeatherService`), Apps (+`AppScanner`), Prayer (+`PrayerCalc`, `PrayerNotifier`), SettingsPanel.

**Visual design:** dark glass card (rounded 24 px, ~78 % black, subtle white border, drop shadow), white text, one accent (amber `#FFC66E`) for highlights; Segoe UI Light for digits, Segoe MDL2 Assets glyph icons in 34 px circular buttons. Panels inherit the same language.

**Networking:** TLS 1.2 forced; `HttpWebRequest`; all fetches async on thread pool; failures degrade to cached data + "—".

**Error handling:** every feature action wrapped; failures show a localized inline message, never crash the widget. Global `DispatcherUnhandledException` → log to `%AppData%\DeskMate\log.txt`, continue.

## 5. Testing

Pure logic (evaluator, prayer calc, MiniJson, WMO mapping) compiled into `SelfTest.exe` by `build.ps1` and executed — build fails on test failure. Prayer values sanity-checked against known published times (±3 min). UI verified by launching the exe and screenshotting the desktop.

## 6. Build & packaging

`build.ps1`: 1) generate `app.ico` (drawn clock, PNG-packed ICO) if absent; 2) csc → `dist\DeskMate.exe` (`/target:winexe /codepage:65001 /win32icon` , refs from `Framework64\v4.0.30319` + `\WPF`); 3) csc + run `SelfTest.exe`; 4) csc installer with `/res:dist\DeskMate.exe` → `dist\DeskMateSetup.exe`.

## 7. Risks

- **Win+D / Show Desktop** hides bottom-z-order windows → mitigated by optional Progman parenting (default ON, auto-fallback if it fails).
- **MDL2 glyph codes** — verified visually via screenshot during QA; wrong glyphs swapped.
- **`C:\Windows\Temp`** needs admin → best-effort, skip inaccessible files (documented in panel).
- **WPF color emoji unsupported on net48** → weather icons use MDL2/monochrome glyphs, not emoji.
