[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$TargetPath,
    [switch]$ElevatedRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ConfigValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Config,
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [object]$DefaultValue
    )

    if ($null -eq $Config) {
        return $DefaultValue
    }

    $property = $Config.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return $DefaultValue
    }

    return $property.Value
}

function Convert-ToInt {
    param(
        [object]$Value,
        [int]$DefaultValue
    )

    try {
        return [int]$Value
    }
    catch {
        return $DefaultValue
    }
}

function Convert-ToBool {
    param(
        [object]$Value,
        [bool]$DefaultValue
    )

    if ($null -eq $Value) {
        return $DefaultValue
    }

    if ($Value -is [bool]) {
        return $Value
    }

    try {
        return [System.Convert]::ToBoolean($Value)
    }
    catch {
        return $DefaultValue
    }
}

function Normalize-PathEntry {
    param(
        [string]$PathEntry
    )

    if ([string]::IsNullOrWhiteSpace($PathEntry)) {
        return $null
    }

    return $PathEntry.Trim().TrimEnd('\\')
}

function Test-PathContainsEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathList,
        [Parameter(Mandatory = $true)]
        [string]$Entry
    )

    $normalizedEntry = Normalize-PathEntry -PathEntry $Entry
    if ([string]::IsNullOrWhiteSpace($normalizedEntry)) {
        return $false
    }

    $pathItems = $PathList -split ';'
    foreach ($pathItem in $pathItems) {
        $normalizedItem = Normalize-PathEntry -PathEntry $pathItem
        if ([string]::IsNullOrWhiteSpace($normalizedItem)) {
            continue
        }

        if ($normalizedItem.Equals($normalizedEntry, [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Add-DirectoryToUserPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DirectoryPath
    )

    $normalizedDirectory = Normalize-PathEntry -PathEntry $DirectoryPath
    if ([string]::IsNullOrWhiteSpace($normalizedDirectory)) {
        return $false
    }

    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    if ($null -eq $userPath) {
        $userPath = ''
    }

    if (Test-PathContainsEntry -PathList $userPath -Entry $normalizedDirectory) {
        return $false
    }

    $newUserPath = $normalizedDirectory
    if (-not [string]::IsNullOrWhiteSpace($userPath)) {
        $newUserPath = "$userPath;$normalizedDirectory"
    }

    [Environment]::SetEnvironmentVariable('Path', $newUserPath, 'User')

    if (-not (Test-PathContainsEntry -PathList $env:Path -Entry $normalizedDirectory)) {
        if ([string]::IsNullOrWhiteSpace($env:Path)) {
            $env:Path = $normalizedDirectory
        }
        else {
            $env:Path = "$env:Path;$normalizedDirectory"
        }
    }

    return $true
}

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Convert-ToAbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $trimmedPath = $Path.Trim()
    if ($trimmedPath.Length -ge 2 -and $trimmedPath.StartsWith('"') -and $trimmedPath.EndsWith('"')) {
        $trimmedPath = $trimmedPath.Substring(1, $trimmedPath.Length - 2)
    }

    $expandedPath = [Environment]::ExpandEnvironmentVariables($trimmedPath)
    if (-not [System.IO.Path]::IsPathRooted($expandedPath)) {
        $expandedPath = Join-Path -Path (Get-Location).Path -ChildPath $expandedPath
    }

    return [System.IO.Path]::GetFullPath($expandedPath)
}

function Convert-ToVerbatimPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ($Path.StartsWith('\\?\')) {
        return $Path
    }

    if ($Path.StartsWith('\\')) {
        return "\\?\UNC\$($Path.TrimStart('\'))"
    }

    return "\\?\$Path"
}

function Convert-ToWslPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$WindowsPath
    )

    $normalized = $WindowsPath -replace '\\', '/'
    if ($normalized -match '^([A-Za-z]):/(.*)$') {
        return "/mnt/$($matches[1].ToLower())/$($matches[2])"
    }

    return $null
}

function Invoke-Ignored {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$ScriptBlock
    )

    try {
        & $ScriptBlock *> $null
    }
    catch {
    }
}

