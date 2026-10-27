# PKHeX Mobile

An Android save editor built on the [Psyduck Yellow PKHeX](https://github.com/ThatAnarchyDude/PKHeX-Psyduck-Yellow) core (.NET MAUI). It opens and exports `.sav` / `.dsv` saves for the games PKHeX supports, with Psyduck Yellow (`SAV4PY`) as the priority game.

**Download:** see the [Android release](https://github.com/ThatAnarchyDude/PKHeX-Mobile/releases/tag/v0.31) (APK + SHA256), or build it yourself below. The app exports an "-edited" copy of your save and never overwrites the original; keep backups.

## Features
- Box and party editing with PC-style wallpapers, legality markers and overlays
- Full Pokémon editor (Main / Met / Stats / Moves / Cosmetic / Tools), PID + gender + Reroll, long-press an IV for Hyper Training
- Check legality, Legalize (auto-fix), Showdown import/export
- Landscape two-pane layout, light/dark toggle
- Trainer Info, Items, Event Flags, Pokédex, Box Layout, Misc Edits, Wondercards, Mail Box, Pokéathlon, Apricorns

## Build
Requirements: .NET 10 SDK with the `maui-android` workload, the Android SDK (platform 36) and a JDK 17+.

Clone this repo and the core side by side:

```
git clone https://github.com/ThatAnarchyDude/PKHeX-Psyduck-Yellow
git -C PKHeX-Psyduck-Yellow checkout pkhex-psyduck-yellow-v5
git -C PKHeX-Psyduck-Yellow apply ../PKHeX-Mobile/patches/automod-mobile.patch
git clone <this repo> PKHeX-Mobile
cd PKHeX-Mobile
dotnet build -c Release -f net10.0-android
```

The signed APK is written to `bin/Release/net10.0-android/`.

The patch makes the Auto-Legality library skip its online version check and tolerate a read-only working folder on Android.

## Item icons
This repository bundles only artwork that ships with PKHeX. Items that have no PKHeX icon show a placeholder. No ROM data or save files are included.

## License
GPL-3.0, like PKHeX and the Auto-Legality Mod it builds on. See `LICENSE`.
