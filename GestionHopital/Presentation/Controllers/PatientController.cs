using GestionHopital.Domaine;
using GestionHopital.Domaine.Models;
using GestionHopital.Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionHopital.Presentation.Controllers;

public class PatientController : BaseController
{
    public PatientController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index(string? recherche, string? statut)
    {
        var query = _db.Patients.AsQueryable();
        if (!string.IsNullOrWhiteSpace(recherche))
            query = query.Where(p => p.Nom.Contains(recherche) || p.Prénom.Contains(recherche) || p.NuméroDossier.Contains(recherche) || (p.Téléphone != null && p.Téléphone.Contains(recherche)));
        if (!string.IsNullOrWhiteSpace(statut) && Enum.TryParse<StatutPatient>(statut, out var s))
            query = query.Where(p => p.Statut == s);

        ViewBag.Recherche = recherche;
        ViewBag.StatutFiltre = statut;
        ViewBag.TotalPatients = await _db.Patients.CountAsync();
        var patients = await query.OrderByDescending(p => p.DateEnregistrement).Take(200).ToListAsync();
        return View(patients);
    }

    [HttpGet]
    public IActionResult Nouveau() => View(new Patient());

    [HttpPost]
    public async Task<IActionResult> Nouveau(Patient model)
    {
        if (!ModelState.IsValid) return View(model);
        model.NuméroDossier = await GénérerIPP();
        model.DateEnregistrement = DateTime.Now;
        _db.Patients.Add(model);
        await _db.SaveChangesAsync();
        Succès($"Patient {model.NomComplet} enregistré — Dossier N° {model.NuméroDossier}");
        return RedirectToAction("Details", new { id = model.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var patient = await _db.Patients
            .Include(p => p.RendezVous).ThenInclude(r => r.Medecin)
            .Include(p => p.Consultations).ThenInclude(c => c.Medecin)
            .Include(p => p.Consultations).ThenInclude(c => c.Ordonnances).ThenInclude(o => o.Lignes)
            .Include(p => p.Examens).ThenInclude(e => e.Résultat)
            .Include(p => p.Examens).ThenInclude(e => e.MedecinPrescripteur)
            .Include(p => p.Hospitalisations).ThenInclude(h => h.Departement)
            .Include(p => p.Hospitalisations).ThenInclude(h => h.Lit)
            .Include(p => p.Hospitalisations).ThenInclude(h => h.MedecinRéférent)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (patient == null) return NotFound();
        return View(patient);
    }

    [HttpGet]
    public async Task<IActionResult> Modifier(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null) return NotFound();
        return View(patient);
    }

    [HttpPost]
    public async Task<IActionResult> Modifier(Patient model)
    {
        if (!ModelState.IsValid) return View(model);
        var exist = await _db.Patients.FindAsync(model.Id);
        if (exist == null) return NotFound();

        exist.Nom = model.Nom;
        exist.Prénom = model.Prénom;
        exist.DateNaissance = model.DateNaissance;
        exist.Sexe = model.Sexe;
        exist.GroupeSanguin = model.GroupeSanguin;
        exist.Adresse = model.Adresse;
        exist.Téléphone = model.Téléphone;
        exist.Email = model.Email;
        exist.NomContactUrgence = model.NomContactUrgence;
        exist.TéléphoneUrgence = model.TéléphoneUrgence;
        exist.Allergies = model.Allergies;
        exist.Antécédents = model.Antécédents;
        exist.NuméroSécuritéSociale = model.NuméroSécuritéSociale;
        exist.Mutuelle = model.Mutuelle;
        await _db.SaveChangesAsync();
        Succès("Dossier patient mis à jour.");
        return RedirectToAction("Details", new { id = model.Id });
    }

    [HttpPost]
    public async Task<IActionResult> ChangerStatut(int id, StatutPatient statut)
    {
        var p = await _db.Patients.FindAsync(id);
        if (p != null) { p.Statut = statut; await _db.SaveChangesAsync(); }
        return RedirectToAction("Details", new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Recherche(string terme)
    {
        if (string.IsNullOrWhiteSpace(terme)) return Json(new List<object>());
        var patients = await _db.Patients
            .Where(p => p.Nom.Contains(terme) || p.Prénom.Contains(terme) || p.NuméroDossier.Contains(terme))
            .Select(p => new { p.Id, p.NuméroDossier, NomComplet = p.Prénom + " " + p.Nom, p.Téléphone, DateNaissance = p.DateNaissance.ToString("dd/MM/yyyy") })
            .Take(10)
            .ToListAsync();
        return Json(patients);
    }

    private async Task<string> GénérerIPP()
    {
        var année = DateTime.Now.Year;
        var count = await _db.Patients.CountAsync(p => p.DateEnregistrement.Year == année);
        return $"P-{année}-{(count + 1):D4}";
    }
}
