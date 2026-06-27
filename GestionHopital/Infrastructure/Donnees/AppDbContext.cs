using GestionHopital.Domaine.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionHopital.Infrastructure.Donnees;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Etablissement> Etablissements => Set<Etablissement>();
    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    public DbSet<Departement> Departements => Set<Departement>();
    public DbSet<Medecin> Medecins => Set<Medecin>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<RendezVous> RendezVous => Set<RendezVous>();
    public DbSet<Consultation> Consultations => Set<Consultation>();
    public DbSet<Ordonnance> Ordonnances => Set<Ordonnance>();
    public DbSet<LigneOrdonnance> LignesOrdonnance => Set<LigneOrdonnance>();
    public DbSet<Examen> Examens => Set<Examen>();
    public DbSet<ResultatExamen> ResultatsExamen => Set<ResultatExamen>();
    public DbSet<LitHospitalisation> Lits => Set<LitHospitalisation>();
    public DbSet<Hospitalisation> Hospitalisations => Set<Hospitalisation>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Utilisateur>().HasIndex(u => u.Login).IsUnique();
        mb.Entity<Patient>().HasIndex(p => p.NuméroDossier).IsUnique();
        mb.Entity<RendezVous>().HasIndex(r => r.NuméroRDV).IsUnique();
        mb.Entity<Examen>().HasIndex(e => e.NuméroExamen).IsUnique();
        mb.Entity<Hospitalisation>().HasIndex(h => h.NuméroHospitalisation).IsUnique();

        mb.Entity<RendezVous>()
            .HasOne(r => r.Consultation)
            .WithOne(c => c.RendezVous)
            .HasForeignKey<Consultation>(c => c.RendezVousId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<Consultation>()
            .HasOne(c => c.Patient)
            .WithMany(p => p.Consultations)
            .HasForeignKey(c => c.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Consultation>()
            .HasOne(c => c.Medecin)
            .WithMany(m => m.Consultations)
            .HasForeignKey(c => c.MedecinId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<RendezVous>()
            .HasOne(r => r.Patient)
            .WithMany(p => p.RendezVous)
            .HasForeignKey(r => r.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<RendezVous>()
            .HasOne(r => r.Medecin)
            .WithMany(m => m.RendezVous)
            .HasForeignKey(r => r.MedecinId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Ordonnance>()
            .HasMany(o => o.Lignes)
            .WithOne(l => l.Ordonnance)
            .HasForeignKey(l => l.OrdonnanceId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Examen>()
            .HasOne(e => e.Résultat)
            .WithOne(r => r.Examen)
            .HasForeignKey<ResultatExamen>(r => r.ExamenId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Examen>()
            .HasOne(e => e.Patient)
            .WithMany(p => p.Examens)
            .HasForeignKey(e => e.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Hospitalisation>()
            .HasOne(h => h.Patient)
            .WithMany(p => p.Hospitalisations)
            .HasForeignKey(h => h.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Hospitalisation>()
            .HasOne(h => h.MedecinRéférent)
            .WithMany(m => m.Hospitalisations)
            .HasForeignKey(h => h.MedecinRéférentId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Medecin>()
            .HasOne(m => m.Departement)
            .WithMany(d => d.Medecins)
            .HasForeignKey(m => m.DépartementId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<LitHospitalisation>()
            .HasOne(l => l.Departement)
            .WithMany(d => d.Lits)
            .HasForeignKey(l => l.DépartementId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Hospitalisation>()
            .HasOne(h => h.Departement)
            .WithMany(d => d.Hospitalisations)
            .HasForeignKey(h => h.DépartementId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Hospitalisation>()
            .HasOne(h => h.Lit)
            .WithMany(l => l.Hospitalisations)
            .HasForeignKey(h => h.LitId)
            .OnDelete(DeleteBehavior.Restrict);

        // Decimal precision
        foreach (var prop in mb.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            prop.SetColumnType("REAL");
        }
    }
}
