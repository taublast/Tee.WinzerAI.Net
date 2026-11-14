# Bildrate: DrawnUi 1.10.8.25 und 1.10.8.26 im Vergleich

Stand: 14.11.2026 · Ergebnis: **WinzerAI bleibt auf DrawnUi 1.10.8.25.** Mit 1.10.8.26 zeichnet das Spiel auf dem Testrechner
in den meisten Ansichten nur noch etwa 30 statt 55 Bilder pro Sekunde. Die Ursache ist der neue Bildtakt der WPF-Ansicht
in der Bibliothek. Am Code des Spiels lässt sich das nicht sauber beheben.

## Kurzfassung

| | 1.10.8.25 | 1.10.8.26 | 1.10.8.26 mit altem Takt |
|---|---|---|---|
| Bilder/s, Mittel aller Ansichten | **54,3** | 38,0 | **53,4** |
| Zeichenzeit je Bild, Mittel | 4,2 ms | 6,3 ms | 4,3 ms |

- 1.10.8.26 ist nicht schneller, sondern in fünf von sieben Ansichten deutlich langsamer. Das Gut, der Weinberg und das Dorf
  fallen auf rund die Hälfte.
- Schaltet man 1.10.8.26 auf den Takt von 1.10.8.25 zurück (`CompositionTarget.Rendering`), ist sie so schnell wie
  1.10.8.25. Der Zeichenkern ist also nicht langsamer geworden, aber auch nicht messbar schneller.
- Die Umgehungen im Spiel (`MainWindow.Show`: `InvalidateVisual` und `InvalidateBelow`) und die Art, wie der Messlauf
  Bilder anfordert, haben keinen Einfluss auf das Ergebnis.

## Testumgebung

- Rechner: Snapdragon X Elite (X1E80100), Windows 11 auf ARM, die App läuft nativ als ARM64
- Anzeige: 60 Hz, Arbeitsbereich 1440 × 912 WPF-Einheiten bei Skalierung 200 % (2880 × 1824 Pixel)
- .NET 11.0.0, Release-Build, beschleunigtes Zeichnen (ANGLE über `D3DImage`) wie im normalen Spiel
- Stromversorgung: Akku. Im Akkubetrieb schwanken die absoluten Werte von Lauf zu Lauf um bis zu 20 %. Deshalb liefen die
  Versionen immer abwechselnd, und verglichen wird nur innerhalb einer Messreihe.

## So wird gemessen

```powershell
dotnet build src\Winzer.Desktop -c Release
src\Winzer.Desktop\bin\Release\net11.0-windows\WinzerAI.exe --messen bericht.txt [--sekunden 5] [--takt wpf|bild] [--wpf-takt]
```

Der Messlauf (`src/Winzer.Desktop/Measure.cs`) öffnet das echte Spielfenster im Vollbild. Er zeichnet zuerst das
Hauptmenü. Danach spielt der Autopilot eine Partie (Regelversion 2, Mosel, Seed 7) bis in den Mai des zweiten Jahres,
und der Lauf zeichnet nacheinander Gut, Weinberg, Keller, Markt, Kontor und Dorf. Jede Ansicht wird 5 Sekunden lang
ständig neu gezeichnet, das Wetter bewegt sich dabei wie im Spiel.

- **Bilder/s:** tatsächlich gezeichnete Bilder (Ereignis `WasDrawn` der Fläche) geteilt durch die Messzeit. Bei 60 Hz
  sind höchstens 60 möglich.
- **Zeichenzeit:** Zeit von `WillDraw` bis `WasDrawn` je Bild, als Mittel, 95-%-Wert und Höchstwert
- `--takt wpf` fordert neue Bilder im Takt von `CompositionTarget.Rendering` an. `--takt bild` fordert gleich nach jedem
  gezeichneten Bild das nächste an, dann bestimmt allein DrawnUi den Takt.
- `--wpf-takt` schaltet DrawnUi ab 1.10.8.26 auf den alten Takt zurück (siehe unten). In 1.10.8.25 bleibt der Schalter
  ohne Wirkung.
- Die erste Zeile des Berichts nennt die DrawnUi-Version, die zweite Takt und Stromversorgung.

Für den Vergleich wurden beide Versionen nebeneinander gebaut:

```powershell
dotnet build src\Winzer.Desktop -c Release -p:DrawnUiVersion=1.10.8.25 -o <Ordner-25>
dotnet build src\Winzer.Desktop -c Release -p:DrawnUiVersion=1.10.8.26 -o <Ordner-26>
```

Im normalen Spiel wird nicht ständig neu gezeichnet. Das Wetter läuft mit 20 Bildern/s, Übergänge wie Überblenden und
Hochzählen mit bis zu 60. Der Messlauf zeigt also, wie viel Luft nach oben bleibt. Unter 1.10.8.26 würden vor allem die
Übergänge sichtbar ruckeln, weil sie statt 60 nur etwa 30 Bilder bekämen.

## Ergebnisse

### 1. Erste Messung (je zwei Läufe hintereinander)

