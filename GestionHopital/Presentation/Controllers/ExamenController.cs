using GestionHopital.Domaine;
using GestionHopital.Domaine.Models;
using GestionHopital.Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionHopital.Presentation.Controllers;

public class ExamenController : BaseController
{
    public ExamenController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index(string? statut, int? type)
    {
        var query = _db.Examens
            .Include(e => e.Patient)
            .Include(e => e.MedecinPrescripteur)
            .Include(e => e.Résultat)
            .AsQueryable();

        if (!string.IsNullOrEmpty(statut) && Enum.TryParse<StatutExamen>(statut, out var s))
            query = query.Where(e => e.Statut == s);
        if (type.HasValue) query = query.Where(e => (int)e.Type == type.Value);

        ViewBag.StatutFiltre = statut;
        ViewBag.TypeFiltre = type;
        return View(await query.OrderByDescending(e => e.DatePrescription).Take(200).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Nouveau(int? patientId, int? consultationId)
    {
        ViewBag.Medecins = await _db.Medecins.Where(m => m.Actif).OrderBy(m => m.Nom).ToListAsync();
        ViewBag.Patient = patientId.HasValue ? await _db.Patients.FindAsync(patientId.Value) : null;
        return View(new Examen
        {
            PatientId = patientId ?? 0,
            ConsultationId = consultationId,
            MedecinPrescripteurId = MedecinId ?? 0,
            DatePrescription = DateTime.Now
        });
    }

    [HttpPost]
    public async Task<IActionResult> Nouveau(Examen model)
    {
        ModelState.Remove("NuméroExamen");
        ModelState.Remove("Patient");
        ModelState.Remove("MedecinPrescripteur");
        ModelState.Remove("Consultation");
        ModelState.Remove("Résultat");
        if (!ModelState.IsValid)
        {
            ViewBag.Medecins = await _db.Medecins.Where(m => m.Actif).OrderBy(m => m.Nom).ToListAsync();
            ViewBag.Patient = await _db.Patients.FindAsync(model.PatientId);
            return View(model);
        }
        model.NuméroExamen = GénérerNuméro("EX");
        _db.Examens.Add(model);
        await _db.SaveChangesAsync();
        Succès("Examen prescrit avec succès.");
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> SaisirRésultat(int id)
    {
        var examen = await _db.Examens.Include(e => e.Patient).Include(e => e.MedecinPrescripteur).FirstOrDefaultAsync(e => e.Id == id);
        if (examen == null) return NotFound();
        ViewBag.Examen = examen;
        return View(new ResultatExamen { ExamenId = id, Date = DateTime.Now });
    }

    [HttpPost]
    public async Task<IActionResult> SaisirRésultat(ResultatExamen model)
    {
        ModelState.Remove("Examen");
        if (!ModelState.IsValid)
        {
            ViewBag.Examen = await _db.Examens.Include(e => e.Patient).FirstOrDefaultAsync(e => e.Id == model.ExamenId);
            return View(model);
        }
        model.Date = DateTime.Now;
        _db.ResultatsExamen.Add(model);

        var examen = await _db.Examens.FindAsync(model.ExamenId);
        if (examen != null)
        {
            examen.Statut = StatutExamen.Résultats;
            examen.DateRéalisation = DateTime.Now;
        }
        await _db.SaveChangesAsync();
        Succès("Résultat enregistré.");
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Details(int id)
    {
        var examen = await _db.Examens
            .Include(e => e.Patient)
            .Include(e => e.MedecinPrescripteur)
            .Include(e => e.Résultat)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (examen == null) return NotFound();
        return View(examen);
    }

    [HttpPost]
    public async Task<IActionResult> ChangerStatut(int id, StatutExamen statut)
    {
        var e = await _db.Examens.FindAsync(id);
        if (e != null) { e.Statut = statut; await _db.SaveChangesAsync(); }
        return RedirectToAction("Index");
    }
}
