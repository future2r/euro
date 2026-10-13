# EuroCoins

A Windows app for collecting **euro coins**. It contains all euro circulation coins of every
issuing country and all €2 commemorative coins, each with an image from the European Central
Bank. Mark the coins you own, keep count and notes, and see your progress by country.

Built with **WinUI 3** (Windows App SDK) and **.NET 10** in the style of the Windows 11 inbox apps
(Notepad, Photos, Clock): Mica, navigation pane, search in the title bar, settings page.

## Features

- **Overview** (start page): overall progress, progress by country, recently added coins.
- **Coin lists**: all coins, circulation coins, €2 commemoratives — as a grid or a list (*View* menu,
  `Ctrl+Shift+1`/`2`), grouped by country, year or face value.
- **Filters**: country (multiple), ownership, face value, year.
- **Search** in the title bar (`Ctrl+F`) with suggestions; `Enter` shows all results.
- **Quick marking**: tap the badge on a coin, use the context menu, or press `Space`.
  The quantity decides ownership (owned from 1); the date a coin was added is kept.
- **Details pane** with image credit and source, quantity and personal note; collapsible, shown as
  an overlay in narrow windows (e.g. a Surface in portrait mode).
- **Tile zoom** with pinch, `Ctrl` + mouse wheel, `Ctrl+Plus`/`Minus` or the *View* menu (remembered per
  device).
- **Settings**: theme (light, dark, system), language (German, English, Windows setting),
  collection location — choose a OneDrive folder to share the collection between devices.
- Single instance; the window position and size are remembered.

## Requirements

- Windows 11 on x64 or ARM64.
- To build: [.NET SDK 10](https://dotnet.microsoft.com/download) (see `global.json`). No workloads
  or Visual Studio required; Visual Studio 2026 with the WinUI workload works too.

## Build, run and test

```powershell
dotnet build EuroCoins.slnx -p:Platform=x64
dotnet run --project src\EuroCoins -p:Platform=x64
dotnet test --project tests\EuroCoins.Tests
```

On an ARM64 device use `-p:Platform=ARM64`.

## Portable app

```powershell
dotnet publish src\EuroCoins -c Release -r win-x64   -p:Platform=x64   -o artifacts\EuroCoins-win-x64
dotnet publish src\EuroCoins -c Release -r win-arm64 -p:Platform=ARM64 -o artifacts\EuroCoins-win-arm64
```

Each folder is self-contained (.NET and the Windows App SDK included, ReadyToRun): copy it anywhere
and start `EuroCoins.exe`. No installation needed.

Releases: pushing a tag `v1.2.3` builds both folders on GitHub Actions and attaches them as ZIP files
to a GitHub release; the version comes from the tag.

## Data

| What | Where |
|---|---|
| Collection | `%LOCALAPPDATA%\EuroCoins\collection.json` (location changeable in the settings) |
| Settings (per device) | `%LOCALAPPDATA%\EuroCoins\settings.json` |
| Error log | `%LOCALAPPDATA%\EuroCoins\Logs\EuroCoins.log` (rotating, warnings and errors) |

The collection file is written atomically after every change. When another device changes it (for
example through OneDrive), the app reloads it; if both changed, the file saved last wins.

## Catalog

The catalog (`src/EuroCoins/Assets/Catalog`: `catalog.json`, images, flags) ships with the app and is
updated with new app versions. The catalog tool rebuilds it from the ECB's
[directory of euro coins](https://www.ecb.europa.eu/euro/coins/):

```powershell
dotnet run --project tools\EuroCoins.CatalogTool -- update
```

- **Circulation coins**: the series per country are curated in
  `tools/EuroCoins.CatalogTool/CirculationSeries.cs` (add a series when a country issues a new design);
  the images come from the ECB pages of each face value.
- **€2 commemoratives**: read completely from the ECB year pages (from 2004 up to next year; years
  without a page yet are skipped), German and English texts.
- **Images** are downloaded only when new or changed and normalized to JPEG with at most 540 pixels per
  side; unused images are removed. Missing flags are taken from flagcdn.com.
- **Stable IDs**: collections reference coins by ID, so known coins keep their ID and an ID is never
  given to another coin. Commemoratives (e.g. `LU-CC-2022-01`: country, year, number) are recognized by
  country, year and title (the ECB reuses image file names for different coins), each with its own
  image file. IDs of removed coins are kept in `retiredIds` in the catalog.
- If a download fails, the catalog is left unchanged; run the update again.

Review the summary of new and removed coins, then build and commit.

## Project structure

```
EuroCoins.slnx
├─ src/EuroCoins/              WinUI 3 app (views, view models, services, resources)
├─ src/EuroCoins.Core/         Catalog, collection, filtering, statistics, settings — no UI, tested
├─ tests/EuroCoins.Tests/      MSTest tests for EuroCoins.Core and the catalog tool
├─ tools/EuroCoins.CatalogTool Catalog update from the ECB web site
└─ EuroCoins.code-workspace    VS Code workspace (tasks, launch configurations, extensions)
```

## Visual Studio Code

Open `EuroCoins.code-workspace` and install the recommended extensions (C# Dev Kit). Launch
configurations: **EuroCoins (x64)**, **EuroCoins (ARM64, e.g. Surface)**, **Catalog tool: update from
ECB** and **Attach to running EuroCoins**. Tasks (`Ctrl+Shift+B` builds, *Run Test Task* tests) cover
building, testing, publishing the portable app, updating the catalog and checking for package updates.

## License

Source code: [MIT](LICENSE). Coin images and descriptions © European Central Bank, flags public
domain — see [NOTICE.md](NOTICE.md).
