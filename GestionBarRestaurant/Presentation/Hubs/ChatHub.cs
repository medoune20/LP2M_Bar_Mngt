using System.Collections.Concurrent;
using Domaine;
using Domaine.Models;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.SignalR;

namespace Presentation.Hubs;

/// <summary>
/// Messagerie temps réel cloisonnée par établissement (tenant) et par salon.
///
/// - Chaque connexion rejoint le groupe « tenant-{TenantId} » (présence + diffusions
///   globales du tenant) ainsi qu'un groupe par salon accessible
///   « tenant-{TenantId}-canal-{cle} ».
/// - Un message n'est diffusé qu'au groupe du salon concerné, lui-même propre au
///   tenant : aucun établissement ne voit les messages d'un autre.
/// - La présence (« qui est connecté à la messagerie ») est suivie en mémoire et
///   rediffusée à tout le tenant à chaque changement.
/// </summary>
public class ChatHub : Hub
{
    private const int TexteMaxLength = 2000;

    private readonly AppDbContext _db;

    public ChatHub(AppDbContext db)
    {
        _db = db;
    }

    // --- Suivi en mémoire (déploiement mono-serveur). -----------------------
    private sealed record InfosConnexion(int TenantId, int UtilisateurId, string Nom, int Role);

    // ConnectionId -> infos de la connexion.
    private static readonly ConcurrentDictionary<string, InfosConnexion> Connexions = new();
    // TenantId -> (UtilisateurId -> nombre de connexions actives).
    private static readonly ConcurrentDictionary<int, ConcurrentDictionary<int, int>> CompteursParTenant = new();
    // Identité affichable la plus récente par (TenantId, UtilisateurId).
    private static readonly ConcurrentDictionary<(int, int), (string Nom, int Role)> IdentiteUtilisateur = new();

    private static string GroupeTenant(int tenantId) => $"tenant-{tenantId}";
    private static string GroupeSalon(int tenantId, string canal) => $"tenant-{tenantId}-canal-{canal}";

    private InfosConnexion? Identite()
    {
        var session = Context.GetHttpContext()?.Session;
        if (session == null) return null;

        var tenantId = session.GetInt32("TenantId");
        var role = session.GetInt32("UtilisateurRole");
        if (tenantId == null || role == null) return null;

        var utilisateurId = session.GetInt32("UtilisateurId") ?? 0;
        var nom = session.GetString("UtilisateurNom") ?? "Utilisateur";
        return new InfosConnexion(tenantId.Value, utilisateurId, nom, role.Value);
    }

    private bool EstSuperAdmin =>
        Context.GetHttpContext()?.Session.GetString("IsSuperAdmin") == "true";

    public override async Task OnConnectedAsync()
    {
        var id = Identite();
        if (id == null)
        {
            Context.Abort();
            return;
        }

        Connexions[Context.ConnectionId] = id;
        IdentiteUtilisateur[(id.TenantId, id.UtilisateurId)] = (id.Nom, id.Role);

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupeTenant(id.TenantId));
        foreach (var salon in SalonsChat.Accessibles(EstSuperAdmin, id.Role))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupeSalon(id.TenantId, salon.Cle));

        var compteurs = CompteursParTenant.GetOrAdd(id.TenantId, _ => new ConcurrentDictionary<int, int>());
        compteurs.AddOrUpdate(id.UtilisateurId, 1, (_, n) => n + 1);

        await DiffuserPresence(id.TenantId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Connexions.TryRemove(Context.ConnectionId, out var id))
        {
            if (CompteursParTenant.TryGetValue(id.TenantId, out var compteurs))
            {
                var restant = compteurs.AddOrUpdate(id.UtilisateurId, 0, (_, n) => n - 1);
                if (restant <= 0) compteurs.TryRemove(id.UtilisateurId, out _);
            }
            await DiffuserPresence(id.TenantId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Renvoie la liste de présence courante au seul appelant.</summary>
    public Task DemanderPresence()
    {
        var id = Identite();
        return id == null ? Task.CompletedTask : EnvoyerPresence(Clients.Caller, id.TenantId);
    }

    private Task DiffuserPresence(int tenantId) => EnvoyerPresence(Clients.Group(GroupeTenant(tenantId)), tenantId);

    private Task EnvoyerPresence(IClientProxy cible, int tenantId)
    {
        var enLigne = new List<object>();
        if (CompteursParTenant.TryGetValue(tenantId, out var compteurs))
        {
            foreach (var uid in compteurs.Keys.OrderBy(k => k))
            {
                IdentiteUtilisateur.TryGetValue((tenantId, uid), out var infos);
                enLigne.Add(new { utilisateurId = uid, nom = infos.Nom ?? "Utilisateur", role = infos.Role });
            }
        }

        return cible.SendAsync("PresenceMaj", enLigne);
    }

    /// <summary>
    /// Envoie un message dans un salon. L'accès au salon est revérifié côté serveur
    /// à partir du rôle de l'utilisateur ; le contenu est persisté avant diffusion.
    /// </summary>
    public async Task Envoyer(string canal, string texte)
    {
        var id = Identite();
        if (id == null)
        {
            Context.Abort();
            return;
        }

        canal = SalonsChat.Normaliser(canal);
        if (!SalonsChat.Accessible(EstSuperAdmin, id.Role, canal))
            return; // salon non autorisé pour ce rôle

        texte = (texte ?? string.Empty).Trim();
        if (texte.Length == 0) return;
        if (texte.Length > TexteMaxLength) texte = texte[..TexteMaxLength];

        var message = new MessageChat
        {
            TenantId = id.TenantId,
            UtilisateurId = id.UtilisateurId,
            AuteurNom = id.Nom,
            AuteurRole = id.Role,
            Canal = canal,
            Texte = texte,
            DateEnvoi = DateTime.Now
        };

        _db.MessagesChat.Add(message);
        await _db.SaveChangesAsync();

        var charge = new
        {
            id = message.Id,
            utilisateurId = message.UtilisateurId,
            auteur = message.AuteurNom,
            role = message.AuteurRole,
            canal = message.Canal,
            texte = message.Texte, // rendu via textContent côté client (anti-XSS)
            heure = message.DateEnvoi.ToString("HH:mm"),
            date = message.DateEnvoi.ToString("dd/MM/yyyy HH:mm")
        };

        // Diffusion au salon (cloisonné tenant) + notification de badge au tenant entier.
        await Clients.Group(GroupeSalon(id.TenantId, canal)).SendAsync("RecevoirMessage", charge);
        await Clients.Group(GroupeTenant(id.TenantId)).SendAsync("NouveauMessageTenant", new { canal, utilisateurId = id.UtilisateurId, id = message.Id });
    }
}
