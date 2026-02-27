[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$toolRoot = Split-Path -Parent $PSCommandPath
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if ($null -eq $userPath) {
    $userPath = ''
}

$normalize = {
    param([string]$value)
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $null
    }

    return $value.Trim().TrimEnd('\\')
}

$normalizedToolRoot = & $normalize $toolRoot
$alreadyInPath = $false
foreach ($item in ($userPath -split ';')) {
    $normalizedItem = & $normalize $item
    if ($null -ne $normalizedItem -and $normalizedItem.Equals($normalizedToolRoot, [StringComparison]::OrdinalIgnoreCase)) {
        $alreadyInPath = $true
        break
    }
}

if (-not $alreadyInPath) {
    $newUserPath = $normalizedToolRoot
    if (-not [string]::IsNullOrWhiteSpace($userPath)) {
        $newUserPath = "$userPath;$normalizedToolRoot"
    }

    [Environment]::SetEnvironmentVariable('Path', $newUserPath, 'User')

    if (-not [string]::IsNullOrWhiteSpace($env:Path)) {
        $env:Path = "$env:Path;$normalizedToolRoot"
    }
    else {
        $env:Path = $normalizedToolRoot
    }

    Write-Host "Added to PATH: $normalizedToolRoot"
}
else {
    Write-Host "Already in PATH: $normalizedToolRoot"
}

Write-Host 'You can now run: exterminate "C:\path\to\target"'
