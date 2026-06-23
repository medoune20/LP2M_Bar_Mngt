using Application.ViewModels;
using Domaine;
using Domaine.Models;
using Infrastructure.Donnees;

namespace Infrastructure.Services;

/// <summary>
/// Moteur comptable OHADA : transforme les ventes, dépenses et mouvements de
/// caisse d'une période en écritures équilibrées (partie double), puis en
/// balance et synthèse de trésorerie. Le calcul est fait à la volée à partir
/// des données existantes (aucune ressaisie).
/// </summary>
public class ComptabiliteService
{
    private readonly AppDbContext _db;

    public ComptabiliteService(AppDbContext db) => _db = db;

    public ParametrageComptable ObtenirParametrage(int tenantId)
    {
        var p = _db.ParametragesComptables.FirstOrDefault(x => x.TenantId == tenantId);
        if (p == null)
        {
            p = new ParametrageComptable { TenantId = tenantId };
            _db.ParametragesComptables.Add(p);
            _db.SaveChanges();
        }
        return p;
    }

    public void MettreAJourParametrage(int tenantId, ParametrageComptable maj)
    {
        var p = ObtenirParametrage(tenantId);
        p.AssujettiTva = maj.AssujettiTva;
        p.TauxTva = maj.TauxTva < 0 ? 0 : maj.TauxTva;
        p.Devise = Def(maj.Devise, "XOF").ToUpperInvariant();
        p.Exercice = maj.Exercice <= 0 ? DateTime.Now.Year : maj.Exercice;
        p.CompteCaisse = Def(maj.CompteCaisse, "571");
        p.CompteBanque = Def(maj.CompteBanque, "521");
        p.CompteClients = Def(maj.CompteClients, "411");
        p.CompteFournisseurs = Def(maj.CompteFournisseurs, "401");
        p.CompteVentes = Def(maj.CompteVentes, "701");
        p.CompteTvaCollectee = Def(maj.CompteTvaCollectee, "4431");
        p.CompteTvaDeductible = Def(maj.CompteTvaDeductible, "4452");
        p.CompteAchats = Def(maj.CompteAchats, "601");
        p.CompteCharges = Def(maj.CompteCharges, "627");
        p.CompteApports = Def(maj.CompteApports, "4711");
        _db.SaveChanges();
    }

