[CmdletBinding()]
param(
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSCommandPath
$projectPath = Join-Path -Path $repoRoot -ChildPath 'src\Exterminate\Exterminate.csproj'
$publishDir = Join-Path -Path $repoRoot -ChildPath 'dist\win-x64'
$exePath = Join-Path -Path $publishDir -ChildPath 'exterminate.exe'
$configPath = Join-Path -Path $repoRoot -ChildPath 'config\exterminate.config.json'

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Missing project file: $projectPath"
}

if (-not $SkipBuild) {
    dotnet publish $projectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:TrimMode=partial -p:InvariantGlobalization=true -p:EnableCompressionInSingleFile=true -o $publishDir
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
