# Guide de mise en ligne — GestionBarRestaurant

Ce guide permet de rendre l'application accessible via Internet (https://votre-domaine.com), avec HTTPS automatique et données persistantes.

## Architecture de déploiement

Internet → Caddy (HTTPS automatique, ports 80/443) → Application ASP.NET Core (port interne 8080) → SQLite (volume persistant `./data`)

Le projet contient déjà tout le nécessaire : `Dockerfile`, `docker-compose.yml`, `Caddyfile`. L'application lit la variable d'environnement `DATA_DIR` pour stocker la base SQLite dans un volume qui survit aux mises à jour.

## Option recommandée : VPS + Docker

Un VPS à partir de ~3 500 à 7 000 FCFA/mois suffit largement (1 vCPU / 1-2 Go RAM) : Contabo, Hetzner, OVH, DigitalOcean, ou un hébergeur local.

### 1. Préparer le serveur (Ubuntu 22.04/24.04)

```bash
ssh root@IP_DU_SERVEUR
apt update && apt upgrade -y
curl -fsSL https://get.docker.com | sh
apt install -y docker-compose-plugin
```

### 2. Pointer le domaine

Chez votre registrar (ex. un domaine .ci auprès de l'ARTCI ou un .com), créez un enregistrement DNS :

```
Type A    gestionbar.mondomaine.com    →    IP_DU_SERVEUR
```

Attendez la propagation (souvent < 1 h).

### 3. Déployer

```bash
# Copier le projet sur le serveur (depuis votre poste)
scp -r GestionBarRestaurant root@IP_DU_SERVEUR:/opt/gestionbar
ssh root@IP_DU_SERVEUR
cd /opt/gestionbar

# Mettre votre vrai domaine dans le Caddyfile
nano Caddyfile   # remplacer gestionbar.example.com

# Lancer
docker compose up -d --build
```

C'est tout : Caddy obtient le certificat HTTPS automatiquement (Let's Encrypt) et le renouvelle seul. L'application est disponible sur https://gestionbar.mondomaine.com.

### 4. Premières actions obligatoires après mise en ligne

1. Se connecter avec `superadmin` / `superadmin` et **changer immédiatement tous les mots de passe par défaut** (superadmin, admin, caissier, manager, admin2).
2. Désactiver les comptes de démonstration inutilisés.
3. Vérifier que la sauvegarde fonctionne (menu Sauvegarde, réservé au super administrateur).

### 5. Sauvegarde automatique quotidienne

```bash
crontab -e
# Copie de la base chaque nuit à 2h, conservation 30 jours
0 2 * * * cp /opt/gestionbar/data/gestionbar_analytics_v3.db /opt/gestionbar/backups/gestionbar_$(date +\%Y\%m\%d).db && find /opt/gestionbar/backups -name "*.db" -mtime +30 -delete
```

(créer le dossier avant : `mkdir -p /opt/gestionbar/backups`)

Pour une sécurité maximale, synchronisez aussi `backups/` vers un stockage externe (rclone vers Google Drive, S3, etc.).

### 6. Mise à jour de l'application

```bash
cd /opt/gestionbar
git pull   # ou re-copier les fichiers
docker compose up -d --build
```

La base de données n'est pas touchée (volume `./data`) ; le schéma se met à jour automatiquement au démarrage.

## Option alternative : plateformes cloud (sans serveur à gérer)

- **Fly.io** : `fly launch` détecte le Dockerfile ; ajoutez un volume (`fly volumes create data`) monté sur `/app/Data`.
- **Railway / Render** : connectez le dépôt GitHub, le Dockerfile est détecté ; configurez un disque persistant monté sur `/app/Data` et la variable `DATA_DIR=/app/Data`.

Attention : sans volume persistant configuré, la base SQLite serait effacée à chaque redéploiement.

## Limites de SQLite en ligne et évolution

SQLite convient très bien pour un établissement ou un petit groupe d'établissements (une seule instance applicative, trafic modéré). Si l'activité grossit (plusieurs dizaines d'utilisateurs simultanés, plusieurs instances, haute disponibilité), prévoir une migration vers PostgreSQL :

1. `dotnet add Presentation package Npgsql.EntityFrameworkCore.PostgreSQL` (idem Infrastructure)
2. Remplacer `UseSqlite(...)` par `UseNpgsql(connectionString)` dans `Program.cs`
3. Supprimer les conversions `HasConversion<double>()` du `AppDbContext` (spécifiques à SQLite) et utiliser des migrations EF Core.

## Sécurité déjà intégrée dans l'application

- HTTPS de bout en bout via Caddy + HSTS
- Cookies de session HttpOnly + SameSite Strict
- Mots de passe hachés PBKDF2 (100 000 itérations, SHA-256)
- Verrouillage de compte : 15 minutes après 5 échecs de connexion
- Protection CSRF globale (antiforgery automatique sur tous les POST)
- En-têtes de sécurité (nosniff, X-Frame-Options DENY, Referrer-Policy)
- Isolation multi-tenant sur toutes les requêtes ; sauvegarde globale réservée au super administrateur
