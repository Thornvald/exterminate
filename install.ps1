[CmdletBinding()]
param(
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSCommandPath
$projectPath = Join-Path -Path $repoRoot -ChildPath 'src\Exterminate\Exterminate.csproj'
$contextProjectPath = Join-Path -Path $repoRoot -ChildPath 'src\Exterminate.Context\Exterminate.Context.csproj'
$publishDir = Join-Path -Path $repoRoot -ChildPath 'dist\win-x64'
$exePath = Join-Path -Path $publishDir -ChildPath 'exterminate.exe'
$contextExePath = Join-Path -Path $publishDir -ChildPath 'exterminate-context.exe'
$configPath = Join-Path -Path $repoRoot -ChildPath 'config\exterminate.config.json'

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Missing project file: $projectPath"
}

if (-not (Test-Path -LiteralPath $contextProjectPath)) {
    throw "Missing project file: $contextProjectPath"
}

if (-not $SkipBuild) {
    dotnet publish $projectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publishDir
    dotnet publish $contextProjectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publishDir
}

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Missing executable: $exePath"
}

if (-not (Test-Path -LiteralPath $contextExePath)) {
    throw "Missing executable: $contextExePath"
}

if (Test-Path -LiteralPath $configPath) {
    & $exePath --config $configPath --install
}
else {
    & $exePath --install
}
exit $LASTEXITCODE
