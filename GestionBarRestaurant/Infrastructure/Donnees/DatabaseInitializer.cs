using System.Data;
using System.Text.RegularExpressions;
using Domaine;
using Domaine.Models;
using Infrastructure.Securite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Donnees;

public static class DatabaseInitializer
{
    private static readonly Regex IdentifiantSql = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private static readonly HashSet<string> TablesAutorisees = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tenants", "Utilisateurs", "Clients", "Produits", "Categories", "Ventes", "LignesVente",
        "Caisses", "MouvementsStock", "Depenses", "ReglesFidelite", "MouvementsFidelite",
        "MouvementsCaisse", "ProfilsAcces", "MessagesChat", "ParametragesComptables", "ClesApi",
        "TablesResto", "Commandes", "LignesCommande"
    };

    public static void Initialiser(AppDbContext db)
    {
        db.Database.EnsureCreated();
        MettreAJourSchemaAnalytics(db);

        if (!db.Tenants.Any())
        {
            db.Tenants.Add(new Tenant
            {
                Nom = "Bar Restaurant Abidjan",
                Code = "BRABJ",
                Telephone = "+225 07 00 00 00 00",
                Adresse = "Abidjan",
                Ville = "Abidjan",
                CouleurPrincipale = "#0078D4",
                Slogan = "Caisse rapide, stock propre, service fluide",
                PiedFacture = "Merci pour votre visite.",
                Actif = true
            });
            db.SaveChanges();
        }

        foreach (var tenant in db.Tenants.OrderBy(t => t.Id).ToList())
        {
            GarantirTenantDefaults(db, tenant.Id, ajouterCatalogue: true);
        }

        var tenantPrincipal = db.Tenants.OrderBy(t => t.Id).First();
        var profilAdminId = IdProfil(db, tenantPrincipal.Id, RoleUtilisateur.Administrateur);

        if (!db.Utilisateurs.Any())
        {
            db.Utilisateurs.Add(new Utilisateur
            {
                TenantId = tenantPrincipal.Id,
                Nom = "Super Administrateur",
                Login = "superadmin",
                MotDePasse = PasswordHelper.Hasher("superadmin"),
                Role = RoleUtilisateur.Administrateur,
                ProfilAccesId = profilAdminId,
                IsSuperAdmin = true,
                Email = "superadmin@local",
                EmailConfirme = true,
                FournisseurConnexion = "Local",
                DateDerniereConnexion = DateTime.Now,
                Actif = true
            });
            db.SaveChanges();
        }
        else
        {
            foreach (var admin in db.Utilisateurs.Where(u => u.IsSuperAdmin && u.ProfilAccesId == 0).ToList())
            {
                admin.Role = RoleUtilisateur.Administrateur;
                admin.ProfilAccesId = IdProfil(db, admin.TenantId, RoleUtilisateur.Administrateur);
                admin.EmailConfirme = true;
            }
            db.SaveChanges();
        }
    }

    public static int GarantirTenantDefaults(AppDbContext db, int tenantId, bool ajouterCatalogue = true)
    {
        CreerProfilsParDefaut(db, tenantId);
        CreerClientComptoir(db, tenantId);
        CreerCategoriesParDefaut(db, tenantId);
        CreerRegleFidelite(db, tenantId);

        if (ajouterCatalogue && !db.Produits.Any(p => p.TenantId == tenantId))
        {
            db.Produits.AddRange(CatalogueBarAbidjan(tenantId));
            db.SaveChanges();
        }

        return IdProfil(db, tenantId, RoleUtilisateur.Administrateur);
    }

    public static int IdProfil(AppDbContext db, int tenantId, RoleUtilisateur role)
    {
        var nom = role.ToString();
        var profil = db.ProfilsAcces.FirstOrDefault(p => p.TenantId == tenantId && p.Nom == nom);
        if (profil != null) return profil.Id;

        profil = new ProfilAcces
        {
            TenantId = tenantId,
            Nom = nom,
            Permissions = ModulesApp.PermissionsPourRole(role),
            Actif = true
        };
        db.ProfilsAcces.Add(profil);
        db.SaveChanges();
        return profil.Id;
    }

    public static RoleUtilisateur RoleDepuisProfil(string? nomProfil)
    {
        if (string.Equals(nomProfil, "Administrateur", StringComparison.OrdinalIgnoreCase)) return RoleUtilisateur.Administrateur;
        if (string.Equals(nomProfil, "Manager", StringComparison.OrdinalIgnoreCase)) return RoleUtilisateur.Manager;
        return RoleUtilisateur.Caissier;
    }

    private static void CreerProfilsParDefaut(AppDbContext db, int tenantId)
    {
        foreach (RoleUtilisateur role in Enum.GetValues<RoleUtilisateur>())
        {
            var nom = role.ToString();
            var profil = db.ProfilsAcces.FirstOrDefault(p => p.TenantId == tenantId && p.Nom == nom);
            if (profil == null)
            {
                db.ProfilsAcces.Add(new ProfilAcces
                {
                    TenantId = tenantId,
                    Nom = nom,
                    Permissions = ModulesApp.PermissionsPourRole(role),
                    Actif = true
                });
            }
            else if (string.IsNullOrWhiteSpace(profil.Permissions))
            {
                profil.Permissions = ModulesApp.PermissionsPourRole(role);
                profil.Actif = true;
            }
        }
        db.SaveChanges();
    }

    private static void CreerClientComptoir(AppDbContext db, int tenantId)
    {
        if (db.Clients.Any(c => c.TenantId == tenantId && c.Nom == "Client comptoir")) return;
        db.Clients.Add(new Client { TenantId = tenantId, Nom = "Client comptoir", TypeClient = TypeClient.Particulier, Actif = true });
        db.SaveChanges();
    }

    private static void CreerCategoriesParDefaut(AppDbContext db, int tenantId)
    {
        var defaults = new[]
        {
            ("Boissons non alcoolisées", 1, "#0078D4"),
            ("Bières et maltées", 2, "#107C10"),
            ("Spiritueux", 3, "#5C2D91"),
            ("Vins et cocktails", 4, "#C239B3"),
            ("Snacks et accompagnements", 5, "#D83B01")
        };

        foreach (var (nom, ordre, couleur) in defaults)
        {
            if (!db.Categories.Any(c => c.TenantId == tenantId && c.Nom == nom))
            {
                db.Categories.Add(new Categorie { TenantId = tenantId, Nom = nom, Ordre = ordre, Couleur = couleur, Actif = true });
            }
        }
        db.SaveChanges();
    }

    private static void CreerRegleFidelite(AppDbContext db, int tenantId)
    {
        if (db.ReglesFidelite.Any(r => r.TenantId == tenantId)) return;
        var code = db.Tenants.Where(t => t.Id == tenantId).Select(t => t.Code).FirstOrDefault() ?? "BAR";
        db.ReglesFidelite.Add(new RegleFidelite
        {
            TenantId = tenantId,
            NomProgramme = $"{code} Fidélité",
            MontantPourUnPoint = 1000,
            ValeurPoint = 10,
            SeuilUtilisationPoints = 100,
            Actif = true
        });
        db.SaveChanges();
    }

    private static IEnumerable<Produit> CatalogueBarAbidjan(int tenantId)
    {
        Produit P(string code, string nom, string cat, decimal achat, decimal vente, int stock, string img) => new()
        {
            TenantId = tenantId,
            CodeBarre = code,
            Nom = nom,
            Categorie = cat,
            PrixAchat = achat,
            PrixVente = vente,
            StockActuel = stock,
            StockMinimum = 5,
            ImagePath = img,
            Actif = true
        };

        const string soft = "Boissons non alcoolisées";
        const string biere = "Bières et maltées";
        const string spirit = "Spiritueux";
        const string vin = "Vins et cocktails";
        const string snack = "Snacks et accompagnements";

        return new[]
        {
            P("BAR001", "Eau minérale Céleste 33 cl", soft, 150, 500, 60, "/img/pos-products/boisson.png"),
            P("BAR002", "Eau minérale Awa 50 cl", soft, 200, 500, 60, "/img/pos-products/boisson.png"),
            P("BAR003", "Eau minérale Awa 1,5 L", soft, 450, 1000, 36, "/img/pos-products/boisson.png"),
            P("BAR004", "Bonbonne eau 18 L", soft, 2400, 0, 10, "/img/pos-products/boisson.png"),
            P("BAR005", "Youki 30 cl verre", soft, 250, 500, 48, "https://solibrachezvous.com/wp-content/uploads/2021/01/youki-1.png.webp"),
            P("BAR006", "Youki 50 cl verre", soft, 450, 1000, 48, "https://solibrachezvous.com/wp-content/uploads/2021/01/youki-1.png.webp"),
            P("BAR007", "Youzou canette 33 cl", soft, 250, 700, 48, "https://solibrachezvous.com/wp-content/uploads/2026/05/logo-youzou-1.png"),
            P("BAR008", "World Cola canette 33 cl", soft, 250, 700, 48, "https://solibrachezvous.com/wp-content/uploads/2026/05/logo-worlcola-1.png"),
            P("BAR009", "Sucrerie au choix 33 cl", soft, 400, 1000, 48, "/img/pos-products/jus-orange.png"),
            P("BAR010", "Malta Guinness 30 cl", soft, 500, 1000, 36, "https://solibrachezvous.com/wp-content/uploads/2024/09/logo-malta-ecommerce.png.webp"),
            P("BAR011", "XXL Energy 33 cl", soft, 355, 1000, 36, "https://solibrachezvous.com/wp-content/uploads/2021/01/xxl-1.png.webp"),
            P("BAR012", "Doppel Energy Malt 33 cl", soft, 440, 1000, 36, "https://solibrachezvous.com/wp-content/uploads/2023/11/dem.png.webp"),

            P("BAR013", "Bock 65 cl", biere, 700, 1000, 48, "https://solibrachezvous.com/wp-content/uploads/2020/04/bock50cl-24-1-430x430.jpg"),
            P("BAR014", "Bock 100 cl", biere, 890, 1500, 36, "https://solibrachezvous.com/wp-content/uploads/2020/04/bock50cl-24-1-430x430.jpg"),
            P("BAR015", "Beaufort 50 cl", biere, 740, 1500, 48, "https://solibrachezvous.com/wp-content/uploads/2020/04/c33beaufort50cl_vc-700x700.jpg.webp"),
            P("BAR016", "Beaufort 33 cl", biere, 565, 1000, 48, "https://solibrachezvous.com/wp-content/uploads/2020/04/c33beaufort50cl_vc-700x700.jpg.webp"),
            P("BAR017", "Castel Beer 50 cl", biere, 670, 1200, 48, "https://solibrachezvous.com/wp-content/uploads/2026/05/logo-castel.png"),
            P("BAR018", "Castel Beer 33 cl", biere, 515, 1000, 48, "https://solibrachezvous.com/wp-content/uploads/2026/05/logo-castel.png"),
            P("BAR019", "Doppel 50 cl", biere, 670, 1200, 48, "https://solibrachezvous.com/wp-content/uploads/2024/07/doppelecommerce-1.png.webp"),
            P("BAR020", "Doppel 33 cl", biere, 415, 1000, 48, "https://solibrachezvous.com/wp-content/uploads/2024/07/doppelecommerce-1.png.webp"),
            P("BAR021", "Chill citron 50 cl", biere, 625, 1200, 36, "https://solibrachezvous.com/wp-content/uploads/2024/07/chillecommerce.png.webp"),
            P("BAR022", "Chill canette 33 cl", biere, 375, 1000, 36, "https://solibrachezvous.com/wp-content/uploads/2024/07/chillecommerce.png.webp"),
            P("BAR023", "Racines 33 cl", biere, 445, 1000, 36, "https://solibrachezvous.com/wp-content/uploads/2021/05/racine.png.webp"),
            P("BAR024", "Racines 50 cl", biere, 610, 1200, 36, "https://solibrachezvous.com/wp-content/uploads/2021/05/racine.png.webp"),
            P("BAR025", "Racines Fort canette 33 cl", biere, 415, 1200, 36, "https://solibrachezvous.com/wp-content/uploads/2023/07/racineSFORT.png.webp"),
            P("BAR026", "Guinness 33 cl", biere, 765, 1500, 36, "https://solibrachezvous.com/wp-content/uploads/2026/05/logo-guiness-1.png"),
            P("BAR027", "Guinness 65 cl", biere, 1225, 2000, 24, "https://solibrachezvous.com/wp-content/uploads/2026/05/logo-guiness-1.png"),
            P("BAR028", "Budweiser 33 cl", biere, 625, 1500, 24, "https://solibrachezvous.com/wp-content/uploads/2020/04/budweiser.png.webp"),
            P("BAR029", "Valpierre 50 cl", biere, 1085, 2000, 24, "https://solibrachezvous.com/wp-content/uploads/2026/05/logo-valpierre-1.png"),
            P("BAR030", "Valpierre 100 cl", biere, 2000, 3500, 24, "https://solibrachezvous.com/wp-content/uploads/2026/05/logo-valpierre-1.png"),

            P("BAR031", "Whisky entrée de gamme verre", spirit, 1000, 1500, 30, "/img/pos-products/boisson.png"),
            P("BAR032", "Whisky moyen de gamme verre", spirit, 2000, 2500, 30, "/img/pos-products/boisson.png"),
            P("BAR033", "Johnnie Walker Red Label 70 cl", spirit, 15000, 30000, 8, "/img/pos-products/boisson.png"),
            P("BAR034", "Johnnie Walker Black Label 70 cl", spirit, 35000, 60000, 6, "/img/pos-products/boisson.png"),
            P("BAR035", "Johnnie Walker Gold Label 75 cl", spirit, 55000, 100000, 4, "/img/pos-products/boisson.png"),
            P("BAR036", "Gin verre", spirit, 1200, 2000, 24, "/img/pos-products/boisson.png"),
            P("BAR037", "Vodka verre", spirit, 1200, 2000, 24, "/img/pos-products/boisson.png"),
            P("BAR038", "Rhum verre", spirit, 1200, 2000, 24, "/img/pos-products/boisson.png"),
            P("BAR039", "Cognac verre", spirit, 2500, 4000, 18, "/img/pos-products/boisson.png"),
            P("BAR040", "Liqueur / crème verre", spirit, 1000, 2000, 18, "/img/pos-products/citronnade.png"),

            P("BAR041", "Vin rouge Valpierre 50 cl", vin, 1085, 2000, 24, "https://solibrachezvous.com/wp-content/uploads/2026/05/logo-valpierre-1.png"),
            P("BAR042", "Vin rouge Valpierre 100 cl", vin, 2000, 3500, 24, "https://solibrachezvous.com/wp-content/uploads/2026/05/logo-valpierre-1.png"),
            P("BAR043", "Vin rouge / blanc simple 75 cl", vin, 6000, 12000, 12, "/img/pos-products/citronnade.png"),
            P("BAR044", "Vin moyen de gamme 75 cl", vin, 12000, 25000, 12, "/img/pos-products/citronnade.png"),
            P("BAR045", "Champagne / mousseux simple 75 cl", vin, 10000, 25000, 8, "/img/pos-products/citronnade.png"),
            P("BAR046", "Cocktail simple verre", vin, 1500, 5000, 40, "/img/pos-products/citronnade.png"),
            P("BAR047", "Cocktail premium verre", vin, 3000, 8000, 30, "/img/pos-products/citronnade.png"),

            P("BAR048", "Arachides grillées portion", snack, 300, 1000, 50, "/img/pos-products/plat-du-jour.png"),
            P("BAR049", "Chips sachet", snack, 700, 1500, 40, "/img/pos-products/plat-du-jour.png"),
            P("BAR050", "Pop-corn portion", snack, 500, 1000, 40, "/img/pos-products/plat-du-jour.png"),
            P("BAR051", "Brochettes unité", snack, 700, 1500, 40, "/img/pos-products/grillade.png"),
            P("BAR052", "Poulet braisé portion", snack, 3500, 8000, 20, "/img/pos-products/grillade.png"),
            P("BAR053", "Poisson braisé portion", snack, 5000, 12000, 20, "/img/pos-products/fish-chips.png"),
            P("BAR054", "Frites portion", snack, 1000, 2500, 30, "/img/pos-products/fish-chips.png"),
            P("BAR055", "Alloco portion", snack, 1000, 2500, 30, "/img/pos-products/plat-du-jour.png"),
            P("BAR056", "Attiéké poisson assiette", snack, 3000, 8000, 20, "/img/pos-products/fish-chips.png")
        };
    }

    private static void MettreAJourSchemaAnalytics(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""ReglesFidelite"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ReglesFidelite"" PRIMARY KEY AUTOINCREMENT,
    ""TenantId"" INTEGER NOT NULL,
    ""NomProgramme"" TEXT NOT NULL DEFAULT 'Programme fidélité',
    ""MontantPourUnPoint"" REAL NOT NULL DEFAULT 1000,
    ""ValeurPoint"" REAL NOT NULL DEFAULT 10,
    ""SeuilUtilisationPoints"" INTEGER NOT NULL DEFAULT 100,
    ""Actif"" INTEGER NOT NULL DEFAULT 1
);");

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""MouvementsFidelite"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_MouvementsFidelite"" PRIMARY KEY AUTOINCREMENT,
    ""TenantId"" INTEGER NOT NULL,
    ""ClientId"" INTEGER NOT NULL,
    ""ClientNom"" TEXT NOT NULL DEFAULT '',
    ""VenteId"" INTEGER NULL,
    ""DateMouvement"" TEXT NOT NULL,
    ""Points"" INTEGER NOT NULL DEFAULT 0,
    ""TypeMouvement"" TEXT NOT NULL DEFAULT '',
    ""Commentaire"" TEXT NOT NULL DEFAULT '',
    ""Utilisateur"" TEXT NOT NULL DEFAULT ''
);");

        AjouterColonneSiAbsente(db, "Clients", "PointsFidelite", "INTEGER NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Clients", "DerniereVisite", "TEXT NULL");
        AjouterColonneSiAbsente(db, "Clients", "TotalAchats", "REAL NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Clients", "PlafondCredit", "REAL NOT NULL DEFAULT 0");

        AjouterColonneSiAbsente(db, "Ventes", "PointsFideliteUtilises", "INTEGER NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Ventes", "PointsFideliteGagnes", "INTEGER NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Ventes", "RemiseFidelite", "REAL NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Ventes", "CaisseSessionId", "INTEGER NULL");
        AjouterColonneSiAbsente(db, "Ventes", "Statut", "INTEGER NOT NULL DEFAULT 1");
        AjouterColonneSiAbsente(db, "Ventes", "DateAnnulation", "TEXT NULL");
        AjouterColonneSiAbsente(db, "Ventes", "MotifAnnulation", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Ventes", "AnnuleePar", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Ventes", "VendeurId", "INTEGER NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Ventes", "ReferencePaiement", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Ventes", "ClientUuid", "TEXT NOT NULL DEFAULT ''");
        db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_Ventes_TenantId_ClientUuid"" ON ""Ventes"" (""TenantId"", ""ClientUuid"");");
        db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_Ventes_TenantId_CaisseSessionId"" ON ""Ventes"" (""TenantId"", ""CaisseSessionId"");");

        AjouterColonneSiAbsente(db, "LignesVente", "PrixAchatUnitaire", "REAL NOT NULL DEFAULT 0");

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""MouvementsCaisse"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_MouvementsCaisse"" PRIMARY KEY AUTOINCREMENT,
    ""TenantId"" INTEGER NOT NULL,
    ""CaisseSessionId"" INTEGER NOT NULL,
    ""DateMouvement"" TEXT NOT NULL,
    ""Type"" INTEGER NOT NULL,
    ""Montant"" REAL NOT NULL DEFAULT 0,
    ""Motif"" TEXT NOT NULL DEFAULT '',
    ""Utilisateur"" TEXT NOT NULL DEFAULT '',
    ""ClientId"" INTEGER NULL,
    ""ClientNom"" TEXT NOT NULL DEFAULT ''
);");
        db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_MouvementsCaisse_TenantId_CaisseSessionId"" ON ""MouvementsCaisse"" (""TenantId"", ""CaisseSessionId"");");

        AjouterColonneSiAbsente(db, "Caisses", "EncaissementsAutres", "REAL NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Caisses", "ClotureePar", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Caisses", "CommentaireCloture", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Caisses", "CaissierId", "INTEGER NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Caisses", "Libelle", "TEXT NOT NULL DEFAULT 'Caisse'");

        AjouterColonneSiAbsente(db, "MouvementsStock", "CoutUnitaire", "REAL NULL");
        AjouterColonneSiAbsente(db, "Depenses", "CaisseSessionId", "INTEGER NULL");

        AjouterColonneSiAbsente(db, "Tenants", "Slogan", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Tenants", "Email", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Tenants", "SiteWeb", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Tenants", "Ville", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Tenants", "RegistreCommerce", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Tenants", "NumeroContribuable", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Tenants", "LogoPath", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Tenants", "PiedFacture", "TEXT NOT NULL DEFAULT ''");

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""ProfilsAcces"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ProfilsAcces"" PRIMARY KEY AUTOINCREMENT,
    ""TenantId"" INTEGER NOT NULL,
    ""Nom"" TEXT NOT NULL DEFAULT '',
    ""Permissions"" TEXT NOT NULL DEFAULT '',
    ""Actif"" INTEGER NOT NULL DEFAULT 1
);");
        db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_ProfilsAcces_TenantId"" ON ""ProfilsAcces"" (""TenantId"");");

        AjouterColonneSiAbsente(db, "Utilisateurs", "ProfilAccesId", "INTEGER NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Utilisateurs", "Email", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Utilisateurs", "EmailConfirme", "INTEGER NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Utilisateurs", "TokenConfirmation", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Utilisateurs", "DateInscription", "TEXT NULL");
        AjouterColonneSiAbsente(db, "Utilisateurs", "TentativesEchouees", "INTEGER NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Utilisateurs", "VerrouJusqua", "TEXT NULL");
        AjouterColonneSiAbsente(db, "Utilisateurs", "FournisseurConnexion", "TEXT NOT NULL DEFAULT 'Local'");
        AjouterColonneSiAbsente(db, "Utilisateurs", "GoogleSubject", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Utilisateurs", "DateDerniereConnexion", "TEXT NULL");

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""Categories"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Categories"" PRIMARY KEY AUTOINCREMENT,
    ""TenantId"" INTEGER NOT NULL,
    ""Nom"" TEXT NOT NULL DEFAULT '',
    ""Ordre"" INTEGER NOT NULL DEFAULT 0,
    ""Couleur"" TEXT NOT NULL DEFAULT '#165DFF',
    ""Actif"" INTEGER NOT NULL DEFAULT 1
);");
        db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_Categories_TenantId"" ON ""Categories"" (""TenantId"");");

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""MessagesChat"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_MessagesChat"" PRIMARY KEY AUTOINCREMENT,
    ""TenantId"" INTEGER NOT NULL,
    ""UtilisateurId"" INTEGER NOT NULL DEFAULT 0,
    ""AuteurNom"" TEXT NOT NULL DEFAULT '',
    ""AuteurRole"" INTEGER NOT NULL DEFAULT 0,
    ""Canal"" TEXT NOT NULL DEFAULT 'general',
    ""Texte"" TEXT NOT NULL DEFAULT '',
    ""DateEnvoi"" TEXT NOT NULL
);");
        AjouterColonneSiAbsente(db, "MessagesChat", "Canal", "TEXT NOT NULL DEFAULT 'general'");
        db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_MessagesChat_TenantId_DateEnvoi"" ON ""MessagesChat"" (""TenantId"", ""DateEnvoi"");");

        AjouterColonneSiAbsente(db, "Utilisateurs", "DerniereLectureChatId", "INTEGER NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Utilisateurs", "TokenReset", "TEXT NOT NULL DEFAULT ''");
        AjouterColonneSiAbsente(db, "Utilisateurs", "TokenResetExpiration", "TEXT NULL");

        // --- Comptabilité OHADA ---
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""ParametragesComptables"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ParametragesComptables"" PRIMARY KEY AUTOINCREMENT,
    ""TenantId"" INTEGER NOT NULL,
    ""AssujettiTva"" INTEGER NOT NULL DEFAULT 1,
    ""TauxTva"" REAL NOT NULL DEFAULT 18,
    ""Devise"" TEXT NOT NULL DEFAULT 'XOF',
    ""Exercice"" INTEGER NOT NULL DEFAULT 0,
    ""CompteCaisse"" TEXT NOT NULL DEFAULT '571',
    ""CompteBanque"" TEXT NOT NULL DEFAULT '521',
    ""CompteClients"" TEXT NOT NULL DEFAULT '411',
    ""CompteFournisseurs"" TEXT NOT NULL DEFAULT '401',
    ""CompteVentes"" TEXT NOT NULL DEFAULT '701',
    ""CompteTvaCollectee"" TEXT NOT NULL DEFAULT '4431',
    ""CompteTvaDeductible"" TEXT NOT NULL DEFAULT '4452',
    ""CompteAchats"" TEXT NOT NULL DEFAULT '601',
    ""CompteCharges"" TEXT NOT NULL DEFAULT '627',
    ""CompteApports"" TEXT NOT NULL DEFAULT '4711'
);");
        db.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ParametragesComptables_TenantId"" ON ""ParametragesComptables"" (""TenantId"");");
        AjouterColonneSiAbsente(db, "ParametragesComptables", "CompteMobileMoney", "TEXT NOT NULL DEFAULT '521'");

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""ClesApi"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ClesApi"" PRIMARY KEY AUTOINCREMENT,
    ""TenantId"" INTEGER NOT NULL,
    ""Libelle"" TEXT NOT NULL DEFAULT '',
    ""Prefixe"" TEXT NOT NULL DEFAULT '',
    ""CleHash"" TEXT NOT NULL DEFAULT '',
    ""Scope"" TEXT NOT NULL DEFAULT 'lecture',
    ""Actif"" INTEGER NOT NULL DEFAULT 1,
    ""DateCreation"" TEXT NOT NULL,
    ""DerniereUtilisation"" TEXT NULL
);");
        db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_ClesApi_CleHash"" ON ""ClesApi"" (""CleHash"");");
        db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_ClesApi_TenantId"" ON ""ClesApi"" (""TenantId"");");

        // --- Service restaurant ---
        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""TablesResto"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_TablesResto"" PRIMARY KEY AUTOINCREMENT,
    ""TenantId"" INTEGER NOT NULL,
    ""Nom"" TEXT NOT NULL DEFAULT '',
    ""Zone"" TEXT NOT NULL DEFAULT 'Salle',
    ""Capacite"" INTEGER NOT NULL DEFAULT 4,
    ""Ordre"" INTEGER NOT NULL DEFAULT 0,
    ""Actif"" INTEGER NOT NULL DEFAULT 1
);");
        db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_TablesResto_TenantId"" ON ""TablesResto"" (""TenantId"");");

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""Commandes"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Commandes"" PRIMARY KEY AUTOINCREMENT,
    ""TenantId"" INTEGER NOT NULL,
    ""TableId"" INTEGER NOT NULL,
    ""TableNom"" TEXT NOT NULL DEFAULT '',
    ""Numero"" TEXT NOT NULL DEFAULT '',
    ""Statut"" INTEGER NOT NULL DEFAULT 1,
    ""DateOuverture"" TEXT NOT NULL,
    ""DateCloture"" TEXT NULL,
    ""OuvertePar"" TEXT NOT NULL DEFAULT '',
    ""Couverts"" INTEGER NOT NULL DEFAULT 1,
    ""ClientNom"" TEXT NOT NULL DEFAULT '',
    ""Note"" TEXT NOT NULL DEFAULT '',
    ""VenteId"" INTEGER NULL
);");
        db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_Commandes_TenantId_TableId_Statut"" ON ""Commandes"" (""TenantId"", ""TableId"", ""Statut"");");

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""LignesCommande"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_LignesCommande"" PRIMARY KEY AUTOINCREMENT,
    ""CommandeId"" INTEGER NOT NULL,
    ""ProduitId"" INTEGER NOT NULL,
    ""ProduitNom"" TEXT NOT NULL DEFAULT '',
    ""Quantite"" INTEGER NOT NULL DEFAULT 1,
    ""PrixUnitaire"" REAL NOT NULL DEFAULT 0,
    ""PrixAchatUnitaire"" REAL NOT NULL DEFAULT 0,
    ""Note"" TEXT NOT NULL DEFAULT '',
    ""Preparation"" INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT ""FK_LignesCommande_Commandes_CommandeId"" FOREIGN KEY (""CommandeId"") REFERENCES ""Commandes"" (""Id"") ON DELETE CASCADE
);");
        db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_LignesCommande_CommandeId"" ON ""LignesCommande"" (""CommandeId"");");
    }

    private static void AjouterColonneSiAbsente(AppDbContext db, string table, string colonne, string definition)
    {
        ValiderIdentifiant(table, nameof(table));
        ValiderIdentifiant(colonne, nameof(colonne));
        if (!TablesAutorisees.Contains(table)) throw new InvalidOperationException($"Table non autorisée : {table}");
        if (ColonneExiste(db, table, colonne)) return;

        var sql = $@"ALTER TABLE ""{table}"" ADD COLUMN ""{colonne}"" {definition};";
        var connection = db.Database.GetDbConnection();
        var fermerApres = connection.State != ConnectionState.Open;
        if (fermerApres) connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        finally
        {
            if (fermerApres) connection.Close();
        }
    }

    private static bool ColonneExiste(AppDbContext db, string table, string colonne)
    {
        ValiderIdentifiant(table, nameof(table));
        ValiderIdentifiant(colonne, nameof(colonne));
        if (!TablesAutorisees.Contains(table)) throw new InvalidOperationException($"Table non autorisée : {table}");

        var connection = db.Database.GetDbConnection();
        var fermerApres = connection.State != ConnectionState.Open;
        if (fermerApres) connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $@"PRAGMA table_info(""{table}"");";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var nom = reader["name"]?.ToString();
                if (string.Equals(nom, colonne, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
        finally
        {
            if (fermerApres) connection.Close();
        }
    }

    private static void ValiderIdentifiant(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value) || !IdentifiantSql.IsMatch(value))
            throw new ArgumentException("Identifiant SQL invalide.", paramName);
    }
}
