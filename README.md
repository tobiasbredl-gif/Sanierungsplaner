# Sanierungsplaner

Native Windows-Desktop-App mit C# und WPF auf .NET 10. Version 0.4.0 bietet lokale Projekte, automatisch zusammengefasste Einkäufe mit Datum und protokollierte Rückzahlungen von Tobias an Lea, Wolfgang und Jennifer.

## Ein Projekt anlegen

1. **Neues Projekt** auswählen.
2. Einen Projektnamen eingeben. Objektadresse und Notizen sind optional.
3. **Projekt speichern** anklicken und über **Alle Projekte** zur Übersicht zurückkehren.
4. Ein gespeichertes Projekt mit **Projekt öffnen** weiterbearbeiten.

Die Projekte werden beim nächsten App-Start wieder geladen. Beim Verlassen eines bearbeiteten Projekts oder Schließen der App kann man Änderungen speichern, verwerfen oder weiterbearbeiten. Der Wechsel zu **Über die App** erhält den aktuellen Entwurf; **Projekte** führt zu ihm zurück.

## Kosten und Zahlungen erfassen

1. Ein Projekt öffnen und **Kosten & Zahlungen** auswählen.
2. Über **Einkauf / Position erfassen** Material/Beschreibung, Datum, Menge, Einheit und Einzelpreis erfassen. Das Datum ist mit heute vorbelegt und für ältere Einkäufe änderbar. Raum und Etage sind optional. Es gibt keine Kategorien oder Unterteilung nach Gewerken.
3. Den Status **Geplant**, **Gekauft** oder **Verbaut** wählen.
4. Bereits gezahlte Beträge in den vier Personenzeilen eintragen; für alle anderen steht dort 0,00. Teilzahlungen und mehrere Zahlende pro Position sind möglich.
5. **Position übernehmen**, anschließend **Projekt speichern** anklicken. Übernehmen allein sichert noch nicht auf der Festplatte.

Die vier Summenzeilen bleiben auch bei 0,00 € sichtbar. Unter **Einzelne Einträge mit Datum** lassen sich die einzelnen Einkäufe ansehen, bearbeiten oder nach Rückfrage entfernen. Geplante Positionen dürfen noch keine Zahlungen enthalten. Gekauft/Verbaut bedeutet nicht automatisch bezahlt: Dafür zählen ausschließlich die eingetragenen Zahlungen. Eine Zahlung über den Positionskosten wird abgewiesen.

Beim ersten Anklicken bzw. beim Tastaturfokus werden Menge, Einzelpreis, Zahlungen, Projektbudget und Rückzahlungsbetrag vollständig markiert. Tippen ersetzt den bisherigen Inhalt sofort. Ohne Eingabe bleibt der Wert erhalten; ein weiterer Klick im bereits fokussierten Feld erlaubt die normale Cursorpositionierung.

## Wiederholte Einkäufe

Neue Einträge mit gleichem Namen, gleicher Einheit, gleichem Raum und gleicher Etage werden automatisch unter einer Position angezeigt. Groß-/Kleinschreibung und überflüssige Leerzeichen spielen keine Rolle. Einträge mit abweichender Einheit oder Zuordnung bleiben getrennt.

Beispiel: 10 kg Estrich zu 1 € und später 5 kg zu 2 € erscheinen als **Estrich: 15 kg, 20 €**. Beide Einkäufe behalten ihr eigenes Datum, ihren Einzelpreis und ihre Zahlungsaufteilung. Die Gesamtkosten sind die Summe der einzeln gerundeten Einkaufsbeträge; unterschiedliche Preise werden nicht überschrieben. Auch zwei tatsächlich getrennte Einkäufe am selben Tag bleiben im Verlauf erhalten.

Bei leichten Tippfehlern wie **Estrcih** fragt die App, ob die vorhandene Position **Estrich** gemeint ist. Man kann einen vorgeschlagenen Treffer bestätigen, bewusst eine neue Position anlegen oder abbrechen. Ähnliche Namen werden niemals ohne Bestätigung zusammengeführt. Die Erkennung ist eine Schreibhilfe, keine automatische Materialbestimmung; unterschiedliche Zahlen in Materialnamen werden nicht als Tippfehler vorgeschlagen.

