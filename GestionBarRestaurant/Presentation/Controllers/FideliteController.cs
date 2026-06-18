using Domaine.Models;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Presentation.Filtres;

namespace Presentation.Controllers;

[AutorisationFiltre]
public class FideliteController : BaseController
{
    private readonly AppDbContext _db;

    public FideliteController(AppDbContext db)
    {
        _db = db;
    }

    public IActionResult Index()
    {
        try
        {
            var regle = _db.ReglesFidelite.FirstOrDefault(r => r.TenantId == TenantId);
            if (regle == null)
            {
                regle = new RegleFidelite
                {
                    TenantId = TenantId,
                    NomProgramme = "Programme fidélité",
                    MontantPourUnPoint = 1000,
                    ValeurPoint = 10,
                    SeuilUtilisationPoints = 100,
                    Actif = true
                };

                _db.ReglesFidelite.Add(regle);
                _db.SaveChanges();
            }

            ViewBag.Regle = regle;
            ViewBag.Mouvements = _db.MouvementsFidelite
                .Where(m => m.TenantId == TenantId)
                .OrderByDescending(m => m.DateMouvement)
                .Take(50)
                .ToList();

            var clients = _db.Clients
                .Where(c => c.TenantId == TenantId && c.Actif)
                .OrderByDescending(c => c.PointsFidelite)
                .ThenByDescending(c => c.TotalAchats)
                .ToList();

            return View(clients);
        }
        catch (Exception ex)
        {
            ViewBag.ErreurTechnique = ex.Message;
            ViewBag.Regle = new RegleFidelite
            {
                TenantId = TenantId,
                NomProgramme = "Programme fidélité",
                MontantPourUnPoint = 1000,
                ValeurPoint = 10,
                SeuilUtilisationPoints = 100,
                Actif = true
            };
            ViewBag.Mouvements = new List<MouvementFidelite>();

            return View(new List<Client>());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ModifierRegle(RegleFidelite regle)
    {
        var existante = _db.ReglesFidelite.FirstOrDefault(r => r.Id == regle.Id && r.TenantId == TenantId);

        if (existante == null)
        {
            regle.TenantId = TenantId;
            regle.MontantPourUnPoint = regle.MontantPourUnPoint <= 0 ? 1000 : regle.MontantPourUnPoint;
            regle.ValeurPoint = regle.ValeurPoint < 0 ? 0 : regle.ValeurPoint;
            regle.SeuilUtilisationPoints = regle.SeuilUtilisationPoints < 0 ? 0 : regle.SeuilUtilisationPoints;
            _db.ReglesFidelite.Add(regle);
        }
        else
        {
            existante.NomProgramme = string.IsNullOrWhiteSpace(regle.NomProgramme) ? "Programme fidélité" : regle.NomProgramme;
            existante.MontantPourUnPoint = regle.MontantPourUnPoint <= 0 ? 1000 : regle.MontantPourUnPoint;
            existante.ValeurPoint = regle.ValeurPoint < 0 ? 0 : regle.ValeurPoint;
            existante.SeuilUtilisationPoints = regle.SeuilUtilisationPoints < 0 ? 0 : regle.SeuilUtilisationPoints;
            existante.Actif = regle.Actif;
        }

        _db.SaveChanges();
        TempData["Succes"] = "Règle de fidélité mise à jour.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AjusterPoints(int clientId, int points, string? commentaire)
    {
        var client = _db.Clients.FirstOrDefault(c => c.Id == clientId && c.TenantId == TenantId);
        if (client == null)
        {
            TempData["Erreur"] = "Client introuvable.";
            return RedirectToAction(nameof(Index));
        }

        client.PointsFidelite = Math.Max(0, client.PointsFidelite + points);

        _db.MouvementsFidelite.Add(new MouvementFidelite
        {
            TenantId = TenantId,
            ClientId = client.Id,
            ClientNom = client.Nom,
            DateMouvement = DateTime.Now,
            Points = points,
            TypeMouvement = points >= 0 ? "Ajustement +" : "Ajustement -",
            Commentaire = string.IsNullOrWhiteSpace(commentaire) ? "Ajustement manuel" : commentaire,
            Utilisateur = UtilisateurNom
        });

        _db.SaveChanges();
        TempData["Succes"] = "Points de fidélité ajustés.";
        return RedirectToAction(nameof(Index));
    }
}
