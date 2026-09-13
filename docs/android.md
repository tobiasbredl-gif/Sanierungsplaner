# Android-App · Testversion 0.9.0

Diese native Android-App ist für Tobias' ersten Handytest vorgesehen. Sie benötigt Android 8.0 oder neuer und einen 64-Bit-Prozessor (ARM64; zusätzlich x64 für Emulatoren).

## Installation auf dem Handy

1. `Sanierungsplaner-Android-v0.9.0.apk` auf das Handy übertragen, z. B. per USB in den Download-Ordner.
2. Die APK auf dem Handy öffnen. Falls Android fragt, die Installation aus dieser Quelle für die verwendete Dateien-App erlauben.
3. Installieren und **Sanierungsplaner** öffnen.
4. Zunächst ein Testprojekt anlegen. Es gibt noch keinen automatischen PC-Abgleich; Handy und Desktop haben getrennte lokale Projekte.

Spätere APK-Updates über die vorhandene App installieren. Nicht vorher deinstallieren: Eine Deinstallation löscht die lokalen Projekte. Die Test-APK ist signiert und braucht keinen Play Store. Vor einer Verteilung an weitere Personen wird der Installations- und Updateweg finalisiert.

## Funktionen

| Desktop-Funktion | Android |
|---|---|
| Projekte anlegen, bearbeiten, speichern, öffnen | Ja; Öffnen führt zu Kosten & Zahlungen |
| Projektdaten und Notizen | Ja |
| Menge, Einheit, Preis, Raum und Etage | Ja; ohne Gewerke/Kategorien |
| Status Gekauft als Standard | Ja |
| Geplante Ausgaben mit Zukunftsdatum | Ja, eigener Reiter |
| Optionale 19 % MwSt. | Ja; identische Rundung |
| Lea, Wolfgang, Jennifer, Tobias | Ja; eigene Summen und Zahlungsfelder |
| Gesamtbetrag pro Person | Ja; andere Teilzahlungen bleiben erhalten |
| Sofortiges Überschreiben von Zahlen | Ja; Auswahl beim Fokus |
| Wiederholte Einkäufe mit Datum gruppieren | Ja; Einzelbelege bleiben erhalten |
| Tippfehler erkennen | Ja; Bestätigung bei ähnlichen Positionen |
| Tobias erstattet an andere | Ja; Teil-/Vollausgleich und Storno |
| Andere zahlen ihren Anteil an Tobias | Ja; auch bei 0 € offen, ohne eigene Mehrbelastung |
| Verkäufe/Gutschriften | Ja; Empfänger, Datum, Notiz und Storno |
| Budget, netto bezahlt, offene Beträge | Ja |
| Dauerhafte Journale und lokales Speichern | Ja; identische Datenvalidierung |
| Schutz ungespeicherter Projektänderungen | Ja; Speichern/Verwerfen/Weiterbearbeiten |

Nach dem Übernehmen einer Eingabe **Projekt speichern** verwenden. Bereits übernommene, noch ungespeicherte Projektänderungen werden beim Hintergrundwechsel als Entwurf gesichert. Ein noch nicht übernommenes Dialogformular sollte vor dem Verlassen der App abgeschlossen werden.

Excel-Export und WLAN-Synchronisierung sind auch in der Desktop-Version noch nicht umgesetzt und gehören nicht zu dieser ersten APK. Die Android-App besitzt keine Netzwerkberechtigung, keine Cloudanbindung und keine Anmeldung. Das bisher vereinbarte sichere Koppeln im Heim-WLAN folgt separat.

## Architektur und Bauen

.NET 10 für Android mit nativen Android-Ansichten. Modelle, Entwürfe, Kostenübersicht, Projektsteuerung, Zuordnung und JSON-Dateispeicher werden als gemeinsame Quelldateien aus dem Desktop-Projekt eingebunden. Android enthält keine WPF-Abhängigkeit. `NativeEditors` übergeben die asynchron ausgefüllten Dialogergebnisse an dieselben geprüften Befehle. `MainActivity` und `Dialogs` enthalten die mobile Darstellung.

```powershell
dotnet workload install android
dotnet build src/Sanierungsplaner.Android -t:InstallAndroidDependencies -f net10.0-android -p:AndroidSdkDirectory=C:/work/android-sdk -p:JavaSdkDirectory=C:/work/android-jdk -p:AcceptAndroidSdkLicenses=True
dotnet build src/Sanierungsplaner.Android -c Release -p:AndroidSdkDirectory=C:/work/android-sdk -p:JavaSdkDirectory=C:/work/android-jdk
dotnet run --project tests/Sanierungsplaner.AndroidTests -c Release
```

APK: `src/Sanierungsplaner.Android/bin/Release/net10.0-android/de.sanierungsplaner.android-Signed.apk`. Die lokale Testsignatur muss für weitere Updates aufbewahrt werden; produktive Signierschlüssel gehören nicht ins Repository. Die separate Android-Lösung lässt sich über `Sanierungsplaner.Android.slnx` bauen; die bestehende Windows-Lösung bleibt unabhängig vom Android-SDK.

Projekte liegen im privaten App-Verzeichnis `FilesDir/Projects` im Dateiformat 6. Es werden keine echten Windows-Projekte automatisch kopiert. Der Android-Test der gemeinsamen Logik verwendet ausschließlich temporäre Dateien.
