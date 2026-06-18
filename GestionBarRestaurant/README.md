# Gestion Bar Restaurant - Version Pro Analytics - .NET 10 + SQLite

Application ASP.NET Core MVC ciblant `net10.0`.

## Fonctionnalités principales

- Base de données SQLite réelle
- Création automatique du fichier `Presentation/Data/gestionbar_analytics_v3.db`
- Multi-tenant
- Images produits
- QR code produit
- Scan QR code / code-barres dans la caisse rapide
- Gestion clients
- Mise à jour multiple / en masse des produits et clients
- Impression de ticket
- Tableau de bord avec graphique
- Sauvegarde téléchargeable de la base SQLite

## Nouveaux modules ajoutés

### Fidélisation clients
- Paramétrage d'une règle de fidélité par tenant
- Attribution automatique de points à chaque vente
- Conversion des points en remise
- Tableau des points clients
- Historique des mouvements de points

### Prévision des ventes
- Prévision simple sur 7, 14, 30 ou 60 jours
- Moyenne journalière basée sur l'historique des ventes
- Coefficient de tendance
- Répartition par jour
- Indicateurs de tendance et projection de chiffre d'affaires

### Évaluation des caissiers
- Nombre de tickets par caissier
- Chiffre d'affaires par caissier
- Panier moyen
- Remises accordées
- Produits vendus
- Score de performance calculé automatiquement
- Classement des caissiers

## Comptes de test

| Profil | Login | Mot de passe | Tenant |
|---|---|---|---|
| Super Admin | superadmin | superadmin | Tous tenants |
| Admin | admin | admin | LP2M Bar |
| Caissier | caissier | caissier | LP2M Bar |
| Manager | manager | manager | LP2M Bar |
| Admin 2 | admin2 | admin2 | Maquis Démo |

## Lancement

```powershell
cd C:\DEV\GestionBarRestaurant_NET10_SQLite_Pro_Analytics\Presentation
dotnet restore
dotnet run
```

## Important

Si vous aviez déjà lancé une ancienne version, supprimez l'ancien fichier `Presentation/Data/gestionbar_analytics_v3.db` pour permettre la création du nouveau schéma avec les tables de fidélisation.


## Correctif Fidélité

Cette version corrige le problème du module Fidélité quand une ancienne base SQLite existe déjà.

Au démarrage, l'application ajoute automatiquement :
- la table `ReglesFidelite`
- la table `MouvementsFidelite`
- les colonnes fidélité dans `Clients`
- les colonnes fidélité dans `Ventes`

Si l'erreur persiste, lancer `Fix_Fidelite_SQLite.ps1` pour sauvegarder puis recréer la base.


## Correctif CORRIGE2

Cette version corrige l'erreur :

```text
The input string '0.0' was not in a correct format.
```

Cause : conversion de valeurs décimales SQLite sur certains postes configurés en culture française.

Correction :
- mapping EF Core `decimal` vers `double` pour SQLite ;
- colonnes fidélité numériques en `REAL` ;
- normalisation au démarrage ;
- script `Fix_Decimal_SQLite_Fidelite.ps1` disponible si une ancienne base bloque encore.


## Correction définitive Fidélité CORRIGE3

Cette version utilise une nouvelle base SQLite :

```text
Presentation\Data\gestionbar_analytics_v3.db
```

Cela évite le conflit avec les anciennes bases qui contenaient des montants stockés en texte comme `0.0`.
Le module Fidélité repart donc avec un schéma propre.


## Correctif sécurité et stabilité ajouté

Cette version contient aussi des corrections de sécurité et de bugs métier :

- mots de passe hachés avec PBKDF2 ;
- migration automatique des anciens mots de passe en clair au premier login ;
- protection CSRF globale sur les formulaires POST ;
- déconnexion sécurisée en POST ;
- validation renforcée des images produits ;
- correction de l’impact des dépenses sur la caisse ;
- correction du lien vente / mouvement fidélité ;
- validation du panier de vente rapide ;
- possibilité d’ajuster un stock final à 0 ;
- ajout d’en-têtes HTTP de sécurité.

Consultez `RAPPORT_CORRECTIONS_SECURITE_BUGS.md` pour le détail.
