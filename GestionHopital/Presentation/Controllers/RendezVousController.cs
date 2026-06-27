using GestionHopital.Domaine;
using GestionHopital.Domaine.Models;
using GestionHopital.Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionHopital.Presentation.Controllers;

public class RendezVousController : BaseController
{
    public RendezVousController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index(string? date, int? medecinId, string? statut)
    {
        var query = _db.RendezVous
            .Include(r => r.Patient)
            .Include(r => r.Medecin)
            .AsQueryable();

        if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var d))
            query = query.Where(r => r.DateHeure.Date == d.Date);
        else if (string.IsNullOrEmpty(date))
            query = query.Where(r => r.DateHeure.Date == DateTime.Today);

        if (medecinId.HasValue) query = query.Where(r => r.MedecinId == medecinId.Value);
        if (!string.IsNullOrEmpty(statut) && Enum.TryParse<StatutRendezVous>(statut, out var s))
            query = query.Where(r => r.Statut == s);

        ViewBag.DateFiltre = string.IsNullOrEmpty(date) ? DateTime.Today.ToString("yyyy-MM-dd") : date;
        ViewBag.MedecinIdFiltre = medecinId;
        ViewBag.StatutFiltre = statut;
        ViewBag.Medecins = await _db.Medecins.Where(m => m.Actif).OrderBy(m => m.Nom).ToListAsync();

        var rdvs = await query.OrderBy(r => r.DateHeure).ToListAsync();
        return View(rdvs);
    }

    [HttpGet]
    public async Task<IActionResult> Nouveau(int? patientId)
    {
        ViewBag.Medecins = await _db.Medecins.Include(m => m.Departement).Where(m => m.Actif).OrderBy(m => m.Nom).ToListAsync();
        ViewBag.Patient = patientId.HasValue ? await _db.Patients.FindAsync(patientId.Value) : null;
        var rdv = new RendezVous
        {
            DateHeure = DateTime.Now.Date.AddHours(8),
            PatientId = patientId ?? 0
        };
        return View(rdv);
    }

    [HttpPost]
    public async Task<IActionResult> Nouveau(RendezVous model)
    {
        ModelState.Remove("NuméroRDV");
        ModelState.Remove("Patient");
        ModelState.Remove("Medecin");
        ModelState.Remove("Consultation");
        if (!ModelState.IsValid)
        {
            ViewBag.Medecins = await _db.Medecins.Include(m => m.Departement).Where(m => m.Actif).OrderBy(m => m.Nom).ToListAsync();
            ViewBag.Patient = await _db.Patients.FindAsync(model.PatientId);
            return View(model);
        }
        model.NuméroRDV = GénérerNuméro("RDV");
        model.DateCréation = DateTime.Now;
        _db.RendezVous.Add(model);
        await _db.SaveChangesAsync();
        Succès("Rendez-vous enregistré avec succès.");
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> ChangerStatut(int id, StatutRendezVous statut)
    {
        var rdv = await _db.RendezVous.FindAsync(id);
        if (rdv != null)
        {
            rdv.Statut = statut;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Index", new { date = DateTime.Today.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    public async Task<IActionResult> Confirmer(int id)
    {
        var rdv = await _db.RendezVous.FindAsync(id);
        if (rdv != null) { rdv.Statut = StatutRendezVous.Confirmé; await _db.SaveChangesAsync(); }
        Succès("Rendez-vous confirmé.");
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Annuler(int id, string? motif)
    {
        var rdv = await _db.RendezVous.FindAsync(id);
        if (rdv != null)
        {
            rdv.Statut = StatutRendezVous.Annulé;
            if (!string.IsNullOrEmpty(motif)) rdv.Notes = $"Annulé: {motif}";
            await _db.SaveChangesAsync();
        }
        Succès("Rendez-vous annulé.");
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> DémarrerConsultation(int id)
    {
        var rdv = await _db.RendezVous.FindAsync(id);
        if (rdv != null) { rdv.Statut = StatutRendezVous.EnCours; await _db.SaveChangesAsync(); }
        return RedirectToAction("Nouvelle", "Consultation", new { rendezVousId = id });
    }

    public async Task<IActionResult> Calendrier(int? medecinId)
    {
        ViewBag.Medecins = await _db.Medecins.Where(m => m.Actif).OrderBy(m => m.Nom).ToListAsync();
        ViewBag.MedecinIdFiltre = medecinId;

        var début = DateTime.Today;
        var fin = début.AddDays(14);
        var query = _db.RendezVous
            .Include(r => r.Patient)
            .Include(r => r.Medecin)
            .Where(r => r.DateHeure >= début && r.DateHeure < fin && r.Statut != StatutRendezVous.Annulé);

        if (medecinId.HasValue) query = query.Where(r => r.MedecinId == medecinId.Value);

        var rdvs = await query.OrderBy(r => r.DateHeure).ToListAsync();
        var calData = rdvs.GroupBy(r => r.DateHeure.Date)
            .ToDictionary(g => g.Key.ToString("yyyy-MM-dd"), g => g.ToList());
        ViewBag.CalendrierData = calData;
        ViewBag.Début = début;
        return View(rdvs);
    }
}
