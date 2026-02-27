[CmdletBinding()]
param(
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSCommandPath
$buildDir = Join-Path -Path $repoRoot -ChildPath 'build'
$distDir = Join-Path -Path $repoRoot -ChildPath 'dist\win-x64'
$exePath = Join-Path -Path $distDir -ChildPath 'exterminate.exe'
$configPath = Join-Path -Path $repoRoot -ChildPath 'config\exterminate.config.json'

if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'CMakeLists.txt'))) {
    throw "Missing CMakeLists.txt in repo root: $repoRoot"
}

if (-not $SkipBuild) {
    cmake -S $repoRoot -B $buildDir -DCMAKE_BUILD_TYPE=Release
    cmake --build $buildDir --config Release
    cmake --install $buildDir --config Release --prefix $distDir
}

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Missing executable: $exePath"
}

if (Test-Path -LiteralPath $configPath) {
    & $exePath --config $configPath --install
}
else {
    & $exePath --install
}

exit $LASTEXITCODE
