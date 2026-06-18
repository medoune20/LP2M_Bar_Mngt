using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Presentation.Filtres;

namespace Presentation.Controllers;

/// <summary>
/// Messagerie interne en temps réel. Tous les rôles connectés du tenant
/// (Administrateur, Manager, Caissier) peuvent échanger. Le cloisonnement
/// multi-tenant est garanti par le filtrage systématique sur TenantId.
/// </summary>
[AutorisationFiltre(1, 2, 3)]
public class ChatController : BaseController
{
    private const int HistoriqueMax = 100;

    private readonly AppDbContext _db;

    public ChatController(AppDbContext db)
    {
        _db = db;
    }

    public IActionResult Index()
    {
        ViewBag.UtilisateurId = UtilisateurId;
        ViewBag.UtilisateurNom = UtilisateurNom;
        ViewBag.TenantNom = TenantNom;
        return View();
    }

    /// <summary>
    /// Historique récent du tenant courant. Sert au chargement initial de la
    /// page et de filet de sécurité si la connexion temps réel est indisponible.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Historique()
    {
        var messages = await _db.MessagesChat
            .Where(m => m.TenantId == TenantId)
            .OrderByDescending(m => m.DateEnvoi)
            .Take(HistoriqueMax)
            .OrderBy(m => m.DateEnvoi)
            .Select(m => new
            {
                id = m.Id,
                utilisateurId = m.UtilisateurId,
                auteur = m.AuteurNom,
                role = m.AuteurRole,
                texte = m.Texte,
                heure = m.DateEnvoi.ToString("HH:mm"),
                date = m.DateEnvoi.ToString("dd/MM/yyyy HH:mm")
            })
            .ToListAsync();

        return Json(messages);
    }
}
