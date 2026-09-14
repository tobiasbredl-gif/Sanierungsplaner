# Geräteabgleich ohne Cloud · Version 0.12.0

Der PC bleibt die Verwaltungszentrale. Er vergibt und widerruft Gerätefreigaben und Rollen und bietet die Konfliktentscheidung. Freigegebene Handys können Änderungen direkt untereinander weitergeben, während der PC ausgeschaltet ist. Es gibt keinen Cloudserver.

## Einmalig einrichten

1. Die Windows-App und alle Android-Apps auf **0.12.0** aktualisieren. APKs über die bestehenden Installationen installieren; nicht deinstallieren.
2. PC und Handys mit demselben privaten Heimnetz verbinden: PC per LAN-Kabel ist in Ordnung, Handys im normalen WLAN, nicht im Gast-WLAN. Unterschiedliche Netzbereiche wie `192.168.178.…` und `192.168.179.…` sind für diesen Abgleich nicht geeignet.
3. Am PC **Handys / WLAN → WLAN-Abgleich starten** wählen. Bei Bedarf den angebotenen Windows-Firewallbutton für die neue Programmversion verwenden. Einmal gestarteter Abgleich wird beim nächsten Start der PC-App automatisch wieder gestartet, wenn dieselbe Netzwerkadresse vorhanden ist. **WLAN-Abgleich stoppen** deaktiviert auch diesen automatischen Start.
4. Bereits gekoppelte Handys behalten ihre Freigabe. Auf jedem Handy **Freigabe prüfen / Jetzt abgleichen** wählen, während der PC läuft. Neue Handys wie bisher einzeln mit Einmalcode koppeln, Prüfnummer vergleichen und am PC mit der gewünschten Rolle bestätigen.
5. Nachdem alle Handys eingerichtet wurden, auf jedem nochmals abgleichen, damit alle die vollständige vom PC bestätigte Geräteliste erhalten. Die erste Einrichtung des direkten Handy-Abgleichs benötigt den PC.
6. Android darf die Benachrichtigung für den lokalen Geräteabgleich anzeigen. **Hintergrundabgleich einschalten/ausschalten** steuert den dauerhaften Dienst. Eine sichtbare Android-Benachrichtigung kennzeichnet den aktiven Dienst.

## Automatik

- **App-Start:** Android versucht beim Öffnen bzw. Zurückkehren in die App den Abgleich; ungespeicherte Eingaben werden nicht verworfen. Die PC-App gleicht nach dem automatischen Start ihres zuvor eingerichteten WLAN-Dienstes ab und versucht anschließend ungefähr jede Minute erneut.
- **Nachts:** Der Android-Dienst versucht zwischen **22:00 einschließlich und 03:00 ausschließlich**, nach lokaler Gerätezeit, ungefähr alle zehn Minuten einen Abgleich. Dazu müssen mindestens zwei freigegebene Geräte im gleichen erreichbaren WLAN sein.
- **PC ausgeschaltet:** Erreichbare Handys geben die signierten Änderungen untereinander weiter. Nicht erreichbare Geräte werden beim nächsten Versuch erneut berücksichtigt.
- **PC wieder geöffnet:** Der PC fragt die erreichbaren Geräte ab und erhält auch Änderungen, die zwischenzeitlich über ein anderes Handy weitergegeben wurden.
- **Offline:** Projekte und ausstehende Änderungen bleiben auf dem jeweiligen Gerät gespeichert. Ein erfolgloser Verbindungsversuch löscht nichts.

Android kann Netzwerkzugriffe und Hintergrundarbeit bei ausgeschaltetem Display, Energiesparmodus oder herstellerspezifischen Beschränkungen verzögern. Das Zeitfenster ist ein geplanter Versuch, keine garantierte nächtliche Zustellung. Nach einem erzwungenen Beenden oder Handy-Neustart die App wieder öffnen. Nach Änderungen an der WLAN-Adresse einmal bei laufendem PC abgleichen; der PC verteilt die aktualisierten Adressen. Es gibt keine automatische Suche über fremde Netze oder Routerfreigaben.

