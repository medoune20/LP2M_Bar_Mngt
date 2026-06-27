using GestionHopital.Domaine;
using GestionHopital.Domaine.Models;
using GestionHopital.Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionHopital.Presentation.Controllers;

public class HospitalisationController : BaseController
{
    public HospitalisationController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index()
    {
        var hosps = await _db.Hospitalisations
            .Include(h => h.Patient)
            .Include(h => h.Departement)
            .Include(h => h.Lit)
            .Include(h => h.MedecinRéférent)
            .Where(h => h.Statut == StatutHospitalisation.EnCours)
            .OrderBy(h => h.DateAdmission)
            .ToListAsync();
        ViewBag.TotalLitsLibres = await _db.Lits.CountAsync(l => l.Statut == StatutLit.Libre);
        ViewBag.TotalHospitalisés = hosps.Count;
        return View(hosps);
    }

    [HttpGet]
    public async Task<IActionResult> Nouvelle(int? patientId)
    {
        ViewBag.Patients = await _db.Patients.Where(p => p.Statut == StatutPatient.Actif).OrderBy(p => p.Nom).ToListAsync();
        ViewBag.Medecins = await _db.Medecins.Where(m => m.Actif).OrderBy(m => m.Nom).ToListAsync();
        ViewBag.Departements = await _db.Departements
            .Include(d => d.Lits)
            .Where(d => d.Actif && d.CapacitéLits > 0)
            .OrderBy(d => d.Nom)
            .ToListAsync();
        ViewBag.Patient = patientId.HasValue ? await _db.Patients.FindAsync(patientId.Value) : null;
        return View(new Hospitalisation { PatientId = patientId ?? 0, DateAdmission = DateTime.Now });
    }

    [HttpPost]
    public async Task<IActionResult> Nouvelle(Hospitalisation model)
    {
        ModelState.Remove("NuméroHospitalisation");
        ModelState.Remove("Patient");
        ModelState.Remove("Departement");
        ModelState.Remove("Lit");
        ModelState.Remove("MedecinRéférent");
        if (!ModelState.IsValid)
        {
            ViewBag.Patients = await _db.Patients.Where(p => p.Statut == StatutPatient.Actif).OrderBy(p => p.Nom).ToListAsync();
            ViewBag.Medecins = await _db.Medecins.Where(m => m.Actif).OrderBy(m => m.Nom).ToListAsync();
            ViewBag.Departements = await _db.Departements.Include(d => d.Lits).Where(d => d.Actif && d.CapacitéLits > 0).OrderBy(d => d.Nom).ToListAsync();
            return View(model);
        }

        model.NuméroHospitalisation = GénérerNuméro("HOSP");

        var lit = await _db.Lits.FindAsync(model.LitId);
        if (lit != null) { lit.Statut = StatutLit.Occupé; }

        var patient = await _db.Patients.FindAsync(model.PatientId);
        if (patient != null) { patient.Statut = StatutPatient.Hospitalisé; }

        _db.Hospitalisations.Add(model);
        await _db.SaveChangesAsync();
        Succès($"Patient admis — N° {model.NuméroHospitalisation}");
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Sortie(int id)
    {
        var h = await _db.Hospitalisations.Include(h => h.Patient).Include(h => h.Departement).Include(h => h.Lit).FirstOrDefaultAsync(h => h.Id == id);
        if (h == null) return NotFound();
        return View(h);
    }

    [HttpPost]
    public async Task<IActionResult> Sortie(int id, string? diagnosticFinal, string? conditionsSortie, string? compteRendu)
    {
        var h = await _db.Hospitalisations.Include(h => h.Patient).Include(h => h.Lit).FirstOrDefaultAsync(h => h.Id == id);
        if (h == null) return NotFound();

        h.DateSortie = DateTime.Now;
        h.Statut = StatutHospitalisation.Sorti;
        h.DiagnosticFinal = diagnosticFinal;
        h.ConditionsSortie = conditionsSortie;
        h.CompteRendu = compteRendu;

        if (h.Lit != null) h.Lit.Statut = StatutLit.Libre;
        if (h.Patient != null) h.Patient.Statut = StatutPatient.Actif;

        await _db.SaveChangesAsync();
        Succès("Patient sorti avec succès.");
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> LitsParDepartement(int deptId)
    {
        var lits = await _db.Lits
            .Where(l => l.DépartementId == deptId && l.Statut == StatutLit.Libre)
            .Select(l => new { l.Id, l.Numéro })
            .ToListAsync();
        return Json(lits);
    }
}
