# Correctif décimaux SQLite pour le module Fidélité
# Problème corrigé : The input string '0.0' was not in a correct format.

$ErrorActionPreference = "Stop"

$racinesPossibles = @(
    "C:\DEV\GestionBarRestaurant_NET10_SQLite_Pro_Analytics_CORRIGE2\Presentation",
    "C:\DEV\GestionBarRestaurant_NET10_SQLite_Pro_Analytics_CORRIGE\Presentation",
    "C:\DEV\GestionBarRestaurant_NET10_SQLite_Pro_Analytics\Presentation"
)

$racine = $racinesPossibles | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $racine) {
    Write-Host "Aucun dossier Presentation trouvé. Vérifiez le chemin du projet." -ForegroundColor Red
    exit 1
}

$db = Join-Path $racine "Data\gestionbar.db"

Write-Host "Dossier détecté : $racine" -ForegroundColor Cyan

if (Test-Path $db) {
    $backup = Join-Path $racine ("Data\gestionbar_backup_decimal_fix_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".db")
    Copy-Item $db $backup -Force
    Write-Host "Sauvegarde créée : $backup" -ForegroundColor Green

    # Méthode la plus propre : recréer la base avec le nouveau mapping.
    Remove-Item $db -Force
    Write-Host "Ancienne base supprimée. Elle sera recréée automatiquement au prochain lancement." -ForegroundColor Green
}
else {
    Write-Host "Aucune base existante à supprimer. La base sera créée au lancement." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Relancez maintenant :" -ForegroundColor Cyan
Write-Host "cd $racine"
Write-Host "dotnet clean"
Write-Host "dotnet run"
