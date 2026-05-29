<<<<<<< HEAD
# LP2M_Bar_Mngt

Application standalone de gestion de bar / restaurant en C#, ASP.NET Core, Windows Forms, WPF et SQLite.

La solution contient maintenant trois interfaces :

- `LP2M_Bar_Mngt.Web` : interface web principale avec formulaires modaux.
- `LP2M_Bar_Mngt.WinForms` : interface principale Windows Forms.
- `LP2M_Bar_Mngt.Presentation` : ancienne interface WPF conservee comme alternative.

## Structure

- `src/LP2M_Bar_Mngt.Domain` : entites metier et enumerations.
- `src/LP2M_Bar_Mngt.Application` : contrats applicatifs et DTOs.
- `src/LP2M_Bar_Mngt.Infrastructure` : SQLite, schema, initialisation et services de lecture.
- `src/LP2M_Bar_Mngt.Web` : application web ASP.NET Core.
- `src/LP2M_Bar_Mngt.WinForms` : application Windows Forms complete.
- `src/LP2M_Bar_Mngt.Presentation` : application WPF alternative.

## Base locale

Au premier lancement, l'application cree automatiquement la base SQLite dans :

`%LOCALAPPDATA%\LP2M_Bar_Mngt\lp2m_bar_mngt.db`

Un utilisateur administrateur est initialise pour la V1 :

- Identifiant : `admin`
- Mot de passe temporaire : `admin123`

Le module Utilisateurs permet de reinitialiser ce mot de passe de demonstration.

## Authentification

La version web affiche un ecran de connexion avant les modules de gestion.
Les API metier sont protegees par une session locale via cookie HTTP-only.
La double authentification peut etre activee par utilisateur avec un code TOTP a 6 chiffres compatible Google Authenticator, Microsoft Authenticator, Authy ou application equivalente.

Compte initial :

- Identifiant : `admin`
- Mot de passe temporaire : `admin123`

Apres connexion, l'utilisateur courant est affiche dans la barre superieure et un bouton `Deconnexion` permet de fermer la session.

Activation de la double authentification :

- aller dans `Utilisateurs` ;
- cliquer sur `Activer 2FA` ou cocher `Double authentification active` dans la fiche utilisateur ;
- saisir la cle generee dans l'application Authenticator ;
- au prochain login, saisir le mot de passe puis le code 2FA.

Le compte `admin` reste sans 2FA par defaut pour eviter un blocage au premier lancement. Activez la 2FA apres avoir configure l'application Authenticator.

## Commandes

```powershell
dotnet build LP2M_Bar_Mngt.slnx
dotnet run --project src\LP2M_Bar_Mngt.Web\LP2M_Bar_Mngt.Web.csproj --urls http://localhost:5057
```

Pour exposer la version web sur le reseau local :

```powershell
dotnet run --project src\LP2M_Bar_Mngt.Web\LP2M_Bar_Mngt.Web.csproj --urls http://0.0.0.0:5057
```

Pour ouvrir l'application plus simplement sous Windows, lancer :

```powershell
.\Lancer-LP2M_Bar_Mngt.cmd
```

Le lanceur principal ouvre la version web. Lanceurs alternatifs :

```powershell
.\Lancer-LP2M_Bar_Mngt-Web.cmd
.\Lancer-LP2M_Bar_Mngt-WinForms.cmd
.\Lancer-LP2M_Bar_Mngt-WPF.cmd
```

En cas de probleme au demarrage, le journal se trouve ici :

`%LOCALAPPDATA%\LP2M_Bar_Mngt\startup.log`

## Modules V1 disponibles

- Caisse : ouvrir et cloturer une session par caissier, journal de mouvements.
- Ventes : creer une vente en panier pour un client, creer une vente rapide, reimprimer le dernier ticket, annuler la derniere vente.
- Produits : ajouter produit, ajouter categorie, mettre a jour un prix.
- Stock : reapprovisionner les alertes, ajuster un stock, verifier les alertes.
- Depenses : enregistrer une depense hors caisse ou payee depuis la caisse.
- Rapports : generer la synthese du jour et exporter un CSV.
- Utilisateurs : creer/modifier un utilisateur, activer/desactiver un compte, reinitialiser le mot de passe admin, ajouter une trace d'audit.
- Securite : connexion par session HTTP-only et double authentification TOTP optionnelle par utilisateur.

