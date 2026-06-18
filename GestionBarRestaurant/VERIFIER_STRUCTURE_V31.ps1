# Vérifie que la structure MVC est complète avant dotnet run
$ErrorActionPreference = 'Stop'
$racine = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $racine

$elements = @(
    'GestionBarRestaurant.sln',
    'Presentation\Presentation.csproj',
    'Presentation\Program.cs',
    'Presentation\Views\_ViewStart.cshtml',
    'Presentation\Views\Shared\_Layout.cshtml',
    'Presentation\wwwroot',
    'Presentation\wwwroot\css\site.css',
    'Presentation\wwwroot\js\site.js',
    'Infrastructure\Donnees\DatabaseInitializer.cs',
    'Domaine\Models\Utilisateur.cs'
)

$manquants = @()
foreach ($e in $elements) {
    if (!(Test-Path (Join-Path $racine $e))) { $manquants += $e }
}

if ($manquants.Count -gt 0) {
    Write-Host "Structure incomplète. Eléments manquants :" -ForegroundColor Red
    $manquants | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    Write-Host "Solution : extraire le ZIP V3.1 directement dans C:\dev\GestionBarRestaurant, sans créer un sous-dossier supplémentaire." -ForegroundColor Yellow
    exit 1
}

Write-Host "Structure OK. Vous pouvez lancer : dotnet run --project .\Presentation\Presentation.csproj" -ForegroundColor Green