| Ansicht | 1.10.8.25 Bilder/s | 1.10.8.26 Bilder/s |
|---|---|---|
| Hauptmenü | 52,8 / 51,9 | 45,9 / 29,5 |
| Gut | 54,2 / 51,2 | 41,0 / 38,4 |
| Weinberg | 44,5 / 46,7 | 26,9 / 30,2 |
| Keller | 56,5 / 56,1 | 56,5 / 44,6 |
| Markt | 55,9 / 56,7 | 44,6 / 54,6 |
| Kontor | 52,6 / 57,1 | 52,9 / 58,5 |
| Dorf | 37,2 / 38,4 | 24,3 / 27,2 |

Die Läufe mit 1.10.8.26 kamen nach denen mit 1.10.8.25. Ein warm gewordener Rechner hätte das Ergebnis verfälschen
können, deshalb liefen die folgenden Reihen abwechselnd.

### 2. Abwechselnd, je drei Läufe (`--takt wpf`)

| Ansicht | 1.10.8.25 Bilder/s | 1.10.8.26 Bilder/s | Zeichenzeit Ø 25 → 26 | 95 % 25 → 26 |
|---|---|---|---|---|
| Hauptmenü | 54,5 | 40,7 | 7,3 → 9,7 ms | 9,6 → 18,0 ms |
| Gut | 53,9 | 29,2 | 3,9 → 6,9 ms | 5,6 → 10,3 ms |
| Weinberg | 45,5 | 28,1 | 6,7 → 9,6 ms | 9,1 → 16,2 ms |
| Keller | 57,1 | 43,6 | 2,4 → 3,3 ms | 4,0 → 5,6 ms |
| Markt | 57,4 | 39,0 | 1,5 → 2,3 ms | 2,4 → 4,0 ms |
| Kontor | 56,7 | 52,2 | 1,7 → 2,1 ms | 2,5 → 3,0 ms |
| Dorf | 39,8 | 24,4 | 7,3 → 11,6 ms | 11,4 → 21,8 ms |

1.10.8.25 lieferte in allen drei Läufen fast dieselben Werte. 1.10.8.26 lag in jeder Ansicht darunter und schwankte stark,
etwa im Keller zwischen 30 und 55 Bildern/s.

### 3. Liegt es an der Art, wie das Spiel Bilder anfordert?

Das Spiel fordert ein Bild mit `Canvas.Update()` an und entwertet zusätzlich den WPF-Baum unter der Fläche
(`InvalidateVisual`, `InvalidateBelow`). Das ist eine Umgehung aus der Zeit, als im beschleunigten Modus sonst das alte
Bild stehen blieb. Diese Messreihe stellte beides gegenüber, jeweils mit beiden Anforderungsarten des Messlaufs
(Mittel aller Ansichten, Bilder/s):

| Anforderung | Anzeige | 1.10.8.25 | 1.10.8.26 |
|---|---|---|---|
| CompositionTarget.Rendering | `Update` + WPF-Baum entwerten (wie im Spiel) | 42,0 | 26,9 |
| CompositionTarget.Rendering | nur `Canvas.Update()` | 40,8 | 29,4 |
| nach jedem Bild | `Update` + WPF-Baum entwerten (wie im Spiel) | 43,8 | 28,7 |
| nach jedem Bild | nur `Canvas.Update()` | 38,9 | 28,4 |

Alle vier Varianten liegen innerhalb der Messschwankung beieinander, der Abstand zwischen den Versionen bleibt. Die Werte
dieser Reihe liegen insgesamt niedriger als in Reihe 2, weil der Rechner im Akkubetrieb sparsamer taktete.

Mit Software-Zeichnung (`--software`) sind beide Versionen gleich langsam: 12,9 und 11,4 Bilder/s im Mittel, bei 200 %
Skalierung ohne Grafikkarte nicht spielbar. Der Unterschied liegt also im beschleunigten Zweig.

### 4. 1.10.8.26 mit dem alten Takt (`--takt bild`, abwechselnd, je zwei Läufe)

| Ansicht | 1.10.8.25 | 1.10.8.26 | 1.10.8.26 mit `--wpf-takt` |
|---|---|---|---|
| Hauptmenü | 55,7 · 7,2 ms | 32,2 · 11,3 ms | 54,5 · 7,4 ms |
| Gut | 55,7 · 3,8 ms | 29,6 · 6,7 ms | 55,3 · 3,8 ms |
| Weinberg | 50,0 · 6,2 ms | 29,3 · 8,8 ms | 47,9 · 6,5 ms |
| Keller | 58,3 · 2,4 ms | 56,4 · 2,5 ms | 58,1 · 2,3 ms |
| Markt | 58,7 · 1,4 ms | 48,7 · 1,9 ms | 58,0 · 1,4 ms |
| Kontor | 58,8 · 1,6 ms | 44,1 · 2,5 ms | 58,1 · 1,7 ms |
| Dorf | 42,9 · 6,9 ms | 26,1 · 10,2 ms | 42,4 · 7,1 ms |
| **Mittel** | **54,3 · 4,2 ms** | **38,0 · 6,3 ms** | **53,4 · 4,3 ms** |

