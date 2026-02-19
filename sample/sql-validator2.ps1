param(
    [Parameter(Mandatory=$true)]
    [string]$FolderPath,

    [Parameter(Mandatory=$true)]
    [string]$ConnectionString
)

$ErrorActionPreference = "Stop"

# Get all SQL files in the folder
$files = Get-ChildItem -Path $FolderPath -Filter "*.sql" -File

if ($files.Count -eq 0) {
    Write-Host "No .sql files found in $FolderPath" -ForegroundColor Yellow
    exit 0
}

Write-Host "Connecting to Dev Database..." -ForegroundColor Cyan
try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    $conn.Open()
} catch {
    Write-Error "Failed to connect to the database: $_"
    exit 1
}

try {
    $cmd = $conn.CreateCommand()
    
    # Enable NOEXEC for this connection session
    $cmd.CommandText = "SET NOEXEC ON;"
    $cmd.ExecuteNonQuery() | Out-Null

    $hasGlobalErrors = $false

    foreach ($file in $files) {
        Write-Host "`nValidating: $($file.Name)" -ForegroundColor Cyan
        $scriptText = Get-Content $file.FullName -Raw

        # Split the script by the 'GO' keyword (handling spaces and newlines)
        $batches = $scriptText -split '(?mi)^\s*GO\s*$'

        $batchNumber = 1
        $fileHasErrors = $false

        foreach ($batch in $batches) {
            if ([string]::IsNullOrWhiteSpace($batch)) { continue }

            try {
                $cmd.CommandText = $batch
                # This will compile the batch and check objects, but will not execute DML
                $cmd.ExecuteNonQuery() | Out-Null 
            } catch {
                $fileHasErrors = $true
                $hasGlobalErrors = $true
                Write-Host "[Batch $batchNumber Error] $($_.Exception.Message)" -ForegroundColor Red
            }
            $batchNumber++
        }

        if (-not $fileHasErrors) {
            Write-Host "Passed." -ForegroundColor Green
        }
    }

    if ($hasGlobalErrors) {
        exit 1
    }

} finally {
    if ($conn.State -eq 'Open') {
        # Optional: Reset NOEXEC before closing, though closing disposes the session anyway
        $cmd.CommandText = "SET NOEXEC OFF;"
        try { $cmd.ExecuteNonQuery() | Out-Null } catch {}
        
        $conn.Close()
    }
}