## Rückzahlungen und Protokoll

1. Unter **Kosten & Zahlungen** in der Zeile von Lea, Wolfgang oder Jennifer auf **Zurückzahlen** klicken.
2. Den bereits zurückgezahlten Betrag erfassen. Der vollständige noch offene Betrag und das heutige Datum sind vorbelegt; Teilrückzahlungen und ein älteres Datum sind möglich. Optional eine Notiz ergänzen.
3. **Rückzahlung übernehmen** und anschließend **Projekt speichern** klicken.

Der offene Betrag der Person sinkt entsprechend. Dieselbe Summe erhöht Tobias' Ausgaben. Beispiel: Lea hat 100 € bezahlt; Tobias erstattet 100 €. Lea steht bei 0 €, Tobias trägt zusätzlich 100 €. Die ursprünglichen Einkäufe, Gesamtausgaben und das Restbudget bleiben unverändert. Bei späteren Einkäufen entsteht wieder ein offener Betrag. Die App erfasst Rückzahlungen lediglich; sie führt keine Banküberweisung aus.

Im **Rückzahlungsprotokoll** stehen Empfänger, Betrag, Rückzahlungsdatum, Erfassungszeit und Notiz. Eine versehentliche Buchung lässt sich nach Rückfrage **stornieren**: Der ursprüngliche Eintrag bleibt erhalten, ein zusätzlicher Storno stellt die vorherigen Salden wieder her. Gespeicherte Protokolleinträge können über die App nicht überschrieben oder gelöscht werden. Dies ist ein lokales Anwendungsprotokoll, kein manipulationssicheres Finanzarchiv.

Mehr als der offene Betrag kann nicht zurückgezahlt werden. Ebenso dürfen ursprüngliche Einkäufe nachträglich nicht so weit reduziert oder entfernt werden, dass die bereits erfolgten Rückzahlungen höher wären als die ursprünglichen Zahlungen. In diesem Fall zuerst den fehlerhaften Rückzahlungseintrag stornieren.

## Budget

Unter **Budget und Gesamtkosten** lässt sich das Projektbudget eintragen. Alle Werte sind Eurobeträge; Zahlen mit Dezimalkomma und ohne Tausendertrennzeichen eingeben. Beträge haben maximal zwei, Mengen maximal drei Nachkommastellen. Mengen und Einzelpreise sind auf 1.000.000 begrenzt, das Budget auf 1.000.000.000 €. Eine Kostenposition wird als Menge × Einzelpreis kaufmännisch auf Cent gerundet.

- **Kalkulierte Kosten:** Summe aller Positionen, unabhängig vom Status.
- **Tatsächlich bezahlt:** Summe der vier Personenzahlungen.
- **Noch zu bezahlen:** Kalkulierte Kosten minus Zahlungen; enthält auch geplante Anschaffungen.
- **Restbudget:** Budget minus Zahlungen.
- **Budget minus kalkulierte Kosten:** Verbleibender Spielraum in der Planung; negative Werte zeigen eine Überschreitung.

Es gibt keine automatische gleichmäßige Aufteilung der Kosten. Die Übersicht zeigt die ursprünglichen Einkäufe, zurückerhaltene Beträge bzw. von Tobias übernommene Ausgaben und den jeweils verbleibenden Anteil.

## Voraussetzungen

- Windows 10 oder Windows 11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) zum Entwickeln und Bauen
- Optional: eine Entwicklungsumgebung mit Unterstützung für .NET 10 und WPF

## Starten

Im Repository-Ordner:

```powershell
dotnet run --project src/Sanierungsplaner.Desktop
```

Alternativ `Sanierungsplaner.slnx` in einer kompatiblen IDE öffnen und `Sanierungsplaner.Desktop` als Startprojekt wählen.

## Bauen und prüfen

```powershell
dotnet build Sanierungsplaner.slnx --configuration Release
dotnet run --project tests/Sanierungsplaner.SmokeTests --configuration Release
```

