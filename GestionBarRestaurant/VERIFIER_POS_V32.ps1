$ErrorActionPreference = "Stop"
Write-Host "Verification V3.2 POS..." -ForegroundColor Cyan
$required = @(
  "Presentation\Views\Shared\_Layout.cshtml",
  "Presentation\Views\Vente\Rapide.cshtml",
  "Presentation\wwwroot\css\pos-fluent-v32.css",
  "Presentation\wwwroot\css\site.css",
  "Presentation\wwwroot\js\site.js",
  "GestionBarRestaurant.sln"
)
foreach ($f in $required) {
  if (!(Test-Path $f)) { throw "Fichier manquant : $f" }
  Write-Host "OK $f" -ForegroundColor Green
}
$layout = Get-Content "Presentation\Views\Shared\_Layout.cshtml" -Raw
if ($layout -notmatch 'RenderSectionAsync\("Styles"') { throw "La section Styles n'est pas rendue dans _Layout.cshtml" }
$view = Get-Content "Presentation\Views\Vente\Rapide.cshtml" -Raw
if ($view -notmatch 'pos-fluent-v32.css') { throw "La vue caisse rapide ne charge pas pos-fluent-v32.css" }
if ($view -notmatch 'gbpos32-shell') { throw "La nouvelle structure POS V3.2 n'est pas présente" }
Write-Host "Structure POS V3.2 valide." -ForegroundColor Green
Write-Host "Lancez ensuite : dotnet run --project .\Presentation\Presentation.csproj" -ForegroundColor Yellow
