using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Donnees;

public static class NormaliserSQLiteDecimal
{
    public static void Executer(AppDbContext db)
    {
        // Normalise certaines anciennes valeurs créées sous forme texte comme '0.0'.
        // Avec le nouveau mapping HasConversion<double>(), les futures écritures seront stables.
        try
        {
            db.Database.ExecuteSqlRaw(@"UPDATE Clients SET TotalAchats = 0 WHERE TotalAchats IS NULL OR TotalAchats = '';");
            db.Database.ExecuteSqlRaw(@"UPDATE Clients SET SoldeCredit = 0 WHERE SoldeCredit IS NULL OR SoldeCredit = '';");
            db.Database.ExecuteSqlRaw(@"UPDATE Ventes SET RemiseFidelite = 0 WHERE RemiseFidelite IS NULL OR RemiseFidelite = '';");
            db.Database.ExecuteSqlRaw(@"UPDATE ReglesFidelite SET MontantPourUnPoint = 1000 WHERE MontantPourUnPoint IS NULL OR MontantPourUnPoint = '' OR MontantPourUnPoint = '0.0' OR MontantPourUnPoint = 0;");
            db.Database.ExecuteSqlRaw(@"UPDATE ReglesFidelite SET ValeurPoint = 10 WHERE ValeurPoint IS NULL OR ValeurPoint = '' OR ValeurPoint = '0.0' OR ValeurPoint = 0;");
        }
        catch
        {
            // Ne bloque pas le démarrage : si une table n'existe pas encore,
            // DatabaseInitializer la créera avant le prochain accès.
        }
    }
}
