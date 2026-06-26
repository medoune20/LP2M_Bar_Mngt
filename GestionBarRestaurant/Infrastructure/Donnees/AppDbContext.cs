using Domaine.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Donnees;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Produit> Produits => Set<Produit>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Vente> Ventes => Set<Vente>();
    public DbSet<LigneVente> LignesVente => Set<LigneVente>();
    public DbSet<CaisseSession> Caisses => Set<CaisseSession>();
    public DbSet<MouvementStock> MouvementsStock => Set<MouvementStock>();
    public DbSet<Depense> Depenses => Set<Depense>();
    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    public DbSet<RegleFidelite> ReglesFidelite => Set<RegleFidelite>();
    public DbSet<MouvementFidelite> MouvementsFidelite => Set<MouvementFidelite>();
    public DbSet<MouvementCaisse> MouvementsCaisse => Set<MouvementCaisse>();
    public DbSet<Categorie> Categories => Set<Categorie>();
    public DbSet<ProfilAcces> ProfilsAcces => Set<ProfilAcces>();
    public DbSet<MessageChat> MessagesChat => Set<MessageChat>();
    public DbSet<ParametrageComptable> ParametragesComptables => Set<ParametrageComptable>();
    public DbSet<CleApi> ClesApi => Set<CleApi>();
    public DbSet<TableResto> Tables => Set<TableResto>();
    public DbSet<Commande> Commandes => Set<Commande>();
    public DbSet<LigneCommande> LignesCommande => Set<LigneCommande>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vente>()
            .HasMany(v => v.Lignes)
            .WithOne()
            .HasForeignKey("VenteId")
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Utilisateur>()
            .HasIndex(u => u.Login)
            .IsUnique();

        modelBuilder.Entity<Utilisateur>()
            .HasIndex(u => u.GoogleSubject);

        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.Code)
            .IsUnique();

        modelBuilder.Entity<Produit>()
            .HasIndex(p => new { p.TenantId, p.CodeBarre })
            .IsUnique();

        // SQLite + decimal :
        // On force la conversion en double pour éviter les erreurs de parsing du type
        // "The input string '0.0' was not in a correct format" sur certains postes FR.
        modelBuilder.Entity<Produit>().Property(p => p.PrixAchat).HasConversion<double>();
        modelBuilder.Entity<Produit>().Property(p => p.PrixVente).HasConversion<double>();

        modelBuilder.Entity<Vente>().Property(v => v.TotalBrut).HasConversion<double>();
        modelBuilder.Entity<Vente>().Property(v => v.Remise).HasConversion<double>();
        modelBuilder.Entity<Vente>().Property(v => v.RemiseFidelite).HasConversion<double>();
        modelBuilder.Entity<Vente>().Property(v => v.MontantRecu).HasConversion<double>();

        modelBuilder.Entity<LigneVente>().Property(l => l.PrixUnitaire).HasConversion<double>();
        modelBuilder.Entity<LigneVente>().Property(l => l.PrixAchatUnitaire).HasConversion<double>();

        modelBuilder.Entity<Depense>().Property(d => d.Montant).HasConversion<double>();

        modelBuilder.Entity<Client>().Property(c => c.SoldeCredit).HasConversion<double>();
        modelBuilder.Entity<Client>().Property(c => c.TotalAchats).HasConversion<double>();
        modelBuilder.Entity<Client>().Property(c => c.PlafondCredit).HasConversion<double>();

        modelBuilder.Entity<CaisseSession>().Property(c => c.MontantOuverture).HasConversion<double>();
        modelBuilder.Entity<CaisseSession>().Property(c => c.Encaissements).HasConversion<double>();
        modelBuilder.Entity<CaisseSession>().Property(c => c.Decaissements).HasConversion<double>();
        modelBuilder.Entity<CaisseSession>().Property(c => c.MontantCloture).HasConversion<double?>();
        modelBuilder.Entity<CaisseSession>().Property(c => c.EncaissementsAutres).HasConversion<double>();

        modelBuilder.Entity<MouvementCaisse>().Property(m => m.Montant).HasConversion<double>();
        modelBuilder.Entity<MouvementCaisse>()
            .HasIndex(m => new { m.TenantId, m.CaisseSessionId });

        modelBuilder.Entity<MouvementStock>().Property(m => m.CoutUnitaire).HasConversion<double?>();

        modelBuilder.Entity<Vente>()
            .HasIndex(v => new { v.TenantId, v.CaisseSessionId });

        modelBuilder.Entity<RegleFidelite>().Property(r => r.MontantPourUnPoint).HasConversion<double>();
        modelBuilder.Entity<RegleFidelite>().Property(r => r.ValeurPoint).HasConversion<double>();

        // Messagerie temps réel : index par tenant + date pour charger l'historique
        // récent de chaque établissement sans scanner toute la table.
        modelBuilder.Entity<MessageChat>()
            .HasIndex(m => new { m.TenantId, m.DateEnvoi });

        // Comptabilité OHADA.
        modelBuilder.Entity<ParametrageComptable>().HasIndex(p => p.TenantId).IsUnique();
        modelBuilder.Entity<ParametrageComptable>().Property(p => p.TauxTva).HasConversion<double>();
        modelBuilder.Entity<CleApi>().HasIndex(c => c.CleHash);
        modelBuilder.Entity<CleApi>().HasIndex(c => c.TenantId);

        // Service restaurant (tables + commandes).
        modelBuilder.Entity<Commande>()
            .HasMany(c => c.Lignes)
            .WithOne()
            .HasForeignKey("CommandeId")
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Commande>().HasIndex(c => new { c.TenantId, c.TableId, c.Statut });
        modelBuilder.Entity<TableResto>().HasIndex(t => t.TenantId);
        modelBuilder.Entity<LigneCommande>().Property(l => l.PrixUnitaire).HasConversion<double>();
        modelBuilder.Entity<LigneCommande>().Property(l => l.PrixAchatUnitaire).HasConversion<double>();

        base.OnModelCreating(modelBuilder);
    }
}
