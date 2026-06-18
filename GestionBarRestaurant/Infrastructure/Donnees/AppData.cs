using Domaine;
using Domaine.Models;
using Infrastructure.Securite;

namespace Infrastructure.Donnees;

public static class AppData
{
    private static readonly object Verrou = new();

    public static List<Tenant> Tenants { get; } = new();
    public static List<Produit> Produits { get; } = new();
    public static List<Client> Clients { get; } = new();
    public static List<Vente> Ventes { get; } = new();
    public static List<CaisseSession> Caisses { get; } = new();
    public static List<MouvementStock> MouvementsStock { get; } = new();
    public static List<Depense> Depenses { get; } = new();
    public static List<Utilisateur> Utilisateurs { get; } = new();

    static AppData()
    {
        Initialiser();
    }

    public static void Initialiser()
    {
        lock (Verrou)
        {
            if (Tenants.Any())
            {
                return;
            }

            Tenants.AddRange(new[]
            {
                new Tenant { Id = 1, Nom = "LP2M Bar", Code = "LP2M", Telephone = "+225 0100000000", Adresse = "Abidjan", CouleurPrincipale = "#165DFF" },
                new Tenant { Id = 2, Nom = "Maquis Démo", Code = "MAQUIS", Telephone = "+225 0700000000", Adresse = "Cocody", CouleurPrincipale = "#D97706" }
            });

            Utilisateurs.AddRange(new[]
            {
                new Utilisateur { Id = 1, TenantId = 1, Nom = "Super Administrateur", Login = "superadmin", MotDePasse = PasswordHelper.Hasher("superadmin"), Role = RoleUtilisateur.Administrateur, IsSuperAdmin = true, Actif = true },
                new Utilisateur { Id = 2, TenantId = 1, Nom = "Administrateur LP2M", Login = "admin", MotDePasse = PasswordHelper.Hasher("admin"), Role = RoleUtilisateur.Administrateur, Actif = true },
                new Utilisateur { Id = 3, TenantId = 1, Nom = "Caissier Principal", Login = "caissier", MotDePasse = PasswordHelper.Hasher("caissier"), Role = RoleUtilisateur.Caissier, Actif = true },
                new Utilisateur { Id = 4, TenantId = 1, Nom = "Manager LP2M", Login = "manager", MotDePasse = PasswordHelper.Hasher("manager"), Role = RoleUtilisateur.Manager, Actif = true },
                new Utilisateur { Id = 5, TenantId = 2, Nom = "Admin Maquis", Login = "admin2", MotDePasse = PasswordHelper.Hasher("admin2"), Role = RoleUtilisateur.Administrateur, Actif = true }
            });

            Clients.AddRange(new[]
            {
                new Client { Id = 1, TenantId = 1, Nom = "Client comptoir", Telephone = "", TypeClient = TypeClient.Particulier },
                new Client { Id = 2, TenantId = 1, Nom = "Entreprise Alpha", Telephone = "+225 0102030405", Email = "contact@alpha.ci", TypeClient = TypeClient.Entreprise },
                new Client { Id = 3, TenantId = 1, Nom = "Client VIP Awa", Telephone = "+225 0707070707", TypeClient = TypeClient.VIP },
                new Client { Id = 4, TenantId = 2, Nom = "Client comptoir", Telephone = "", TypeClient = TypeClient.Particulier }
            });

            Produits.AddRange(new[]
            {
                new Produit { Id = 1, TenantId = 1, Nom = "Eau minérale", Categorie = "Boissons", PrixAchat = 250, PrixVente = 500, StockActuel = 60, StockMinimum = 10, CodeBarre = "LP2M-0001" },
                new Produit { Id = 2, TenantId = 1, Nom = "Soda", Categorie = "Boissons", PrixAchat = 400, PrixVente = 800, StockActuel = 45, StockMinimum = 10, CodeBarre = "LP2M-0002" },
                new Produit { Id = 3, TenantId = 1, Nom = "Jus local", Categorie = "Boissons", PrixAchat = 500, PrixVente = 1000, StockActuel = 25, StockMinimum = 8, CodeBarre = "LP2M-0003" },
                new Produit { Id = 4, TenantId = 1, Nom = "Café", Categorie = "Boissons chaudes", PrixAchat = 200, PrixVente = 700, StockActuel = 30, StockMinimum = 5, CodeBarre = "LP2M-0004" },
                new Produit { Id = 5, TenantId = 1, Nom = "Plat du jour", Categorie = "Restauration", PrixAchat = 1500, PrixVente = 3000, StockActuel = 20, StockMinimum = 5, CodeBarre = "LP2M-0005" },
                new Produit { Id = 6, TenantId = 1, Nom = "Brochette", Categorie = "Restauration", PrixAchat = 800, PrixVente = 1500, StockActuel = 18, StockMinimum = 6, CodeBarre = "LP2M-0006" },
                new Produit { Id = 7, TenantId = 2, Nom = "Poulet braisé", Categorie = "Restauration", PrixAchat = 2500, PrixVente = 5000, StockActuel = 12, StockMinimum = 4, CodeBarre = "MAQ-0001" },
                new Produit { Id = 8, TenantId = 2, Nom = "Jus de bissap", Categorie = "Boissons", PrixAchat = 350, PrixVente = 1000, StockActuel = 20, StockMinimum = 5, CodeBarre = "MAQ-0002" }
            });

            Caisses.Add(new CaisseSession
            {
                Id = 1,
                TenantId = 1,
                Caissier = "Administrateur LP2M",
                DateOuverture = DateTime.Now.Date.AddHours(8),
                MontantOuverture = 50000,
                Statut = StatutCaisse.Ouverte
            });

            Depenses.Add(new Depense
            {
                Id = 1,
                TenantId = 1,
                Libelle = "Achat glaçons",
                Categorie = "Approvisionnement",
                Montant = 5000,
                DateDepense = DateTime.Now.Date.AddHours(10),
                Beneficiaire = "Fournisseur local",
                SaisiPar = "Administrateur LP2M"
            });

            Caisses[0].Decaissements = 5000;
        }
    }

    public static int NextId<T>(IEnumerable<T> liste, Func<T, int> idSelector)
    {
        return liste.Any() ? liste.Max(idSelector) + 1 : 1;
    }

    public static CaisseSession? CaisseOuverte(int tenantId)
    {
        return Caisses.OrderByDescending(c => c.DateOuverture)
            .FirstOrDefault(c => c.TenantId == tenantId && c.Statut == StatutCaisse.Ouverte);
    }

    public static Tenant? GetTenant(int tenantId)
    {
        return Tenants.FirstOrDefault(t => t.Id == tenantId);
    }
}
