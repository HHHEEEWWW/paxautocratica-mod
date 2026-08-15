# Build and deploy PaxAutocraticaHelper plugin (one command)
# Supports BepInEx-Manager isolated mode: the BepInEx tree lives under the
# manager's plugin-library; doorstop_config.ini in the game dir points at the
# active profile's preloader. This script resolves that profile automatically.
$GameDir = 'E:\steam\steamapps\common\Pax Autocratica'
$Proj = $PSScriptRoot
$Runtime = 'net6.0'

# Resolve BepInEx dir: isolated profile (doorstop target) > game-dir fallback
$BepDir = "$GameDir\BepInEx"
$Ini = "$GameDir\doorstop_config.ini"
if (Test-Path $Ini) {
    $line = Get-Content $Ini | Where-Object { $_ -match '^\s*target_assembly\s*=' } | Select-Object -First 1
    if ($line -match '=\s*(.+)$') {
        $target = $Matches[1].Trim()
        if ($target -and (Test-Path $target) -and $target -match 'plugin-library') {
            # target = <lib>/<gameRoot>/<profileId>/BepInEx/core/<preloader>.dll
            $BepDir = Split-Path (Split-Path $target -Parent) -Parent
            Write-Host "Isolated profile detected: $BepDir"
        }
    }
}
if (-not (Test-Path "$BepDir\core\BepInEx.Core.dll")) {
    Write-Host "ERROR: BepInEx framework not found under $BepDir"
    exit 1
}

$PluginDir = "$BepDir\plugins\PaxAutocraticaHelper"
if (Test-Path $PluginDir -PathType Leaf) {
    Remove-Item $PluginDir -Force
}
New-Item -ItemType Directory -Path $PluginDir -Force | Out-Null

dotnet build "$Proj\src\PaxAutocraticaHelper\PaxAutocraticaHelper.csproj" -c Release -p:BepDir="$BepDir"
if ($LASTEXITCODE -ne 0) { Write-Host 'BUILD FAILED'; exit 1 }

# Backup old plugin before replace
if (Test-Path "$PluginDir\PaxAutocraticaHelper.dll") {
    $bak = "$PluginDir\PaxAutocraticaHelper.dll.bak"
    try {
        Copy-Item "$PluginDir\PaxAutocraticaHelper.dll" $bak -Force -ErrorAction Stop
        Write-Host "Backed up old plugin to $bak"
    } catch {
        Write-Host "WARN: backup failed: $_"
    }
}

# Deploy with verification (fail loudly if the game is running and locks the dll)
$srcDll = "$Proj\src\PaxAutocraticaHelper\bin\Release\$Runtime\PaxAutocraticaHelper.dll"
try {
    Copy-Item $srcDll $PluginDir -Force -ErrorAction Stop
    $srcHash = (Get-FileHash $srcDll -Algorithm SHA256).Hash
    $dstHash = (Get-FileHash "$PluginDir\PaxAutocraticaHelper.dll" -Algorithm SHA256).Hash
    if ($srcHash -ne $dstHash) {
        Write-Host 'ERROR: deploy verification failed (hash mismatch)'
        exit 1
    }
    Write-Host 'Deployed: PaxAutocraticaHelper.dll -> plugins\PaxAutocraticaHelper\'
} catch {
    Write-Host "DEPLOY FAILED: $_"
    Write-Host 'HINT: close the game first, the plugin dll is locked while the game is running.'
    exit 1
}
Write-Host 'Restart the game to load the new plugin.'
