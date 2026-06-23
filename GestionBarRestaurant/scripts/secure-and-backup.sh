#!/usr/bin/env bash
#
# Sauvegarde de la base SQLite de LP2M APPS + installation d'une sauvegarde
# automatique quotidienne (cron). Conçu pour un déploiement Docker où la base
# vit dans le volume ./data du dossier GestionBarRestaurant.
#
# Usage :
#   ./secure-and-backup.sh              # sauvegarde immédiate + installe le cron
#   ./secure-and-backup.sh --now        # sauvegarde immédiate uniquement
#   ./secure-and-backup.sh --install-cron
#   ./secure-and-backup.sh --restore <fichier.db>
#
# Variables (surchargables) :
#   APP_DIR     dossier contenant docker-compose.yml (défaut : dossier parent du script)
#   RETENTION   nombre de jours de conservation (défaut : 30)
#   BACKUP_DIR  dossier des sauvegardes (défaut : $APP_DIR/backups)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="${APP_DIR:-$(cd "$SCRIPT_DIR/.." && pwd)}"
DATA_DIR="$APP_DIR/data"
DB_FILE="$DATA_DIR/gestionbar_analytics_v3.db"
BACKUP_DIR="${BACKUP_DIR:-$APP_DIR/backups}"
RETENTION="${RETENTION:-30}"

couleur() { printf "\033[1;36m%s\033[0m\n" "$*"; }
erreur()  { printf "\033[1;31m%s\033[0m\n" "$*" >&2; }

sauvegarde_maintenant() {
  if [[ ! -f "$DB_FILE" ]]; then
    erreur "Base introuvable : $DB_FILE"
    erreur "Vérifiez APP_DIR (actuel : $APP_DIR) ou que l'application a démarré au moins une fois."
    exit 1
  fi
  mkdir -p "$BACKUP_DIR"
  local horodatage dest
  horodatage="$(date +%Y%m%d_%H%M%S)"
  dest="$BACKUP_DIR/gb_${horodatage}.db"

  if command -v sqlite3 >/dev/null 2>&1; then
    # Sauvegarde cohérente même si l'application écrit en même temps.
    sqlite3 "$DB_FILE" ".backup '$dest'"
  else
    # Repli : copie du fichier + journaux WAL/SHM éventuels.
    cp "$DB_FILE" "$dest"
    [[ -f "$DB_FILE-wal" ]] && cp "$DB_FILE-wal" "$dest-wal" || true
    [[ -f "$DB_FILE-shm" ]] && cp "$DB_FILE-shm" "$dest-shm" || true
  fi

  gzip -f "$dest"
  couleur "Sauvegarde créée : ${dest}.gz"

  # Purge des sauvegardes trop anciennes.
  find "$BACKUP_DIR" -name 'gb_*.db.gz' -mtime "+$RETENTION" -delete 2>/dev/null || true
  couleur "Conservation : $RETENTION jours. Sauvegardes présentes : $(find "$BACKUP_DIR" -name 'gb_*.db.gz' | wc -l)"
}

installer_cron() {
  mkdir -p "$BACKUP_DIR"
  local ligne marqueur
  marqueur="# LP2M APPS sauvegarde quotidienne"
  ligne="0 2 * * * APP_DIR='$APP_DIR' RETENTION='$RETENTION' bash '$SCRIPT_DIR/secure-and-backup.sh' --now >> '$BACKUP_DIR/backup.log' 2>&1 $marqueur"

  local cron_actuel
  cron_actuel="$(crontab -l 2>/dev/null || true)"
  # Retire l'ancienne ligne (idempotent) puis ajoute la nouvelle.
  cron_actuel="$(printf "%s\n" "$cron_actuel" | grep -v "$marqueur" || true)"
  printf "%s\n%s\n" "$cron_actuel" "$ligne" | sed '/^$/d' | crontab -
  couleur "Sauvegarde automatique installée : chaque nuit à 02h00."
  couleur "Journal : $BACKUP_DIR/backup.log"
}

restaurer() {
  local source="$1"
  if [[ ! -f "$source" ]]; then erreur "Fichier introuvable : $source"; exit 1; fi
  couleur "ATTENTION : la base actuelle va être remplacée."
  read -r -p "Confirmer la restauration ? (oui/non) " rep
  [[ "$rep" == "oui" ]] || { couleur "Annulé."; exit 0; }

  # Sauvegarde de sécurité avant restauration.
  [[ -f "$DB_FILE" ]] && cp "$DB_FILE" "$DB_FILE.avant-restauration"
  if [[ "$source" == *.gz ]]; then
    gunzip -c "$source" > "$DB_FILE"
  else
    cp "$source" "$DB_FILE"
  fi
  rm -f "$DB_FILE-wal" "$DB_FILE-shm"
  couleur "Restauration effectuée. Redémarrez l'application : docker compose restart app"
}

case "${1:-}" in
  --now)            sauvegarde_maintenant ;;
  --install-cron)   installer_cron ;;
  --restore)        restaurer "${2:?Indiquez le fichier à restaurer}" ;;
  "" )              sauvegarde_maintenant; installer_cron ;;
  *)                erreur "Option inconnue : $1"; exit 1 ;;
esac
