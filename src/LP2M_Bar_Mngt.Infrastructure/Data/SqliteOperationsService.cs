using System.Globalization;
using System.Text;
using LP2M_Bar_Mngt.Application.Abstractions;
using LP2M_Bar_Mngt.Application.DTOs;
using LP2M_Bar_Mngt.Infrastructure.Security;
using Microsoft.Data.Sqlite;

namespace LP2M_Bar_Mngt.Infrastructure.Data;

public sealed class SqliteOperationsService : IOperationsService
{
    private const long DefaultOpeningAmountCents = 10_000;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly PasswordHasher _passwordHasher;

    public SqliteOperationsService(SqliteConnectionFactory connectionFactory, PasswordHasher passwordHasher)
    {
        _connectionFactory = connectionFactory;
        _passwordHasher = passwordHasher;
    }

    public async Task<OperationsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return new OperationsSnapshot(
            await GetDashboardRowsAsync(connection, cancellationToken),
            await GetCashRowsAsync(connection, cancellationToken),
            await GetSalesRowsAsync(connection, cancellationToken),
            await GetProductRowsAsync(connection, cancellationToken),
            await GetStockRowsAsync(connection, cancellationToken),
            await GetExpenseRowsAsync(connection, cancellationToken),
            await GetReportRowsAsync(connection, cancellationToken),
            await GetUserRowsAsync(connection, cancellationToken));
    }

    public async Task<OperationResult> OpenCashSessionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var openSessionId = await GetOpenCashSessionIdAsync(connection, adminId, cancellationToken);

        if (openSessionId > 0)
        {
            return new OperationResult(false, "Une session de caisse est deja ouverte pour l'administrateur.");
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO cash_sessions
                (cashier_id, opened_at, opening_amount, expected_closing_amount, status)
            VALUES
                ($cashierId, $openedAt, $openingAmount, $openingAmount, 1);
            """,
            parameters =>
            {
                parameters.AddWithValue("$cashierId", adminId);
                parameters.AddWithValue("$openedAt", Now());
                parameters.AddWithValue("$openingAmount", DefaultOpeningAmountCents);
            },
            cancellationToken);

        var sessionId = await LastInsertRowIdAsync(connection, transaction, cancellationToken);
        await WriteAuditAsync(connection, transaction, adminId, "OPEN_CASH_SESSION", "cash_sessions", sessionId, "Ouverture de session caisse", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, "Session de caisse ouverte avec un fond initial de 100,00.");
    }

    public async Task<OperationResult> CloseCashSessionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var sessionId = await GetOpenCashSessionIdAsync(connection, adminId, cancellationToken);

        if (sessionId == 0)
        {
            return new OperationResult(false, "Aucune session ouverte a cloturer.");
        }

        var expectedClosingAmount = await ScalarLongAsync(
            connection,
            """
            SELECT opening_amount + COALESCE((
                SELECT SUM(CASE
                    WHEN movement_type IN (1, 3) THEN amount
                    WHEN movement_type IN (2, 4) THEN -amount
                    ELSE 0
                END)
                FROM cash_movements
                WHERE cash_session_id = $sessionId
            ), 0)
            FROM cash_sessions
            WHERE id = $sessionId;
            """,
            p => p.AddWithValue("$sessionId", sessionId),
            cancellationToken);

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE cash_sessions
            SET closed_at = $closedAt,
                expected_closing_amount = $expectedAmount,
                declared_closing_amount = $expectedAmount,
                difference_amount = 0,
                status = 2
            WHERE id = $sessionId;
            """,
            parameters =>
            {
                parameters.AddWithValue("$closedAt", Now());
                parameters.AddWithValue("$expectedAmount", expectedClosingAmount);
                parameters.AddWithValue("$sessionId", sessionId);
            },
            cancellationToken);

        await WriteAuditAsync(connection, transaction, adminId, "CLOSE_CASH_SESSION", "cash_sessions", sessionId, "Cloture de session caisse", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, $"Session cloturee. Solde attendu : {Money(expectedClosingAmount)}.");
    }

    public async Task<OperationResult> CreateSaleAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var sessionId = await GetOpenCashSessionIdAsync(connection, adminId, cancellationToken);

        if (sessionId == 0)
        {
            return new OperationResult(false, "Ouvre d'abord une session de caisse avant de vendre.");
        }

        var product = await GetSellableProductAsync(connection, cancellationToken);
        if (product is null)
        {
            return new OperationResult(false, "Aucun produit vendable avec stock disponible.");
        }

        var ticketNumber = $"LP2M-{DateTime.Now:yyyyMMdd-HHmmssfff}";
        var saleDate = Now();

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO sales
                (ticket_number, cash_session_id, cashier_id, sale_date, subtotal_amount, discount_amount, total_amount, payment_method, status)
            VALUES
                ($ticketNumber, $sessionId, $cashierId, $saleDate, $amount, 0, $amount, 1, 1);
            """,
            parameters =>
            {
                parameters.AddWithValue("$ticketNumber", ticketNumber);
                parameters.AddWithValue("$sessionId", sessionId);
                parameters.AddWithValue("$cashierId", adminId);
                parameters.AddWithValue("$saleDate", saleDate);
                parameters.AddWithValue("$amount", product.SalePriceCents);
            },
            cancellationToken);

        var saleId = await LastInsertRowIdAsync(connection, transaction, cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO sale_items
                (sale_id, product_id, product_name, quantity, unit_price, discount_amount, total_amount)
            VALUES
                ($saleId, $productId, $productName, 1, $unitPrice, 0, $unitPrice);
            """,
            parameters =>
            {
                parameters.AddWithValue("$saleId", saleId);
                parameters.AddWithValue("$productId", product.Id);
                parameters.AddWithValue("$productName", product.Name);
                parameters.AddWithValue("$unitPrice", product.SalePriceCents);
            },
            cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO cash_movements
                (cash_session_id, movement_type, amount, description, user_id, created_at)
            VALUES
                ($sessionId, 1, $amount, $description, $userId, $createdAt);
            """,
            parameters =>
            {
                parameters.AddWithValue("$sessionId", sessionId);
                parameters.AddWithValue("$amount", product.SalePriceCents);
                parameters.AddWithValue("$description", $"Paiement ticket {ticketNumber}");
                parameters.AddWithValue("$userId", adminId);
                parameters.AddWithValue("$createdAt", saleDate);
            },
            cancellationToken);

        if (product.IsStockManaged)
        {
            await ApplyStockDeltaAsync(connection, transaction, product.Id, -1, 2, $"Vente {ticketNumber}", saleId, adminId, cancellationToken);
        }

        await WriteAuditAsync(connection, transaction, adminId, "CREATE_SALE", "sales", saleId, $"Vente {ticketNumber}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, $"Vente creee : ticket {ticketNumber} - {product.Name} - {Money(product.SalePriceCents)}.");
    }

    public async Task<OperationResult> ReprintLastTicketAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var ticketNumber = await ScalarStringAsync(connection, "SELECT ticket_number FROM sales ORDER BY sale_date DESC LIMIT 1;", null, cancellationToken);

        return string.IsNullOrWhiteSpace(ticketNumber)
            ? new OperationResult(false, "Aucun ticket a reimprimer.")
            : new OperationResult(true, $"Ticket {ticketNumber} pret pour reimpression.");
    }

    public async Task<OperationResult> CancelLastSaleAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var saleId = await ScalarLongAsync(
            connection,
            "SELECT id FROM sales WHERE status = 1 ORDER BY sale_date DESC LIMIT 1;",
            null,
            cancellationToken);

        if (saleId == 0)
        {
            return new OperationResult(false, "Aucune vente valide a annuler.");
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var sale = await QuerySingleRowAsync(
            connection,
            transaction,
            "SELECT ticket_number, cash_session_id, total_amount FROM sales WHERE id = $saleId;",
            p => p.AddWithValue("$saleId", saleId),
            cancellationToken);

        var ticketNumber = Convert.ToString(sale["ticket_number"], CultureInfo.InvariantCulture) ?? string.Empty;
        var cashSessionId = Convert.ToInt64(sale["cash_session_id"], CultureInfo.InvariantCulture);
        var totalAmount = Convert.ToInt64(sale["total_amount"], CultureInfo.InvariantCulture);

        await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE sales
            SET status = 2,
                cancelled_at = $cancelledAt,
                cancelled_by = $cancelledBy,
                cancel_reason = $reason
            WHERE id = $saleId;
            """,
            parameters =>
            {
                parameters.AddWithValue("$cancelledAt", Now());
                parameters.AddWithValue("$cancelledBy", adminId);
                parameters.AddWithValue("$reason", "Annulation depuis le module Ventes");
                parameters.AddWithValue("$saleId", saleId);
            },
            cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO cash_movements
                (cash_session_id, movement_type, amount, description, user_id, created_at)
            VALUES
                ($sessionId, 4, $amount, $description, $userId, $createdAt);
            """,
            parameters =>
            {
                parameters.AddWithValue("$sessionId", cashSessionId);
                parameters.AddWithValue("$amount", totalAmount);
                parameters.AddWithValue("$description", $"Annulation ticket {ticketNumber}");
                parameters.AddWithValue("$userId", adminId);
                parameters.AddWithValue("$createdAt", Now());
            },
            cancellationToken);

        await using var itemsCommand = connection.CreateCommand();
        itemsCommand.Transaction = transaction;
        itemsCommand.CommandText = """
            SELECT si.product_id, si.quantity, p.is_stock_managed
            FROM sale_items si
            INNER JOIN products p ON p.id = si.product_id
            WHERE si.sale_id = $saleId;
            """;
        itemsCommand.Parameters.AddWithValue("$saleId", saleId);

        var stockReturns = new List<(long ProductId, double Quantity)>();
        await using var reader = await itemsCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.GetInt64(2) == 1)
            {
                stockReturns.Add((reader.GetInt64(0), reader.GetDouble(1)));
            }
        }

        await reader.DisposeAsync();

        foreach (var stockReturn in stockReturns)
        {
            await ApplyStockDeltaAsync(connection, transaction, stockReturn.ProductId, stockReturn.Quantity, 5, $"Annulation {ticketNumber}", saleId, adminId, cancellationToken);
        }

        await WriteAuditAsync(connection, transaction, adminId, "CANCEL_SALE", "sales", saleId, $"Annulation {ticketNumber}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, $"Vente {ticketNumber} annulee et stock restaure.");
    }

    public async Task<OperationResult> AddProductAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var categoryId = await EnsureCategoryAsync(connection, "Boissons", cancellationToken);
        var suffix = DateTime.Now.ToString("HHmmssfff", CultureInfo.InvariantCulture);
        var productName = $"Produit maison {suffix}";

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO products
                (category_id, name, sku, barcode, sale_price, cost_price, is_stock_managed, low_stock_threshold, is_active, created_at)
            VALUES
                ($categoryId, $name, $sku, $barcode, 250, 150, 1, 8, 1, $createdAt);
            """,
            parameters =>
            {
                parameters.AddWithValue("$categoryId", categoryId);
                parameters.AddWithValue("$name", productName);
                parameters.AddWithValue("$sku", $"AUTO-{suffix}");
                parameters.AddWithValue("$barcode", $"700000{suffix}");
                parameters.AddWithValue("$createdAt", Now());
            },
            cancellationToken);

        var productId = await LastInsertRowIdAsync(connection, transaction, cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO stock_levels (product_id, quantity) VALUES ($productId, 24);",
            p => p.AddWithValue("$productId", productId),
            cancellationToken);
        await ApplyStockDeltaAsync(connection, transaction, productId, 24, 1, "Stock initial produit", null, adminId, cancellationToken, updateLevel: false);
        await WriteAuditAsync(connection, transaction, adminId, "ADD_PRODUCT", "products", productId, productName, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, $"Produit ajoute : {productName}.");
    }

    public async Task<OperationResult> AddCategoryAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var name = $"Categorie {DateTime.Now:HHmmssfff}";

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO product_categories (name, is_active) VALUES ($name, 1);",
            p => p.AddWithValue("$name", name),
            cancellationToken);
        await WriteAuditAsync(connection, transaction, adminId, "ADD_CATEGORY", "product_categories", await LastInsertRowIdAsync(connection, transaction, cancellationToken), name, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, $"Categorie ajoutee : {name}.");
    }

    public async Task<OperationResult> UpdateProductPriceAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var product = await QuerySingleRowAsync(
            connection,
            null,
            "SELECT id, name, sale_price FROM products WHERE is_active = 1 ORDER BY id LIMIT 1;",
            null,
            cancellationToken);

        if (product.Count == 0)
        {
            return new OperationResult(false, "Aucun produit actif a mettre a jour.");
        }

        var productId = Convert.ToInt64(product["id"], CultureInfo.InvariantCulture);
        var name = Convert.ToString(product["name"], CultureInfo.InvariantCulture) ?? string.Empty;
        var oldPrice = Convert.ToInt64(product["sale_price"], CultureInfo.InvariantCulture);
        var newPrice = oldPrice + 25;

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            "UPDATE products SET sale_price = $newPrice WHERE id = $productId;",
            parameters =>
            {
                parameters.AddWithValue("$newPrice", newPrice);
                parameters.AddWithValue("$productId", productId);
            },
            cancellationToken);
        await WriteAuditAsync(connection, transaction, adminId, "UPDATE_PRICE", "products", productId, $"{name} : {Money(oldPrice)} -> {Money(newPrice)}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, $"Prix mis a jour : {name} passe a {Money(newPrice)}.");
    }

    public async Task<OperationResult> RestockLowProductsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var lowProducts = await QueryRowsAsync(
            connection,
            """
            SELECT p.id, p.name, COALESCE(sl.quantity, 0) AS quantity, p.low_stock_threshold
            FROM products p
            LEFT JOIN stock_levels sl ON sl.product_id = p.id
            WHERE p.is_active = 1
              AND p.is_stock_managed = 1
              AND COALESCE(sl.quantity, 0) <= p.low_stock_threshold;
            """,
            null,
            cancellationToken);

        if (lowProducts.Count == 0)
        {
            return new OperationResult(true, "Aucune alerte stock a corriger.");
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (var row in lowProducts)
        {
            var productId = Convert.ToInt64(row["id"], CultureInfo.InvariantCulture);
            var quantity = Convert.ToDouble(row["quantity"], CultureInfo.InvariantCulture);
            var threshold = Convert.ToDouble(row["low_stock_threshold"], CultureInfo.InvariantCulture);
            var delta = Math.Max(1, threshold + 20 - quantity);

            await ApplyStockDeltaAsync(connection, transaction, productId, delta, 1, "Reapprovisionnement stock faible", null, adminId, cancellationToken);
        }

        await WriteAuditAsync(connection, transaction, adminId, "RESTOCK_LOW_PRODUCTS", "stock_levels", null, $"{lowProducts.Count} produit(s) reapprovisionne(s)", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, $"{lowProducts.Count} produit(s) sous seuil reapprovisionne(s).");
    }

    public async Task<OperationResult> AdjustStockAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var productId = await ScalarLongAsync(
            connection,
            "SELECT id FROM products WHERE is_active = 1 AND is_stock_managed = 1 ORDER BY id LIMIT 1;",
            null,
            cancellationToken);

        if (productId == 0)
        {
            return new OperationResult(false, "Aucun produit stockable disponible.");
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ApplyStockDeltaAsync(connection, transaction, productId, 5, 4, "Ajustement inventaire +5", null, adminId, cancellationToken);
        await WriteAuditAsync(connection, transaction, adminId, "ADJUST_STOCK", "products", productId, "Ajustement +5", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, "Stock ajuste de +5 sur le premier produit stockable.");
    }

    public async Task<OperationResult> CheckStockAlertsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await ScalarLongAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM products p
            LEFT JOIN stock_levels sl ON sl.product_id = p.id
            WHERE p.is_active = 1
              AND p.is_stock_managed = 1
              AND COALESCE(sl.quantity, 0) <= p.low_stock_threshold;
            """,
            null,
            cancellationToken);

        return new OperationResult(true, count == 0 ? "Aucune alerte stock faible." : $"{count} alerte(s) stock faible detectee(s).");
    }

    public Task<OperationResult> RecordExpenseAsync(CancellationToken cancellationToken = default)
    {
        return InsertExpenseAsync(750, paidFromCashRegister: false, cancellationToken);
    }

    public Task<OperationResult> RecordCashExpenseAsync(CancellationToken cancellationToken = default)
    {
        return InsertExpenseAsync(500, paidFromCashRegister: true, cancellationToken);
    }

    public async Task<OperationResult> GenerateDailyReportAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var start = DateTime.Now.Date.ToString("O", CultureInfo.InvariantCulture);
        var end = DateTime.Now.Date.AddDays(1).ToString("O", CultureInfo.InvariantCulture);
        var salesTotal = await ScalarLongAsync(
            connection,
            "SELECT COALESCE(SUM(total_amount), 0) FROM sales WHERE status = 1 AND sale_date >= $start AND sale_date < $end;",
            p =>
            {
                p.AddWithValue("$start", start);
                p.AddWithValue("$end", end);
            },
            cancellationToken);
        var ticketCount = await ScalarLongAsync(
            connection,
            "SELECT COUNT(1) FROM sales WHERE status = 1 AND sale_date >= $start AND sale_date < $end;",
            p =>
            {
                p.AddWithValue("$start", start);
                p.AddWithValue("$end", end);
            },
            cancellationToken);

        return new OperationResult(true, $"Rapport du jour : {ticketCount} ticket(s), total {Money(salesTotal)}.");
    }

    public async Task<OperationResult> ExportDailyReportAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await GetReportRowsAsync(connection, cancellationToken);
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LP2M_Bar_Mngt", "exports");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, $"rapport-{DateTime.Now:yyyyMMdd-HHmmss}.csv");

        var builder = new StringBuilder();
        builder.AppendLine("Section;Element;Valeur;Details");
        foreach (var row in rows)
        {
            builder.AppendLine($"{Escape(row.Section)};{Escape(row.Item)};{Escape(row.Value)};{Escape(row.Details)}");
        }

        await File.WriteAllTextAsync(filePath, builder.ToString(), Encoding.UTF8, cancellationToken);
        return new OperationResult(true, $"Rapport exporte : {filePath}");
    }

    public async Task<OperationResult> AddCashierUserAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var cashierRoleId = await ScalarLongAsync(connection, "SELECT id FROM roles WHERE name = 'Caissier' LIMIT 1;", null, cancellationToken);
        var suffix = DateTime.Now.ToString("HHmmssfff", CultureInfo.InvariantCulture);
        var username = $"caissier{suffix}";
        var passwordHash = _passwordHasher.HashPassword("caissier123");

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO users (username, password_hash, full_name, role_id, is_active, created_at)
            VALUES ($username, $passwordHash, $fullName, $roleId, 1, $createdAt);
            """,
            parameters =>
            {
                parameters.AddWithValue("$username", username);
                parameters.AddWithValue("$passwordHash", passwordHash);
                parameters.AddWithValue("$fullName", $"Caissier {suffix}");
                parameters.AddWithValue("$roleId", cashierRoleId);
                parameters.AddWithValue("$createdAt", Now());
            },
            cancellationToken);
        await WriteAuditAsync(connection, transaction, adminId, "ADD_USER", "users", await LastInsertRowIdAsync(connection, transaction, cancellationToken), username, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, $"Utilisateur cree : {username} / mot de passe temporaire caissier123.");
    }

    public async Task<OperationResult> ResetAdminPasswordAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var hash = _passwordHasher.HashPassword("admin123");

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            "UPDATE users SET password_hash = $hash WHERE username = 'admin';",
            p => p.AddWithValue("$hash", hash),
            cancellationToken);
        await WriteAuditAsync(connection, transaction, adminId, "RESET_ADMIN_PASSWORD", "users", adminId, "Reset mot de passe admin", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, "Mot de passe admin reinitialise a admin123.");
    }

    public async Task<OperationResult> WriteAuditEntryAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await WriteAuditAsync(connection, transaction, adminId, "AUDIT_CHECK", "audit_logs", null, "Verification manuelle depuis le module Utilisateurs", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, "Entree d'audit ajoutee.");
    }

    private async Task<OperationResult> InsertExpenseAsync(long amountCents, bool paidFromCashRegister, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var categoryId = await EnsureExpenseCategoryAsync(connection, "Autres", cancellationToken);
        long? sessionId = null;

        if (paidFromCashRegister)
        {
            var openSessionId = await GetOpenCashSessionIdAsync(connection, adminId, cancellationToken);
            if (openSessionId == 0)
            {
                return new OperationResult(false, "Ouvre une session de caisse avant d'enregistrer une depense payee depuis la caisse.");
            }

            sessionId = openSessionId;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO expenses
                (category_id, cash_session_id, user_id, amount, description, expense_date, paid_from_cash_register, created_at, status)
            VALUES
                ($categoryId, $cashSessionId, $userId, $amount, $description, $expenseDate, $paidFromCash, $createdAt, 'VALIDATED');
            """,
            parameters =>
            {
                parameters.AddWithValue("$categoryId", categoryId);
                parameters.AddWithValue("$cashSessionId", (object?)sessionId ?? DBNull.Value);
                parameters.AddWithValue("$userId", adminId);
                parameters.AddWithValue("$amount", amountCents);
                parameters.AddWithValue("$description", paidFromCashRegister ? "Depense caisse rapide" : "Depense fournisseur rapide");
                parameters.AddWithValue("$expenseDate", Now());
                parameters.AddWithValue("$paidFromCash", paidFromCashRegister ? 1 : 0);
                parameters.AddWithValue("$createdAt", Now());
            },
            cancellationToken);

        var expenseId = await LastInsertRowIdAsync(connection, transaction, cancellationToken);
        if (paidFromCashRegister && sessionId is not null)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO cash_movements
                    (cash_session_id, movement_type, amount, description, user_id, created_at)
                VALUES
                    ($sessionId, 2, $amount, $description, $userId, $createdAt);
                """,
                parameters =>
                {
                    parameters.AddWithValue("$sessionId", sessionId.Value);
                    parameters.AddWithValue("$amount", amountCents);
                    parameters.AddWithValue("$description", "Depense payee depuis la caisse");
                    parameters.AddWithValue("$userId", adminId);
                    parameters.AddWithValue("$createdAt", Now());
                },
                cancellationToken);
        }

        await WriteAuditAsync(connection, transaction, adminId, "ADD_EXPENSE", "expenses", expenseId, $"Depense {Money(amountCents)}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, paidFromCashRegister
            ? $"Depense caisse enregistree : {Money(amountCents)}."
            : $"Depense fournisseur enregistree : {Money(amountCents)}.");
    }

    private static async Task<IReadOnlyList<ModuleRowDto>> GetDashboardRowsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<ModuleRowDto>();
        var todaySales = await ScalarLongAsync(connection, "SELECT COALESCE(SUM(total_amount), 0) FROM sales WHERE status = 1 AND sale_date >= $start;", p => p.AddWithValue("$start", DateTime.Now.Date.ToString("O")), cancellationToken);
        var products = await ScalarLongAsync(connection, "SELECT COUNT(1) FROM products WHERE is_active = 1;", null, cancellationToken);
        var users = await ScalarLongAsync(connection, "SELECT COUNT(1) FROM users WHERE is_active = 1;", null, cancellationToken);

        rows.Add(new ModuleRowDto("Resume", "Ventes du jour", Money(todaySales), "Total des ventes validees depuis minuit"));
        rows.Add(new ModuleRowDto("Resume", "Produits actifs", products.ToString(CultureInfo.InvariantCulture), "Catalogue disponible a la vente"));
        rows.Add(new ModuleRowDto("Resume", "Utilisateurs actifs", users.ToString(CultureInfo.InvariantCulture), "Comptes autorises"));
        return rows;
    }

    private static Task<IReadOnlyList<ModuleRowDto>> GetCashRowsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        return QueryModuleRowsAsync(
            connection,
            """
            SELECT
                CASE cs.status WHEN 1 THEN 'Ouverte' WHEN 2 THEN 'Cloturee' ELSE 'Annulee' END AS section,
                u.full_name AS item,
                printf('%.2f', cs.expected_closing_amount / 100.0) AS value,
                'Ouverture ' || strftime('%d/%m/%Y %H:%M', cs.opened_at) AS details
            FROM cash_sessions cs
            INNER JOIN users u ON u.id = cs.cashier_id
            ORDER BY cs.opened_at DESC
            LIMIT 12;
            """,
            cancellationToken);
    }

    private static Task<IReadOnlyList<ModuleRowDto>> GetSalesRowsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        return QueryModuleRowsAsync(
            connection,
            """
            SELECT
                CASE s.status WHEN 1 THEN 'Validee' ELSE 'Annulee' END AS section,
                s.ticket_number AS item,
                printf('%.2f', s.total_amount / 100.0) AS value,
                u.full_name || ' - ' || strftime('%d/%m/%Y %H:%M', s.sale_date) AS details
            FROM sales s
            INNER JOIN users u ON u.id = s.cashier_id
            ORDER BY s.sale_date DESC
            LIMIT 12;
            """,
            cancellationToken);
    }

    private static Task<IReadOnlyList<ModuleRowDto>> GetProductRowsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        return QueryModuleRowsAsync(
            connection,
            """
            SELECT
                c.name AS section,
                p.name AS item,
                printf('%.2f', p.sale_price / 100.0) AS value,
                CASE p.is_stock_managed WHEN 1 THEN 'Stock gere - seuil ' || p.low_stock_threshold ELSE 'Non stockable' END AS details
            FROM products p
            INNER JOIN product_categories c ON c.id = p.category_id
            WHERE p.is_active = 1
            ORDER BY p.name
            LIMIT 20;
            """,
            cancellationToken);
    }

    private static Task<IReadOnlyList<ModuleRowDto>> GetStockRowsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        return QueryModuleRowsAsync(
            connection,
            """
            SELECT
                CASE WHEN COALESCE(sl.quantity, 0) <= p.low_stock_threshold THEN 'Alerte' ELSE 'OK' END AS section,
                p.name AS item,
                printf('%.0f', COALESCE(sl.quantity, 0)) AS value,
                'Seuil ' || p.low_stock_threshold || ' - ' || c.name AS details
            FROM products p
            INNER JOIN product_categories c ON c.id = p.category_id
            LEFT JOIN stock_levels sl ON sl.product_id = p.id
            WHERE p.is_active = 1 AND p.is_stock_managed = 1
            ORDER BY COALESCE(sl.quantity, 0) ASC, p.name
            LIMIT 20;
            """,
            cancellationToken);
    }

    private static Task<IReadOnlyList<ModuleRowDto>> GetExpenseRowsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        return QueryModuleRowsAsync(
            connection,
            """
            SELECT
                ec.name AS section,
                e.description AS item,
                printf('%.2f', e.amount / 100.0) AS value,
                CASE e.paid_from_cash_register WHEN 1 THEN 'Payee depuis caisse' ELSE 'Hors caisse' END || ' - ' || strftime('%d/%m/%Y %H:%M', e.expense_date) AS details
            FROM expenses e
            INNER JOIN expense_categories ec ON ec.id = e.category_id
            ORDER BY e.expense_date DESC
            LIMIT 12;
            """,
            cancellationToken);
    }

    private static async Task<IReadOnlyList<ModuleRowDto>> GetReportRowsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<ModuleRowDto>();
        var start = DateTime.Now.Date.ToString("O", CultureInfo.InvariantCulture);
        var end = DateTime.Now.Date.AddDays(1).ToString("O", CultureInfo.InvariantCulture);

        var salesTotal = await ScalarLongAsync(connection, "SELECT COALESCE(SUM(total_amount), 0) FROM sales WHERE status = 1 AND sale_date >= $start AND sale_date < $end;", p => { p.AddWithValue("$start", start); p.AddWithValue("$end", end); }, cancellationToken);
        var expensesTotal = await ScalarLongAsync(connection, "SELECT COALESCE(SUM(amount), 0) FROM expenses WHERE expense_date >= $start AND expense_date < $end;", p => { p.AddWithValue("$start", start); p.AddWithValue("$end", end); }, cancellationToken);
        var tickets = await ScalarLongAsync(connection, "SELECT COUNT(1) FROM sales WHERE status = 1 AND sale_date >= $start AND sale_date < $end;", p => { p.AddWithValue("$start", start); p.AddWithValue("$end", end); }, cancellationToken);
        var lowStock = await ScalarLongAsync(connection, "SELECT COUNT(1) FROM products p LEFT JOIN stock_levels sl ON sl.product_id = p.id WHERE p.is_active = 1 AND p.is_stock_managed = 1 AND COALESCE(sl.quantity, 0) <= p.low_stock_threshold;", null, cancellationToken);

        rows.Add(new ModuleRowDto("Jour", "Chiffre d'affaires", Money(salesTotal), "Ventes validees"));
        rows.Add(new ModuleRowDto("Jour", "Tickets", tickets.ToString(CultureInfo.InvariantCulture), "Nombre de tickets valides"));
        rows.Add(new ModuleRowDto("Jour", "Depenses", Money(expensesTotal), "Depenses enregistrees"));
        rows.Add(new ModuleRowDto("Stock", "Alertes", lowStock.ToString(CultureInfo.InvariantCulture), "Produits sous seuil"));
        return rows;
    }

    private static Task<IReadOnlyList<ModuleRowDto>> GetUserRowsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        return QueryModuleRowsAsync(
            connection,
            """
            SELECT
                r.name AS section,
                u.username AS item,
                CASE u.is_active WHEN 1 THEN 'Actif' ELSE 'Inactif' END AS value,
                u.full_name || ' - cree le ' || strftime('%d/%m/%Y', u.created_at) AS details
            FROM users u
            INNER JOIN roles r ON r.id = u.role_id
            ORDER BY u.username
            LIMIT 20;
            """,
            cancellationToken);
    }

    private static async Task<IReadOnlyList<ModuleRowDto>> QueryModuleRowsAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        var result = new List<ModuleRowDto>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ModuleRowDto(
                reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3)));
        }

        return result;
    }

    private static async Task<List<Dictionary<string, object>>> QueryRowsAsync(
        SqliteConnection connection,
        string sql,
        Action<SqliteParameterCollection>? bind,
        CancellationToken cancellationToken)
    {
        var result = new List<Dictionary<string, object>>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind?.Invoke(command.Parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index)] = reader.IsDBNull(index) ? DBNull.Value : reader.GetValue(index);
            }

            result.Add(row);
        }

        return result;
    }

    private static async Task<Dictionary<string, object>> QuerySingleRowAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        Action<SqliteParameterCollection>? bind,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        bind?.Invoke(command.Parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return new Dictionary<string, object>();
        }

        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < reader.FieldCount; index++)
        {
            row[reader.GetName(index)] = reader.IsDBNull(index) ? DBNull.Value : reader.GetValue(index);
        }

        return row;
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        Action<SqliteParameterCollection>? bind,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        bind?.Invoke(command.Parameters);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> ScalarLongAsync(
        SqliteConnection connection,
        string sql,
        Action<SqliteParameterCollection>? bind,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind?.Invoke(command.Parameters);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async Task<long> LastInsertRowIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT last_insert_rowid();";

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async Task<string?> ScalarStringAsync(
        SqliteConnection connection,
        string sql,
        Action<SqliteParameterCollection>? bind,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind?.Invoke(command.Parameters);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToString(result, CultureInfo.InvariantCulture);
    }

    private static async Task<long> GetAdminUserIdAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        return await ScalarLongAsync(connection, "SELECT id FROM users WHERE username = 'admin' LIMIT 1;", null, cancellationToken);
    }

    private static async Task<long> GetOpenCashSessionIdAsync(SqliteConnection connection, long cashierId, CancellationToken cancellationToken)
    {
        return await ScalarLongAsync(
            connection,
            "SELECT id FROM cash_sessions WHERE cashier_id = $cashierId AND status = 1 ORDER BY opened_at DESC LIMIT 1;",
            p => p.AddWithValue("$cashierId", cashierId),
            cancellationToken);
    }

    private static async Task<long> EnsureCategoryAsync(SqliteConnection connection, string categoryName, CancellationToken cancellationToken)
    {
        var id = await ScalarLongAsync(connection, "SELECT id FROM product_categories WHERE name = $name LIMIT 1;", p => p.AddWithValue("$name", categoryName), cancellationToken);
        if (id > 0)
        {
            return id;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, transaction, "INSERT INTO product_categories (name, is_active) VALUES ($name, 1);", p => p.AddWithValue("$name", categoryName), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LastInsertRowIdAsync(connection, transaction, cancellationToken);
    }

    private static async Task<long> EnsureExpenseCategoryAsync(SqliteConnection connection, string categoryName, CancellationToken cancellationToken)
    {
        var id = await ScalarLongAsync(connection, "SELECT id FROM expense_categories WHERE name = $name LIMIT 1;", p => p.AddWithValue("$name", categoryName), cancellationToken);
        if (id > 0)
        {
            return id;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, transaction, "INSERT INTO expense_categories (name, is_active) VALUES ($name, 1);", p => p.AddWithValue("$name", categoryName), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LastInsertRowIdAsync(connection, transaction, cancellationToken);
    }

    private static async Task<ProductForSale?> GetSellableProductAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var row = await QuerySingleRowAsync(
            connection,
            null,
            """
            SELECT p.id, p.name, p.sale_price, p.is_stock_managed, COALESCE(sl.quantity, 0) AS quantity
            FROM products p
            LEFT JOIN stock_levels sl ON sl.product_id = p.id
            WHERE p.is_active = 1
              AND (p.is_stock_managed = 0 OR COALESCE(sl.quantity, 0) > 0)
            ORDER BY p.is_stock_managed DESC, p.id
            LIMIT 1;
            """,
            null,
            cancellationToken);

        if (row.Count == 0)
        {
            return null;
        }

        return new ProductForSale(
            Convert.ToInt64(row["id"], CultureInfo.InvariantCulture),
            Convert.ToString(row["name"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToInt64(row["sale_price"], CultureInfo.InvariantCulture),
            Convert.ToInt64(row["is_stock_managed"], CultureInfo.InvariantCulture) == 1);
    }

    private static async Task ApplyStockDeltaAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long productId,
        double delta,
        int movementType,
        string reason,
        long? saleId,
        long userId,
        CancellationToken cancellationToken,
        bool updateLevel = true)
    {
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO stock_movements
                (product_id, movement_type, quantity, reason, sale_id, user_id, created_at)
            VALUES
                ($productId, $movementType, $quantity, $reason, $saleId, $userId, $createdAt);
            """,
            parameters =>
            {
                parameters.AddWithValue("$productId", productId);
                parameters.AddWithValue("$movementType", movementType);
                parameters.AddWithValue("$quantity", Math.Abs(delta));
                parameters.AddWithValue("$reason", reason);
                parameters.AddWithValue("$saleId", (object?)saleId ?? DBNull.Value);
                parameters.AddWithValue("$userId", userId);
                parameters.AddWithValue("$createdAt", Now());
            },
            cancellationToken);

        if (!updateLevel)
        {
            return;
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO stock_levels (product_id, quantity)
            VALUES ($productId, $delta)
            ON CONFLICT(product_id) DO UPDATE SET quantity = quantity + $delta;
            """,
            parameters =>
            {
                parameters.AddWithValue("$productId", productId);
                parameters.AddWithValue("$delta", delta);
            },
            cancellationToken);
    }

    private static Task WriteAuditAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long? userId,
        string action,
        string entityType,
        long? entityId,
        string details,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO audit_logs (user_id, action, entity_type, entity_id, details, created_at)
            VALUES ($userId, $action, $entityType, $entityId, $details, $createdAt);
            """,
            parameters =>
            {
                parameters.AddWithValue("$userId", (object?)userId ?? DBNull.Value);
                parameters.AddWithValue("$action", action);
                parameters.AddWithValue("$entityType", entityType);
                parameters.AddWithValue("$entityId", (object?)entityId ?? DBNull.Value);
                parameters.AddWithValue("$details", details);
                parameters.AddWithValue("$createdAt", Now());
            },
            cancellationToken);
    }

    private static string Now()
    {
        return DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
    }

    private static string Money(long cents)
    {
        return (cents / 100m).ToString("N2", CultureInfo.CurrentCulture);
    }

    private static string Escape(string value)
    {
        return value.Replace("\"", "\"\"", StringComparison.Ordinal).Replace(";", ",", StringComparison.Ordinal);
    }

    private sealed record ProductForSale(long Id, string Name, long SalePriceCents, bool IsStockManaged);
}
