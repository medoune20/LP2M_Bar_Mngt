using Domaine;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Presentation.Filtres;

namespace Presentation.Controllers;

/// <summary>
/// Messagerie interne en temps réel. Tous les rôles connectés du tenant peuvent
/// échanger ; l'accès aux salons dépend du rôle (voir <see cref="SalonsChat"/>).
/// Le cloisonnement multi-tenant est garanti par le filtrage sur TenantId.
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

    private string[] CanauxAccessibles() => SalonsChat.ClesAccessibles(IsSuperAdmin, RoleId);

    public IActionResult Index()
    {
        ViewBag.UtilisateurId = UtilisateurId;
        ViewBag.UtilisateurNom = UtilisateurNom;
        ViewBag.TenantNom = TenantNom;
        ViewBag.Salons = SalonsChat.Accessibles(IsSuperAdmin, RoleId).ToList();
        return View();
    }

    /// <summary>Historique récent d'un salon (chargement initial + filet de sécurité).</summary>
    [HttpGet]
    public async Task<IActionResult> Historique(string? canal)
    {
        canal = SalonsChat.Normaliser(canal);
        if (!SalonsChat.Accessible(IsSuperAdmin, RoleId, canal)) canal = "general";

        var messages = await _db.MessagesChat
            .Where(m => m.TenantId == TenantId && m.Canal == canal)
            .OrderByDescending(m => m.DateEnvoi)
            .Take(HistoriqueMax)
            .OrderBy(m => m.DateEnvoi)
            .Select(m => new
            {
                id = m.Id,
                utilisateurId = m.UtilisateurId,
                auteur = m.AuteurNom,
                role = m.AuteurRole,
                canal = m.Canal,
                texte = m.Texte,
                heure = m.DateEnvoi.ToString("HH:mm"),
                date = m.DateEnvoi.ToString("dd/MM/yyyy HH:mm")
            })
            .ToListAsync();

        await MarquerToutLu();
        return Json(messages);
    }

    /// <summary>Nombre de messages non lus dans les salons accessibles (badge du menu).</summary>
    [HttpGet]
    public async Task<IActionResult> NonLus()
    {
        var canaux = CanauxAccessibles();
        var dernierLu = DernierLuCourant();

        var count = await _db.MessagesChat
            .Where(m => m.TenantId == TenantId
                        && canaux.Contains(m.Canal)
                        && m.Id > dernierLu
                        && m.UtilisateurId != UtilisateurId)
            .CountAsync();

        return Json(new { count });
    }

    /// <summary>Marque tous les messages accessibles comme lus (remise à zéro du badge).</summary>
    [HttpGet]
    public async Task<IActionResult> MarquerLu()
    {
        await MarquerToutLu();
        return Json(new { count = 0 });
    }

    private int DernierLuCourant()
    {
        var session = HttpContext.Session.GetInt32("DerniereLectureChatId");
        if (session.HasValue) return session.Value;

        var valeur = _db.Utilisateurs.Where(u => u.Id == UtilisateurId)
            .Select(u => u.DerniereLectureChatId).FirstOrDefault();
        HttpContext.Session.SetInt32("DerniereLectureChatId", valeur);
        return valeur;
    }

    private async Task MarquerToutLu()
    {
        var canaux = CanauxAccessibles();
        var maxId = await _db.MessagesChat
            .Where(m => m.TenantId == TenantId && canaux.Contains(m.Canal))
            .Select(m => (int?)m.Id)
            .MaxAsync() ?? 0;

        if (maxId <= DernierLuCourant()) return;

        var utilisateur = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Id == UtilisateurId);
        if (utilisateur != null)
        {
            utilisateur.DerniereLectureChatId = maxId;
            await _db.SaveChangesAsync();
        }
        HttpContext.Session.SetInt32("DerniereLectureChatId", maxId);
    }
}
