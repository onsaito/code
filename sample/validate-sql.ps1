param(
    [Parameter(Mandatory=$true)]
    [string]$ScriptPath,

    [Parameter(Mandatory=$true)]
    [string]$DacpacPath
)

$ErrorActionPreference = "Stop"

# Resolve paths relative to script location
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$libDir = Join-Path $scriptDir "lib"

# Load bundled DacFx DLLs
$dlls = @(
    "Microsoft.SqlServer.TransactSql.ScriptDom.dll",
    "Microsoft.SqlServer.Types.dll",
    "Microsoft.SqlServer.Dac.dll",
    "Microsoft.SqlServer.Dac.Extensions.dll"
)

foreach ($dll in $dlls) {
    $dllPath = Join-Path $libDir $dll
    if (-Not (Test-Path $dllPath)) {
        Write-Error "Missing required DLL: $dllPath"
        exit 1
    }
    Add-Type -Path $dllPath
}

# Load the DACPAC into a script-backed memory model
Write-Host "Loading DACPAC model from $DacpacPath..." -ForegroundColor Cyan
$loadOptions = New-Object Microsoft.SqlServer.Dac.Model.ModelLoadOptions
$loadOptions.LoadAsScriptBackedModel = $true

try {
    $model = [Microsoft.SqlServer.Dac.Model.TSqlModel]::Load($DacpacPath, $loadOptions)
} catch {
    Write-Error "Failed to load DACPAC: $_"
    exit 1
}

# Read and inject the target SQL script
$scriptText = Get-Content $ScriptPath -Raw
Write-Host "Injecting SQL script for validation..." -ForegroundColor Cyan

try {
    $objOptions = New-Object Microsoft.SqlServer.Dac.Model.TSqlObjectOptions
    $model.AddOrUpdateObjects($scriptText, "TargetScript.sql", $objOptions)
} catch {
    Write-Error "Syntax error or critical failure adding script: $_"
    exit 1
}

# Validate the combined model
Write-Host "Validating against schema..." -ForegroundColor Cyan
$messages = $model.Validate()

$errors = $messages | Where-Object { $_.MessageType -eq [Microsoft.SqlServer.Dac.Model.DacMessageType]::Error }
$warnings = $messages | Where-Object { $_.MessageType -eq [Microsoft.SqlServer.Dac.Model.DacMessageType]::Warning }

if ($errors.Count -gt 0) {
    Write-Host "`nValidation Failed ($($errors.Count) errors):" -ForegroundColor Red
    $errors | ForEach-Object { 
        Write-Host "[$($_.Prefix)$($_.Number)] $($_.Message) (Line: $($_.Line))" -ForegroundColor Red 
    }
} else {
    Write-Host "`nValidation Passed!" -ForegroundColor Green
}

if ($warnings.Count -gt 0) {
    Write-Host "`nWarnings ($($warnings.Count)):" -ForegroundColor Yellow
    $warnings | ForEach-Object { 
        Write-Host "[$($_.Prefix)$($_.Number)] $($_.Message) (Line: $($_.Line))" -ForegroundColor Yellow 
    }
}
