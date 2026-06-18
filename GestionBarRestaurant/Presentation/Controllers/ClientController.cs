using Domaine;
using Domaine.Models;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Mvc;
using Presentation.Filtres;

namespace Presentation.Controllers;

[AutorisationFiltre]
public class ClientController : BaseController
{
    private readonly AppDbContext _db;
    public ClientController(AppDbContext db) { _db = db; }

    public IActionResult Index(string? recherche)
    {
        var clients = _db.Clients.Where(c => c.TenantId == TenantId).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(recherche))
        {
            clients = clients.Where(c => c.Nom.Contains(recherche, StringComparison.OrdinalIgnoreCase) || c.Telephone.Contains(recherche, StringComparison.OrdinalIgnoreCase) || c.Email.Contains(recherche, StringComparison.OrdinalIgnoreCase));
        }
        ViewBag.Recherche = recherche;
        return View(clients.OrderBy(c => c.Nom).ToList());
    }

    [HttpGet]
    public IActionResult Nouveau() => View(new Client { TenantId = TenantId });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Nouveau(Client client)
    {
        if (!ModelState.IsValid) return View(client);
        client.TenantId = TenantId;
        client.DateCreation = DateTime.Now;
        _db.Clients.Add(client);
        _db.SaveChanges();
        TempData["Succes"] = "Client ajouté.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Modifier(int id)
    {
        var client = _db.Clients.FirstOrDefault(c => c.Id == id && c.TenantId == TenantId);
        return client == null ? NotFound() : View(client);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Modifier(Client client)
    {
        if (!ModelState.IsValid) return View(client);
        var existant = _db.Clients.FirstOrDefault(c => c.Id == client.Id && c.TenantId == TenantId);
        if (existant == null) return NotFound();
        existant.Nom = client.Nom;
        existant.Telephone = client.Telephone;
        existant.Email = client.Email;
        existant.TypeClient = client.TypeClient;
        existant.Adresse = client.Adresse;
        existant.PlafondCredit = client.PlafondCredit;
        existant.Actif = client.Actif;
        _db.SaveChanges();
        TempData["Succes"] = "Client modifié.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Règlement d'une créance client (vente à crédit) : le solde crédit n'est jamais
    /// modifié à la main, il diminue uniquement par encaissement journalisé.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ReglerCredit(int id, decimal montant, string modePaiement)
    {
        var client = _db.Clients.FirstOrDefault(c => c.Id == id && c.TenantId == TenantId);
        if (client == null) return NotFound();

        if (montant <= 0)
        {
            TempData["Erreur"] = "Le montant du règlement doit être supérieur à 0.";
            return RedirectToAction(nameof(Index));
        }
        if (montant > client.SoldeCredit)
        {
            TempData["Erreur"] = $"Le règlement ({montant:N0} FCFA) dépasse l'encours du client ({client.SoldeCredit:N0} FCFA).";
            return RedirectToAction(nameof(Index));
        }

        modePaiement = string.IsNullOrWhiteSpace(modePaiement) ? "Espèces" : modePaiement.Trim();
        var caisse = _db.Caisses.OrderByDescending(c => c.DateOuverture).FirstOrDefault(c => c.TenantId == TenantId && c.Statut == StatutCaisse.Ouverte);

        if (caisse == null)
        {
            TempData["Erreur"] = "Aucune caisse ouverte : ouvrez une caisse pour encaisser le règlement.";
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(modePaiement, "Espèces", StringComparison.OrdinalIgnoreCase)) caisse.Encaissements += montant;
        else caisse.EncaissementsAutres += montant;

        client.SoldeCredit -= montant;
        _db.MouvementsCaisse.Add(new MouvementCaisse
        {
            TenantId = TenantId,
            CaisseSessionId = caisse.Id,
            Type = TypeMouvementCaisse.ReglementCredit,
            Montant = montant,
            Motif = $"Règlement créance ({modePaiement})",
            Utilisateur = UtilisateurNom,
            ClientId = client.Id,
            ClientNom = client.Nom
        });
        _db.SaveChanges();
        TempData["Succes"] = $"Règlement de {montant:N0} FCFA encaissé. Encours restant de {client.Nom} : {client.SoldeCredit:N0} FCFA.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult MiseAJourMasse(int[] ids, string? typeClient, string? actionMasse)
    {
        if (ids.Length == 0)
        {
            TempData["Erreur"] = "Veuillez sélectionner au moins un client.";
            return RedirectToAction(nameof(Index));
        }
        var clients = _db.Clients.Where(c => c.TenantId == TenantId && ids.Contains(c.Id)).ToList();
        foreach (var client in clients)
        {
            if (Enum.TryParse<Domaine.TypeClient>(typeClient, out var type)) client.TypeClient = type;
            if (actionMasse == "activer") client.Actif = true;
            else if (actionMasse == "desactiver") client.Actif = false;
        }
        _db.SaveChanges();
        TempData["Succes"] = $"{clients.Count} client(s) mis à jour en masse.";
        return RedirectToAction(nameof(Index));
    }
}
