# Reset complet des anciennes bases SQLite GestionBar
# À utiliser si l'erreur "The input string '0.0' was not in a correct format" persiste.

$ErrorActionPreference = "Stop"

$baseDirs = @(
    "C:\DEV\GestionBarRestaurant_NET10_SQLite_Pro_Analytics_CORRIGE3\Presentation\Data",
    "C:\DEV\GestionBarRestaurant_NET10_SQLite_Pro_Analytics_CORRIGE2\Presentation\Data",
    "C:\DEV\GestionBarRestaurant_NET10_SQLite_Pro_Analytics_CORRIGE\Presentation\Data",
    "C:\DEV\GestionBarRestaurant_NET10_SQLite_Pro_Analytics\Presentation\Data",
    "C:\DEV\GestionBarRestaurant_NET10_SQLite_Pro\Presentation\Data"
)

foreach ($dir in $baseDirs) {
    if (Test-Path $dir) {
        $dbFiles = Get-ChildItem $dir -Filter "*.db" -ErrorAction SilentlyContinue
        foreach ($db in $dbFiles) {
            $backup = Join-Path $dir ($db.BaseName + "_backup_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".db")
            Copy-Item $db.FullName $backup -Force
            Remove-Item $db.FullName -Force
            Write-Host "Base sauvegardée puis supprimée : $($db.FullName)" -ForegroundColor Green
            Write-Host "Backup : $backup" -ForegroundColor DarkGreen
        }
    }
}

Write-Host ""
Write-Host "Relance maintenant la version CORRIGE3 :" -ForegroundColor Cyan
Write-Host "cd C:\DEV\GestionBarRestaurant_NET10_SQLite_Pro_Analytics_CORRIGE3\Presentation"
Write-Host "dotnet clean"
Write-Host "dotnet restore"
Write-Host "dotnet run"
