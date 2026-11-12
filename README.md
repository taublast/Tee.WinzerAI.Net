# WinzerAI (.NET)

Rundenbasierte Weingut-Simulation – Neuauflage von [Tee.Winzer](../Tee.Winzer) auf .NET 11 mit einer gezeichneten Oberfläche (DrawnUi) als Windows-App. Ein Browser-Kopf (Blazor WebAssembly) ist vorbereitet und folgt, sobald DrawnUi und SkiaSharp unter .NET 11 für WebAssembly bauen.

- [docs/konzept.md](docs/konzept.md) – Architektur, Oberfläche, Spielkonzept, Phasen, Risiken und Stand der Umsetzung
- [docs/portierung.md](docs/portierung.md) – Regeln für die 1:1-Übertragung der TypeScript-Engine
- [docs/oberflaeche.md](docs/oberflaeche.md) – Bausteine und Regeln der Oberfläche
- [docs/mockups](docs/mockups) – Entwürfe

## Entwicklung

```powershell
dotnet build Winzer.slnx
dotnet test Winzer.slnx                                   # Regeltests und Vergleichspartien gegen das Original
dotnet run --project src/Winzer.Desktop                   # Spiel starten
dotnet run --project tools/Winzer.Sim -c Release          # Balancing-Report (Optionen: --seeds N --years N --rules 1|2)
```

Bildschirmfoto ohne Klicken (Entwicklerhilfe, sichert nichts):

```powershell
src\Winzer.Desktop\bin\Debug\net11.0-windows\WinzerAI.exe --shot bild.png --scene Keller --months 33 --play
```

## Aufbau

| Projekt | Inhalt |
|---|---|
| `src/Winzer.Engine` | Spielregeln ohne UI-Abhängigkeit. `Game.ApplyAction(state, action)` ist eine reine Funktion, der Zufall ist Seed-basiert. Eine Datei je Modul des Originals; `RulesV2.cs` enthält die erweiterten Regeln. |
| `src/Winzer.Presentation` | `GameSession` (Züge, Computergegner, Vorspulen, Sichern), `Navigator` (Zustand der Oberfläche), Lexikon, Schnittstellen für Spielstände und Ton |
| `src/Winzer.Ui` | Oberfläche aus DrawnUi-Steuerelementen in C#: `Kit` (Bausteine), `Shell` (Rahmen), `Scenes/` (Gut, Weinberg, Keller, Markt, Kontor, Dorf, Hauptmenü, Versteigerung, Lexikon) |
| `src/Winzer.Desktop` | WPF-Fenster mit einer gezeichneten Fläche, Spielstände unter `%LOCALAPPDATA%\WinzerAI\Spielstände` |
| `tests/Winzer.Engine.Tests` | Übertragene Regeltests, `TraceTests` (70 KI-Partien des Originals Zug für Zug), `RulesV2Tests` |
| `tools/Winzer.Sim` | Balancing-Report |

## Regelversionen

- **1 – Original:** verhält sich exakt wie das TypeScript-Spiel. Die Vergleichsprotokolle in `tests/Winzer.Engine.Tests/Traces` erzeugt `npm run trace` im alten Repository.
- **2 – Erweitert:** Ab-Hof-Preise selbst setzen, Jahresziele mit Jahresrückblick, Ambitionen, Gutsausbau und Stiftung, Prämierung mit drei Juroren, Lese in Halbmonaten und ein gemeinsamer Winterzug für Januar und Februar.

## Pipeline

- **CI** (`.github/workflows/ci.yml`): Bauen und Testen bei jedem Push auf `main` und jedem Pull Request.
- **Release** (`.github/workflows/release.yml`): Ein Versions-Tag (`v0.3.0`) baut die Desktop-App für x64 und ARM64 und legt einen Release-Entwurf an. Die Version steht in `Directory.Build.props`.

## Rechtliches

MIT-Lizenz wie das Original. Weingüter, Personen, Bio-Siegel und Prämierungen im Spiel sind frei erfunden.
