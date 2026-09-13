# Architektur

## Entscheidung

C# mit WPF auf .NET 10 bildet die native Windows-Oberfläche. Für das Grundgerüst sind keine zusätzlichen NuGet-Pakete erforderlich. Das SDK ist über `global.json` auf die .NET-10-Familie begrenzt; neuere installierte Feature-Bands innerhalb dieser Familie sind zulässig.

## Ablauf

`App.xaml` lädt die gemeinsamen Ressourcen und startet `Views/MainWindow.xaml`. Das Fenster setzt ein `MainWindowViewModel` als Datenkontext. WPF-Bindings lesen Titel und Beschreibung aus diesem Modell. Die Navigationsbuttons rufen `ICommand`-Befehle auf; das Modell meldet Änderungen über `INotifyPropertyChanged`, woraufhin WPF die Texte aktualisiert.

Das Code-behind initialisiert den Datenkontext und delegiert den Schließschutz an das ViewModel. `IProjectStore` trennt die Dateispeicherung vom Oberflächenzustand; `IUnsavedChangesPrompt` macht Speichern/Verwerfen/Abbrechen unabhängig vom Windows-Dialog testbar. Separate Domain- und Infrastrukturprojekte werden erst ergänzt, wenn konkrete Anforderungen diese Trennung benötigen.

## Daten und Authentifizierung

`RenovationProject` ist ein unveränderlicher Datensatz mit GUID, Revisions-GUID, Name, Adresse, Notizen und UTC-Zeitstempeln. Die Oberfläche arbeitet mit einem getrennten Entwurf. Erst nach erfolgreichem Speichern wird dieser in die Projektliste übernommen. Ein fehlgeschlagener Schreibversuch erhält den Entwurf.

`JsonProjectStore` schreibt pro Projekt eine Datei `<GUID>.json` unter `%LOCALAPPDATA%\Sanierungsplaner\Projects`. Neue Dokumente tragen `SchemaVersion: 3`; Versionen 1 und 2 bleiben lesbar. Fehlende Kosten/Budgets (v1), Einkaufsdaten und Rückzahlungen werden mit leerem Bestand bzw. unbekanntem Datum repräsentiert. Erst beim nächsten Speichern erfolgt die Aktualisierung. Unbekannte Versionen und ungültige Daten führen zu einem sichtbaren Fehler. Datendateien liegen außerhalb des Repositories und des Veröffentlichungsordners.

## Kosten und Zahlungen

`CostItem` enthält stabile ID, Material/Beschreibung, optionale Etage und Raum, Menge, Einheit, Einzelpreis, Status und vier personenbezogene Zahlungsbeträge. Es gibt keine Gewerke oder Kategorien. `Payments` erlaubt Teilzahlungen und gemeinsam bezahlte Positionen, jedoch keine negativen, überhöhten oder ungenauen Beträge. Geplante Positionen haben keine Zahlungen. Gekaufte und verbaute Positionen können unbezahlt oder teilweise bezahlt sein.

Geld wird ausschließlich mit `decimal` verarbeitet. Positionskosten werden mit `MidpointRounding.AwayFromZero` auf zwei Dezimalstellen gerundet. Die Personensummen berechnen sich aus den Zahlungen aller Positionen. Mengen werden auf drei, Preise und Zahlungen auf zwei Nachkommastellen geprüft. Eingaben akzeptieren bewusst deutsches Dezimalkomma ohne Gruppentrenner, damit z. B. `12.50` nicht versehentlich als `1250` interpretiert wird.

`CostItemDraft` ist ein separater Dialogentwurf; Abbrechen verändert die Position nicht. `CostPlanViewModel` übernimmt bestätigte Änderungen zunächst nur in den Projektentwurf. Das gemeinsame Speichern mit Stammdaten, Budget und allen Positionen bewahrt die bisherigen Konflikt- und Schließschutzregeln. Erst nach erfolgreichem Dateischreiben wird der gespeicherte Zustand aktualisiert.

## Einkaufsverlauf und Zuordnung

Jeder Einkauf bleibt ein eigener `CostItem` mit stabiler ID, optionalem `DateOnly`-Datum und Erfassungszeitpunkt. Neue Eingaben haben das aktuelle lokale Datum; alte Einträge behalten `null` statt eines erfundenen Einkaufsdatums. Die Oberfläche bildet `PurchaseGroup`-Ansichten anhand normalisiertem Material, Einheit, Raum und Etage. Es werden keine historischen Einkaufsbeträge überschrieben oder mit einem Durchschnittspreis neu berechnet.

`CostItemMatching` vereinheitlicht Unicode, Leerzeichen und Groß-/Kleinschreibung. Bei exakten Treffern wird automatisch derselbe Gruppenschlüssel verwendet. Sonst werden nur Kandidaten mit gleicher Einheit, Raum und Etage geprüft. Die begrenzte Damerau-Levenshtein-Variante erkennt einzelne Schreibfehler und benachbarte Buchstabendreher. Kurze Namen unter vier Zeichen und unterschiedliche Zahlenfolgen werden nicht unscharf zugeordnet. Bei ähnlichen Namen entscheidet der Benutzer im Dialog; nur ein bestätigter Kandidat liefert den kanonischen Namen.

