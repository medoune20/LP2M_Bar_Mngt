# Rapport de mise à jour - Interface Caisse Rapide type CLYO POS

## Objectif
Refondre l'écran **Vente > Caisse rapide** pour qu'il se rapproche du modèle de caisse enregistreuse fourni : grande grille produits à gauche, ticket de caisse à droite, barre supérieure bleue, boutons de paiement, pavé numérique et onglets de catégories.

## Fichiers modifiés
- `Presentation/Views/Vente/Rapide.cshtml`
- `Presentation/wwwroot/css/site.css`

## Améliorations appliquées

### 1. Nouveau layout caisse enregistreuse
- Suppression de l'ancienne présentation sombre POS Pro sur la caisse rapide.
- Mise en place d'un écran plein type terminal de caisse.
- En-tête bleu avec logo CLYO, date, heure, poste, mode direct, vendeur et état de caisse.
- Zone produits à gauche et ticket à droite, comme le modèle demandé.

### 2. Grille de produits avec images
- Chaque produit est affiché sous forme de grande tuile tactile.
- Les tuiles affichent : nom du produit, image, prix.
- Si un produit possède déjà une image, elle est utilisée.
- Si un produit n'a pas d'image, une illustration SVG locale est générée automatiquement selon le type de produit : sandwich, burger, salade, sushi, pizza, boisson, café, dessert, grillade, etc.

### 3. Couleurs par famille de produits
- Rouge : sandwiches, burgers, grillades.
- Vert : salades, soupes, produits frais.
- Violet : sushi, saumon, thon, crevette.
- Orange : pizzas, pâtes, desserts.
- Bleu / cyan : boissons, cafés, smoothies.

### 4. Ticket de caisse à droite
- Tableau ticket avec colonnes : Qté, Article, Prix, Sous-total.
- Boutons + / - pour modifier les quantités.
- Suppression rapide d'une ligne.
- Calcul automatique du total brut et du total à payer.

### 5. Zone paiement et validation
- Boutons de paiement : Espèces, C.B., Livraison, Ticket R, Offert / remis.
- Pavé numérique tactile.
- Bouton vert **VALIDER** pour encaisser.
- Actions rapides : attente, note, impression ticket, ouverture tiroir.

## Points conservés
- Protection CSRF existante.
- Contrôle de caisse ouverte avant vente.
- Logique existante d'enregistrement de vente.
- Gestion du stock.
- Gestion client et fidélité.
- Contrôle du montant reçu pour le paiement espèces.

## Test recommandé
1. Lancer l'application.
2. Aller dans **Caisse rapide**.
3. Vérifier l'affichage en plein écran.
4. Cliquer sur plusieurs produits.
5. Modifier les quantités dans le ticket.
6. Choisir un mode de paiement.
7. Saisir un montant avec le pavé numérique ou sélectionner un paiement non espèces.
8. Valider la vente.
