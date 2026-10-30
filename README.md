# Schach mit Avalonia

Ein kleines lokales Zwei-Personen-Schachspiel für Windows, macOS und Linux. Die Oberfläche ist mit Avalonia umgesetzt; die Schachregeln liegen in einer separaten C#-Klasse.

## Start

Voraussetzung: .NET 10 SDK und Internetzugang für das erstmalige Laden der Avalonia-Pakete.

```sh
Set-Location .\Schach.Desktop
dotnet run
```

Für Windows bauen:
```sh
Set-Location .\Schach.Desktop
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Für Linux bauen:
```sh
Set-Location .\Schach.Desktop
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Klicken Sie zuerst eine eigene Figur und danach das Zielfeld. „Speichern“ legt den Zugverlauf als JSON ab; „Laden“ stellt die Partie durch erneutes Anwenden der gespeicherten Züge wieder her.

Enthalten sind Zugwechsel, legale Züge einschließlich Schutz des eigenen Königs, Schach, Schachmatt, Patt, Rochade, en passant und automatische Umwandlung zur Dame. Die Umwandlungsauswahl, Remisregeln wie dreifache Stellungswiederholung und 50-Züge-Regel sowie Zeitkontrollen sind nicht enthalten.
