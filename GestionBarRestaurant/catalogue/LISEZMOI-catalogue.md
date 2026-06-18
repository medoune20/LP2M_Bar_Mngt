# Catalogue Le Rustique — extrait du cahier de recettes

Ce catalogue a été reconstitué à partir des 11 photos du cahier (relevés journaliers
du 18 mai au 13 juin). L'écriture manuscrite étant par endroits difficile à lire,
**vérifie et corrige les prix** : tu les connais mieux que personne.

## Comment charger ce catalogue dans l'application

1. Crée d'abord le tenant « Le Rustique » (menu Tenants, en tant que super admin) et
   un utilisateur administrateur pour ce tenant.
2. Connecte-toi avec ce compte administrateur Le Rustique.
3. Va dans **Produits → Importer un catalogue**.
4. Le catalogue est déjà pré-rempli dans la zone de texte : vérifie/corrige, puis clique **Importer**.
   (Tu peux aussi choisir le fichier `catalogue-le-rustique.csv`.)

Les produits déjà existants et la ligne d'en-tête sont ignorés : tu peux réimporter
sans créer de doublons.

## Format
`Nom;Catégorie;PrixAchat;PrixVente;Stock;StockMini`

Le prix d'achat est estimé (~65 % du prix de vente) pour que le calcul de marge
fonctionne — ajuste-le avec tes vrais coûts d'achat. Le stock initial est arbitraire.
