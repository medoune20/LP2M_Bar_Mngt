using Domaine;
using Domaine.Models;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Presentation.Filtres;

namespace Presentation.Controllers;

[AutorisationFiltre]
public class ProduitController : BaseController
{
    private const long TailleMaxImageBytes = 3 * 1024 * 1024;
    private static readonly HashSet<string> ExtensionsImagesAutorisees = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly HashSet<string> TypesImagesAutorises = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ProduitController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public IActionResult Index(string? recherche, string? categorie)
    {
        recherche = recherche?.Trim();
        categorie = categorie?.Trim();

        var produits = _db.Produits.Where(p => p.TenantId == TenantId).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(recherche))
        {
            produits = produits.Where(p =>
                p.Nom.Contains(recherche, StringComparison.OrdinalIgnoreCase)
                || p.Categorie.Contains(recherche, StringComparison.OrdinalIgnoreCase)
                || p.CodeBarre.Contains(recherche, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(categorie)) produits = produits.Where(p => p.Categorie.Equals(categorie, StringComparison.OrdinalIgnoreCase));
        ViewBag.Recherche = recherche;
        ViewBag.Categorie = categorie;
        ViewBag.Categories = _db.Produits.Where(p => p.TenantId == TenantId).Select(p => p.Categorie).Distinct().OrderBy(c => c).ToList();
        return View(produits.OrderBy(p => p.Categorie).ThenBy(p => p.Nom).ToList());
    }

    [HttpGet]
    public IActionResult Nouveau()
    {
        ChargerCategories();
        return View(new Produit { TenantId = TenantId });
    }

    private void ChargerCategories()
    {
        ViewBag.Categories = _db.Categories
            .Where(c => c.TenantId == TenantId && c.Actif)
            .OrderBy(c => c.Ordre).ThenBy(c => c.Nom)
            .Select(c => c.Nom).ToList();
    }

    [HttpPost]
    public async Task<IActionResult> Nouveau(Produit produit, IFormFile? image)
    {
        NormaliserProduit(produit);
        ValiderCodeBarreUnique(produit.CodeBarre, produit.Id);

        if (!ModelState.IsValid) { ChargerCategories(); return View(produit); }

        var imageSauvee = await SauverImageProduit(image);
        if (!string.IsNullOrWhiteSpace(imageSauvee.Erreur))
        {
            ModelState.AddModelError("image", imageSauvee.Erreur);
            ChargerCategories(); return View(produit);
        }

        produit.TenantId = TenantId;
        produit.DateCreation = DateTime.Now;
        produit.ImagePath = imageSauvee.Chemin;
        _db.Produits.Add(produit);
        _db.SaveChanges();

        if (string.IsNullOrWhiteSpace(produit.CodeBarre))
        {
            produit.CodeBarre = $"T{TenantId}-P{produit.Id:000000}";
            _db.SaveChanges();
        }

        _db.MouvementsStock.Add(new MouvementStock { TenantId = TenantId, ProduitId = produit.Id, ProduitNom = produit.Nom, Type = TypeMouvementStock.Ajustement, Quantite = produit.StockActuel, Motif = "Stock initial", Utilisateur = UtilisateurNom });
        _db.SaveChanges();
        TempData["Succes"] = "Produit ajouté avec image et QR code.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Modifier(int id)
    {
        ChargerCategories();
        var produit = _db.Produits.FirstOrDefault(p => p.Id == id && p.TenantId == TenantId);
        return produit == null ? NotFound() : View(produit);
    }

    [HttpPost]
    public async Task<IActionResult> Modifier(Produit produit, IFormFile? image)
    {
        NormaliserProduit(produit);
        ValiderCodeBarreUnique(produit.CodeBarre, produit.Id);

        if (!ModelState.IsValid) { ChargerCategories(); return View(produit); }

        var imageSauvee = await SauverImageProduit(image);
        if (!string.IsNullOrWhiteSpace(imageSauvee.Erreur))
        {
            ModelState.AddModelError("image", imageSauvee.Erreur);
            ChargerCategories(); return View(produit);
        }

        var existant = _db.Produits.FirstOrDefault(p => p.Id == produit.Id && p.TenantId == TenantId);
        if (existant == null) return NotFound();
        existant.Nom = produit.Nom;
        existant.Categorie = produit.Categorie;
        existant.PrixAchat = produit.PrixAchat;
        existant.PrixVente = produit.PrixVente;
        existant.StockActuel = produit.StockActuel;
        existant.StockMinimum = produit.StockMinimum;
        existant.CodeBarre = produit.CodeBarre;
        existant.Actif = produit.Actif;
        if (!string.IsNullOrWhiteSpace(imageSauvee.Chemin))
        {
            SupprimerImageLocale(existant.ImagePath);
            existant.ImagePath = imageSauvee.Chemin;
        }
        _db.SaveChanges();
        TempData["Succes"] = "Produit modifié.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Supprimer(int id)
    {
        var produit = _db.Produits.FirstOrDefault(p => p.Id == id && p.TenantId == TenantId);
        if (produit != null)
        {
            produit.Actif = false;
            _db.SaveChanges();
            TempData["Succes"] = "Produit désactivé.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult MiseAJourMasse(int[]? ids, decimal? prixVente, int? stockMinimum, string? categorie, string? actionMasse)
    {
        if (ids == null || ids.Length == 0)
        {
            TempData["Erreur"] = "Veuillez sélectionner au moins un produit.";
            return RedirectToAction(nameof(Index));
        }
        var produits = _db.Produits.Where(p => p.TenantId == TenantId && ids.Contains(p.Id)).ToList();
        foreach (var produit in produits)
        {
            if (prixVente.HasValue && prixVente.Value >= 0) produit.PrixVente = prixVente.Value;
            if (stockMinimum.HasValue && stockMinimum.Value >= 0) produit.StockMinimum = stockMinimum.Value;
            if (!string.IsNullOrWhiteSpace(categorie)) produit.Categorie = categorie.Trim();
            if (actionMasse == "activer") produit.Actif = true;
            else if (actionMasse == "desactiver") produit.Actif = false;
        }
        _db.SaveChanges();
        TempData["Succes"] = $"{produits.Count} produit(s) mis à jour en masse.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Importer() => View();

    [HttpPost]
    public async Task<IActionResult> Importer(string? donnees, IFormFile? fichier)
    {
        var texte = donnees ?? string.Empty;
        if (fichier != null && fichier.Length > 0 && fichier.Length < 1024 * 1024)
        {
            using var lecteur = new StreamReader(fichier.OpenReadStream());
            texte = await lecteur.ReadToEndAsync();
        }

        if (string.IsNullOrWhiteSpace(texte))
        {
            TempData["Erreur"] = "Aucune donnée à importer. Collez le catalogue ou choisissez un fichier .csv.";
            return RedirectToAction(nameof(Importer));
        }

        var lignes = texte.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var existants = _db.Produits.Where(p => p.TenantId == TenantId).Select(p => p.Nom.ToLower()).ToHashSet();
        int ajoutes = 0, ignores = 0, erreurs = 0;

        foreach (var ligne in lignes)
        {
            var l = ligne.Trim();
            if (l.Length == 0) continue;

            // Séparateur : point-virgule en priorité (compatible chiffres français), sinon tabulation ou virgule.
            var sep = l.Contains(';') ? ';' : (l.Contains('\t') ? '\t' : ',');
            var champs = l.Split(sep);

            var nom = champs.ElementAtOrDefault(0)?.Trim() ?? string.Empty;
            // Ignorer la ligne d'en-tête éventuelle
            if (string.IsNullOrWhiteSpace(nom) || nom.Equals("Nom", StringComparison.OrdinalIgnoreCase)) { ignores++; continue; }
            if (existants.Contains(nom.ToLower())) { ignores++; continue; }

            decimal LireDecimal(int i)
            {
                var v = champs.ElementAtOrDefault(i)?.Trim().Replace(" ", "").Replace(",", ".") ?? "0";
                return decimal.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
            }
            int LireEntier(int i)
            {
                var v = champs.ElementAtOrDefault(i)?.Trim() ?? "0";
                return int.TryParse(v, out var n) ? n : 0;
            }

            var produit = new Produit
            {
                TenantId = TenantId,
                Nom = nom,
                Categorie = string.IsNullOrWhiteSpace(champs.ElementAtOrDefault(1)) ? "Divers" : champs[1].Trim(),
                PrixAchat = LireDecimal(2),
                PrixVente = LireDecimal(3),
                StockActuel = LireEntier(4),
                StockMinimum = champs.Length > 5 ? LireEntier(5) : 5,
                Actif = true,
                DateCreation = DateTime.Now
            };

            if (produit.PrixVente <= 0) { erreurs++; continue; }

            _db.Produits.Add(produit);
            _db.SaveChanges();
            produit.CodeBarre = $"T{TenantId}-P{produit.Id:000000}";
            if (produit.StockActuel > 0)
            {
                _db.MouvementsStock.Add(new MouvementStock { TenantId = TenantId, ProduitId = produit.Id, ProduitNom = produit.Nom, Type = TypeMouvementStock.Ajustement, Quantite = produit.StockActuel, Motif = "Stock initial (import)", Utilisateur = UtilisateurNom });
            }
            _db.SaveChanges();
            existants.Add(nom.ToLower());
            ajoutes++;
        }

        TempData["Succes"] = $"Import terminé : {ajoutes} produit(s) ajouté(s), {ignores} ignoré(s) (déjà présents ou en-tête), {erreurs} en erreur.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Lookup(string code)
    {
        code = (code ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 200) return Json(new { found = false });

        int? idDepuisQr = ExtraireIdDepuisQr(code);
        var produit = idDepuisQr.HasValue
            ? _db.Produits.FirstOrDefault(p => p.TenantId == TenantId && p.Id == idDepuisQr.Value && p.Actif)
            : _db.Produits.AsEnumerable().FirstOrDefault(p => p.TenantId == TenantId && p.Actif && string.Equals(p.CodeBarre, code, StringComparison.OrdinalIgnoreCase));

        produit ??= _db.Produits.AsEnumerable().FirstOrDefault(p => p.TenantId == TenantId && p.Actif && p.Nom.Contains(code, StringComparison.OrdinalIgnoreCase));
        if (produit == null) return Json(new { found = false });
        return Json(new { found = true, id = produit.Id, nom = produit.Nom, prix = produit.PrixVente, stock = produit.StockActuel });
    }

    private void NormaliserProduit(Produit produit)
    {
        produit.TenantId = TenantId;
        produit.Nom = (produit.Nom ?? string.Empty).Trim();
        produit.Categorie = (produit.Categorie ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(produit.Categorie)) produit.Categorie = "Divers";
        produit.CodeBarre = (produit.CodeBarre ?? string.Empty).Trim();
        produit.ImagePath ??= string.Empty;
        // Champs gérés côté serveur : ne pas bloquer la validation du formulaire.
        ModelState.Remove(nameof(Produit.CodeBarre));
        ModelState.Remove(nameof(Produit.ImagePath));
        ModelState.Remove(nameof(Produit.Categorie));
    }

    private void ValiderCodeBarreUnique(string codeBarre, int produitId)
    {
        if (string.IsNullOrWhiteSpace(codeBarre)) return;

        var existe = _db.Produits.Any(p => p.TenantId == TenantId && p.Id != produitId && (p.CodeBarre ?? string.Empty).ToLower() == codeBarre.ToLower());
        if (existe)
        {
            ModelState.AddModelError(nameof(Produit.CodeBarre), "Ce code-barres est déjà utilisé pour ce tenant.");
        }
    }

    private static int? ExtraireIdDepuisQr(string code)
    {
        foreach (var morceau in code.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            if (morceau.StartsWith("ID:", StringComparison.OrdinalIgnoreCase) && int.TryParse(morceau[3..], out int id)) return id;
        }
        return null;
    }

    private async Task<(string Chemin, string? Erreur)> SauverImageProduit(IFormFile? image)
    {
        if (image == null || image.Length == 0) return (string.Empty, null);
        if (image.Length > TailleMaxImageBytes) return (string.Empty, "L'image ne doit pas dépasser 3 Mo.");

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (!ExtensionsImagesAutorisees.Contains(extension))
        {
            return (string.Empty, "Format d'image non autorisé. Formats acceptés : JPG, PNG, WEBP.");
        }

        if (!TypesImagesAutorises.Contains(image.ContentType))
        {
            return (string.Empty, "Type MIME d'image non autorisé.");
        }

        await using var memoire = new MemoryStream();
        await image.CopyToAsync(memoire);
        var contenu = memoire.ToArray();
        if (!SignatureImageValide(extension, contenu, contenu.Length))
        {
            return (string.Empty, "Le contenu du fichier ne correspond pas à une image valide.");
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var dossier = Path.Combine(webRoot, "uploads", "products");
        Directory.CreateDirectory(dossier);
        var nomFichier = $"{Guid.NewGuid():N}{extension}";
        var chemin = Path.Combine(dossier, nomFichier);

        await System.IO.File.WriteAllBytesAsync(chemin, contenu);
        return ($"/uploads/products/{nomFichier}", null);
    }

    private static bool SignatureImageValide(string extension, byte[] header, int lus)
    {
        if ((extension == ".jpg" || extension == ".jpeg") && lus >= 3)
        {
            return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        }

        if (extension == ".png" && lus >= 8)
        {
            return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
        }

        if (extension == ".webp" && lus >= 12)
        {
            return header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
        }

        return false;
    }

    private void SupprimerImageLocale(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !imagePath.StartsWith("/uploads/products/", StringComparison.OrdinalIgnoreCase)) return;

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var nomFichier = Path.GetFileName(imagePath);
        var chemin = Path.Combine(webRoot, "uploads", "products", nomFichier);
        if (System.IO.File.Exists(chemin))
        {
            System.IO.File.Delete(chemin);
        }
    }
}
