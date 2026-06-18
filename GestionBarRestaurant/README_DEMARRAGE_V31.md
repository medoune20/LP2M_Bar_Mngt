# GestionBarRestaurant V3.1 — Démarrage corrigé

Cette version corrige le problème de démarrage suivant :

- `The WebRootPath was not found: ...\Presentation\wwwroot`
- `The layout view '_Layout' could not be located`

## Cause probable

Le ZIP précédent contenait un dossier racine `GestionBarRestaurant/`. Si le ZIP est extrait dans un dossier qui s'appelle déjà `GestionBarRestaurant`, les nouveaux fichiers peuvent se retrouver dans :

```text
C:\dev\GestionBarRestaurant\GestionBarRestaurant\Presentation\...
```

alors que la commande est lancée depuis :

```text
C:\dev\GestionBarRestaurant\Presentation
```

Dans ce cas, ASP.NET démarre sur l'ancien dossier, où `wwwroot` et `_Layout.cshtml` peuvent être absents.

## Installation recommandée

1. Fermer l'application si elle tourne.
2. Sauvegarder l'ancien dossier si nécessaire.
3. Extraire le contenu de ce ZIP directement dans :

```text
C:\dev\GestionBarRestaurant
```

Le dossier doit contenir directement :

```text
Application\
Domaine\
Infrastructure\
Presentation\
GestionBarRestaurant.sln
```

## Commandes

Depuis `C:\dev\GestionBarRestaurant` :

```powershell
dotnet restore .\GestionBarRestaurant.sln
dotnet build .\GestionBarRestaurant.sln -c Release
dotnet run --project .\Presentation\Presentation.csproj
```

Ou utiliser :

```powershell
.\REPARER_ET_LANCER_V31.ps1
```

## Compte par défaut

- Login : `superadmin`
- Mot de passe : `superadmin`

Changez ce mot de passe après la première connexion.
