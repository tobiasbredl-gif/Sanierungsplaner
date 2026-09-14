# Prüfung Geräteabgleich 0.12.0

Stand 14.09.2026, ausschließlich isolierte Testdaten.

- Automatisierte Tests: direkter Austausch zwischen zwei Handy-Datenbeständen ohne laufenden PC, Zusammenführung paralleler Einkäufe, spätere Weitergabe an den PC, unveränderte Herkunftssignaturen, wiederholte Übertragung ohne Duplikate, Konflikterhaltung, PC-Konfliktentscheidung und Übernahme auf beiden Handys.
- Berechtigungen: vom PC signierte Geräteliste, ungültige Signaturen, nicht erlaubte Projektlöschung durch Lea, erlaubte Offline-Löschung durch Tobias und Weitergabe über Lea, Widerruf, Schutz vor Rücknahme einer neueren Geräteliste, Neustart-Persistenz und dauerhafte Löschmarkierungen.
- Zeitplan: Grenzfälle 21:59, 22:00, 02:59 und 03:00 geprüft.
- Bestehende HTTPS-/Kopplungs-, Android-Rechenlogik- und Windows-Oberflächentests bestanden.
- Native Release-APK auf Android-15-Emulator: Updateinstallation, PC-Kopplung, Übernahme der signierten Geräteliste und Projektabgleich erfolgreich. Android bestätigt den Dienst als `connectedDevice`-Foreground-Service mit dauerhafter Benachrichtigung.
- Echter eingehender HTTPS-Aufruf an den Android-WLAN-Empfänger bei im Hintergrund befindlicher App: Zertifikat-Pinning und signierte Antwort erfolgreich geprüft. Die virtuelle Ethernet/WLAN-Trennung des Emulators wurde dafür ausschließlich innerhalb des Testemulators weitergeleitet.
- Native Oberfläche: Überschrift „Android-Testhaus · Sanierungsplaner“, Löschbestätigung mit genau diesem Namen, Abbrechen erhält das Projekt, System-PIN wird verlangt, bestätigte Löschung funktioniert bei ausgeschaltetem Test-PC.
- Finale APK-Signaturprüfung erfolgreich.

Nicht praktisch über eine ganze Nacht geprüft: herstellerspezifischer Energiesparmodus, zwei physische Handys in einem echten Heim-WLAN, Fingerabdruckhardware. Android kann die geplanten Nachtversuche verschieben oder unterdrücken. Der deterministische Zeitfenstertest ersetzt keinen physischen Nachttest.
