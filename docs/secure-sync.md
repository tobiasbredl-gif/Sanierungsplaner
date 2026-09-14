# Sichere Gerätefreigabe und Tobias-Rolle · 0.10.0

## Einmalig verbinden

1. Die neue Windows-Version starten und Projekte speichern. PC und Handy mit demselben privaten Heimnetz verbinden.
2. Am PC **Handys / WLAN** öffnen, die passende Heimnetz-Adresse auswählen und den Dienst starten. Falls die Verbindung blockiert ist, über den angebotenen Windows-Button Zugriff im privaten Heimnetz erlauben. Windows fragt dafür nach Administratorrechten. Öffentliche Netzwerke werden nicht freigegeben.
3. Am PC die Rolle **Tobias** auswählen und einen Kopplungscode erzeugen. Für andere Handys später Lea, Wolfgang oder Jennifer wählen.
4. Den Code auf das Handy übertragen: kopieren oder als Kopplungsdatei speichern und per USB übertragen. Er ist fünf Minuten gültig und nur einmal nutzbar.
5. Auf dem Handy **PC koppeln / WLAN-Abgleich → PC koppeln** öffnen, den Code einfügen oder **Kopplungsdatei öffnen** verwenden. Einen erkennbaren Gerätenamen eingeben und übernehmen.
6. Die achtstellige Prüfnummer auf PC und Handy vergleichen und genau dieses Gerät am PC freigeben. Anschließend am Handy **Freigabe prüfen / Jetzt abgleichen** wählen.
7. Für Tobias-Erstattungen muss Android mit einer sicheren PIN, einem Passwort oder einem Entsperrmuster geschützt sein. Beim Buchen und Stornieren bestätigt Tobias über den Android-Systemdialog; unterstützte Fingerabdrücke sind ebenfalls möglich.

Die APK als Update über die vorhandene Installation installieren. Nicht deinstallieren, da dies lokale Handydaten löscht. Der Windows-Datenordner bleibt bei einem Programmupdate erhalten.

## Rechte und Bedienung

Die Rolle wird ausschließlich am PC vergeben. Auf einem unbekannten oder noch nicht freigegebenen Handy bleiben Erstattungen gesperrt. Lea, Wolfgang und Jennifer sehen **Tobias erstattet an …** und dessen Stornofunktion nicht. Der PC lehnt auch manipulierte Erstattungsänderungen dieser Geräte ab.

**An Tobias zahlen** bleibt für alle verfügbar, auch bei 0 € offen. Diese Beiträge senken weiterhin nur Tobias' Kostenanteil. Einkäufe, Planung, Verkäufe, Gutschriften und Summen bleiben erhalten. Alle freigegebenen Geräte können die gemeinsam synchronisierten Projekte lesen und die übrigen Projektfunktionen nutzen; dies ist keine getrennte Datenfreigabe pro Person.

Gespeicherte Projekte werden am Handy manuell über **Jetzt mit PC abgleichen** synchronisiert. In der Projektübersicht prüft die App außerdem beim Öffnen und ungefähr jede Minute, solange sie im Vordergrund ist. Während einer Bearbeitung oder eines Formulars läuft kein automatischer Abgleich. Die letzte erfolgreiche Synchronisierung steht in der Kopplungsansicht. Am PC **Erneut laden** nutzen, um eingegangene Änderungen anzuzeigen; bei einem geöffneten Projekt zuerst zur Übersicht wechseln. Ungespeicherte PC-Änderungen werden nicht automatisch ersetzt.

Bei Änderungen desselben Projekts auf beiden Geräten fragt die App, welcher Stand gelten soll. Abbrechen erhält beide Stände. Vor einer bewusst gewählten Ersetzung werden beide Versionen im privaten Handyordner `Sync/Konfliktsicherungen` gesichert. Eine Übertragung darf bestehende gespeicherte Protokolle nicht entfernen. In diesem Fall wird sie abgewiesen; die PC-Version kann bewusst übernommen werden.

## Widerruf und Offlinebetrieb

Am PC kann jedes Gerät einzeln widerrufen werden. Weitere Serverzugriffe werden abgewiesen. Beim nächsten Verbindungsversuch verliert das Handy seine lokale Tobias-Freigabe. Ohne Verbindung bleibt die zuletzt erteilte Rolle für Offlinearbeit erhalten; ein sofortiger Widerruf auf einem offline befindlichen Handy ist nicht möglich. Bereits gespeicherte lokale Projekte werden durch den Widerruf nicht gelöscht.

