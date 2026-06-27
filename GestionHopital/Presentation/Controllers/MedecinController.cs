using GestionHopital.Domaine.Models;
using GestionHopital.Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionHopital.Presentation.Controllers;

public class MedecinController : BaseController
{
    public MedecinController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index()
    {
        var médecins = await _db.Medecins
            .Include(m => m.Departement)
            .OrderBy(m => m.Departement!.Nom).ThenBy(m => m.Nom)
            .ToListAsync();
        return View(médecins);
    }

    [HttpGet]
    public async Task<IActionResult> Nouveau()
    {
        ViewBag.Departements = await _db.Departements.Where(d => d.Actif).OrderBy(d => d.Nom).ToListAsync();
        return View(new Medecin());
    }

    [HttpPost]
    public async Task<IActionResult> Nouveau(Medecin model)
    {
        ModelState.Remove("Departement");
        ModelState.Remove("RendezVous");
        ModelState.Remove("Consultations");
        ModelState.Remove("Hospitalisations");
        if (!ModelState.IsValid)
        {
            ViewBag.Departements = await _db.Departements.Where(d => d.Actif).OrderBy(d => d.Nom).ToListAsync();
            return View(model);
        }
        _db.Medecins.Add(model);
        await _db.SaveChangesAsync();
        Succès($"Dr {model.NomComplet} ajouté.");
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Modifier(int id)
    {
        var m = await _db.Medecins.FindAsync(id);
        if (m == null) return NotFound();
        ViewBag.Departements = await _db.Departements.Where(d => d.Actif).OrderBy(d => d.Nom).ToListAsync();
        return View(m);
    }

    [HttpPost]
    public async Task<IActionResult> Modifier(Medecin model)
    {
        ModelState.Remove("Departement");
        ModelState.Remove("RendezVous");
        ModelState.Remove("Consultations");
        ModelState.Remove("Hospitalisations");
        if (!ModelState.IsValid)
        {
            ViewBag.Departements = await _db.Departements.Where(d => d.Actif).OrderBy(d => d.Nom).ToListAsync();
            return View(model);
        }
        var exist = await _db.Medecins.FindAsync(model.Id);
        if (exist == null) return NotFound();
        exist.Nom = model.Nom;
        exist.Prénom = model.Prénom;
        exist.Spécialité = model.Spécialité;
        exist.DépartementId = model.DépartementId;
        exist.Téléphone = model.Téléphone;
        exist.Email = model.Email;
        exist.NuméroOrdre = model.NuméroOrdre;
        exist.Actif = model.Actif;
        await _db.SaveChangesAsync();
        Succès("Médecin mis à jour.");
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Planning(int id)
    {
        var médecin = await _db.Medecins.Include(m => m.Departement).FirstOrDefaultAsync(m => m.Id == id);
        if (médecin == null) return NotFound();
        var aujourd_hui = DateTime.Today;
        var rdvs = await _db.RendezVous
            .Include(r => r.Patient)
            .Where(r => r.MedecinId == id && r.DateHeure >= aujourd_hui && r.DateHeure < aujourd_hui.AddDays(7))
            .OrderBy(r => r.DateHeure)
            .ToListAsync();
        ViewBag.Medecin = médecin;
        ViewBag.RendezVous = rdvs;
        return View();
    }
}
