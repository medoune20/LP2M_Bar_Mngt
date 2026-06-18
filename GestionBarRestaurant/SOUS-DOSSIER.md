# Héberger plusieurs applications sur lp2medoune.com

L'application Gestion Bar est désormais servie sous **lp2medoune.com/gestionbar**.

## Comment ça marche
- `docker-compose.yml` : la variable `PATH_BASE=/gestionbar` indique à l'application
  son sous-dossier. ASP.NET Core préfixe alors automatiquement tous les liens, le CSS,
  le JavaScript et les images.
- `Caddyfile` : le bloc `handle /gestionbar*` envoie ce chemin vers l'application.
  La racine `lp2medoune.com/` redirige vers `/gestionbar/`.

## Ajouter une 2e application plus tard
1. Lance ta nouvelle application dans son propre conteneur (sur le même réseau Docker),
   par exemple un service `autreapp` exposant le port 8080, configuré pour vivre sous
   son propre sous-dossier (équivalent d'un PATH_BASE=/autreapp).
2. Dans le `Caddyfile`, décommente et adapte le bloc :
   ```
   handle /autreapp* {
       reverse_proxy autreapp:8080
   }
   ```
3. `docker compose restart caddy`

Chaque application garde ainsi sa propre base et son propre conteneur, sous le même
domaine et le même certificat HTTPS.

## Changer le sous-dossier
Modifie `PATH_BASE` dans `docker-compose.yml` ET le `handle /...` dans le `Caddyfile`
(les deux doivent correspondre), puis `docker compose up -d`.