## Version web

La version web est disponible sur :

`http://localhost:5057`

Le lanceur web ecoute aussi sur `http://0.0.0.0:5057`, ce qui permet un acces depuis un autre poste ou une tablette du meme reseau via `http://ADRESSE_IP_DU_PC:5057` si le pare-feu Windows l'autorise.

Un endpoint de controle est disponible sur :

`http://localhost:5057/health`

Elle propose des tableaux par module et des fenetres modales de formulaire pour :

- creer/modifier un produit ;
- creer une categorie ;
- ajuster le stock ;
- enregistrer une depense ;
- creer/modifier un utilisateur ;
- ouvrir/cloturer la caisse ;
- creer une vente en panier avec client, plusieurs produits, quantites, remise, session de caisse et mode de paiement ;
- utiliser une calculatrice de vente avec recherche, filtre categorie, miniatures produit, ajout au panier et total calcule en FCFA ;
- creer/annuler une vente rapide ;
- generer/exporter le rapport du jour.

L'interface web a ete modernisee pour un usage SaaS/POS professionnel :

- menu lateral avec icones ;
- header avec utilisateur, date, deconnexion et theme clair/sombre ;
- dashboard avec KPI, depenses du jour, solde caisse, benefice estimatif, graphique simple et top produits ;
- tableaux avec recherche, filtre et pagination locale ;
- badges de statut, cartes d'action et formulaires modaux homogenes ;
- ecran de vente type POS avec cartes produit, panier, retrait de ligne et bouton Encaisser.

## Theme et catalogue CI

La version web utilise un theme LP2M `La pause de Medoune` inspire cafe, lait, chocolat, table en bois et gestion en FCFA.

Au demarrage, une migration locale applique un catalogue ivoirien avec des prix en FCFA. Les anciens produits de demonstration sont archives/masques, pas supprimes, afin de conserver les historiques existants. Categories principales :

- Cafe & chocolat ;
- Boissons fraiches ;
- Plats ivoiriens ;
- Snacks & accompagnements ;
- Spiritueux.

Exemples de produits et prix : cafe noir 250 FCFA, cafe au lait LP2M 500 FCFA, eau minerale 0,5L 300 FCFA, soda 33cl 500 FCFA, biere locale 65cl 1 000 FCFA, garba 1 500 FCFA, attieke poisson 3 000 FCFA, poulet braise 3 500 FCFA.

Les produits et categories du catalogue CI disposent de miniatures locales integrees en SVG dans SQLite. Elles s'affichent dans les tableaux produits/categories et dans la calculatrice de vente.

Les tableaux proposent aussi une suppression logique et un masquage des objets :

- `Masquer` retire l'objet de l'affichage standard.
- `Afficher masques` dans la barre superieure permet de revoir les objets archives.
- `Afficher` restaure un objet masque.
- `Supprimer` archive l'objet sans suppression physique, afin de conserver l'historique et l'audit.

Fonctions avancees disponibles dans la version web :

- actions multiples par selection de lignes : masquer, afficher, supprimer ;
- personnalisation du bar : nom, sigle, adresse, contact, logo, image et pied de ticket ;
- tickets personnalises avec logo, coordonnees, lignes de vente et impression navigateur ;
- validation de vente par client avec ticket multi-lignes et nom du client sur le ticket ;
- exports CSV : ventes, produits, stock, depenses, utilisateurs ;
- scan QR/code-barres via camera quand le navigateur le permet, avec saisie manuelle en secours ;
- prise de photo par camera pour produit, categorie, logo et image d'accueil ;
- images pour les produits et les categories ;
- en-tetes web de base pour reduire les risques de contenu injecte et d'integration externe non souhaitee.
- double authentification TOTP par utilisateur avec generation/reinitialisation de cle 2FA.
=======
# LP2M_Bar_Mngt
>>>>>>> 2a421f799cf67ef62ebe970f7fbdf4d6ab6cc039