## Gleichzeitige Änderungen und Rechte

Unabhängige Ergänzungen und Änderungen werden anhand des vorherigen Projektstands zusammengeführt. Zwei unterschiedliche Änderungen desselben Feldes oder unvereinbare Protokolle bleiben als signierte Konflikte erhalten. Am PC unter **Handys / WLAN → Konflikte prüfen** den PC-Stand behalten oder den Gerätestand übernehmen. Die Entscheidung wird weitergegeben. Ein späterer, noch unbekannter Bearbeitungsstand wird dabei nicht ungeprüft überschrieben. Gespeicherte Protokolle dürfen auch bei einer Konfliktentscheidung nicht nachträglich entfernt werden.

Jede Änderung ist mit dem Geräteschlüssel des ursprünglichen Verfassers signiert. Die Geräteliste mit Rollen, Zertifikaten und Adressen ist vom PC signiert. Ein weiterleitendes Handy erhält dadurch keine zusätzlichen Rechte. Die HTTPS-Verbindung prüft zusätzlich das erwartete Gerätezertifikat. Unbekannte Geräte, veränderte Signaturen, alte Gerätelisten und unberechtigte Löschungen werden abgewiesen.

Ein Widerruf wirkt am PC sofort. Andere Handys übernehmen ihn, sobald sie die aktualisierte, signierte Geräteliste erhalten. Offline bereits gespeicherte Daten werden nicht aus der Ferne gelöscht. Das ist dieselbe grundsätzliche Offline-Grenze wie bei der bisherigen PC-Kopplung.

## Projekte löschen und Überschrift

Bei geöffnetem Projekt steht oben beispielsweise **„Hausumbau · Sanierungsplaner“**. Der tatsächliche Projektname erscheint auch in der Rückfrage:

> Bist du sicher, dass du das Projekt „Hausumbau“ für alle Nutzer löschen möchtest?

Auf Android folgen **Abbrechen / Für alle löschen** und bei Bestätigung die System-PIN beziehungsweise Fingerabdruckprüfung. Nur die Tobias-Rolle darf mobil löschen. Eine einmal eingerichtete Tobias-App kann die Löschung auch offline speichern; PC und andere Handys übernehmen sie beim nächsten Abgleich. Am PC gibt es ebenfalls eine Rückfrage; dort bleibt der angemeldete Windows-Benutzer die vertrauenswürdige Verwaltungsperson.

Die Projektsicherung bleibt im Unterordner `Projects/Deleted`. Die dauerhafte Löschmarkierung verhindert Wiederherstellung durch eine alte Handy-Kopie. Es werden keine echten Projekte allein durch ein Programmupdate gelöscht.

## Speicherung und Grenzen

Android speichert seinen privaten Signier-/TLS-Schlüssel mit AES-GCM verschlüsselt; der dafür verwendete Schlüssel bleibt im Android Keystore. Der PC-Schlüssel bleibt im Windows-CNG-Speicher. Projektdateien und signierte Änderungsverläufe liegen lokal; kein Server außerhalb eurer Geräte ist beteiligt.

Die Änderungsverläufe werden schrittweise übertragen. Ein einzelner Projektstand darf dafür höchstens etwa 6 MB groß sein; größere Stände werden mit einer Fehlermeldung angehalten und bleiben lokal erhalten. Signierte Verläufe werden für Konfliktprüfung und Weitergabe aufbewahrt, dadurch wächst der lokale Speicherbedarf. Keine Ende-zu-Ende-Verfügbarkeit ist möglich, wenn alle anderen Geräte offline sind.

Die vorigen Anleitungen für 0.10/0.11 beschreiben den damaligen reinen PC-Abgleich. Für die Automatik und direkte Handy-Weitergabe gilt diese Anleitung.
