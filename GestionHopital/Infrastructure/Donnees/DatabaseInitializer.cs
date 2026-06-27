using GestionHopital.Domaine;
using GestionHopital.Domaine.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionHopital.Infrastructure.Donnees;

public static class DatabaseInitializer
{
    public static async Task InitialiserAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (!db.Etablissements.Any())
        {
            db.Etablissements.Add(new Etablissement
            {
                Nom = "Hôpital LP2M",
                Adresse = "123 Avenue de la Santé",
                Téléphone = "+225 27 22 00 00 00",
                Email = "contact@hopital-lp2m.ci",
                Directeur = "Dr. Konan Kouadio",
                Couleur = "#0ea5e9"
            });
            await db.SaveChangesAsync();
        }

        if (!db.Utilisateurs.Any())
        {
            db.Utilisateurs.AddRange(
                new Utilisateur
                {
                    Login = "admin",
                    MotDePasse = HashMotDePasse("admin"),
                    Nom = "Administrateur",
                    Prénom = "Système",
                    Email = "admin@hopital-lp2m.ci",
                    Role = RoleUtilisateur.SuperAdmin,
                    Actif = true
                },
                new Utilisateur
                {
                    Login = "receptionniste",
                    MotDePasse = HashMotDePasse("1234"),
                    Nom = "Kouassi",
                    Prénom = "Aya",
                    Email = "aya.kouassi@hopital-lp2m.ci",
                    Role = RoleUtilisateur.Réceptionniste,
                    Actif = true
                }
            );
            await db.SaveChangesAsync();
        }

