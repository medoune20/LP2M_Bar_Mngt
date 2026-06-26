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

public enum StatutCommande
{
    Ouverte = 1,
    Encaissee = 2,
    Annulee = 3
}

/// <summary>Cycle de préparation d'une ligne de commande (écran cuisine / KDS).</summary>
public enum StatutPreparation
{
    EnAttente = 1,
    EnCuisine = 2,
    Prete = 3,
    Servie = 4
}
