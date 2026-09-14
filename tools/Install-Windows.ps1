$ErrorActionPreference='Stop'
try {
 $source=$PSScriptRoot
 if(!(Test-Path -LiteralPath (Join-Path $source 'Sanierungsplaner.exe'))){throw 'Bitte dieses Skript aus dem vollständigen Windows-Programmpaket starten.'}
 $target=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Sanierungsplaner'
 $exe=Join-Path $target 'Sanierungsplaner.exe'
 if(Get-Process Sanierungsplaner -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $exe}){throw 'Bitte die installierte Sanierungsplaner-App schließen und die Installation erneut starten.'}
 New-Item -ItemType Directory -Force -Path $target | Out-Null
 Get-ChildItem -LiteralPath $source | Where-Object {$_.Name -notin @('Installieren.cmd','Installieren.ps1')} | Copy-Item -Destination $target -Recurse -Force
 $shell=New-Object -ComObject WScript.Shell
 $desktop=$shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Sanierungsplaner.lnk'))
 $desktop.TargetPath=$exe;$desktop.WorkingDirectory=$target;$desktop.IconLocation=$exe+',0';$desktop.Save()
 $programs=[Environment]::GetFolderPath('Programs')
 $start=$shell.CreateShortcut((Join-Path $programs 'Sanierungsplaner.lnk'));$start.TargetPath=$exe;$start.WorkingDirectory=$target;$start.IconLocation=$exe+',0';$start.Save()
 Write-Host 'Sanierungsplaner wurde installiert. Deine Projekte und Kopplungen bleiben erhalten.'
 Write-Host 'Du findest die App auf dem Desktop und im Startmenü.'
 Start-Process -FilePath $exe
} catch {Write-Host $_.Exception.Message -ForegroundColor Red;exit 1}
