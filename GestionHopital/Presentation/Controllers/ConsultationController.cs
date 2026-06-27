using GestionHopital.Domaine;
using GestionHopital.Domaine.Models;
using GestionHopital.Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionHopital.Presentation.Controllers;

public class ConsultationController : BaseController
{
    public ConsultationController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index(string? date, int? medecinId)
    {
        var query = _db.Consultations
            .Include(c => c.Patient)
            .Include(c => c.Medecin)
            .AsQueryable();

        if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var d))
            query = query.Where(c => c.DateHeure.Date == d.Date);
        else
            query = query.Where(c => c.DateHeure.Date == DateTime.Today);

        if (medecinId.HasValue) query = query.Where(c => c.MedecinId == medecinId.Value);

        ViewBag.DateFiltre = string.IsNullOrEmpty(date) ? DateTime.Today.ToString("yyyy-MM-dd") : date;
        ViewBag.Medecins = await _db.Medecins.Where(m => m.Actif).OrderBy(m => m.Nom).ToListAsync();
        ViewBag.MedecinIdFiltre = medecinId;
        return View(await query.OrderByDescending(c => c.DateHeure).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Nouvelle(int? rendezVousId, int? patientId)
    {
        RendezVous? rdv = null;
        Patient? patient = null;

        if (rendezVousId.HasValue)
        {
            rdv = await _db.RendezVous.Include(r => r.Patient).Include(r => r.Medecin).FirstOrDefaultAsync(r => r.Id == rendezVousId.Value);
            patient = rdv?.Patient;
        }
        else if (patientId.HasValue)
        {
            patient = await _db.Patients.FindAsync(patientId.Value);
        }

        ViewBag.RendezVous = rdv;
        ViewBag.Patient = patient;
        ViewBag.Medecins = await _db.Medecins.Where(m => m.Actif).OrderBy(m => m.Nom).ToListAsync();

        var consultation = new Consultation
        {
            DateHeure = DateTime.Now,
            PatientId = patient?.Id ?? 0,
            MedecinId = rdv?.MedecinId ?? (MedecinId ?? 0),
            RendezVousId = rendezVousId,
            Motif = rdv?.Motif ?? "",
            Type = rdv?.TypeConsultation ?? TypeConsultation.Générale
        };
        return View(consultation);
    }

    [HttpPost]
    public async Task<IActionResult> Nouvelle(Consultation model, string? médicaments, string? dosages, string? fréquences, string? durées)
    {
        ModelState.Remove("Patient");
        ModelState.Remove("Medecin");
        ModelState.Remove("RendezVous");
        ModelState.Remove("Ordonnances");
        ModelState.Remove("Examens");
        if (!ModelState.IsValid)
        {
            ViewBag.Medecins = await _db.Medecins.Where(m => m.Actif).OrderBy(m => m.Nom).ToListAsync();
            ViewBag.Patient = await _db.Patients.FindAsync(model.PatientId);
            return View(model);
        }

        model.DateHeure = DateTime.Now;
        _db.Consultations.Add(model);
        await _db.SaveChangesAsync();

        // Liaison RDV
        if (model.RendezVousId.HasValue)
        {
            var rdv = await _db.RendezVous.FindAsync(model.RendezVousId.Value);
            if (rdv != null) { rdv.Statut = StatutRendezVous.Terminé; rdv.ConsultationId = model.Id; await _db.SaveChangesAsync(); }
        }

        // Ordonnance si médicaments saisis
        if (!string.IsNullOrEmpty(médicaments))
        {
            var meds = médicaments.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var dos = (dosages ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var fréqs = (fréquences ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var durs = (durées ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (meds.Length > 0)
            {
                var ordo = new Ordonnance { ConsultationId = model.Id, MedecinId = model.MedecinId, PatientId = model.PatientId, Date = DateTime.Now };
                _db.Ordonnances.Add(ordo);
                await _db.SaveChangesAsync();
                for (int i = 0; i < meds.Length; i++)
                {
                    _db.LignesOrdonnance.Add(new LigneOrdonnance
                    {
                        OrdonnanceId = ordo.Id,
                        Médicament = meds[i].Trim(),
                        Dosage = i < dos.Length ? dos[i].Trim() : "",
                        Fréquence = i < fréqs.Length ? fréqs[i].Trim() : "",
                        Durée = i < durs.Length ? durs[i].Trim() : "",
                        Ordre = i + 1
                    });
                }
                await _db.SaveChangesAsync();
            }
        }

        Succès("Consultation enregistrée avec succès.");
        return RedirectToAction("Details", new { id = model.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var consultation = await _db.Consultations
            .Include(c => c.Patient)
            .Include(c => c.Medecin).ThenInclude(m => m!.Departement)
            .Include(c => c.RendezVous)
            .Include(c => c.Ordonnances).ThenInclude(o => o.Lignes)
            .Include(c => c.Examens).ThenInclude(e => e.Résultat)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (consultation == null) return NotFound();
        return View(consultation);
    }
}
