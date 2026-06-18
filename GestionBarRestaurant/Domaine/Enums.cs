namespace Domaine;

public enum RoleUtilisateur
{
    Administrateur = 1,
    Caissier = 2,
    Manager = 3
}

public enum TypeMouvementStock
{
    Entree = 1,
    Sortie = 2,
    Ajustement = 3
}

public enum StatutCaisse
{
    Ouverte = 1,
    Fermee = 2
}

public enum TypeClient
{
    Particulier = 1,
    Entreprise = 2,
    VIP = 3
}

public enum StatutVente
{
    Validee = 1,
    Annulee = 2
}

public enum TypeMouvementCaisse
{
    Apport = 1,
    Retrait = 2,
    ReglementCredit = 3
}
