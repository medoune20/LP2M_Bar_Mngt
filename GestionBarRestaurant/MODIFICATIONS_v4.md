# Modifications v4

## 1. Programme de fidélité réparé
La case « Programme actif » plaçait le champ caché `false` AVANT la case à cocher.
Quand on cochait, le formulaire envoyait `false,true` et ASP.NET conservait la première
valeur (`false`) : la règle était donc toujours enregistrée comme INACTIVE, et le POS
n'attribuait jamais de points. La case est désormais placée correctement.

> Après mise à jour : ouvrez Fidélité, vérifiez que « Programme actif » est coché et
> ré-enregistrez la règle une fois (les anciennes règles avaient été stockées inactives).

## 2. Informations établissement (factures & publicité)
Le tenant dispose maintenant de : slogan, email, site web/page, ville, logo (upload),
registre de commerce (RCCM), compte contribuable (NCC) et mention de bas de facture.
Ces informations s'affichent automatiquement sur le ticket de vente (en-tête avec logo
et coordonnées, pied avec mentions légales).

## 3. Module de gestion des catégories de produits
Nouveau menu « Catégories » (admin/manager) : créer, renommer (répercuté sur les produits),
réordonner, colorer, activer/désactiver et supprimer (bloqué si des produits l'utilisent).
Les formulaires produits proposent désormais les catégories existantes (saisie assistée).
À la première ouverture, les catégories sont amorcées depuis les produits existants.

## 4. Séparation des droits
- **Super administrateur** : voit TOUT, tous les tenants (liste des tenants, sauvegarde
  globale, changement de tenant).
- **Administrateur de tenant** : voit tout MAIS uniquement sur son établissement. Gère
  produits, catégories, stock, clients, fidélité, dépenses, rapports, utilisateurs, et
  édite la fiche « Mon établissement ». Ne voit pas les autres tenants.
- **Manager** : supervise les caissiers — accède aux ventes, caisses, rapports et à la
  page « Caissiers » pour toute l'activité du tenant.
- **Caissier** : ne voit QUE sa propre activité — ses ventes, sa caisse, son tableau de
  bord. Chaque caissier gère sa propre session de caisse. Pas d'accès aux produits, stock,
  clients, fidélité, dépenses ni rapports.

Techniquement : chaque vente mémorise le caissier (`VendeurId`) et chaque session de caisse
son ouvreur (`CaissierId`) ; les listes Ventes / Caisse / Dashboard sont filtrées pour les
caissiers. Le menu s'adapte au rôle.

## 5. Création rapide de client au POS (caissier inclus)
Un bouton « + » à côté du sélecteur de client dans la caisse rapide ouvre une fenêtre
(nom + téléphone optionnel). Le client est créé instantanément et sélectionné, sans quitter
l'écran ni perdre le panier — pratique pour une vente à crédit ou l'attribution de points.
Accessible à tous les rôles (y compris caissier), sans pour autant ouvrir au caissier la
gestion complète des clients.
