# v4.5 — Profils d'accès réutilisables (droits par module)

## Principe
- Un **profil** définit, pour chaque module, le niveau : Aucun accès / Consulter / Consulter + Modifier.
- Les profils sont **réutilisables** : on les crée une fois (menu « Profils d'accès », admin),
  puis on les affecte à des utilisateurs (champ « Profil d'accès » dans la fiche utilisateur).
- Modules couverts : Produits, Catégories, Stock, Clients, Fidélité, Ventes & caisse rapide,
  Caisse, Dépenses, Rapports & analyses.

## Règles d'accès effectives
- **Super administrateur** et **Administrateur de tenant** : accès complet en permanence
  (un profil ne les restreint jamais — protection anti-verrouillage).
- Utilisateur **avec profil** : son accès est exactement celui du profil.
- Utilisateur **sans profil** : droits par défaut de son rôle (Manager = tout le tenant ;
  Caissier = ventes + caisse uniquement). Le comportement actuel est donc conservé.
- En consultation (affichage) le droit « Consulter » suffit ; toute action d'enregistrement
  exige « Modifier ». Le menu masque automatiquement les modules non autorisés.

## Mise en application
Les changements de profil prennent effet à la **prochaine connexion** de l'utilisateur
(ses droits sont chargés en session au login).

## Pour créer un staff sur mesure
1. Menu « Profils d'accès » → créer un profil (ex. « Serveur ») → configurer les modules.
2. Fiche utilisateur → choisir ce profil dans « Profil d'accès ».
3. L'utilisateur se reconnecte : il ne voit que ses modules autorisés.