    public RapportComptaVm GenererRapport(int tenantId, DateTime du, DateTime au)
    {
        var p = ObtenirParametrage(tenantId);
        var debut = du.Date;
        var finExclusive = au.Date.AddDays(1);

        var ventes = _db.Ventes
            .Where(v => v.TenantId == tenantId && v.Statut == StatutVente.Validee
                        && v.DateVente >= debut && v.DateVente < finExclusive)
            .ToList();
        var depenses = _db.Depenses
            .Where(d => d.TenantId == tenantId && d.DateDepense >= debut && d.DateDepense < finExclusive)
            .ToList();
        var mouvements = _db.MouvementsCaisse
            .Where(m => m.TenantId == tenantId && m.DateMouvement >= debut && m.DateMouvement < finExclusive)
            .ToList();

        var rapport = new RapportComptaVm
        {
            Du = debut,
            Au = au.Date,
            AssujettiTva = p.AssujettiTva,
            TauxTva = p.TauxTva,
            Devise = p.Devise
        };
        var ecritures = new List<EcritureVm>();

        foreach (var v in ventes)
        {
            var (ht, tva) = SplitTva(v.Total, p);
            var treso = CompteTresorerie(v.ModePaiement, p);
            var e = new EcritureVm
            {
                Date = v.DateVente,
                Journal = "VT",
                Piece = string.IsNullOrWhiteSpace(v.NumeroTicket) ? $"VT{v.Id:000000}" : v.NumeroTicket,
                Libelle = $"Vente {v.NumeroTicket}".Trim(),
                Tiers = v.ClientNom
            };
            e.Lignes.Add(Ligne(treso, v.Total, 0));
            e.Lignes.Add(Ligne(p.CompteVentes, 0, ht));
            if (tva > 0) e.Lignes.Add(Ligne(p.CompteTvaCollectee, 0, tva));
            ecritures.Add(e);

            rapport.Recettes += v.Total;
            rapport.TvaCollectee += tva;
        }

        foreach (var d in depenses)
        {
            var (ht, tva) = SplitTva(d.Montant, p);
            var contrepartie = d.CaisseSessionId.HasValue ? p.CompteCaisse : p.CompteFournisseurs;
            var e = new EcritureVm
            {
                Date = d.DateDepense,
                Journal = d.CaisseSessionId.HasValue ? "CD" : "AC",
                Piece = $"DEP{d.Id:000000}",
                Libelle = string.IsNullOrWhiteSpace(d.Libelle) ? d.Categorie : d.Libelle,
                Tiers = d.Beneficiaire
            };
            e.Lignes.Add(Ligne(p.CompteCharges, ht, 0));
            if (tva > 0) e.Lignes.Add(Ligne(p.CompteTvaDeductible, tva, 0));
            e.Lignes.Add(Ligne(contrepartie, 0, d.Montant));
            ecritures.Add(e);

            rapport.Depenses += d.Montant;
            rapport.TvaDeductible += tva;
        }

        foreach (var m in mouvements)
        {
            var e = new EcritureVm
            {
                Date = m.DateMouvement,
                Journal = m.Type == TypeMouvementCaisse.Apport ? "OD" : "TR",
                Piece = $"MC{m.Id:000000}",
                Libelle = string.IsNullOrWhiteSpace(m.Motif) ? m.Type.ToString() : m.Motif,
                Tiers = m.ClientNom
            };
            switch (m.Type)
            {
                case TypeMouvementCaisse.Apport:
                    e.Lignes.Add(Ligne(p.CompteCaisse, m.Montant, 0));
                    e.Lignes.Add(Ligne(p.CompteApports, 0, m.Montant));
                    break;
                case TypeMouvementCaisse.Retrait:
                    e.Lignes.Add(Ligne(p.CompteBanque, m.Montant, 0));
                    e.Lignes.Add(Ligne(p.CompteCaisse, 0, m.Montant));
                    break;
                case TypeMouvementCaisse.ReglementCredit:
                    e.Lignes.Add(Ligne(p.CompteCaisse, m.Montant, 0));
                    e.Lignes.Add(Ligne(p.CompteClients, 0, m.Montant));
                    break;
            }
            ecritures.Add(e);
        }

        rapport.Ecritures = ecritures.OrderBy(x => x.Date).ThenBy(x => x.Journal).ToList();
        rapport.Balance = ConstruireBalance(rapport.Ecritures);
        // Résultat HT simplifié = ventes HT - charges HT.
        rapport.Resultat = (rapport.Recettes - rapport.TvaCollectee) - (rapport.Depenses - rapport.TvaDeductible);
        rapport.SoldeTresorerie = rapport.Balance
            .Where(b => b.Compte == p.CompteCaisse || b.Compte == p.CompteBanque)
            .Sum(b => b.SoldeDebiteur - b.SoldeCrediteur);

        return rapport;
    }

    private static LigneEcritureVm Ligne(string compte, decimal debit, decimal credit)
        => new() { Compte = compte, Intitule = ComptabiliteOhada.Intitule(compte), Debit = debit, Credit = credit };

    private static List<BalanceLigneVm> ConstruireBalance(List<EcritureVm> ecritures)
        => ecritures.SelectMany(e => e.Lignes)
            .GroupBy(l => l.Compte)
            .Select(g => new BalanceLigneVm
            {
                Compte = g.Key,
                Intitule = ComptabiliteOhada.Intitule(g.Key),
                TotalDebit = g.Sum(x => x.Debit),
                TotalCredit = g.Sum(x => x.Credit)
            })
            .OrderBy(b => b.Compte)
            .ToList();

    private static (decimal ht, decimal tva) SplitTva(decimal ttc, ParametrageComptable p)
    {
        if (!p.AssujettiTva || p.TauxTva <= 0) return (ttc, 0);
        var ht = Math.Round(ttc / (1 + p.TauxTva / 100m), 0, MidpointRounding.AwayFromZero);
        return (ht, ttc - ht);
    }

    private static string CompteTresorerie(string? modePaiement, ParametrageComptable p)
    {
        var m = (modePaiement ?? string.Empty).ToLowerInvariant();
        if (m.Length == 0) return p.CompteCaisse;
        if (m.Contains("crédit") || m.Contains("credit") || m.Contains("avoir")) return p.CompteClients;
        if (m.Contains("esp") || m.Contains("cash") || m.Contains("comptant")) return p.CompteCaisse;
        return p.CompteBanque; // carte, mobile money, wave, virement…
    }

    private static string Def(string? valeur, string defaut)
        => string.IsNullOrWhiteSpace(valeur) ? defaut : valeur.Trim();
}
