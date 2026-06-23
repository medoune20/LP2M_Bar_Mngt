# Checklist de mise en production — LP2M APPS

À faire avant et juste après la mise en ligne. Coche chaque point.

## 1. 🔴 Mots de passe (critique)
- [ ] Se connecter en `superadmin` puis **changer immédiatement** son mot de passe (règles : 8+ caractères, majuscule, minuscule, chiffre, caractère spécial).
- [ ] **Désactiver ou changer** tous les comptes de démonstration (admin, caissier, manager, admin2…).
- [ ] Chaque établissement (tenant) a son propre administrateur avec un mot de passe unique.
- [ ] Le mécanisme **« Mot de passe oublié »** fonctionne (SMTP configuré, voir §4).

## 2. 🔒 HTTPS et domaine
- [ ] Le domaine pointe vers le serveur (enregistrement DNS `A`).
- [ ] Le vrai domaine est renseigné dans le `Caddyfile`.
- [ ] `https://VOTRE_DOMAINE/health` répond `{"status":"ok"}`.
- [ ] Le certificat HTTPS est délivré automatiquement par Caddy (Let's Encrypt).

## 3. 🧱 Pare-feu serveur (Ubuntu)
```bash
ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw enable
ufw status
```
> N'exposez PAS le port 8080 (l'app n'est jointe que par Caddy en interne).

## 4. ✉️ Emails (mot de passe oublié / inscription)
- [ ] `cp .env.example .env` puis renseigner les valeurs SMTP (voir `.env.example`).
- [ ] `.env` n'est **pas** versionné (déjà dans `.gitignore`).
- [ ] Test : page de connexion → « Mot de passe oublié ? » → réception du lien.

## 5. 💾 Sauvegardes (critique)
Toute l'activité (ventes, caisse, **comptabilité**) est dans un seul fichier SQLite.
```bash
cd /opt/lp2m/GestionBarRestaurant
bash scripts/secure-and-backup.sh          # sauvegarde immédiate + cron quotidien (02h00)
```
- [ ] Une sauvegarde apparaît dans `backups/`.
- [ ] Le cron est installé (`crontab -l`).
- [ ] (Recommandé) Copie des sauvegardes vers un stockage externe (rclone → Google Drive / S3).

**Restauration** (en cas de besoin) :
```bash
bash scripts/secure-and-backup.sh --restore backups/gb_AAAAMMJJ_HHMMSS.db.gz
docker compose restart app
```

## 6. 🔐 API comptable
- [ ] Les clés API ne sont créées que pour les intégrations nécessaires (menu Comptabilité → Clés API).
- [ ] Révoquer toute clé non utilisée.

## 7. 🔄 Mises à jour
```bash
cd /opt/lp2m && git pull
cd GestionBarRestaurant && docker compose up -d --build
```
> La base est préservée (volume `./data`) ; le schéma se met à jour automatiquement (migrations idempotentes).

## 8. ✅ Vérifications finales
- [ ] Connexion, vente test, encaissement, ticket.
- [ ] Caisse rapide affichée entièrement (pas de débordement horizontal).
- [ ] Comptabilité : écritures, balance, export CSV.
- [ ] Messagerie temps réel entre deux utilisateurs du même établissement.
