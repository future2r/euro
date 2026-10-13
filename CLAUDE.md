# CLAUDE.md

Guidance for working in this repository. Features, data locations and commands are in
[README.md](README.md) — don't duplicate them here.

## What this is

EuroCoins, a WinUI 3 / .NET 10 Windows app for collecting euro coins. Everything must be typical for
Windows, .NET and WinUI — look and feel (Windows 11 inbox apps such as Notepad and Photos), project
structure, naming, idioms. Targets: Windows 11 on x64 and ARM64 (touch-capable devices like Surface).

## Commands

- Build: `dotnet build EuroCoins.slnx -p:Platform=x64` (the app needs a platform: x64 or ARM64)
- Test: `dotnet test --project tests\EuroCoins.Tests` (Microsoft.Testing.Platform, see `global.json`)
- Run: `dotnet run --project src\EuroCoins -p:Platform=x64`

## Build constraints

- Package versions live only in `Directory.Packages.props` (central package management); keep them
  on the latest stable release.
- Shared settings in `Directory.Build.props`: nullable, `TreatWarningsAsErrors`, analyzers
  `latest-recommended`, code style enforced in build. `EuroCoins.Core` requires XML docs on all
  public API.
- The app is unpackaged and self-contained (`WindowsPackageType=None`,
  `WindowsAppSDKSelfContained`). APIs that need package identity (`ApplicationData`,
  `Windows.Globalization.ApplicationLanguages`) are not available — use the Windows App SDK
  equivalents or files under `%LOCALAPPDATA%\EuroCoins`.

## Conventions

- MVVM with CommunityToolkit.Mvvm (partial `[ObservableProperty]` properties, `[RelayCommand]`);
  services via Microsoft.Extensions.DependencyInjection (composed in `App.xaml.cs`).
- Domain logic goes into `EuroCoins.Core` (no UI types) and gets MSTest tests. View models adapt it;
  views only bind (`x:Bind`) and handle purely presentational concerns in code-behind.
- UI text is localized in `src/EuroCoins/Strings/{de-DE,en-US}/Resources.resw`: `x:Uid` in XAML,
  `Localizer` in code. Every new string goes into both files. Language changes apply after restart.
- Prefer `x:Bind` function bindings (`Views/XamlFunctions.cs`) over value converters. x:Bind does not
  re-evaluate a function when an argument becomes `null` — bind a bool property (e.g. `IsEnlarged`)
  instead of passing a nullable object.
- Use Fluent theme resources (`ThemeResource …Brush`, built-in text styles) instead of hard-coded
  colors; the accent color follows Windows.
- The quantity is authoritative: a coin is owned when its quantity is at least 1; the "added" date
  is set when the quantity goes from 0 to 1 and cleared at 0.
- The catalog is rebuilt from the ECB web site with `tools/EuroCoins.CatalogTool` (`update`); there is
  no other data source. Catalog images are normalized to at most 540 px (JPEG).
- Coin IDs must stay stable across catalog updates (collections reference them) and an ID must never
  be reused for another coin — see `CommemorativeIdAssigner` and `retiredIds` in `catalog.json`. The
  ECB reuses image file names, so never identify a commemorative by its image name alone.
- Parsing of the ECB pages is tested with HTML snippets in `CatalogToolTests` (no network in tests).
- Comments, docs and identifiers in English; UI in German and English.
