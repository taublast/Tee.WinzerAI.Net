# Oberfläche bauen: Regeln und Bausteine

Die Oberfläche liegt in `src/Winzer.Ui` und besteht aus DrawnUi-Steuerelementen, die in C# zusammengesetzt werden. Vorlage für jede Szene ist `Scenes/EstateScene.cs`; die Bausteine stehen in `Kit.cs`, der Rahmen in `Shell.cs`.

## Grundsätze

1. **Nichts scrollt.** Jede Ansicht passt auf die Bühne. Was nicht passt, wird geblättert (`Kit.Pager` mit `session.Nav.Page(...)`), in Reiter geteilt (`Kit.Tabs`) oder erst bei Auswahl im Inspektor gezeigt.
2. **Entwurfseinheiten.** Alle Maße beziehen sich auf eine Fläche von 1920 × 1080. `Kit` rechnet um; nie selbst mit `Kit.S` multiplizieren, außer bei Eigenschaften, die `Kit` nicht setzt.
3. **Neuaufbau statt Bindung.** Nach jeder Änderung (`session.Changed`) baut der Kopf den ganzen Baum neu. Szenen sind deshalb reine Funktionen `Build(GameSession)` ohne eigenen Zustand. Alles, was einen Neuaufbau überdauern muss (Reiter, Auswahl, Seite, Schieberstand), liegt im `Navigator` (`session.Nav`).
4. **Auswählen, dann handeln.** Links/Mitte die Dinge (Parzellen, Tanks, Posten), rechts der Inspektor mit Werten und Aktionen des gewählten Dings. Ist nichts gewählt, gilt das erste Ding.
5. **Jede Aktion sagt vorher, was sie tut.** Schaltflächen tragen eine Unterzeile mit Wirkung und Kosten. Geht eine Aktion gerade nicht, wird `session.Problem(action)` als `problem` übergeben – die Schaltfläche ist dann gesperrt und zeigt den Grund.
6. **Texte auf Deutsch**, Zahlen über `Util.FormatEuro` / `Util.FormatNumber`, gerundet mit `Js.Round`.

## Bühne

Eine Szene liefert einen `Kit.Layer(0, 0, Shell.StageWidth, Shell.StageHeight)` (1808 × 988). Kopfzeile und Leiste kommen vom Rahmen.

| Bereich | Lage auf der Bühne | Zweck |
|---|---|---|
| Hintergrund | 0, 0, 1808 × 988 | `Kit.Paint` mit eigener Szenenmalerei |
| Reiter | x 28, y 20 | `Kit.Tabs(28, 20, …)`; aktiver Reiter `session.Nav.Tab`, Wechsel `session.Nav.SetTab(i)` |
| Arbeitsfläche | x 28 … 1230, y 90 … 960 | Dinge zum Auswählen |
| Inspektor | `Kit.Card(1260, 20, 524, 948, 22)` | Titel (Serif 32), Werte (`Kit.Row`), Mengenwahl (`Kit.AmountSlider`), Aktionen (`Kit.Button`, 460 × 84, Abstand 96) |

## Bausteine (`Kit`)

- `Layer(x, y, w, h)` – Behälter mit eigenen Koordinaten; `.With(kinder…)` hängt Kinder an (null wird übergangen).
- `Box`, `Card` (Pergament), `Glass` (dunkle Tafel) – Flächen; sie können selbst Kinder tragen.
- `Text(text, x, y, w, h, größe, farbe, bold, serif, align, maxLines)` – kürzt mit „…“, bricht bei `maxLines > 1` um. Emojis funktionieren.
- `Button(titel, x, y, w, h, onTap, kind, sub, problem, fontSize, align)` – `ButtonKind.Primary` (Weinrot), `Secondary` (Rahmen, auf hellem Grund), `Ghost` (hell gefüllt), `Gold`, `Dark` (auf dunklem Grund).
- `IconButton`, `Badge`, `Pager`, `Tabs`, `Row`, `AmountSlider`.
- `Paint(x, y, w, h, (canvas, w, h) => …)` – freie Zeichnung mit SkiaSharp in Entwurfseinheiten; Hilfen in `Draw` (`Fill`, `Stroke`, `VerticalGradient`, `Path` mit SVG-Pfad, `Ring`).
- `.Tap(action)` macht jedes Element antippbar; `.At(x, y, w, h)` setzt Lage und Größe.
- Farben in `Theme` (DrawnUi-`Color` und `Sk…` für SkiaSharp). Auf dunklem Grund `Cream`/`CreamSoft`/`Gold`, auf Pergament `Ink`/`InkSoft`/`Wine`.

