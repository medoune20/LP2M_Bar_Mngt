# Correction POS Microsoft 365 / Fluent - Gestion Bar Restaurant

## Problème signalé
- Impossible de valider le paiement depuis la caisse rapide.
- Design POS à améliorer fortement.
- Base à vider pour repartir sur une configuration propre.
- Conserver uniquement le compte `superadmin`.
- Créer les profils par défaut : Administrateur, Manager, Caissier.
- Précharger les catégories et les produits bar d'Abidjan avec prix indicatifs et images réelles.

## Correctifs appliqués

### 1. Paiement débloqué
La caisse rapide ouvre désormais automatiquement une session de caisse personnelle si aucune session ouverte n'existe.
Ainsi, une base fraîche permet de vendre immédiatement après connexion.

Fichiers modifiés :
- `Presentation/Controllers/VenteController.cs`
- `Presentation/Views/Vente/Rapide.cshtml`

### 2. Nouvelle caisse rapide
La vue `Rapide.cshtml` a été refaite avec une expérience proche Microsoft 365 / Fluent :
- en-tête POS compact ;
- barre de recherche et scanner/code-barres ;
- rail de catégories ;
- grille tactile produits ;
- ticket fixe à droite ;
- modes de paiement clairs ;
- montant encaissé visible ;
- monnaie rendue visible ;
- protection contre double validation ;
- validation côté client avant envoi.

### 3. Base propre au premier lancement
La base SQLite livrée a été retirée du package. Au premier lancement, l'application recrée automatiquement une base propre avec :
- 1 tenant : `Bar Restaurant Abidjan` ;
- 1 utilisateur : `superadmin` / `superadmin` ;
- 3 profils standards ;
- 5 catégories standards ;
- un catalogue de produits bar Abidjan.

Un script manuel de reset est ajouté :
- `Reset_Base_Propre_POS.ps1`

### 4. Profils par défaut
Profils créés automatiquement :
- Administrateur : accès complet ;
- Manager : accès complet métier ;
- Caissier : produits en consultation, ventes et caisse en modification.

### 5. Catégories par défaut
- Boissons non alcoolisées
- Bières et maltées
- Spiritueux
- Vins et cocktails
- Snacks et accompagnements

### 6. Catalogue produit initial
Le catalogue initial reprend les familles demandées : eaux, sucreries, bières Solibra, Guinness, Valpierre, spiritueux, vins, cocktails et snacks/cuisine simple.
Les prix sont indicatifs pour Abidjan et les visuels produits sont renseignés via des URLs publiques de produits réels lorsque disponibles.

## Identifiants de test

```text
Login       : superadmin
Mot de passe: superadmin
```

## Commandes de vérification

```powershell
dotnet restore .\GestionBarRestaurant.sln
dotnet build .\GestionBarRestaurant.sln -c Release
dotnet run --project .\Presentation\Presentation.csproj
```

## Limite connue
Les images distantes nécessitent Internet côté navigateur. Si une image externe est indisponible, la caisse rapide affiche automatiquement une image locale de remplacement.
