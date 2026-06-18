# v4.6 — Inscription en ligne + validation superadmin + email

## Parcours
1. Sur la page de connexion : lien « Inscrire mon établissement ».
2. Le demandeur saisit son établissement + son compte administrateur. Un tenant et son
   admin sont créés INACTIFS, avec une date d'inscription.
3. Un email de confirmation lui est envoyé (lien de vérification d'adresse).
4. Le super administrateur ouvre « Demandes d'inscription », voit les demandes (et si
   l'email est confirmé), puis Active ou Rejette. À l'activation, le tenant et le compte
   passent actifs et un email d'activation est envoyé.
5. À la connexion, un compte non encore validé reçoit un message clair (email à confirmer
   ou en attente de validation).

## Configuration email (SMTP)
L'envoi d'emails s'active via des variables d'environnement (voir docker-compose.yml).
Sans configuration, l'inscription fonctionne quand même : le superadmin voit les demandes
dans l'application (mais aucun email n'est envoyé).

Exemple avec Gmail (créer un « mot de passe d'application ») :
    SMTP_HOST=smtp.gmail.com
    SMTP_PORT=587
    SMTP_USER=tonadresse@gmail.com
    SMTP_PASS=mot_de_passe_application
    SMTP_FROM=tonadresse@gmail.com
    SMTP_SSL=true

## Connexion Google / Apple — NON incluse (voir explication)
Cette partie nécessite des comptes développeur (Google Cloud, Apple Developer payant),
des identifiants OAuth et un changement du mode d'authentification. Elle sera ajoutée
séparément une fois les identifiants disponibles.
