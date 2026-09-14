param(
 [Parameter(Mandatory)][string]$PreviousApk,
 [Parameter(Mandatory)][string]$NewApk,
 [Parameter(Mandatory)][string]$AndroidSdk,
 [Parameter(Mandatory)][string]$JavaHome
)
$ErrorActionPreference='Stop'
$env:JAVA_HOME=(Resolve-Path -LiteralPath $JavaHome).Path
$buildTools=Get-ChildItem -LiteralPath (Join-Path $AndroidSdk 'build-tools') -Directory | Where-Object {$_.Name -match '^\d+\.\d+\.\d+$'} | Sort-Object {[version]$_.Name} -Descending | Select-Object -First 1
if(!$buildTools){throw 'Android Build Tools fehlen.'}
function Package-Info([string]$Path){
 $resolved=(Resolve-Path -LiteralPath $Path).Path
 $signer=& (Join-Path $buildTools.FullName 'apksigner.bat') verify --print-certs $resolved
 if($LASTEXITCODE -ne 0){throw 'APK-Signaturprüfung fehlgeschlagen.'}
 $certificate=($signer | Select-String '^Signer #1 certificate SHA-256 digest: (.+)$').Matches.Groups[1].Value
 if(!$certificate){throw 'Signaturfingerabdruck fehlt.'}
 $badging=& (Join-Path $buildTools.FullName 'aapt.exe') dump badging $resolved
 if($LASTEXITCODE -ne 0){throw 'APK-Paketinformationen nicht lesbar.'}
 $package=($badging | Select-String "^package: name='([^']+)' versionCode='(\d+)' versionName='([^']+)'").Matches
 if(!$package){throw 'Paketkennung fehlt.'}
 return [pscustomobject]@{Name=$package.Groups[1].Value;Code=[long]$package.Groups[2].Value;Version=$package.Groups[3].Value;Certificate=$certificate}
}
$old=Package-Info $PreviousApk;$new=Package-Info $NewApk
if($new.Name -ne 'de.sanierungsplaner.android' -or $new.Name -ne $old.Name){throw 'Paketkennung geändert: Es würde eine zweite App entstehen.'}
if($new.Certificate -ne $old.Certificate){throw 'Signatur geändert: Bestehende Installationen können nicht aktualisiert werden.'}
if($new.Code -le $old.Code){throw 'Die interne Android-Versionsnummer muss steigen.'}
Write-Output "PASS: $($old.Version) → $($new.Version), gleiche App und Signatur, Versionsnummer $($old.Code) → $($new.Code). Android ersetzt die installierte Version ohne vorherige Deinstallation."
