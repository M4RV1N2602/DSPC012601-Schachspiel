# Testabdeckung

Die Regeltests laufen als dependencyfreies .NET-Konsolenprojekt gegen die interne Klasse `Board`.

## Ausführen

```powershell
dotnet run --project Schach.Tests\Schach.Tests.csproj
```

## Umfang

- Startposition und legale Anfangszüge
- unzulässige Züge, Zugwechsel und Zugverlauf
- Schutz des eigenen Königs bei gefesselten Figuren
- Schach, Schachmatt und Patt
- kurze Rochade, en passant und automatische Umwandlung zur Dame
- JSON-Roundtrip des gespeicherten Zugverlaufs und Wiederherstellung durch `Board.Load`
- Ablehnung eines ungültigen gespeicherten Zuges