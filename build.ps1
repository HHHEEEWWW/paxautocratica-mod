# Build and deploy PaxAutocraticaHelper plugin (one command)
$GameDir = 'E:\steam\steamapps\common\Pax Autocratica'
$Proj = $PSScriptRoot
$Runtime = 'net6.0'
$PluginDir = "$GameDir\BepInEx\plugins\PaxAutocraticaHelper"

dotnet build "$Proj\src\PaxAutocraticaHelper\PaxAutocraticaHelper.csproj" -c Release
if ($LASTEXITCODE -ne 0) { Write-Host 'BUILD FAILED'; exit 1 }

# Backup old plugin before replace
if (Test-Path "$PluginDir\PaxAutocraticaHelper.dll") {
    $bak = "$PluginDir\PaxAutocraticaHelper.dll.bak"
    Copy-Item "$PluginDir\PaxAutocraticaHelper.dll" $bak -Force
    Write-Host "Backed up old plugin to $bak"
}

Copy-Item "$Proj\src\PaxAutocraticaHelper\bin\Release\$Runtime\PaxAutocraticaHelper.dll" $PluginDir -Force
Write-Host 'Deployed: PaxAutocraticaHelper.dll -> BepInEx\plugins\PaxAutocraticaHelper\'
Write-Host 'Restart the game to load the new plugin.'
