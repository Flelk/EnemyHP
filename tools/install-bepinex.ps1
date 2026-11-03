<#
  Installs BepInExPack (from Thunderstore) directly into the R.E.P.O. game folder, for testing without a mod manager.
  To remove BepInEx later: delete winhttp.dll, doorstop_config.ini and the BepInEx folder from the game folder.
#>
param(
    [string]$GameDir = $(if ($env:REPO_GAME_DIR) { $env:REPO_GAME_DIR } else { 'C:\Program Files (x86)\Steam\steamapps\common\REPO' }),
    [string]$Version = '5.4.2305'
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path "$GameDir\REPO.exe")) { throw "R.E.P.O. not found at '$GameDir'." }

$tmp = Join-Path $env:TEMP "bepinexpack-$Version"
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $tmp | Out-Null
$zip = "$tmp\pack.zip"
Invoke-WebRequest "https://thunderstore.io/package/download/BepInEx/BepInExPack/$Version/" -OutFile $zip -UseBasicParsing
Expand-Archive $zip "$tmp\x"

$src = Get-ChildItem "$tmp\x" -Directory | Where-Object { Test-Path "$($_.FullName)\winhttp.dll" } | Select-Object -First 1
if (-not $src) { throw "Unexpected BepInExPack layout." }
Copy-Item "$($src.FullName)\*" $GameDir -Recurse -Force
Remove-Item $tmp -Recurse -Force
Write-Host "BepInEx $Version installed into $GameDir. Launch the game once so it creates its folders." -ForegroundColor Green