## Rückzahlungen

`Reimbursement` protokolliert Empfänger, positiven Betrag, Rückzahlungsdatum, Erfassungszeit und Notiz. Eine Rückzahlung ist ausschließlich von Tobias an Lea, Wolfgang oder Jennifer möglich. Ein Storno verweist über `ReversesId` auf genau einen vorherigen, noch nicht stornierten Originaleintrag und wirkt mit umgekehrtem Vorzeichen. Originale werden nicht entfernt. Der Dateispeicher prüft unter der bestehenden Sperre, dass bereits gespeicherte Protokolleinträge als unveränderter Präfix erhalten bleiben.

Für Lea/Wolfgang/Jennifer gilt: ursprüngliche Zahlungen minus wirksame Rückzahlungen. Für Tobias gilt: eigene ursprüngliche Zahlungen plus alle wirksamen Rückzahlungen. Die Summe aller vier Ausgaben bleibt gleich den tatsächlich bezahlten Projektkosten. Die Validierung verhindert Überzahlungen, doppelte Stornos und spätere Änderungen an Einkäufen, die zu einer negativen offenen Forderung führen würden. Das lokale JSON-Protokoll ist nicht gegen manuelle Dateimanipulation außerhalb der App geschützt.

`SelectAllOnFocus` markiert numerische Textfelder bei Tastaturfokus und beim ersten Mausklick. Der erste Klick wird nach Fokusübergabe behandelt, damit die TextBox nicht anschließend die Auswahl aufhebt. Erneute Klicks in ein fokussiertes Feld erlauben Cursorpositionierung; ohne Tastatureingabe wird kein Wert verändert.

Speichervorgänge schreiben zuerst eine temporäre Datei im selben Ordner, flushen deren Inhalt und ersetzen erst danach die Zieldatei. Eine exklusive `.write.lock`-Dateisperre serialisiert Lese- und Schreibvorgänge der App. Der Vergleich der erwarteten Revision verhindert das stille Überschreiben einer zwischenzeitlich in einem anderen Fenster gespeicherten Änderung. `.tmp`-Reste werden beim Lesen ignoriert. Es gibt noch keine automatische Sicherung; die Dateiersetzung ersetzt keine Backup-Strategie. Direkte Änderungen durch andere Programme unterliegen nicht der App-Dateisperre.

Für die kleine lokale Projektmenge erfolgen Dateioperationen synchron. Bei großen Datenmengen oder weiteren Dateitypen sollten sie asynchron mit Ladezustand und Abbruchmöglichkeit erfolgen.

Die App besitzt weiterhin keinen HTTP-Client, Benutzerlogin oder Token-Verarbeitung. Projektdaten werden nicht verschlüsselt und verlassen den Computer nicht durch die App.

## Prüfung

Der Windows-Test prüft die JSON-Persistenz samt Umlauten und Zeilenumbrüchen, Überarbeitung mit stabiler Projekt-ID, veraltete Revisionen, Dateisperren, beschädigte und unbekannte Dateiformate, Pflichtfelder, Schreibfehler, Wiederladen und Speichern/Verwerfen/Abbrechen. Der Oberflächentest befüllt echte WPF-Textfelder und bedient Buttons über Automation-Peers. Er prüft außerdem das Abbrechen des Fensterschließens. Alle Daten sind temporär und von Benutzerprojekten getrennt.

Zusätzlich werden Personensummen (einschließlich Nullzeilen), gemeinsame und teilweise Zahlungen, Cent-Rundung, Budgetüberschreitungen, Bearbeitung ohne Doppelzählung, Entfernen/Abbrechen, deutsches Zahlenformat und die Migration alter Projekte geprüft. Das Kostenformular wird über echte WPF-Textfelder und Buttons bedient.

Zusätzliche Tests prüfen Teil- und Vollausgleich, Überzahlungen, Stornos und deren Unveränderlichkeit, spätere Einkäufe nach Ausgleich, Saldeninvarianz, Neustart, genaue/unscharfe Zuordnung, unterschiedliche Einheiten und Räume, abweichende Preise und Daten, Datumsübernahme aus Altbeständen sowie Maus- und Tastaturfokus. Rückzahlungs- und Zuordnungsdialog werden als echte WPF-Fenster getestet.

Optional akzeptiert der Test einen PNG-Zielpfad als einziges Argument. Neben der Projektübersicht entstehen weitere PNGs für Details, Kosten, Rückzahlung, ausgeglichene Personensalden, Zuordnungsdialog, Protokoll und Einkaufsverlauf.

## Weitere Ausbauschritte

Die aus dem früheren Gespräch übernommenen Vorgaben stehen in [product-requirements.md](product-requirements.md). Dazu gehören Excel-Export, Android-Offline-Eingaben und ausschließlich authentifizierte, verschlüsselte WLAN-Synchronisierung mit expliziter Gerätefreigabe. Diese Netzwerkanbindung ist in Version 0.4.0 noch nicht vorhanden.
