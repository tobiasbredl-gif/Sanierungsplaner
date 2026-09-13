# Sanierungsplaner

Native Windows-Desktop-App mit C# und WPF auf .NET 10. Version 0.1.0 ist ein lauffähiges Grundgerüst mit deutschem Startfenster und Navigation zwischen Übersicht und App-Informationen.

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

Der Smoke-Test öffnet das echte WPF-Fenster kurz, lädt die XAML-Ressourcen und prüft beide Navigationsbefehle inklusive Änderungsbenachrichtigungen. Er beendet sich bei Fehlern mit Exitcode 1. Ein interaktiver Windows-Desktop ist erforderlich. Es werden keine externen Testpakete benötigt.

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
  Resources/               Farben und gemeinsame Stile
  ViewModels/              Oberflächenzustand und Navigation
  Views/                   WPF-Fenster und Layout
tests/Sanierungsplaner.SmokeTests/
docs/                      Architektur und Umfang
```

## Umfang dieser Version

Der verfügbare frühere Chat enthält keine konkreten fachlichen Anforderungen. Daher wurde bewusst eine Grundlage erstellt. Projektverwaltung, Maßnahmen, Kostenberechnung und Speicherung sind noch nicht implementiert. Die Startseite kennzeichnet das ausdrücklich.

Die Anwendung arbeitet lokal, stellt keine Netzwerkverbindungen her und enthält weder Anmeldung noch Credentials, Tokens oder Backend. GitHub-Zugangsdaten gehören ausschließlich zur Entwicklungsumgebung und niemals in die App oder ins Repository.

Weitere Entscheidungen stehen in [docs/architecture.md](docs/architecture.md). Einstieg in die verwendete UI-Technik: [Microsoft WPF-Dokumentation](https://learn.microsoft.com/dotnet/desktop/wpf/).
