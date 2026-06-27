using GestionHopital.Domaine;
using GestionHopital.Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionHopital.Presentation.Controllers;

public class AccueilController : BaseController
{
    public AccueilController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index()
    {
        var aujourd_hui = DateTime.Today;
        var demain = aujourd_hui.AddDays(1);

        // KPIs
        ViewBag.TotalPatients = await _db.Patients.CountAsync(p => p.Statut != StatutPatient.Décédé);
        ViewBag.PatientsHospitalisés = await _db.Patients.CountAsync(p => p.Statut == StatutPatient.Hospitalisé);
        ViewBag.RDVAujourdhui = await _db.RendezVous.CountAsync(r => r.DateHeure >= aujourd_hui && r.DateHeure < demain && r.Statut != StatutRendezVous.Annulé);
        ViewBag.ConsultationsAujourdhui = await _db.Consultations.CountAsync(c => c.DateHeure >= aujourd_hui && c.DateHeure < demain);
        ViewBag.LitsLibres = await _db.Lits.CountAsync(l => l.Statut == StatutLit.Libre);
        ViewBag.TotalMédecins = await _db.Medecins.CountAsync(m => m.Actif);
        ViewBag.ExamensPending = await _db.Examens.CountAsync(e => e.Statut == StatutExamen.Prescrit || e.Statut == StatutExamen.EnAttente);

        // RDV du jour
        ViewBag.RDVJour = await _db.RendezVous
            .Include(r => r.Patient)
            .Include(r => r.Medecin)
            .Where(r => r.DateHeure >= aujourd_hui && r.DateHeure < demain && r.Statut != StatutRendezVous.Annulé)
            .OrderBy(r => r.DateHeure)
            .Take(10)
            .ToListAsync();

        // Hospitalisations en cours
        ViewBag.Hospitalisations = await _db.Hospitalisations
            .Include(h => h.Patient)
            .Include(h => h.Departement)
            .Include(h => h.Lit)
            .Where(h => h.Statut == StatutHospitalisation.EnCours)
            .OrderBy(h => h.DateAdmission)
            .Take(8)
            .ToListAsync();

        // Chart: RDV 7 derniers jours
        var labels = new List<string>();
        var dataRdv = new List<int>();
        var dataConsult = new List<int>();
        for (int i = 6; i >= 0; i--)
        {
            var jour = aujourd_hui.AddDays(-i);
            var lendemain = jour.AddDays(1);
            labels.Add(jour.ToString("ddd dd/MM"));
            dataRdv.Add(await _db.RendezVous.CountAsync(r => r.DateHeure >= jour && r.DateHeure < lendemain));
            dataConsult.Add(await _db.Consultations.CountAsync(c => c.DateHeure >= jour && c.DateHeure < lendemain));
        }
        ViewBag.GraphLabels = labels;
        ViewBag.GraphRDV = dataRdv;
        ViewBag.GraphConsult = dataConsult;

        // Départements
        ViewBag.Departements = await _db.Departements
            .Include(d => d.Lits)
            .Where(d => d.Actif)
            .OrderBy(d => d.Nom)
            .ToListAsync();

        var etab = await _db.Etablissements.FirstOrDefaultAsync();
        ViewBag.NomHopital = etab?.Nom ?? "Hôpital";
        ViewBag.CouleurHopital = etab?.Couleur ?? "#0ea5e9";

        return View();
    }

    public IActionResult Erreur() => View();
}
