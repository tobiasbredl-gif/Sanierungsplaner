# Sanierungsplaner 1.0.0 installieren

## Windows
Das Windows-ZIP vollständig entpacken und im entpackten Ordner **Installieren.cmd** starten. Die Installation legt das Programm für den aktuellen Windows-Benutzer sowie Verknüpfungen mit dem neuen Symbol auf dem Desktop und im Startmenü an. Eine laufende installierte Ausgabe vorher schließen. Alternativ lässt sich Sanierungsplaner.exe direkt aus dem vollständigen Programmordner starten. Keine separate .NET-Installation nötig.

## Android
Sanierungsplaner-Android-v1.0.0.apk auf das Handy übertragen und öffnen. Bei einer vorhandenen Installation **Aktualisieren** bestätigen. Nicht vorher deinstallieren: Beim Update bleiben Projekte und Gerätefreigabe erhalten. Auf neuen Handys installieren und am PC die passende Rolle freigeben.

Zukünftige passende APK-Versionen ersetzen die installierte App. Es entsteht keine zweite App. Heruntergeladene alte APK-Dateien im Download-Ordner werden dadurch nicht gelöscht. Updates werden durch Öffnen der neuen APK installiert; es gibt keinen automatischen Internet-Download.

## Abgleich
Der PC bleibt die Zentrale für Freigaben und Rollen. Für Abgleich und Nachtbetrieb müssen die Geräte im gemeinsamen privaten Heimnetz erreichbar sein. Nach einem Neustart des Handys die App wieder öffnen und den Hintergrundabgleich aktiviert lassen. Android kann Hintergrundarbeit verzögern; ein vollständiger nächtlicher Test auf den echten Familienhandys steht noch aus.

## Prüfung dieser Ausgabe
Windows-Release gebaut, Funktionstests bestanden und die eigenständig startbare Ausgabe geöffnet. Android-Release gebaut; im Emulator 0.12.0 durch 1.0.0 ersetzt: genau eine Installation, Projektdateien und verschlüsselte Gerätefreigabe unverändert. Das neue Symbol wurde im Android-Startbildschirm geprüft.
## Künftige Ausgaben bauen
Die Android-App-Kennung bleibt de.sanierungsplaner.android. Der interne Versionscode muss bei jeder neuen Ausgabe steigen (1.0.0 verwendet 5). Alle Updates müssen mit demselben bisherigen Signierschlüssel erstellt werden. Diese Ausgabe verwendet weiterhin die vorhandene lokale Testsignatur, damit bisherige Installationen aktualisiert werden können. Den privaten Schlüssel dauerhaft außerhalb des Repositorys sichern; ein verlorener oder gewechselter Schlüssel verhindert diesen Updateweg.

Vor der Weitergabe mit tools/Verify-AndroidUpdate.ps1 prüfen: Parameter PreviousApk, NewApk, AndroidSdk und JavaHome. Die Prüfung verlangt gleiche App-Kennung und Signatur sowie einen höheren Versionscode.

tools/Generate-Icons.ps1 erzeugt die Windows-ICO-Datei und die PNG-Vorschau. Android verwendet die zugehörigen Vektor- und adaptiven Symbolressourcen.

Für ein Windows-Paket zuerst selbständig für win-x64 veröffentlichen und tools/Install-Windows.ps1 als Installieren.ps1 neben die EXE kopieren. Installieren.cmd ruft dieses Skript auf. Die Installation kopiert nach LocalAppData/Programs/Sanierungsplaner; Projektdaten bleiben im separaten Datenverzeichnis. Die Installation auf einem echten Benutzerkonto wurde nicht automatisiert ausgeführt.