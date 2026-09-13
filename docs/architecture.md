# Architektur

## Entscheidung

C# mit WPF auf .NET 10 bildet die native Windows-Oberfläche. Für das Grundgerüst sind keine zusätzlichen NuGet-Pakete erforderlich. Das SDK ist über `global.json` auf die .NET-10-Familie begrenzt; neuere installierte Feature-Bands innerhalb dieser Familie sind zulässig.

## Ablauf

`App.xaml` lädt die gemeinsamen Ressourcen und startet `Views/MainWindow.xaml`. Das Fenster setzt ein `MainWindowViewModel` als Datenkontext. WPF-Bindings lesen Titel und Beschreibung aus diesem Modell. Die Navigationsbuttons rufen `ICommand`-Befehle auf; das Modell meldet Änderungen über `INotifyPropertyChanged`, woraufhin WPF die Texte aktualisiert.

Das Code-behind initialisiert den Datenkontext und delegiert den Schließschutz an das ViewModel. `IProjectStore` trennt die Dateispeicherung vom Oberflächenzustand; `IUnsavedChangesPrompt` macht Speichern/Verwerfen/Abbrechen unabhängig vom Windows-Dialog testbar. Separate Domain- und Infrastrukturprojekte werden erst ergänzt, wenn konkrete Anforderungen diese Trennung benötigen.

## Daten und Authentifizierung

`RenovationProject` ist ein unveränderlicher Datensatz mit GUID, Revisions-GUID, Name, Adresse, Notizen und UTC-Zeitstempeln. Die Oberfläche arbeitet mit einem getrennten Entwurf. Erst nach erfolgreichem Speichern wird dieser in die Projektliste übernommen. Ein fehlgeschlagener Schreibversuch erhält den Entwurf.

`JsonProjectStore` schreibt pro Projekt eine Datei `<GUID>.json` unter `%LOCALAPPDATA%\Sanierungsplaner\Projects`. Jedes Dokument trägt `SchemaVersion: 1`; unbekannte Versionen und ungültige Daten führen zu einem sichtbaren Fehler. Datendateien liegen außerhalb des Repositories und des Veröffentlichungsordners.

Speichervorgänge schreiben zuerst eine temporäre Datei im selben Ordner, flushen deren Inhalt und ersetzen erst danach die Zieldatei. Eine exklusive `.write.lock`-Dateisperre serialisiert Lese- und Schreibvorgänge der App. Der Vergleich der erwarteten Revision verhindert das stille Überschreiben einer zwischenzeitlich in einem anderen Fenster gespeicherten Änderung. `.tmp`-Reste werden beim Lesen ignoriert. Es gibt noch keine automatische Sicherung; die Dateiersetzung ersetzt keine Backup-Strategie. Direkte Änderungen durch andere Programme unterliegen nicht der App-Dateisperre.

Für die kleine lokale Projektmenge erfolgen Dateioperationen synchron. Bei großen Datenmengen oder weiteren Dateitypen sollten sie asynchron mit Ladezustand und Abbruchmöglichkeit erfolgen.

Die App besitzt weiterhin keinen HTTP-Client, Benutzerlogin oder Token-Verarbeitung. Projektdaten werden nicht verschlüsselt und verlassen den Computer nicht durch die App.

## Prüfung

Der Windows-Test prüft die JSON-Persistenz samt Umlauten und Zeilenumbrüchen, Überarbeitung mit stabiler Projekt-ID, veraltete Revisionen, Dateisperren, beschädigte und unbekannte Dateiformate, Pflichtfelder, Schreibfehler, Wiederladen und Speichern/Verwerfen/Abbrechen. Der Oberflächentest befüllt echte WPF-Textfelder und bedient Buttons über Automation-Peers. Er prüft außerdem das Abbrechen des Fensterschließens. Alle Daten sind temporär und von Benutzerprojekten getrennt.

Optional akzeptiert der Test einen PNG-Zielpfad als einziges Argument. Neben der Projektübersicht entstehen `.detail.png` und `.small.png` zur visuellen Kontrolle des Editors und seiner Mindestgröße.

## Offene Produktentscheidungen

- Welche zusätzlichen Gebäude- und Projektdaten werden benötigt?
- Welche Maßnahmen, Kosten und Zeitpläne werden benötigt?
- Welche Exportformate und Sicherungsfunktionen sind gewünscht?
- Wird später Zusammenarbeit oder eine Anmeldung benötigt?

Diese Punkte sind noch keine zugesagten Funktionen der Version 0.2.0. Der nächste vorgesehene Ausbauschritt ist die Maßnahmen- und Kostenplanung pro Projekt.
