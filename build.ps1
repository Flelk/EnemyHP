<#
  Builds the mod and packages a Thunderstore-format zip in .\dist\ that friends can import.
  The mod name and version come from thunderstore\manifest.json.

  .\build.ps1                      build + package
  .\build.ps1 -Install             also copy the DLL into your r2modman profile (or the game's BepInEx) for testing
  .\build.ps1 -Install -R2Profile "Name"   pick the r2modman profile (default: 'Default', or the only profile)
  .\build.ps1 -GameDir "D:\...\REPO"   if R.E.P.O. is installed somewhere else
#>
param(
    [string]$GameDir = $(if ($env:REPO_GAME_DIR) { $env:REPO_GAME_DIR } else { 'C:\Program Files (x86)\Steam\steamapps\common\REPO' }),
    [switch]$Install,
    [string]$R2Profile = ''
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

if (-not (Test-Path "$GameDir\REPO_Data\Managed\Assembly-CSharp.dll")) {
    throw "R.E.P.O. not found at '$GameDir'. Pass -GameDir or set REPO_GAME_DIR."
}

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" }
if (-not (Test-Path $dotnet)) { throw ".NET SDK not found. Install it from https://dot.net (8.0 or newer)." }

$manifest = Get-Content "$root\thunderstore\manifest.json" -Raw | ConvertFrom-Json
$name = $manifest.name
$ver  = $manifest.version_number

& $dotnet build "$root\$name.csproj" -c Release -nologo -v q "-p:Version=$ver" "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$dll   = "$root\bin\Release\$name.dll"
$stage = "$root\dist\stage"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $stage | Out-Null
Copy-Item $dll, "$root\thunderstore\manifest.json", "$root\thunderstore\README.md", "$root\thunderstore\CHANGELOG.md", "$root\thunderstore\icon.png" $stage

$zip = "$root\dist\$name-$ver.zip"
Remove-Item $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Remove-Item $stage -Recurse -Force
Write-Host "Package: $zip" -ForegroundColor Green

if ($Install) {
    # Prefer the r2modman profile; fall back to a BepInEx installed directly in the game folder.
    $profilesRoot = "$env:APPDATA\r2modmanPlus-local\REPO\profiles"
    if (-not $R2Profile) {
        $names = @(Get-ChildItem $profilesRoot -Directory -Name -ErrorAction SilentlyContinue)
        $R2Profile = if ($names -contains 'Default') { 'Default' } elseif ($names.Count -eq 1) { $names[0] } else { 'Default' }
    }
    $profileDir = "$profilesRoot\$R2Profile"
    if (Test-Path "$profileDir\BepInEx\core") {
        $plugins = "$profileDir\BepInEx\plugins"
    } elseif (Test-Path "$GameDir\BepInEx\core") {
        $plugins = "$GameDir\BepInEx\plugins"
    } else {
        throw "No BepInEx found. In r2modman, select R.E.P.O., pick a profile and import the zip once (or run .\tools\install-bepinex.ps1)."
    }

    # Reuse the folder r2modman made on import (e.g. flelk-<name>) so the mod isn't loaded twice.
    $target = Get-ChildItem $plugins -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq $name -or $_.Name -like "*-$name" } |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $target) {
        $target = "$plugins\$name"
        New-Item -ItemType Directory $target -Force | Out-Null
    }
    Copy-Item $dll "$target\" -Force
    Write-Host "Installed to $target" -ForegroundColor Green
}
