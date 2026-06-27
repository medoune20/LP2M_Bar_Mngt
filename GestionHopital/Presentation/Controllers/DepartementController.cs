using GestionHopital.Domaine.Models;
using GestionHopital.Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionHopital.Presentation.Controllers;

public class DepartementController : BaseController
{
    public DepartementController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index()
    {
        var depts = await _db.Departements
            .Include(d => d.Medecins)
            .Include(d => d.Lits)
            .Include(d => d.Hospitalisations.Where(h => h.Statut == StatutHospitalisation.EnCours))
            .Where(d => d.Actif)
            .OrderBy(d => d.Nom)
            .ToListAsync();
        return View(depts);
    }

    [HttpGet]
    public IActionResult Nouveau() => View(new Departement());

    [HttpPost]
    public async Task<IActionResult> Nouveau(Departement model)
    {
        ModelState.Remove("Medecins");
        ModelState.Remove("Lits");
        ModelState.Remove("Hospitalisations");
        if (!ModelState.IsValid) return View(model);
        _db.Departements.Add(model);
        await _db.SaveChangesAsync();

        // Auto-créer les lits
        for (int i = 1; i <= model.CapacitéLits; i++)
        {
            _db.Lits.Add(new LitHospitalisation
            {
                Numéro = $"{model.Nom[0]}{model.Id:D2}-{i:D2}",
                DépartementId = model.Id,
                Statut = StatutLit.Libre
            });
        }
        await _db.SaveChangesAsync();
        Succès($"Service '{model.Nom}' créé avec {model.CapacitéLits} lits.");
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Lits(int id)
    {
        var dept = await _db.Departements
            .Include(d => d.Lits)
            .ThenInclude(l => l.Hospitalisations.Where(h => h.Statut == StatutHospitalisation.EnCours))
            .Include(d => d.Lits)
            .ThenInclude(l => l.Hospitalisations.Where(h => h.Statut == StatutHospitalisation.EnCours))
            .FirstOrDefaultAsync(d => d.Id == id);
        if (dept == null) return NotFound();

        // On recharge avec les patients dans les hospitalisations actives
        var hospsActives = await _db.Hospitalisations
            .Include(h => h.Patient)
            .Where(h => h.DépartementId == id && h.Statut == StatutHospitalisation.EnCours)
            .ToListAsync();

        ViewBag.Departement = dept;
        ViewBag.HospitalisationsActives = hospsActives;
        return View(dept.Lits.OrderBy(l => l.Numéro).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> ChangerStatutLit(int litId, StatutLit statut)
    {
        var lit = await _db.Lits.FindAsync(litId);
        if (lit != null) { lit.Statut = statut; await _db.SaveChangesAsync(); }
        return RedirectToAction("Lits", new { id = lit?.DépartementId });
    }
}
