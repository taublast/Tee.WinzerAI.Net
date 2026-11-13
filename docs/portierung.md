# Portierungsregeln: TypeScript-Engine → C#

Quelle: [`src/engine/`](https://github.com/teebeast/Tee.WinzerAI/tree/main/src/engine) im Original-Repository [Tee.WinzerAI](https://github.com/teebeast/Tee.WinzerAI). Ziel: `src/Winzer.Engine/` (Namensraum `Winzer.Engine`, Stammdaten in `Winzer.Engine.Data`).

Die Portierung ist **1:1**. Reihenfolge der Anweisungen, der Zufallsaufrufe und der Rundungen bleibt exakt wie im Original, damit Partien mit gleichem Seed gleich verlaufen. Nichts „verbessern“, nichts umstrukturieren.

## Zuordnung

| TypeScript | C# |
|---|---|
| Datei `foo.ts` | `Foo.cs` mit `public static class Foo` |
| `cellarmaster.ts` | `CellarMasters.cs`, Klasse `CellarMasters` (der Typ `CellarMaster` ist schon vergeben) |
| `data/bio.ts` | `Data/BioData.cs`, Klasse `BioData` (das Modul `bio.ts` wird `Bio`) |
| exportierte Funktion `fooBar(a, b)` | `public static Foo.FooBar(a, b)` – gleicher Name in PascalCase, gleiche Parameterreihenfolge |
| nicht exportierte Funktion | `static` (privat) in derselben Klasse; `internal`, wenn andere Module sie brauchen |
| Konstante `FOO_BAR` | `FooBar` (in `Economy`: bereits vorhanden) |
| `GRAPES[id]`, `REGIONS[id]` | `Grapes.All[id]`, `Regions.All[id]` |
| `QUALITY_LEVELS`, `qualityInfo(l)`, `levelForOechsle(o)` | `Quality.QualityLevels`, `Quality.QualityInfo(l)`, `Quality.LevelForOechsle(o)` |
| `BIO_LABELS`, `BIO_ORDER`, `bioInfo(p)` | `BioData.BioLabels`, `BioData.BioOrder`, `BioData.BioInfo(p)` |
| `nextFloat(rng)`, `nextRange(rng, a, b)` | `Rng.NextFloat(rng)`, `Rng.NextRange(rng, a, b)` |
| `newId`, `addLog`, `formatEuro` | `Util.NewId`, `Util.AddLog`, `Util.FormatEuro` |
| `x.toLocaleString('de-DE')` | `Util.FormatNumber(x)`; mit `maximumFractionDigits: n` → `Util.FormatNumber(x, n)` |
| Zahl in Template-String `${x}` | ganzzahlige `int` direkt; `double` über `Util.JsNumber(x)` |
| `Math.round(x)` | `Js.Round(x)` (rundet .5 nach oben wie JavaScript – **nie** `Math.Round`) |
| `Math.floor/ceil/min/max/abs/pow/sqrt` | `Math.Floor/Ceiling/Min/Max/Abs/Pow/Sqrt` |
| `structuredClone(state)` | `state.Clone()` (auch `player.Clone()`, `batch.Clone()` …) |
| `throw new RuleError('…')` | `throw new RuleException("…")` |
| `Action` mit `type: 'plant'` | `GameAction.Plant` usw. (siehe `Actions.cs`); `autoSkip` heißt `GameAction.AutoSkipTurn` |
| String-Union (`'gold' \| 'silber'`) | Enum aus `Types.cs` |
| `tone: 'good' \| 'bad'` | `LogTone.Good` / `LogTone.Bad` |
| `TrendKey` aus `GrapeId` | `(TrendKey)(int)grape`; `'sekt'` → `TrendKey.Sekt`, `'rose'` → `TrendKey.Rose` |
| optionale Wahrheitswerte (`hailNet?`, `rose?`) | `bool`, Standard `false` |
| `Record<string, number>` im Zustand | `Dictionary<string, double>` (Einfügereihenfolge bleibt erhalten, nichts entfernen und neu einfügen, wo das Original es nicht tut) |
| `Number.MAX_SAFE_INTEGER` | `Js.MaxSafeInteger`; `NO_POINT_TARGET` → `Rules.NoPointTarget` |

## Zahlen

- Geld, Punkte, Bekanntheit, Hektar, Hektoliter, Flaschen, Oechsle, Güte, Faktoren: `double` (wie in `Types.cs`).
- Jahr, Monat, fortlaufender Spielmonat, Zähler, Indizes: `int`.
- Wo das Original eine Kommazahl in ein `int`-Feld schreibt, vorher so runden, wie das Original es tut. Explizite Casts nur, wenn der Wert sicher ganzzahlig ist.
- Vergleiche und Rechenreihenfolge nicht umstellen (Gleitkomma ist nicht assoziativ).

## Verhalten, auf das zu achten ist

- **Reihenfolge der Zufallsaufrufe** ist heilig. Kurzschlussauswertung (`a && nextFloat() < p`) genauso übernehmen. Kein Zufallsaufruf darf in einen LINQ-Ausdruck wandern, der ihn öfter, seltener oder in anderer Reihenfolge ausführt.
- `array.sort(cmp)` ist in JavaScript **stabil**. `List.Sort` ist es nicht – deshalb `OrderBy`/`OrderByDescending(...).ThenBy(...)` (stabil) verwenden und das Ergebnis zurückschreiben.
- `array.filter(...)` erzeugt ein neues Array; wenn das Original das Ergebnis zuweist (`player.cellar = player.cellar.filter(...)`), in C# ebenfalls eine neue Liste zuweisen.
- `Object.keys/values/entries` über Stammdaten: Reihenfolge der Wörterbücher in `Data/` entspricht dem Original.
- Texte (Logmeldungen, Fehlermeldungen, Namen) wortgleich übernehmen, inklusive Emojis und typografischer Zeichen.
- `x ?? y`, `x?.y` und optionale Felder entsprechen `null`-Prüfungen. `0`, `''` und `null` sind in JavaScript „falsy“ – bei `if (x)` prüfen, welcher Fall gemeint ist.
- Kommentare des Originals sinngemäß mitnehmen (deutsch).

## Bewusste Abweichungen vom Original

Die Vergleichspartien bleiben davon unberührt (sie laufen ohne erreichbares Punkteziel).

- `Ai.PlayAiTurn`: Steht mitten im Zug ein Sieger fest (Weinprüfung, gelieferter Auftrag), beendet der Computergegner seinen Zug nicht mehr. Das Original scheiterte hier mit „Das Spiel ist beendet“ und die Partie blieb stehen; dort ist der Fehler inzwischen genauso behoben.
- `Vineyard.RipeningGain`: neue Funktion mit derselben Rechnung wie in `GrowPlot`, für die Halbmonate der Regelversion 2.
- Alles in `RulesV2.cs` und die mit `RulesV2.On(state)` bzw. `v2` markierten Stellen in `Game.cs`, `Ai.cs` und `Advisor.cs`.

## Bereits vorhanden (nicht neu anlegen)

`Types.cs`, `Actions.cs`, `Rng.cs`, `Util.cs` (`Util`, `Js`), `Weather.cs`, `Rules.cs`, `Data/Economy.cs`, `Data/Grapes.cs`, `Data/Quality.cs`, `Data/Regions.cs`, `Data/BioData.cs`.

Fehlt in diesen Dateien etwas (Feld, Hilfsfunktion), das ein Modul braucht: in der eigenen Datei als `internal static` ergänzen oder im Abschlussbericht nennen – die gemeinsamen Dateien nicht ändern.
