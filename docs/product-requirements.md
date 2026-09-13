# Vereinbarte Produktanforderungen

Diese Anforderungen wurden aus dem früheren Planungsgespräch übernommen und durch die aktuelle Festlegung der vier Personen konkretisiert.

## Desktop und Kalkulation

- Eigenständig startbare Windows-App für die Haussanierung, ohne Programmierkenntnisse bedienbar.
- Lokale, dauerhafte Speicherung; schrittweise erweiterbar.
- Gemeinsame Kostenliste ohne Unterteilung nach Gewerken oder Kategorien (aktuelle Korrektur; ersetzt die frühere Bereicheinteilung).
- Zuordnung zu Raum und Etage.
- Material bzw. Kostenposition, Menge, Einheit, Einzelpreis und automatisch berechnete Positions- und Gesamtkosten.
- Status geplant, gekauft und verbaut.
- Projektbudget, Restbudget und Abweichung der Kalkulation vom Budget.
- Ausgaben nachvollziehbar je Person: **Lea, Wolfgang, Jennifer und Tobias**, jeweils eine eigene Summenzeile, zusätzlich Zahlungen je Position.
- Geplante Kosten und tatsächlich geleistete Zahlungen getrennt ausweisen. Teilzahlungen und von mehreren Personen bezahlte Positionen sind möglich; keine automatische gleichmäßige Kostenaufteilung.

## Weitere Ausbauschritte

- Auswertung nach Raum und Etage.
- Excel-Export, später gegebenenfalls Import und zusätzliche Sicherungsfunktionen.
- Android-App als installierbare APK mit lokalen Offline-Eingaben.
- PC als Zentrale; Synchronisierung im Heim-WLAN, wenn PC und Handy erreichbar sind.
- Automatischer Abgleich, zusätzlicher manueller Synchronisierungsbutton und Anzeige der letzten erfolgreichen Synchronisierung.
- Personenprofile, damit Herkunft und Zahlung einer Eingabe nachvollziehbar bleiben.

## Verbindliche Anforderungen vor einer Netzwerkanbindung

- Ein unbekanntes bzw. nicht freigegebenes Gerät **darf keinen Zugriff** erhalten. Die Mitgliedschaft im WLAN ist keine Zugriffsberechtigung.
- Explizite Gerätekopplung mit individueller Authentifizierung, verschlüsselte Verbindung, sichere Speicherung von Geräteschlüsseln.
- Freigaben müssen widerrufbar sein. Kopplungscodes sind einmalig und zeitlich begrenzt.
- Kein notwendiger Cloud-Dienst, keine Freigabe ins öffentliche Internet; Netzwerkzugriff auf das private Heimnetz begrenzen.
- Konfliktbehandlung und Tests für unautorisierte Geräte gehören zur Umsetzung vor Freigabe der Synchronisierung.

Die aktuelle Desktop-Version besitzt keine Netzwerkfreigabe. Beschreibungen früherer Prototypen sind kein Nachweis, dass diese Funktionen in dieser neu aufgebauten Codebasis bereits existieren.