Der Test prüft Anlegen, Bearbeiten, erneutes Laden, Pflichtfelder, Speicherkonflikte, beschädigte Dateien, Schreibfehler und den Schutz ungespeicherter Änderungen. Er öffnet das echte WPF-Fenster kurz und bedient die Formular- und Speicherbuttons über die Windows-Automatisierungsschnittstelle. Testdaten liegen in einem eigenen temporären Ordner und werden anschließend entfernt; echte Projekte bleiben unberührt. Bei Fehlern endet der Test mit Exitcode 1. Ein interaktiver Windows-Desktop ist erforderlich. Es werden keine externen Testpakete benötigt.

## Eigenständig startbare Windows-Version

```powershell
dotnet publish src/Sanierungsplaner.Desktop --configuration Release --runtime win-x64 --self-contained true --output artifacts/win-x64
```

Anschließend `artifacts/win-x64/Sanierungsplaner.exe` starten. Den **gesamten** Ordner weitergeben; auf dem Zielgerät ist keine separate .NET-Installation erforderlich. Dies ist eine portable Ausgabe, noch kein Installer und nicht digital signiert.

## Projektstruktur

```text
src/Sanierungsplaner.Desktop/
  App.xaml                 Anwendungsstart und globale Ressourcen
  Commands/                Befehle für die Oberfläche
  Models/                  Projektdaten und Validierung
  Resources/               Farben und gemeinsame Stile
  Services/                Lokale Speicherung und Rückfragen
  ViewModels/              Oberflächenzustand und Navigation
  Views/                   WPF-Fenster und Layout
tests/Sanierungsplaner.SmokeTests/
docs/                      Architektur und Umfang
```

## Umfang dieser Version

Enthalten sind Projektname (Pflichtfeld, maximal 120 Zeichen), Objektadresse (300 Zeichen), Notizen (10.000 Zeichen), Zeitstempel und Kostenpositionen samt Zahlungen und Budget. Excel-Export, Auswertungen nach Raum/Etage, Android-App und abgesicherte WLAN-Synchronisierung folgen in weiteren Schritten. Die abgestimmten Anforderungen stehen in [docs/product-requirements.md](docs/product-requirements.md).

Bestehende Projekte aus v0.2 und v0.3 werden weiterhin geladen. Fehlende Einkaufsdaten erscheinen als **Datum unbekannt (Altbestand)**; die App erfindet kein Datum. Bereits vorhandene gleiche Positionen werden in der Ansicht gruppiert, ihre ursprünglichen Einträge bleiben erhalten. Alte Projekte beginnen ohne Rückzahlungen. Erst beim Speichern wird auf Dateiformat 3 aktualisiert; danach ist mindestens App-Version 0.4 erforderlich. Ältere App-Versionen verweigern das unbekannte Format, anstatt Informationen zu überschreiben.

## Lokale Daten und Sicherung

Projekte liegen unter `%LOCALAPPDATA%\Sanierungsplaner\Projects`, jeweils in einer JSON-Datei. Der konkrete Pfad steht unter **Über die App** und kann dort kopiert werden. Die portable App speichert Daten außerhalb ihres Programmordners; eine neue Programmversion verwendet denselben Speicherort.

Es gibt noch keine automatische Datensicherung. Zum Sichern bei geschlossener App den gesamten Projektordner kopieren. Zur Wiederherstellung die gesicherten Projektdateien in diesen Ordner zurückkopieren, bevor die App gestartet wird. Die Dateien enthalten Adresse und Notizen im Klartext und sind über das Windows-Benutzerkonto geschützt, nicht zusätzlich verschlüsselt.

Bei einem Lesefehler zeigt die App den betroffenen Dateinamen an und sperrt neue Bearbeitungen. Die Originaldateien werden nicht überschrieben. Nach Behebung des Problems **Erneut laden** wählen. Bei einem Speicherkonflikt bleiben die Formulareingaben erhalten: Änderungen bei Bedarf kopieren, über **Alle Projekte** den alten Entwurf verwerfen und **Erneut laden** wählen.

Die Anwendung arbeitet lokal, stellt keine Netzwerkverbindungen her und enthält weder Anmeldung noch Credentials, Tokens oder Backend. GitHub-Zugangsdaten gehören ausschließlich zur Entwicklungsumgebung und niemals in die App oder ins Repository.

Weitere Entscheidungen stehen in [docs/architecture.md](docs/architecture.md). Einstieg in die verwendete UI-Technik: [Microsoft WPF-Dokumentation](https://learn.microsoft.com/dotnet/desktop/wpf/).
