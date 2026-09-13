# Sanierungsplaner

Native Windows-Desktop-App mit C# und WPF auf .NET 10. Version 0.2.0 bietet eine lokale Projektverwaltung mit deutschem Startfenster.

## Ein Projekt anlegen

1. **Neues Projekt** auswählen.
2. Einen Projektnamen eingeben. Objektadresse und Notizen sind optional.
3. **Projekt speichern** anklicken und über **Alle Projekte** zur Übersicht zurückkehren.
4. Ein gespeichertes Projekt mit **Projekt öffnen** weiterbearbeiten.

Die Projekte werden beim nächsten App-Start wieder geladen. Beim Verlassen eines bearbeiteten Projekts oder Schließen der App kann man Änderungen speichern, verwerfen oder weiterbearbeiten. Der Wechsel zu **Über die App** erhält den aktuellen Entwurf; **Projekte** führt zu ihm zurück.

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

Enthalten sind Projektname (Pflichtfeld, maximal 120 Zeichen), Objektadresse (300 Zeichen), Notizen (10.000 Zeichen), Anlege- und Änderungszeitpunkt sowie eine nach letzter Änderung sortierte Projektübersicht. Maßnahmen, Kostenberechnung, Löschen, Import/Export und Cloud-Synchronisierung sind noch nicht implementiert.

## Lokale Daten und Sicherung

Projekte liegen unter `%LOCALAPPDATA%\Sanierungsplaner\Projects`, jeweils in einer JSON-Datei. Der konkrete Pfad steht unter **Über die App** und kann dort kopiert werden. Die portable App speichert Daten außerhalb ihres Programmordners; eine neue Programmversion verwendet denselben Speicherort.

Es gibt noch keine automatische Datensicherung. Zum Sichern bei geschlossener App den gesamten Projektordner kopieren. Zur Wiederherstellung die gesicherten Projektdateien in diesen Ordner zurückkopieren, bevor die App gestartet wird. Die Dateien enthalten Adresse und Notizen im Klartext und sind über das Windows-Benutzerkonto geschützt, nicht zusätzlich verschlüsselt.

Bei einem Lesefehler zeigt die App den betroffenen Dateinamen an und sperrt neue Bearbeitungen. Die Originaldateien werden nicht überschrieben. Nach Behebung des Problems **Erneut laden** wählen. Bei einem Speicherkonflikt bleiben die Formulareingaben erhalten: Änderungen bei Bedarf kopieren, über **Alle Projekte** den alten Entwurf verwerfen und **Erneut laden** wählen.

Die Anwendung arbeitet lokal, stellt keine Netzwerkverbindungen her und enthält weder Anmeldung noch Credentials, Tokens oder Backend. GitHub-Zugangsdaten gehören ausschließlich zur Entwicklungsumgebung und niemals in die App oder ins Repository.

Weitere Entscheidungen stehen in [docs/architecture.md](docs/architecture.md). Einstieg in die verwendete UI-Technik: [Microsoft WPF-Dokumentation](https://learn.microsoft.com/dotnet/desktop/wpf/).
