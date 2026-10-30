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

Zum Bauen für Android muss die .NET-Android-Toolchain und die Java JDK installiert werden.
Die .NET-Android-Toolchain Version 36.1.118 funktioniert mit Java JDK 17 bis 21. JDK 25 geht auch, produziert aber Warnungen.
Wenn JAVA_HOME nicht gesetzt ist muss `-p:JavaSdkDirectory=C:\path\to\jdk-25.0.5.0.3` angehängt werden.

Für Android bauen:
```sh
Set-Location .\Schach.Android
dotnet publish -f net10.0-android -c Release -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=myapp.keystore -p:AndroidSigningKeyAlias=myapp -p:AndroidSigningKeyPass=123456 -p:AndroidSigningStorePass=123456
```

Klicken Sie zuerst eine eigene Figur und danach das Zielfeld. „Speichern“ legt den Zugverlauf als JSON ab; „Laden“ stellt die Partie durch erneutes Anwenden der gespeicherten Züge wieder her.

Enthalten sind Zugwechsel, legale Züge einschließlich Schutz des eigenen Königs, Schach, Schachmatt, Patt, Rochade, en passant und automatische Umwandlung zur Dame. Die Umwandlungsauswahl, Remisregeln wie dreifache Stellungswiederholung und 50-Züge-Regel sowie Zeitkontrollen sind nicht enthalten.