Angegeben sind jeweils Bilder/s und die mittlere Zeichenzeit je Bild.

## Was die Bibliothek zu 1.10.8.26 sagt

Die Release-Notes von [DrawnUI 1.10.8.26](https://github.com/DrawnUi/DrawnUi.Net/releases/tag/1.10.8.26) versprechen
für WPF „a steady 60 FPS“ und ein ruhendes Zeichnen bei minimiertem Fenster. Außerdem soll auf allen Plattformen
schneller gezeichnet werden, weil je Bild weniger Eigenschaften nachgeschlagen werden.

Für WPF stecken dahinter zwei Commits in `src/Wpf/Drawnui.Wpf/Views/DrawnUiElement.cs`:

- **230e9d3e:** Die Bilder kommen nicht mehr aus `CompositionTarget.Rendering`. Ein eigener Hintergrund-Thread wartet
  mit `DwmFlush()` auf jeden Durchgang des Desktop-Compositors und lässt dann auf dem UI-Thread zeichnen (Priorität
  `Render`). Laut Commit hat das die eigene 3D-Galerie der Bibliothek von 56–59 auf 59,9–60 Bilder/s gebracht.
- **14b1dcc6:** Der Thread schläft bei minimiertem Fenster und weckt den UI-Thread nicht mehr, wenn eine halbe Sekunde
  lang nichts zu zeichnen war.

Das eigentliche Zeichnen auf der Grafikkarte (`SkiaViewAccelerated.Angle.cs`) ist zwischen beiden Versionen unverändert.
Es sperrt ein `D3DImage`, zeichnet mit ANGLE in dessen Puffer, wartet mit `glFinish()` auf die Grafikkarte und gibt den
Puffer dann frei.

## Ursache

Der neue Takt ist die Ursache. Mit dem alten Takt erreicht 1.10.8.26 dieselben Werte wie 1.10.8.25 (Reihe 4).

Vermutlich fällt der Zeitpunkt, zu dem der `DwmFlush`-Thread zeichnen lässt, ungünstig mit dem Moment zusammen, in dem
WPF den `D3DImage`-Puffer für die Anzeige kopiert. `Lock()` und `glFinish()` warten dann, und ein Bild braucht zwei
Bildschirmtakte. Dafür spricht:

- Die leichten Ansichten (Markt, Kontor, 1,5 bis 2 ms Zeichenzeit) erreichen mit dem neuen Takt mal 56, mal 30 Bilder/s,
  ohne dass sich an der Arbeit etwas ändert.
- Die gemessene Zeichenzeit steigt mit dem neuen Takt, obwohl dieselbe Arbeit anfällt. Gemessen wird von `WillDraw` bis
  `WasDrawn`, die Wartezeit steckt also mit drin.

Belegt ist das nicht. Die Bibliothek hat ihre Verbesserung an einer eigenen Szene gemessen, vermutlich auf einem x64-Rechner.
Auf diesem ARM64-Rechner mit Adreno-Grafik und ANGLE kehrt sich der Effekt um. Einen x64-Rechner gab es für die Gegenprobe
nicht.

## Muss am Code des Spiels etwas angepasst werden?

Nein, nicht für 1.10.8.25. Für 1.10.8.26 gibt es keine Anpassung, die das Problem sauber löst:

- **Wie das Spiel Bilder anfordert:** Das Spiel stößt neue Bilder selbst an, mit einem `DispatcherTimer` (16 ms bei
  Übergängen, sonst 50 ms) und `Canvas.Update()`. Das passt auch zum neuen Takt der Bibliothek, die genau dann zeichnet,
  wenn die Fläche nach `Update()` als schmutzig gilt.
- **Die Umgehung `InvalidateBelow`:** Sie kostet nichts Messbares (Reihe 3). Unter 1.10.8.26 wäre sie wohl überflüssig,
  weil die Bibliothek die GPU-Fläche nach jedem Bild selbst auffrischt. Solange das Spiel auf 1.10.8.25 bleibt, wird sie
  noch gebraucht.
- **Den alten Takt erzwingen:** Das ginge nur über nicht öffentliche Teile von `DrawnUiElement` (`StopFrames`,
  `OnCompositionRendering`, `_renderingSubscribed`), wie es `--wpf-takt` im Messlauf tut. Für die Auslieferung ist das zu
  zerbrechlich, denn jede neue Version kann diese Teile umbenennen. Eine öffentliche Einstellung für den Takt gibt es nicht.
  Den alten Weg nimmt die Bibliothek nur, wenn `DwmFlush` fehlschlägt.

**Empfehlung:**

1. Auf 1.10.8.25 bleiben (`DrawnUiVersion` in `Directory.Build.props`).
2. Den Befund an das DrawnUi-Projekt melden: neuer WPF-Takt, ARM64, ANGLE/`D3DImage`, halbe Bildrate, mit den Zahlen
   aus Reihe 4. Hilfreich wäre ein öffentlicher Schalter für den Takt.
3. Jede neue DrawnUi-Version mit `--messen` (und `--wpf-takt`) gegen 1.10.8.25 prüfen, bevor sie übernommen wird.
