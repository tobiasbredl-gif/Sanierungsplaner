# Prüfung Version 0.10.0

Stand: 13.09.2026. Alle Prüfungen mit isolierten Testprojekten.

- Windows Release-Build: ohne Warnungen oder Fehler. Bestehende Oberflächen-, Rechen-, Persistenz-, Migrations- und Protokolltests bestanden.
- Gemeinsame Android-Logik: bestehende Funktionen bestanden. Zusätzlich gesperrte Standardrolle, fehlende Gerätebestätigung, einmalige Bestätigung und zwischenzeitlich entzogene Berechtigung geprüft.
- HTTPS-Integration: ausstehende Freigabe, Einmalcode-Wiederverwendung, Ablauf, vom PC zugewiesene Rolle, falscher Zertifikatsfingerabdruck, unbekanntes Gerät, manipulierte Lea-Erstattung, erlaubte Tobias-Erstattung, Konfliktabbruch und bewusste PC-Übernahme sowie Widerruf bestanden.
- Native Android-APK auf Pixel-5-Emulator mit Android 15: Updateinstallation, Kopplung, verschlüsselter Projektimport, verschlüsselte Speicherung der Freigabe und Wiederherstellung nach Prozessneustart bestanden.
- Android-System-PIN: Abbrechen erhält den offenen Betrag; richtige PIN bucht die Erstattung. Die gebuchte Erstattung wurde gespeichert und im PC-Testprojekt nach dem Abgleich nachgewiesen.
- Widerruf am Test-PC: der nächste Zugriff wird verweigert; Android zeigt keine aktive Rolle und sperrt Erstattungen.
- Signierte Release-APK: Signaturprüfung erfolgreich (v2 und v3).

Nicht auf einem physischen Handy geprüft: herstellerspezifische Fingerabdruckdialoge, USB-Dateiauswahl und Heimnetz-Firewallverhalten. API 26–29 verwendet den System-Gerätesperrdialog; der praktische Emulatorlauf erfolgte auf API 35. Der automatische PC-Testdienst vergibt Freigaben nur in einem ausdrücklich gestarteten Testmodus; in der ausgelieferten PC-App muss der Benutzer jedes Gerät selbst bestätigen.

Prüfbefehle:

```powershell
dotnet build Sanierungsplaner.slnx -c Release
dotnet run --project tests/Sanierungsplaner.AndroidTests -c Release
dotnet run --project tests/Sanierungsplaner.SyncTests -c Release
dotnet run --project tests/Sanierungsplaner.SmokeTests -c Release -- C:/Temp/Sanierungsplaner-Test.png
```

Die HTTPS-Tests benötigen Windows und eine aktive private IPv4-Netzwerkschnittstelle. Sie verwenden Port 58443 und entfernen ihren isolierten Windows-CNG-Testschlüssel anschließend. Vorher einen laufenden Sanierungsplaner-Testdienst beenden.

## Ergänzung 0.11.0 – Projektlöschung

Automatisierter HTTPS-Test bestanden: Lea darf weder über den Client noch über eine manipulierte Anfrage löschen; Tobias darf löschen; die Sicherung enthält das Journal. Ein zweiter Handy-Datenbestand übernimmt die Löschung statt seine alte Kopie hochzuladen. Wiederholte Löschungen sind unschädlich. Speichern einer gelöschten Kennung wird abgewiesen, eine veraltete Revision verhindert eine neue Löschung. Desktop-Löschung und bestehende Windows-/Android-Logiktests bestanden.

Die neue Löschoberfläche wurde gebaut; ein praktischer Löschdurchlauf auf einem physischen Handy steht aus.
