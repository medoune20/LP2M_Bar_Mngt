namespace GestionHopital.Domaine;

public enum StatutRendezVous
{
    Planifié = 0,
    Confirmé = 1,
    EnCours = 2,
    Terminé = 3,
    Annulé = 4,
    NoShow = 5
}

public enum TypeConsultation
{
    Générale = 0,
    Spécialisée = 1,
    Urgence = 2,
    Téléconsultation = 3,
    Contrôle = 4
}

public enum StatutPatient
{
    Actif = 0,
    Hospitalisé = 1,
    Sorti = 2,
    Transféré = 3,
    Décédé = 4
}

public enum StatutHospitalisation
{
    EnCours = 0,
    Sorti = 1,
    Transféré = 2
}

public enum StatutLit
{
    Libre = 0,
    Occupé = 1,
    Maintenance = 2,
    Réservé = 3
}

public enum SexePatient
{
    Masculin = 0,
    Féminin = 1,
    Autre = 2
}

public enum GroupeSanguin
{
    A_pos = 0,
    A_neg = 1,
    B_pos = 2,
    B_neg = 3,
    AB_pos = 4,
    AB_neg = 5,
    O_pos = 6,
    O_neg = 7,
    Inconnu = 8
}

public enum RoleUtilisateur
{
    SuperAdmin = 0,
    Administrateur = 1,
    Médecin = 2,
    Infirmier = 3,
    Réceptionniste = 4,
    Laborantin = 5
}

public enum StatutExamen
{
    Prescrit = 0,
    EnAttente = 1,
    EnCours = 2,
    Résultats = 3,
    Annulé = 4
}

public enum TypeExamen
{
    Biologie = 0,
    Radiologie = 1,
    Échographie = 2,
    Scanner = 3,
    IRM = 4,
    Électrocardiogramme = 5,
    Autre = 6
}

public enum StatutEtape
{
    EnAttente = 0,
    EnCours = 1,
    Terminé = 2
}