## Hover und Bewegung

- **Hover:** `Kit.Tap` macht ein Element antippbar und legt zugleich einen durchscheinenden Schleier mit goldenem Rand darüber, der nur unter dem Mauszeiger sichtbar ist; das Fenster zeigt dann die Hand. Wer eine Tippfläche baut, bekommt den Effekt also von selbst – nie `OnTapped` direkt aufrufen.
- **Bewegte Bilder:** Ein Maler, der sich bewegen soll, holt die Zeit mit `Anim.Use()` (Sekunden). Das Fenster zeichnet dann rund 20-mal je Sekunde neu, solange es vorn liegt; ohne bewegten Maler auf dem Schirm ruht es. Bewegung wird nur aus der Zeit berechnet, nichts wird gemerkt. Das Wetter (`Scenes/WeatherFx.cs`: Sonne, Wolken, Regen, Schnee, Laub, Sterne, Rauch) läuft in der Gutsansicht und im Hauptmenü.
- **Bausteine der Bewegung** (`Anim`): `Use()` für Dauerbewegung in Malern, `Tween(schlüssel, ziel)` für Werte, die weich auf ihr Ziel zulaufen (Konto und Punkte in der Kopfleiste, Füllstand der Tanks, Sonnenstand), `Since(schlüssel)` für die Zeit seit dem ersten Erscheinen (Aufdecken der Prämierung), `Drive(schritt)` für Steuerelemente, die keine Maler sind, und darauf aufbauend `Kit.Reveal` zum gestaffelten Einblenden. Eine neue Partie setzt alles zurück (`Anim.Reset`).
- **Wo sich etwas bewegt:** Kopfleiste (Zahlen zählen), Gutsansicht (Wetter, wiegende Rebzeilen, pulsierender Rand lesereifer Lagen, Rauch und Licht im Gutshaus, Vogelschwarm, Traktor zur Lese – `Scenes/EstateFx.cs`; Jahreszeiten blenden über), Keller (Gärbläschen, Füllstand steigt), Prämierung (Noten, Wertung und Medaille erscheinen nacheinander).
- **Gutsansicht:** `EstateScene.Layout` verteilt bis zu 24 Parzellen auf den Rebhang zwischen Hügelkante und Fluss – bis vier groß in einer Reihe, darüber in zwei oder drei Reihen mit schmalen einzeiligen Schildern. Alles wird auf den Hang beschnitten.

## Fenster

Das Spiel startet ohne Titelleiste in der Größe des Arbeitsbereichs (`MainWindow.FillWorkArea`), die Taskleiste bleibt sichtbar. Beendet wird über das Menü oder Alt+F4. Die Prüfläufe (`--shot`, `--selbstspiel`) nutzen ein gewöhnliches Fenster.

Beim Wechsel zwischen Hauptmenü und Szenen (`AppHost.View`) wird übergeblendet: `AppHost.Build` löst die alte Szene aus ihrem Baum, legt sie über die neue und lässt sie in einer Drittelsekunde durchsichtig werden. Die neue Szene ist darunter sofort bedienbar. `FadeSeconds = 0` schneidet hart. (Ein Foto des Fensters taugt dafür nicht: Im beschleunigten Zeichenmodus liefert WPF ein verschobenes Bild – das gilt auch für `--shot … --accel`, dessen Bild nur zur Not taugt; die Textdatei daneben stimmt.)

