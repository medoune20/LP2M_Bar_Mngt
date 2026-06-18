# Rapport d'analyse et corrections - Gestion Bar Restaurant

## Limite de vérification
L'environnement de correction ne dispose pas du SDK `dotnet`; la compilation réelle n'a donc pas pu être exécutée ici. Les corrections ont été appliquées par analyse statique du code source ASP.NET Core MVC / EF Core / SQLite.

## Failles et bugs corrigés

### 1. Mots de passe stockés en clair
- Ajout d'un helper de hachage PBKDF2 SHA-256 avec sel aléatoire.
- Les comptes de démonstration créés au démarrage sont maintenant hachés.
- Les anciens comptes en clair restent compatibles : au premier login réussi, le mot de passe est automatiquement migré en hash.
- Les hashes ne sont plus renvoyés dans le formulaire de modification utilisateur.

Fichiers modifiés :
- `Infrastructure/Securite/PasswordHelper.cs`
- `Infrastructure/Donnees/DatabaseInitializer.cs`
- `Infrastructure/Donnees/AppData.cs`
- `Presentation/Controllers/AuthController.cs`
- `Presentation/Controllers/UtilisateurController.cs`
- `Domaine/Models/Utilisateur.cs`

### 2. Déconnexion vulnérable au CSRF
- La déconnexion est passée de GET à POST.
- Le formulaire de déconnexion contient un token anti-CSRF.
- Un filtre global `AutoValidateAntiforgeryToken` protège les actions POST.

Fichiers modifiés :
- `Presentation/Program.cs`
- `Presentation/Controllers/AuthController.cs`
- `Presentation/Views/Shared/_Layout.cshtml`

### 3. Upload d'image insuffisamment contrôlé
- Limitation à 3 Mo.
- Formats acceptés : JPG, PNG, WEBP uniquement.
- Vérification de l'extension, du type MIME et de la signature réelle du fichier.
- Suppression de l'ancienne image locale lors du remplacement.
- Les GIF et autres formats risqués sont refusés.

Fichiers modifiés :
- `Presentation/Controllers/ProduitController.cs`
- `Presentation/Views/Produit/Nouveau.cshtml`
- `Presentation/Views/Produit/Modifier.cshtml`

### 4. Doublons fonctionnels
- Contrôle de l'unicité du login utilisateur.
- Contrôle de l'unicité du code tenant.
- Contrôle de l'unicité du code-barres produit par tenant.
- Ajout d'index EF Core correspondants.

Fichiers modifiés :
- `Presentation/Controllers/UtilisateurController.cs`
- `Presentation/Controllers/TenantController.cs`
- `Presentation/Controllers/ProduitController.cs`
- `Infrastructure/Donnees/AppDbContext.cs`

### 5. Vente rapide : panier incohérent et lien fidélité incorrect
- Validation de la cohérence `produitIds` / `quantites` pour éviter une erreur d'index.
- Transaction SQLite autour de l'enregistrement d'une vente.
- Numéro de ticket rendu plus unique.
- Correction du lien `VenteId` dans les mouvements de fidélité gagnés.
- Rollback en cas d'échec d'enregistrement.

Fichier modifié :
- `Presentation/Controllers/VenteController.cs`

### 6. Caisse et dépenses
- Validation serveur des montants d'ouverture et de clôture.
- Correction de l'impact des modifications/suppressions de dépenses sur les décaissements de la caisse ouverte.
- Une dépense datée avant l'ouverture de caisse n'impacte plus la caisse courante.

Fichiers modifiés :
- `Presentation/Controllers/CaisseController.cs`
- `Presentation/Controllers/DepenseController.cs`

### 7. Stock
- Correction de l'ajustement de stock : il est désormais possible de mettre le stock final à 0.
- Les entrées/sorties restent obligatoirement strictement positives.

Fichiers modifiés :
- `Presentation/Controllers/StockController.cs`
- `Presentation/Views/Stock/Index.cshtml`

### 8. Prévisions
- Remplacement d'un filtre EF basé sur `.Date` par une comparaison de bornes dates, plus robuste avec SQLite.

Fichier modifié :
- `Presentation/Controllers/PrevisionController.cs`

### 9. En-têtes de sécurité HTTP
- Ajout de `X-Content-Type-Options: nosniff`.
- Ajout de `X-Frame-Options: DENY`.
- Ajout de `Referrer-Policy: strict-origin-when-cross-origin`.
- Durcissement du cookie de session : `HttpOnly`, `SameSite=Strict`.

Fichier modifié :
- `Presentation/Program.cs`

## Points restant à vérifier sur ton poste
1. Installer le SDK .NET correspondant au `global.json`.
2. Exécuter :
   ```powershell
   cd Presentation
   dotnet restore
   dotnet build
   dotnet run
   ```
3. Tester les parcours critiques : connexion, création produit avec image, ouverture caisse, vente rapide, dépense, fermeture caisse, sauvegarde.
4. Pour une mise en production réelle, changer immédiatement les mots de passe de démonstration.