        if (!db.Departements.Any())
        {
            db.Departements.AddRange(
                new Departement { Nom = "Médecine Générale", Localisation = "Bâtiment A - RDC", CapacitéLits = 20, ChefService = "Dr. Yao Akissi", Téléphone = "Poste 101" },
                new Departement { Nom = "Cardiologie", Localisation = "Bâtiment B - 1er étage", CapacitéLits = 15, ChefService = "Dr. Kouamé Assi", Téléphone = "Poste 201" },
                new Departement { Nom = "Pédiatrie", Localisation = "Bâtiment C - RDC", CapacitéLits = 25, ChefService = "Dr. Bamba Mariam", Téléphone = "Poste 301" },
                new Departement { Nom = "Gynécologie-Obstétrique", Localisation = "Bâtiment D - 1er étage", CapacitéLits = 20, ChefService = "Dr. Koné Fatoumata", Téléphone = "Poste 401" },
                new Departement { Nom = "Chirurgie", Localisation = "Bâtiment B - 2ème étage", CapacitéLits = 18, ChefService = "Dr. Diabaté Seydou", Téléphone = "Poste 501" },
                new Departement { Nom = "Urgences", Localisation = "Bâtiment A - RDC", CapacitéLits = 10, ChefService = "Dr. Traoré Moussa", Téléphone = "Poste 600" },
                new Departement { Nom = "Radiologie & Imagerie", Localisation = "Bâtiment A - Sous-sol", CapacitéLits = 0, ChefService = "Dr. Gnanou Clarisse", Téléphone = "Poste 701" },
                new Departement { Nom = "Laboratoire", Localisation = "Bâtiment A - Sous-sol", CapacitéLits = 0, ChefService = "Dr. Coulibaly Boubacar", Téléphone = "Poste 801" }
            );
            await db.SaveChangesAsync();

            // Médecins
            var depts = await db.Departements.ToListAsync();
            var mgId = depts.First(d => d.Nom == "Médecine Générale").Id;
            var cardioId = depts.First(d => d.Nom == "Cardiologie").Id;
            var pédiaId = depts.First(d => d.Nom == "Pédiatrie").Id;
            var gynécoId = depts.First(d => d.Nom == "Gynécologie-Obstétrique").Id;
            var chiroId = depts.First(d => d.Nom == "Chirurgie").Id;
            var urgId = depts.First(d => d.Nom == "Urgences").Id;

            db.Medecins.AddRange(
                new Medecin { Nom = "Yao", Prénom = "Akissi", Spécialité = "Médecine Générale", DépartementId = mgId, Téléphone = "+225 07 00 00 01", NuméroOrdre = "CM-2018-0001" },
                new Medecin { Nom = "Kouamé", Prénom = "Assi", Spécialité = "Cardiologie", DépartementId = cardioId, Téléphone = "+225 07 00 00 02", NuméroOrdre = "CM-2015-0002" },
                new Medecin { Nom = "Bamba", Prénom = "Mariam", Spécialité = "Pédiatrie", DépartementId = pédiaId, Téléphone = "+225 07 00 00 03", NuméroOrdre = "CM-2019-0003" },
                new Medecin { Nom = "Koné", Prénom = "Fatoumata", Spécialité = "Gynécologie-Obstétrique", DépartementId = gynécoId, Téléphone = "+225 07 00 00 04", NuméroOrdre = "CM-2017-0004" },
                new Medecin { Nom = "Diabaté", Prénom = "Seydou", Spécialité = "Chirurgie Générale", DépartementId = chiroId, Téléphone = "+225 07 00 00 05", NuméroOrdre = "CM-2014-0005" },
                new Medecin { Nom = "Traoré", Prénom = "Moussa", Spécialité = "Médecine d'Urgence", DépartementId = urgId, Téléphone = "+225 07 00 00 06", NuméroOrdre = "CM-2020-0006" }
            );
            await db.SaveChangesAsync();

            // Lits
            var médecins = await db.Medecins.ToListAsync();
            var lits = new List<LitHospitalisation>();
            foreach (var dept in depts.Where(d => d.CapacitéLits > 0))
            {
                for (int i = 1; i <= Math.Min(dept.CapacitéLits, 6); i++)
                    lits.Add(new LitHospitalisation
                    {
                        Numéro = $"{dept.Nom[0]}{dept.Id:D2}-{i:D2}",
                        DépartementId = dept.Id,
                        Statut = StatutLit.Libre
                    });
            }
            db.Lits.AddRange(lits);
            await db.SaveChangesAsync();

            // Patients de démonstration
            var patients = new List<Patient>
            {
                new() { NuméroDossier = "P-2024-0001", Nom = "Kouassi", Prénom = "Jean", DateNaissance = new DateTime(1980, 5, 12), Sexe = SexePatient.Masculin, GroupeSanguin = GroupeSanguin.O_pos, Téléphone = "+225 07 10 10 10", Adresse = "Cocody, Abidjan", Allergies = "Pénicilline", Statut = StatutPatient.Actif },
                new() { NuméroDossier = "P-2024-0002", Nom = "Diallo", Prénom = "Aminata", DateNaissance = new DateTime(1992, 8, 23), Sexe = SexePatient.Féminin, GroupeSanguin = GroupeSanguin.A_pos, Téléphone = "+225 05 20 20 20", Adresse = "Yopougon, Abidjan", Statut = StatutPatient.Actif },
                new() { NuméroDossier = "P-2024-0003", Nom = "Touré", Prénom = "Moussa", DateNaissance = new DateTime(1965, 3, 7), Sexe = SexePatient.Masculin, GroupeSanguin = GroupeSanguin.B_pos, Téléphone = "+225 01 30 30 30", Adresse = "Plateau, Abidjan", Antécédents = "Hypertension artérielle, Diabète type 2", Statut = StatutPatient.Actif },
                new() { NuméroDossier = "P-2024-0004", Nom = "Coulibaly", Prénom = "Fatoumata", DateNaissance = new DateTime(2001, 11, 18), Sexe = SexePatient.Féminin, GroupeSanguin = GroupeSanguin.AB_pos, Téléphone = "+225 07 40 40 40", Adresse = "Marcory, Abidjan", Statut = StatutPatient.Actif },
                new() { NuméroDossier = "P-2024-0005", Nom = "Bah", Prénom = "Ibrahima", DateNaissance = new DateTime(1955, 7, 30), Sexe = SexePatient.Masculin, GroupeSanguin = GroupeSanguin.O_neg, Téléphone = "+225 05 50 50 50", Adresse = "Treichville, Abidjan", Antécédents = "Insuffisance cardiaque", Statut = StatutPatient.Hospitalisé }
            };
            db.Patients.AddRange(patients);
            await db.SaveChangesAsync();
        }
    }

    private static string HashMotDePasse(string mdp)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(mdp));
        return Convert.ToHexString(bytes).ToLower();
    }

    public static string Hash(string mdp) => HashMotDePasse(mdp);
}
