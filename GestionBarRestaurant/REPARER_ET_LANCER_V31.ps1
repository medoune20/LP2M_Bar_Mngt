# Réparation/lancement GestionBarRestaurant V3.1
# À exécuter depuis le dossier racine du projet : C:\dev\GestionBarRestaurant

$ErrorActionPreference = 'Stop'
$racine = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $racine

Write-Host "Racine projet : $racine" -ForegroundColor Cyan

$layout = Join-Path $racine 'Presentation\Views\Shared\_Layout.cshtml'
$wwwroot = Join-Path $racine 'Presentation\wwwroot'

if (!(Test-Path $layout) -or !(Test-Path $wwwroot)) {
    Write-Host "Structure MVC incomplète. Vérification d'un dossier imbriqué..." -ForegroundColor Yellow
    $racineImbriquee = Join-Path $racine 'GestionBarRestaurant'
    $layoutImbrique = Join-Path $racineImbriquee 'Presentation\Views\Shared\_Layout.cshtml'
    $wwwrootImbrique = Join-Path $racineImbriquee 'Presentation\wwwroot'

    if ((Test-Path $layoutImbrique) -and (Test-Path $wwwrootImbrique)) {
        Write-Host "Dossier imbriqué détecté. Copie des fichiers manquants vers la vraie racine..." -ForegroundColor Yellow
        Copy-Item -Path (Join-Path $racineImbriquee '*') -Destination $racine -Recurse -Force
    }
}

if (!(Test-Path $layout)) {
    throw "Fichier introuvable : Presentation\Views\Shared\_Layout.cshtml. Réextraire le ZIP V3.1 directement dans C:\dev\GestionBarRestaurant."
}
if (!(Test-Path $wwwroot)) {
    throw "Dossier introuvable : Presentation\wwwroot. Réextraire le ZIP V3.1 directement dans C:\dev\GestionBarRestaurant."
}

Write-Host "Nettoyage des anciennes bases locales V3..." -ForegroundColor Cyan
Remove-Item -Path (Join-Path $racine 'Presentation\Data\*.db') -Force -ErrorAction SilentlyContinue
Remove-Item -Path (Join-Path $racine 'Presentation\Data\*.sqlite') -Force -ErrorAction SilentlyContinue

Write-Host "Restauration des packages..." -ForegroundColor Cyan
dotnet restore .\GestionBarRestaurant.sln

Write-Host "Build Release..." -ForegroundColor Cyan
dotnet build .\GestionBarRestaurant.sln -c Release

Write-Host "Lancement de l'application..." -ForegroundColor Green
dotnet run --project .\Presentation\Presentation.csproj
