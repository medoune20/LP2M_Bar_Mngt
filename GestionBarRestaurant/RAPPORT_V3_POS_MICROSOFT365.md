# Gestion Bar Restaurant — V3 Pro Microsoft 365 POS

## Identifiants initiaux
- Login : `superadmin`
- Mot de passe : `superadmin`
- Base créée au premier lancement : `Presentation/Data/gestionbar_analytics_v3.db`

## Correctifs appliqués
- Correction du warning EF1002 : suppression de l'appel `ExecuteSqlRaw` interpolé pour les colonnes dynamiques ; ajout d'une liste blanche tables/colonnes et exécution via `DbCommand`.
- Correction du warning ASPDEPR005 : remplacement de `KnownNetworks` par `KnownIPNetworks`.
- Nouvelle caisse rapide Microsoft 365 / Fluent UX : rail catégories, catalogue tactile, images uniformes, ticket à droite, quick cash, référence transaction, validation anti double-clic.
- Profils et rôles alignés : `Administrateur`, `Manager`, `Caissier` sont créés pour chaque tenant.
- Tous les modules sont exposés dans la gestion des profils/rôles : dashboard, caisse rapide, ventes, caisse, produits, catégories, stock, clients, fidélité, dépenses, rapports, prévisions, évaluation caissiers, utilisateurs, profils, tenants, sauvegarde, paramètres.
- Création d'un nouveau tenant : catégories, client comptoir, règles fidélité, profils/rôles et catalogue bar Abidjan sont créés automatiquement.
- Google Login prêt : bouton `Continuer avec Google`, actions OAuth, création d'une demande d'établissement après première connexion Google.
- Référence obligatoire pour Mobile Money et Carte bancaire.
- Base livrée vide : aucun fichier SQLite n'est inclus dans le ZIP.

## Configuration Google OAuth
Renseigner dans `Presentation/appsettings.json` ou via variables d'environnement :

```json
"Authentication": {
  "Google": {
    "ClientId": "VOTRE_CLIENT_ID",
    "ClientSecret": "VOTRE_CLIENT_SECRET"
  }
}
```

Callback attendu côté Google : `/signin-google` sur l'URL publique de l'application.

## Règles de gestion ajoutées / renforcées
- Le POS ouvre une session personnelle de caisse si aucune session n'est disponible.
- Une vente à crédit exige un client identifié.
- Mobile Money / Carte exigent une référence transaction.
- Les ventes validées restent traçables ; l'annulation est une contre-passation, pas une suppression.
- Les profils système ne peuvent pas être supprimés.
- Le stock baisse à la validation et revient en cas d'annulation.

## À tester après extraction
```powershell
dotnet restore .\GestionBarRestaurant.sln
dotnet build .\GestionBarRestaurant.sln -c Release
dotnet run --project .\Presentation\Presentation.csproj
```
