# Réinitialise la base SQLite de la V3.
# Au prochain lancement, l'application recrée uniquement :
# - tenant Bar Restaurant Abidjan
# - compte superadmin / superadmin
# - profils/rôles Administrateur, Manager, Caissier
# - catégories par défaut, client comptoir, fidélité, catalogue bar Abidjan

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$data = Join-Path $root "Presentation\Data"
if (Test-Path $data) {
    Get-ChildItem $data -Filter "*.db" -ErrorAction SilentlyContinue | Remove-Item -Force
    Get-ChildItem $data -Filter "*.db-*" -ErrorAction SilentlyContinue | Remove-Item -Force
}
Write-Host "Base V3 supprimée. Relancez : dotnet run --project .\Presentation\Presentation.csproj" -ForegroundColor Green
