# Architektur und Ausgangspunkt

## Entscheidung

C# mit WPF auf .NET 10 bildet die native Windows-Oberfläche. Für das Grundgerüst sind keine zusätzlichen NuGet-Pakete erforderlich. Das SDK ist über `global.json` auf die .NET-10-Familie begrenzt; neuere installierte Feature-Bands innerhalb dieser Familie sind zulässig.

## Ablauf

`App.xaml` lädt die gemeinsamen Ressourcen und startet `Views/MainWindow.xaml`. Das Fenster setzt ein `MainWindowViewModel` als Datenkontext. WPF-Bindings lesen Titel und Beschreibung aus diesem Modell. Die Navigationsbuttons rufen `ICommand`-Befehle auf; das Modell meldet Änderungen über `INotifyPropertyChanged`, woraufhin WPF die Texte aktualisiert.

Das Code-behind enthält nur die Initialisierung. Fachlogik und zukünftige Speicherdienste sollen unabhängig vom Fenster implementiert werden. Separate Domain- und Infrastrukturprojekte werden erst ergänzt, wenn konkrete Anforderungen diese Trennung benötigen.

## Daten und Authentifizierung

Aktuell existiert nur flüchtiger Oberflächenzustand. Es gibt weder Dateien mit Projektdaten noch Datenbank, HTTP-Client, Benutzerkonto oder Token-Verarbeitung. Vor der späteren Umsetzung von Speicherung oder Cloud-Anbindung müssen Datenmodell, Speicherort und gegebenenfalls Authentifizierungsverfahren festgelegt werden.

## Prüfung

Der Windows-Smoke-Test prüft das Laden des echten Fensters samt Ressourcen und die Navigation. Optional akzeptiert er einen PNG-Zielpfad als einziges Argument zur Kontrolle des gerenderten Startfensters. Fachliche Tests kommen zusammen mit den jeweiligen Funktionen hinzu.

## Offene Produktentscheidungen

- Welche Gebäude- und Projektdaten werden erfasst?
- Welche Maßnahmen, Kosten und Zeitpläne werden benötigt?
- Welche lokalen Speicher- und Exportformate sind gewünscht?
- Wird später Zusammenarbeit oder eine Anmeldung benötigt?

Diese Punkte sind noch keine zugesagten Funktionen der Version 0.1.0.
