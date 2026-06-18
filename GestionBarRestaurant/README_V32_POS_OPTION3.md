# GestionBarRestaurant - V3.2 POS Option 3

## Objet
Cette version applique la vraie refonte de la caisse rapide avec une approche hybride :

- structure de caisse tactile type commerce ;
- langage visuel Microsoft 365 / Fluent ;
- CSS dédiée et versionnée pour éviter que l'ancien POS reste en cache ;
- images produits uniformisées dans des cartes de taille constante ;
- ticket en cours clairement séparé du catalogue.

## Fichiers modifiés

- `Presentation/Views/Vente/Rapide.cshtml`
- `Presentation/wwwroot/css/pos-fluent-v32.css`
- `Presentation/Views/Shared/_Layout.cshtml`

## Correction importante

Le layout charge désormais une section `Styles`, ce qui permet à la caisse rapide de charger sa feuille CSS dédiée :

```cshtml
@await RenderSectionAsync("Styles", required: false)
```

Dans la vue POS :

```cshtml
@section Styles {
    <link href="~/css/pos-fluent-v32.css" rel="stylesheet" asp-append-version="true" />
}
```

Ainsi, même si `site.css` est en cache, le nouveau POS utilise un fichier séparé.

## Commandes

```powershell
dotnet restore .\GestionBarRestaurant.sln
dotnet build .\GestionBarRestaurant.sln -c Release
dotnet run --project .\Presentation\Presentation.csproj
```

## Après lancement

Faire un rafraîchissement forcé du navigateur :

- Chrome / Edge : `Ctrl + F5`
- ou ouvrir en navigation privée

Compte par défaut :

```text
Login : superadmin
Mot de passe : superadmin
```
