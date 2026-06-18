# Nouvelles règles de gestion — version 3

Toutes les évolutions ci-dessous sont rétrocompatibles : au premier démarrage, le schéma SQLite existant est mis à jour automatiquement (nouvelles colonnes et table `MouvementsCaisse`), sans perte de données.

## 1. Caisse : règle du tiroir-espèces

- **Seules les espèces entrent dans le tiroir.** `Encaissements` ne compte plus que les ventes en espèces ; les autres modes (Mobile Money, carte, ticket) sont suivis dans `EncaissementsAutres`. Le solde théorique à compter à la clôture correspond donc enfin à ce qu'il y a réellement dans le tiroir.
- **Ventilation par mode de paiement** affichée sur l'écran caisse (mini rapport Z de session).
- **Apports et retraits d'espèces** en cours de session (appoint de monnaie, remise en banque) avec motif obligatoire, journalisés dans `MouvementsCaisse` avec auteur et horodatage. Un retrait ne peut pas dépasser le solde théorique.
- **Justification d'écart obligatoire** : impossible de clôturer avec un écart (manquant ou excédent) sans commentaire écrit. L'écart, sa justification et l'auteur de la clôture sont conservés dans l'historique.

## 2. Ventes : annulation auditée et marge réelle

- **Annulation de vente** (Manager/Administrateur uniquement, motif obligatoire) : pas de suppression physique — le ticket reste en base avec statut « Annulée », date, motif et auteur (piste d'audit). L'annulation :
  - restitue le stock (mouvement d'entrée tracé),
  - reprend les points fidélité gagnés et restitue les points utilisés,
  - contre-passe la caisse si la session d'origine est encore ouverte ; sinon, le remboursement sort de la caisse ouverte sous forme de décaissement journalisé,
  - reprend l'encours crédit du client pour une vente à crédit.
- **Rattachement à la session de caisse** : chaque vente mémorise `CaisseSessionId`, ce qui fiabilise les rapports de session.
- **Marge brute réelle** : le coût d'achat unitaire est figé sur chaque ligne au moment de la vente (`PrixAchatUnitaire`). Les changements ultérieurs de prix d'achat ne faussent plus les marges historiques.

## 3. Crédit client encadré

- Nouveau mode de paiement **« Crédit »** sur le POS, qui exige un client identifié (refusé pour le client comptoir).
- **Plafond de crédit** par client (0 = illimité) : la vente est bloquée si l'encours dépasserait le plafond.
- Le **solde crédit n'est plus modifiable à la main** (lecture seule dans la fiche client) : il augmente par vente à crédit et diminue uniquement par **règlement de créance** encaissé (bouton « Régler crédit » dans la liste clients), journalisé dans la caisse avec le mode de paiement.

## 4. Dépenses verrouillées après clôture

- Chaque dépense est rattachée à la session de caisse ouverte au moment de la saisie.
- Une dépense liée à une **session clôturée ne peut plus être modifiée ni supprimée** (intégrité comptable des arrêtés de caisse).

## 5. Stock : valorisation CMUP

- À l'entrée de stock, possibilité de saisir le **coût d'achat unitaire** : le prix d'achat du produit est recalculé au **Coût Moyen Unitaire Pondéré** et le coût est tracé dans le mouvement.

## 6. Sécurité

- **Verrouillage de compte** : 5 échecs de connexion → compte bloqué 15 minutes (anti force brute, indispensable pour une application exposée sur Internet).
- **Sauvegarde de la base réservée au super administrateur** : la base SQLite contient les données de tous les tenants, son téléchargement par l'admin d'un seul tenant constituait une fuite inter-tenant.
- Mot de passe : minimum 6 caractères (déjà en place), hachage PBKDF2.

## 7. Préparation à la mise en ligne

- Répertoire de données configurable via la variable d'environnement `DATA_DIR` (volume Docker persistant).
- Prise en charge des en-têtes `X-Forwarded-For` / `X-Forwarded-Proto` (reverse proxy HTTPS).
- `Dockerfile`, `docker-compose.yml` (application + Caddy avec HTTPS automatique) et guide complet dans `DEPLOIEMENT.md`.
- Correction du libellé « Livraison » → « Mobile Money » sur le POS.


## 8. Version mobile (v3.1)

- **Application installable (PWA)** : manifest, icônes et couleur de thème ajoutés. Sur téléphone, ouvrir le site dans Chrome/Safari → « Ajouter à l'écran d'accueil » : l'application se lance alors en plein écran comme une app native, avec sa propre icône.
- **Navigation mobile** : barre fixe en bas d'écran (Accueil, Ventes, Caisse rapide en bouton central, Stock, Session) sur téléphone et petite tablette ; le menu hamburger complet reste disponible en haut.
- **POS tactile adapté téléphone** :
  - grille produits 3 colonnes (2 sur très petits écrans), tuiles compactes,
  - catégories en défilement horizontal,
  - le panier devient un tiroir plein écran : barre fixe en bas avec nombre d'articles + total net + bouton ENCAISSER, qui ouvre le panier (clavier, modes de paiement, validation), bouton « Retour aux produits » pour revenir à la grille,
  - clavier numérique avec touches dimensionnées pour le doigt.
- **Confort mobile général** : tableaux défilants, champs à 16 px (supprime le zoom automatique iOS), boutons d'action agrandis, zones sécurisées (encoche iPhone) respectées.

## 9. Correctifs v3.2

### Enregistrement des produits réparé
- **Culture invariante forcée** côté serveur : le binding des nombres (prix, stock) ne dépend plus de la locale du conteneur Linux ni du navigateur du téléphone. C'était la cause de l'échec silencieux à l'enregistrement.
- **Catégorie rendue optionnelle** (valeur « Divers » par défaut si vide) : un champ obligatoire laissé vide ne bloque plus l'enregistrement sans explication.
- **Champs gérés par le serveur** (code-barres, image, catégorie) retirés de la validation du formulaire.
- **Erreurs de validation désormais visibles** : un encadré rouge « Impossible d'enregistrer » affiche la raison exacte, au lieu d'un rechargement muet de la page.

### POS mobile simplifié
Sur téléphone, l'écran de caisse rapide retire les éléments redondants avec un mobile et garde l'essentiel :
- **Clavier numérique tactile masqué** : le clavier natif du téléphone est utilisé via le champ « Montant encaissé » (en mode décimal).
- **Actions secondaires masquées** (mise en attente, note, impression, ouverture tiroir) : peu utiles depuis un téléphone, elles restent disponibles sur PC.
- **Cartes de total réduites** : seule la carte « TOTAL À PAYER » est affichée, en grand.
- **Modes de paiement** présentés en 2 colonnes lisibles ; les modes rares (Ticket Restaurant, Offert) masqués sur mobile.
- **Bouton VALIDER LA VENTE** pleine largeur, bien visible en bas du tiroir panier.

## 10. Correctifs v3.4 — audit caisse / ventes / rapports / stock

### Bug systémique corrigé : échecs silencieux d'enregistrement
Les formulaires de création/modification des **Clients, Tenants, Utilisateurs et Dépenses**
n'affichaient aucune erreur de validation : si un champ obligatoire était vide ou invalide,
la page se rechargeait sans message (« rien ne se passe »). Un encadré rouge « Impossible
d'enregistrer » a été ajouté à tous ces formulaires (même correctif que pour les produits).

### Nouvelle fonction : import de catalogue
Produits → Importer un catalogue (CSV / collage), réutilisable, sans doublon.

### Vérifications effectuées (aucune anomalie trouvée)
- Tous les formulaires POST possèdent leur jeton anti-CSRF.
- Le JavaScript de la caisse rapide est syntaxiquement valide (boutons fonctionnels).
- Les contrats vue↔contrôleur de la Caisse et du Stock correspondent exactement.
- Les contrôleurs Rapport, Accueil, Prévision et Évaluation compilent et sont correctement câblés.
- Culture invariante forcée pour la lecture des montants (indépendante du serveur/navigateur).
