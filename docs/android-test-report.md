# Prüfung Android 0.9.0 und Windows 0.9.0

Stand: 13.09.2026. Ausschließlich isolierte Testprojekte; echte Benutzerprojekte unverändert.

- Android Release-APK gebaut: keine Compilerwarnungen oder Fehler. ARM64 für das Handy und x64 für Emulatoren; Mindestversion Android 8 / API 26.
- Gemeinsame Android-Fachlogiktests bestanden: Formularadapter, MwSt., Gesamtbetrag, Personenzahlungen, Beiträge bei 0 offen, Erstattungen, Verkäufe, Planung, Gruppierung, Tippfehler, Speichern/Neustart und Stornos.
- Vollständige bestehende Windows-Smoke-Tests bestanden, einschließlich echter WPF-Dialoge.
- Signierte Release-APK auf einem lokalen Pixel-5-Emulator mit Android 15 / API 35 installiert und gestartet.
- Echtes Android-Projekt angelegt und gespeichert; Öffnen führt zu Kosten & Zahlungen.
- Einkauf über 4.000 € im nativen Formular erfasst und mit Gesamtbetrag Tobias zugeordnet.
- Lea zahlt 1.000 € bei 0 offen: Lea bleibt bei 0, separat 1.000 € bezahlt, Tobias netto 3.000 €.
- Verkauf über 80 € an Tobias: aktuelle Nettoausgaben 2.920 €.
- Geplante Fenster für 100 € zuzüglich 19 % MwSt.: geplante Ausgaben 119 €, Zahlungen 0 €.
- Neue Summenübersicht in Android zeigt 2.920 € aktuell, 4.000 € ursprünglich bezahlt und 119 € geplant; Windows-Ansicht ebenfalls visuell kontrolliert.
- Speichern, erneutes Öffnen, vollständiger App-Neustart und APK-Update erhalten die Android-Testdaten.
- Nach Behebung der im Emulator entdeckten Start- und Dialogfehler blieb das Android-Absturzprotokoll während der finalen Tests leer.

Noch nicht geprüft: Installation auf Tobias' physischem Handy und andere Android-Herstelleroberflächen. WLAN-Synchronisierung ist noch nicht implementiert. Ein noch nicht übernommenes Dialogformular sollte vor dem Verlassen der App abgeschlossen werden; übernommene Projektentwürfe werden beim Hintergrundwechsel gesichert.