function Try-DeleteTarget {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [bool]$ForceTakeOwnership,
        [Parameter(Mandatory = $true)]
        [bool]$GrantAdministratorsFullControl,
        [Parameter(Mandatory = $true)]
        [bool]$GrantCurrentUserFullControl,
        [Parameter(Mandatory = $true)]
        [bool]$UseRobocopyMirrorFallback,
        [Parameter(Mandatory = $true)]
        [bool]$UseWslFallbackIfAvailable
    )

    $isDirectory = Test-Path -LiteralPath $Path -PathType Container
    $verbatimPath = Convert-ToVerbatimPath -Path $Path

    if ($isDirectory) {
        Invoke-Ignored { & attrib.exe '-R' '-S' '-H' $Path '/S' '/D' }
    }
    else {
        Invoke-Ignored { & attrib.exe '-R' '-S' '-H' $Path }
    }

    if ($ForceTakeOwnership) {
        if ($isDirectory) {
            Invoke-Ignored { & takeown.exe '/F' $Path '/A' '/R' '/D' 'Y' }
        }
        else {
            Invoke-Ignored { & takeown.exe '/F' $Path '/A' '/D' 'Y' }
        }
    }

    if ($GrantAdministratorsFullControl) {
        if ($isDirectory) {
            Invoke-Ignored { & icacls.exe $Path '/grant' '*S-1-5-32-544:(OI)(CI)F' '/T' '/C' }
        }
        else {
            Invoke-Ignored { & icacls.exe $Path '/grant' '*S-1-5-32-544:F' '/C' }
        }
    }

    if ($GrantCurrentUserFullControl) {
        $currentUser = "$env:USERDOMAIN\$env:USERNAME"
        if ($isDirectory) {
            Invoke-Ignored { & icacls.exe $Path '/grant' "${currentUser}:(OI)(CI)F" '/T' '/C' }
        }
        else {
            Invoke-Ignored { & icacls.exe $Path '/grant' "${currentUser}:F" '/C' }
        }
    }

    Invoke-Ignored { Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop }

    if (Test-Path -LiteralPath $Path) {
        if ($isDirectory) {
            Invoke-Ignored { & cmd.exe '/d' '/c' "rd /s /q \"$verbatimPath\"" }
        }
        else {
            Invoke-Ignored { & cmd.exe '/d' '/c' "del /f /q \"$verbatimPath\"" }
        }
    }

    if (Test-Path -LiteralPath $Path) {
        if ($isDirectory) {
            Invoke-Ignored { [System.IO.Directory]::Delete($verbatimPath, $true) }
        }
        else {
            Invoke-Ignored { [System.IO.File]::Delete($verbatimPath) }
        }
    }

    if ($UseRobocopyMirrorFallback -and $isDirectory -and (Test-Path -LiteralPath $Path)) {
        $emptyDir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ("exterminate-empty-" + [Guid]::NewGuid().ToString('N'))
        try {
            New-Item -ItemType Directory -Path $emptyDir -Force | Out-Null
            Invoke-Ignored { & robocopy.exe $emptyDir $Path '/MIR' '/NFL' '/NDL' '/NJH' '/NJS' '/NP' '/R:0' '/W:0' }
            Invoke-Ignored { & cmd.exe '/d' '/c' "rd /s /q \"$verbatimPath\"" }
            Invoke-Ignored { Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop }
        }
        finally {
            Invoke-Ignored { Remove-Item -LiteralPath $emptyDir -Recurse -Force -ErrorAction Stop }
        }
    }

    if ($UseWslFallbackIfAvailable -and (Test-Path -LiteralPath $Path)) {
        $wslCommand = Get-Command wsl.exe -ErrorAction SilentlyContinue
        if ($null -ne $wslCommand) {
            $wslPath = Convert-ToWslPath -WindowsPath $Path
            if (-not [string]::IsNullOrWhiteSpace($wslPath)) {
                Invoke-Ignored { & wsl.exe '--exec' 'rm' '-rf' '--' $wslPath }
            }
        }
    }
}

