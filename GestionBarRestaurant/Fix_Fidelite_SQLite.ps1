# Correctif module Fidélité / SQLite
# À lancer dans PowerShell si le module Fidélité affiche encore "Une erreur est survenue".

$ErrorActionPreference = "Stop"

$racine = "C:\DEV\GestionBarRestaurant_NET10_SQLite_Pro_Analytics_CORRIGE\Presentation"
$db = Join-Path $racine "Data\gestionbar.db"

Write-Host "Arrêtez l'application si elle est en cours d'exécution." -ForegroundColor Yellow

if (Test-Path $db) {
    $backup = Join-Path $racine ("Data\gestionbar_backup_avant_fix_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".db")
    Copy-Item $db $backup -Force
    Remove-Item $db -Force
    Write-Host "Ancienne base sauvegardée puis supprimée :" -ForegroundColor Green
    Write-Host $backup
}
else {
    Write-Host "Aucune ancienne base trouvée." -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Relancez maintenant :" -ForegroundColor Cyan
Write-Host "cd $racine"
Write-Host "dotnet run"