Der PC-Dienst muss laufen und erreichbar sein. Kein Internetserver, keine Portweiterleitung und kein Cloudkonto sind erforderlich. Unterstützt werden private IPv4-Adressen im gleichen lokalen Subnetz. Bei geänderter PC-Adresse oder neu eingerichteter PC-Identität erneut koppeln und die alte Freigabe widerrufen.

## Technische Umsetzung

- `Desktop/Sync/LanSyncServer.cs`: ausdrücklich gestarteter Kestrel-HTTPS-Dienst auf Port 58443, Prüfung privater Adressen und des ausgewählten Subnetzes, begrenzte Anfragen und Verbindungen.
- `DeviceRegistry.cs`: fünf Minuten gültige Einmal-Einladungen, ausstehende Anfragen, PC-Freigabe, serverseitige Rollen und Widerruf. Der PC speichert SHA-256-Hashes zufälliger 256-Bit-Gerätetokens, keine Klartexttokens.
- `SharedSync/SyncClient.cs`: HTTPS-Zertifikat-Pinning aus dem Kopplungscode, keine Weiterleitungen, Geräteauthentifizierung, Revisionsvergleich und Konfliktbehandlung.
- `Android/SecureBindingStore.cs`: Token und zugewiesene Rolle werden zusammen mit AES-GCM verschlüsselt im privaten App-Speicher abgelegt. Der Schlüssel bleibt im Android Keystore. Die Rolle lässt sich nicht über eine normale App-Einstellung ändern.
- Der private PC-TLS-Schlüssel liegt nicht exportierbar im Windows-CNG-Schlüsselspeicher des angemeldeten Benutzers. Die öffentliche Zertifikatsdatei, Gerätefreigaben und Synchronisierungsprotokolle liegen unter `%LOCALAPPDATA%\Sanierungsplaner\Sync`.
- `SyncRules.AuthorizeUpload` prüft die tatsächliche serverseitige Rolle für jede Übertragung. Gespeicherte Erstattungs-, Verkaufs- und Beitragsprotokolle dürfen nicht rückwirkend entfernt oder verändert werden.
- Die Android-Gerätesperre bestätigt jede mobile Tobias-Erstattung bzw. deren Storno. Die PC-App vertraut dem angemeldeten Windows-Benutzer. Personen mit dessen vollständigem Dateizugriff oder einem vollständig kompromittierten Handy liegen außerhalb dieser App-Rollentrennung.

Die Tests verwenden ausschließlich isolierte Testdaten. Den produktiven PC-Dienst und die Firewallfreigabe startet der Benutzer selbst.

## Projekte für alle löschen · ab Version 0.11.0

In der Projektübersicht steht bei freigegebener Tobias-Rolle **Projekt für alle löschen**. Nach der Rückfrage ist zusätzlich die Android-Gerätesperre zu bestätigen. Der PC muss erreichbar sein. Unterschiedliche Projektstände zunächst abgleichen; ein inzwischen geändertes Projekt wird nicht ungeprüft gelöscht.

Die Löschung wird am PC gespeichert. Alle anderen Handys entfernen das Projekt beim nächsten Abgleich, auch wenn sie vorher offline waren. Es verschwindet mitsamt Kosten, Zahlungen und Planung aus den aktiven Projekten. Die bisherige Projektdatei bleibt als Sicherung unter `Projects/Deleted` erhalten. Eine dauerhafte Löschmarkierung verhindert, dass alte Kopien wieder hochgeladen werden. Das ist keine vollständige Vernichtung aller lokalen Sicherungskopien.

Die PC-App bietet denselben Button mit Rückfrage. Wie bei den übrigen Tobias-Verwaltungsfunktionen gilt dort der angemeldete Windows-Benutzer als vertrauenswürdig. Andere mobile Rollen erhalten keinen Löschbutton; der Server prüft die Rolle auch bei direkt gesendeten Löschanfragen.

PC und alle Handys auf **0.11.0** aktualisieren. Ältere Android-Versionen übernehmen Löschungen noch nicht automatisch. Die neue Android-Version benötigt den neuen PC-Dienst. Vorhandene Rollen und Projekte bleiben beim Update erhalten. Falls die Windows-Firewall noch den alten Programmordner freigibt, im neuen Programm erneut **Windows-Zugriff im privaten Heimnetz erlauben** verwenden.

## Version 0.12.0

Für den direkten Handy-Abgleich und die neue Automatik gilt [Geräteabgleich ohne Cloud](offline-device-sync.md). Diese Version ersetzt den ausschließlich PC-abhängigen Ablauf aus den vorherigen Abschnitten. Alle Geräte müssen aktualisiert und einmal bei laufendem PC abgeglichen werden.