## Navigator

- `session.Nav.Go(scene, tab?)`, `SetTab(i)`
- `Selected("keller.batch")` / `Select("keller.batch", id)`
- `Page("keller.lots", seitenzahl)` / `SetPage(...)`
- `Amount("keller.amount", 1)` / `SetAmount(...)` – Anteil 0–1 für Mengen
- `Show(Overlay.…)`

## Spielzüge

`session.Run(new GameAction.Bottle(id, hl))` führt aus, sichert und baut neu auf. `session.Problem(action)` liefert den Grund, warum ein Zug nicht geht, oder `null`. Regeln und Berechnungen (Preise, Vorschau, Kosten) kommen aus `Winzer.Engine` – nichts davon in der Oberfläche nachrechnen, wenn die Engine eine Funktion dafür hat.

## Ansehen ohne Klicken

```powershell
$env:Path = "C:\Program Files\dotnet;" + $env:Path
dotnet build src\Winzer.Desktop
src\Winzer.Desktop\bin\Debug\net11.0-windows\WinzerAI.exe --shot bild.png --scene Keller --tab 1 --months 33 --play
```

`--shot` spielt eine Partie die angegebenen Züge weiter (`--play` liest, füllt ab und leiht dabei), zeigt Szene und Reiter, speichert ein Bild (1280 × 720) und beendet sich. Mit den erweiterten Regeln hat ein Jahr 14 Züge, `--months 9` ist also die erste Oktoberhälfte. Weitere Schalter: `--seed`, `--region`, `--rules 1|2`, `--overlay Lexicon|Menu|Decision|Plan|Review|Show|Auto`, `--otab`, `--summary`, `--menu`.

`--click 1750,1015` klickt vor dem Foto mit der echten Maus an eine Stelle der Entwurfsfläche, `--key Space` drückt eine echte Taste (`Space`, `Escape`, `Tab`, `Enter`, Ziffer oder Buchstabe); beides mehrfach und in dieser Reihenfolge. Neben dem Bild entsteht `bild.png.txt` mit dem Stand danach (Szene, Zug, Konto, Chargen …) – so lässt sich prüfen, ob eine Schaltfläche wirklich wirkt. Mehrere Prüfläufe gleichzeitig warten aufeinander, weil Klicks das vorderste Fenster treffen.

`--hover 1750,1015` stellt den echten Mauszeiger vor dem Foto auf eine Stelle (Hover-Effekt prüfen), `--plots 14` füllt das Gut fürs Bild auf so viele Parzellen auf, `--weather sonnig|wechselhaft|nass` setzt das Wetter des letzten Monats. In der Textdatei steht mit `zeichnungen=`, wie oft bemalte Flächen gezeichnet wurden – bei laufender Animation deutlich mehr als eine Handvoll.

## Eine ganze Partie von selbst spielen

```powershell
WinzerAI.exe --selbstspiel ordner [--seed 7] [--rules 1|2] [--region mosel] [--humans 2] [--target 600] [--wild] [--delay 70]
```

Der Autopilot (`Winzer.Presentation.AutoPilot`) zieht für die Menschen über die Spielsitzung; nach jedem Zug werden reihum alle Szenen, Reiter und Überlagerungen mit dem aktuellen Stand gezeichnet. Im Ordner landen Bildschirmfotos und `bericht.txt` mit Verlauf, benutzten Zugarten, der Zahl der Zeichenvorgänge und allen Fehlern. `--wild` lässt den Piloten auch riskante Züge ausprobieren (Panschen, Sabotage, Eiswein-Wette, Roden …). Der Lauf endet mit Code 0, wenn die Partie zu Ende gespielt wurde und nichts schiefging.

Das Fenster bleibt dabei vorn. Ein verdecktes Fenster zeichnet nicht – dann prüfte der Lauf nur den Aufbau, nicht das Malen; die Zahl der Zeichenvorgänge im Bericht zeigt, ob wirklich gemalt wurde.
