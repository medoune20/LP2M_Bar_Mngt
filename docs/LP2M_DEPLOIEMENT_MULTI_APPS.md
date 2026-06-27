# LP2M - Déploiement multi-applications

Ce document décrit l'organisation cible du serveur LP2M autour d'un domaine unique :

- `https://lp2medoune.com/gestionbar` : LP2M Gestion Bar / Restaurant
- `https://lp2medoune.com/hopital` : LP2M Gestion Hôpital

## Architecture cible

```text
Internet
  ↓
Caddy Docker : ports 80 / 443
  ↓
/gestionbar → gestionbar-app:8080
/hopital    → host.docker.internal:5050 → gestionhopital:8080
```

Caddy est le seul service public qui doit écouter sur les ports 80 et 443.
Nginx et Certbot ne doivent pas être lancés en parallèle sur ces ports.

## Dépôts GitHub

- `medoune20/LP2M_Bar_Mngt`
  - application `GestionBarRestaurant/`
  - Caddy principal LP2M
  - configuration HTTPS et routage multi-applications

- `medoune20/LP2M_Gestion_Hopital`
  - application hôpital
  - exposée localement sur le port hôte `5050`
  - hébergée publiquement sous le préfixe `/hopital`

## Organisation recommandée sur le serveur

```text
/opt/lp2m
├── GestionBarRestaurant
└── gestionhopital
```

Le Caddy actif est celui de `GestionBarRestaurant`.

## Mise à jour de Gestion Bar / Caddy

```bash
cd /opt/lp2m
# ou le dossier où se trouve le clone LP2M_Bar_Mngt

git fetch --all --prune
git reset --hard origin/main

cd /opt/lp2m/GestionBarRestaurant
docker compose up -d --build --force-recreate
```

## Mise à jour de Gestion Hôpital

```bash
cd /opt/lp2m/gestionhopital

git fetch --all --prune
git reset --hard origin/main

docker compose up -d --build --force-recreate
```

## Vérifications

```bash
docker ps
sudo ss -ltnp | grep -E ':80|:443|:5050'
```

Résultat attendu :

- `gestionbar-caddy` écoute sur `80` et `443`.
- `gestionbar-app` écoute en interne sur `8080`.
- `gestionhopital` écoute sur le port hôte `5050` et le port conteneur `8080`.

## Tests HTTP

```bash
curl -Ik https://lp2medoune.com/gestionbar/Auth/Connexion
curl -Ik https://lp2medoune.com/hopital/
```

Codes acceptables :

- `200` : page OK
- `302` : redirection normale
- `401/403` : application joignable mais accès protégé

## Diagnostic Caddy

```bash
cd /opt/lp2m/GestionBarRestaurant
docker compose ps
docker logs gestionbar-caddy --tail=150
cat Caddyfile
```

## Diagnostic Hôpital

```bash
curl -I http://localhost:5050/health
docker logs gestionhopital --tail=100
```

## Règle importante

Ne pas lancer :

```bash
certbot --nginx
```

Caddy gère déjà automatiquement le certificat HTTPS Let's Encrypt.
