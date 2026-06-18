using Domaine.Models;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.SignalR;

namespace Presentation.Hubs;

/// <summary>
/// Messagerie temps réel cloisonnée par établissement (tenant).
///
/// Chaque connexion est rattachée au groupe SignalR « tenant-{TenantId} » lu
/// depuis la session de l'utilisateur. Un message envoyé n'est diffusé qu'aux
/// connexions du même tenant : aucun établissement ne voit les messages d'un
/// autre. Les messages sont également persistés pour recharger l'historique.
/// </summary>
public class ChatHub : Hub
{
    private const int TexteMaxLength = 2000;

    private readonly AppDbContext _db;

    public ChatHub(AppDbContext db)
    {
        _db = db;
    }

    private static string NomGroupe(int tenantId) => $"tenant-{tenantId}";

    private (int tenantId, int utilisateurId, string nom, int role)? Identite()
    {
        var session = Context.GetHttpContext()?.Session;
        if (session == null) return null;

        var tenantId = session.GetInt32("TenantId");
        var role = session.GetInt32("UtilisateurRole");
        if (tenantId == null || role == null) return null;

        var utilisateurId = session.GetInt32("UtilisateurId") ?? 0;
        var nom = session.GetString("UtilisateurNom") ?? "Utilisateur";
        return (tenantId.Value, utilisateurId, nom, role.Value);
    }

    public override async Task OnConnectedAsync()
    {
        var id = Identite();
        if (id == null)
        {
            // Connexion non authentifiée : on coupe immédiatement.
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, NomGroupe(id.Value.tenantId));
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Envoie un message à tous les utilisateurs connectés du même tenant.
    /// Le contenu est persisté avant diffusion.
    /// </summary>
    public async Task Envoyer(string texte)
    {
        var id = Identite();
        if (id == null)
        {
            Context.Abort();
            return;
        }

        texte = (texte ?? string.Empty).Trim();
        if (texte.Length == 0) return;
        if (texte.Length > TexteMaxLength) texte = texte[..TexteMaxLength];

        var message = new MessageChat
        {
            TenantId = id.Value.tenantId,
            UtilisateurId = id.Value.utilisateurId,
            AuteurNom = id.Value.nom,
            AuteurRole = id.Value.role,
            Texte = texte,
            DateEnvoi = DateTime.Now
        };

        _db.MessagesChat.Add(message);
        await _db.SaveChangesAsync();

        // Diffusion au seul groupe du tenant. Le texte brut est envoyé tel quel ;
        // l'échappement HTML est réalisé côté client (textContent) pour éviter le XSS.
        await Clients.Group(NomGroupe(id.Value.tenantId)).SendAsync("RecevoirMessage", new
        {
            id = message.Id,
            utilisateurId = message.UtilisateurId,
            auteur = message.AuteurNom,
            role = message.AuteurRole,
            texte = message.Texte,
            heure = message.DateEnvoi.ToString("HH:mm"),
            date = message.DateEnvoi.ToString("dd/MM/yyyy HH:mm")
        });
    }
}