$toolRoot = Split-Path -Parent $PSCommandPath
$configPath = Join-Path -Path $toolRoot -ChildPath 'config\exterminate.config.json'
$configObject = $null
if (Test-Path -LiteralPath $configPath) {
    try {
        $configObject = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    }
    catch {
        $configObject = $null
    }
}

$retries = Convert-ToInt -Value (Get-ConfigValue -Config $configObject -Name 'retries' -DefaultValue 6) -DefaultValue 6
$retryDelayMs = Convert-ToInt -Value (Get-ConfigValue -Config $configObject -Name 'retryDelayMs' -DefaultValue 350) -DefaultValue 350
$autoElevate = Convert-ToBool -Value (Get-ConfigValue -Config $configObject -Name 'autoElevate' -DefaultValue $true) -DefaultValue $true
$selfInstallToUserPath = Convert-ToBool -Value (Get-ConfigValue -Config $configObject -Name 'selfInstallToUserPath' -DefaultValue $true) -DefaultValue $true
$forceTakeOwnership = Convert-ToBool -Value (Get-ConfigValue -Config $configObject -Name 'forceTakeOwnership' -DefaultValue $true) -DefaultValue $true
$grantAdministratorsFullControl = Convert-ToBool -Value (Get-ConfigValue -Config $configObject -Name 'grantAdministratorsFullControl' -DefaultValue $true) -DefaultValue $true
$grantCurrentUserFullControl = Convert-ToBool -Value (Get-ConfigValue -Config $configObject -Name 'grantCurrentUserFullControl' -DefaultValue $true) -DefaultValue $true
$useRobocopyMirrorFallback = Convert-ToBool -Value (Get-ConfigValue -Config $configObject -Name 'useRobocopyMirrorFallback' -DefaultValue $true) -DefaultValue $true
$useWslFallbackIfAvailable = Convert-ToBool -Value (Get-ConfigValue -Config $configObject -Name 'useWslFallbackIfAvailable' -DefaultValue $true) -DefaultValue $true

if ($selfInstallToUserPath) {
    if (Add-DirectoryToUserPath -DirectoryPath $toolRoot) {
        Write-Host "Added to PATH: $toolRoot"
    }
}

if ($autoElevate -and -not $ElevatedRun -and -not (Test-IsAdmin)) {
    $quotedScript = '"' + $PSCommandPath + '"'
    $quotedTarget = '"' + $TargetPath.Replace('"', '""') + '"'
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File $quotedScript -TargetPath $quotedTarget -ElevatedRun"
    $process = Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $arguments -PassThru -Wait
    exit $process.ExitCode
}

$absoluteTargetPath = Convert-ToAbsolutePath -Path $TargetPath

if (-not (Test-Path -LiteralPath $absoluteTargetPath)) {
    Write-Host "Already gone: $absoluteTargetPath"
    exit 0
}

if ($retries -lt 0) {
    $retries = 0
}

if ($retryDelayMs -lt 0) {
    $retryDelayMs = 0
}

for ($attempt = 0; $attempt -le $retries; $attempt++) {
    if (-not (Test-Path -LiteralPath $absoluteTargetPath)) {
        break
    }

    Try-DeleteTarget -Path $absoluteTargetPath -ForceTakeOwnership $forceTakeOwnership -GrantAdministratorsFullControl $grantAdministratorsFullControl -GrantCurrentUserFullControl $grantCurrentUserFullControl -UseRobocopyMirrorFallback $useRobocopyMirrorFallback -UseWslFallbackIfAvailable $useWslFallbackIfAvailable

    if ((Test-Path -LiteralPath $absoluteTargetPath) -and $attempt -lt $retries) {
        Start-Sleep -Milliseconds $retryDelayMs
    }
}

if (Test-Path -LiteralPath $absoluteTargetPath) {
    Write-Error "Failed to delete: $absoluteTargetPath"
    exit 1
}

Write-Host "Deleted: $absoluteTargetPath"
exit 0
