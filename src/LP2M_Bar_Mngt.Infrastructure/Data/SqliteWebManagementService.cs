using System.Globalization;
using LP2M_Bar_Mngt.Application.Abstractions;
using LP2M_Bar_Mngt.Application.DTOs;
using LP2M_Bar_Mngt.Infrastructure.Security;
using Microsoft.Data.Sqlite;

namespace LP2M_Bar_Mngt.Infrastructure.Data;

public sealed class SqliteWebManagementService : IWebManagementService
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly PasswordHasher _passwordHasher;
    private readonly TotpService _totpService;

    public SqliteWebManagementService(SqliteConnectionFactory connectionFactory, PasswordHasher passwordHasher, TotpService totpService)
    {
        _connectionFactory = connectionFactory;
        _passwordHasher = passwordHasher;
        _totpService = totpService;
    }

    public async Task<WebDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var start = DateTime.Now.Date.ToString("O", CultureInfo.InvariantCulture);
        var end = DateTime.Now.Date.AddDays(1).ToString("O", CultureInfo.InvariantCulture);

        var todayRevenue = await ScalarLongAsync(
            connection,
            "SELECT COALESCE(SUM(total_amount), 0) FROM sales WHERE status = 1 AND is_hidden = 0 AND sale_date >= $start AND sale_date < $end;",
            p => BindPeriod(p, start, end),
            cancellationToken);

        var todayTickets = await ScalarLongAsync(
            connection,
            "SELECT COUNT(1) FROM sales WHERE status = 1 AND is_hidden = 0 AND sale_date >= $start AND sale_date < $end;",
            p => BindPeriod(p, start, end),
            cancellationToken);

        var openSessions = await ScalarLongAsync(connection, "SELECT COUNT(1) FROM cash_sessions WHERE status = 1 AND is_hidden = 0;", null, cancellationToken);
        var lowStock = await ScalarLongAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM products p
            LEFT JOIN stock_levels sl ON sl.product_id = p.id
            WHERE p.is_active = 1 AND p.is_hidden = 0 AND p.is_stock_managed = 1 AND COALESCE(sl.quantity, 0) <= p.low_stock_threshold;
            """,
            null,
            cancellationToken);
        var products = await ScalarLongAsync(connection, "SELECT COUNT(1) FROM products WHERE is_active = 1 AND is_hidden = 0;", null, cancellationToken);
        var users = await ScalarLongAsync(connection, "SELECT COUNT(1) FROM users WHERE is_active = 1 AND is_hidden = 0;", null, cancellationToken);
        var todayExpenses = await ScalarLongAsync(
            connection,
            "SELECT COALESCE(SUM(amount), 0) FROM expenses WHERE is_hidden = 0 AND expense_date >= $start AND expense_date < $end;",
            p => BindPeriod(p, start, end),
            cancellationToken);
        var openCashBalance = await ScalarLongAsync(
            connection,
            "SELECT COALESCE(SUM(expected_closing_amount), 0) FROM cash_sessions WHERE status = 1 AND is_hidden = 0;",
            null,
            cancellationToken);
        var grossMargin = await ScalarLongAsync(
            connection,
            """
            SELECT COALESCE(SUM(si.total_amount - CAST(ROUND(p.cost_price * si.quantity) AS INTEGER)), 0)
            FROM sale_items si
            INNER JOIN sales s ON s.id = si.sale_id
            INNER JOIN products p ON p.id = si.product_id
            WHERE s.status = 1 AND s.is_hidden = 0 AND s.sale_date >= $start AND s.sale_date < $end;
            """,
            p => BindPeriod(p, start, end),
            cancellationToken);
        var chartStart = DateTime.Now.Date.AddDays(-6);

        return new WebDashboardDto(
            CentsToDecimal(todayRevenue),
            (int)todayTickets,
            (int)openSessions,
            (int)lowStock,
            (int)products,
            (int)users,
            CentsToDecimal(todayExpenses),
            CentsToDecimal(openCashBalance),
            CentsToDecimal(grossMargin - todayExpenses),
            await GetSalesChartAsync(connection, chartStart, DateTime.Now.Date.AddDays(1), cancellationToken),
            await GetTopProductsAsync(connection, DateTime.Now.Date.AddDays(-30), DateTime.Now.Date.AddDays(1), cancellationToken));
    }

    public async Task<WebDataSetDto> GetDataSetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return new WebDataSetDto(
            await GetBusinessProfileAsync(cancellationToken),
            await GetDashboardAsync(cancellationToken),
            await GetCategoriesAsync(connection, "product_categories", cancellationToken),
            await GetCategoriesAsync(connection, "expense_categories", cancellationToken),
            await GetRolesAsync(connection, cancellationToken),
            await GetProductsAsync(connection, cancellationToken),
            await GetStockAsync(connection, cancellationToken),
            await GetExpensesAsync(connection, cancellationToken),
            await GetUsersAsync(connection, cancellationToken),
            await GetSalesAsync(connection, cancellationToken),
            await GetCashSessionsAsync(connection, cancellationToken));
    }

    public async Task<BusinessProfileDto> GetBusinessProfileAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name, sigle, address, contact, logo_data, cover_image_data, ticket_footer
            FROM business_profile
            WHERE id = 1;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return DefaultBusinessProfile();
        }

        return new BusinessProfileDto(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetString(6));
    }

    public async Task<BusinessProfileDto> SaveBusinessProfileAsync(BusinessProfileSaveRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Name, "Le nom du bar est obligatoire.");
        ValidateRequired(request.Sigle, "Le sigle est obligatoire.");

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO business_profile
                (id, name, sigle, address, contact, logo_data, cover_image_data, ticket_footer)
            VALUES
                (1, $name, $sigle, $address, $contact, $logoData, $coverData, $ticketFooter)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                sigle = excluded.sigle,
                address = excluded.address,
                contact = excluded.contact,
                logo_data = excluded.logo_data,
                cover_image_data = excluded.cover_image_data,
                ticket_footer = excluded.ticket_footer;
            """,
            p =>
            {
                p.AddWithValue("$name", request.Name.Trim());
                p.AddWithValue("$sigle", request.Sigle.Trim());
                p.AddWithValue("$address", (request.Address ?? string.Empty).Trim());
                p.AddWithValue("$contact", (request.Contact ?? string.Empty).Trim());
                p.AddWithValue("$logoData", string.IsNullOrWhiteSpace(request.LogoData) ? DBNull.Value : request.LogoData);
                p.AddWithValue("$coverData", string.IsNullOrWhiteSpace(request.CoverImageData) ? DBNull.Value : request.CoverImageData);
                p.AddWithValue("$ticketFooter", string.IsNullOrWhiteSpace(request.TicketFooter) ? "Merci pour votre visite." : request.TicketFooter.Trim());
            },
            cancellationToken);

        await WriteAuditAsync(connection, transaction, adminId, "SAVE_BUSINESS_PROFILE_WEB", "business_profile", 1, request.Name, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetBusinessProfileAsync(cancellationToken);
    }

    public async Task<TicketDto> GetTicketAsync(long? saleId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var resolvedSaleId = saleId is > 0
            ? saleId.Value
            : await ScalarLongAsync(connection, "SELECT id FROM sales WHERE is_hidden = 0 ORDER BY sale_date DESC LIMIT 1;", null, cancellationToken);

        if (resolvedSaleId <= 0)
        {
            throw new InvalidOperationException("Aucun ticket disponible.");
        }

        var sale = await QuerySingleRowAsync(
            connection,
            null,
            """
            SELECT
                s.id, s.ticket_number, u.full_name, s.sale_date,
                COALESCE(NULLIF(s.customer_name, ''), 'Client comptoir') AS customer_name,
                CASE s.payment_method WHEN 1 THEN 'Especes' WHEN 2 THEN 'Carte' WHEN 3 THEN 'Mobile money' ELSE 'Credit client' END AS payment_method,
                s.subtotal_amount, s.discount_amount, s.total_amount
            FROM sales s
            INNER JOIN users u ON u.id = s.cashier_id
            WHERE s.id = $saleId;
            """,
            p => p.AddWithValue("$saleId", resolvedSaleId),
            cancellationToken);

        if (sale.Count == 0)
        {
            throw new InvalidOperationException("Ticket introuvable.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT product_name, quantity, unit_price, discount_amount, total_amount
            FROM sale_items
            WHERE sale_id = $saleId
            ORDER BY id;
            """;
        command.Parameters.AddWithValue("$saleId", resolvedSaleId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<TicketItemDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new TicketItemDto(
                reader.GetString(0),
                reader.GetDouble(1),
                CentsToDecimal(reader.GetInt64(2)),
                CentsToDecimal(reader.GetInt64(3)),
                CentsToDecimal(reader.GetInt64(4))));
        }

        return new TicketDto(
            await GetBusinessProfileAsync(cancellationToken),
            Convert.ToInt64(sale["id"], CultureInfo.InvariantCulture),
            Convert.ToString(sale["ticket_number"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(sale["full_name"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(sale["customer_name"], CultureInfo.InvariantCulture) ?? "Client comptoir",
            DateTime.Parse(Convert.ToString(sale["sale_date"], CultureInfo.InvariantCulture) ?? string.Empty, CultureInfo.InvariantCulture),
            Convert.ToString(sale["payment_method"], CultureInfo.InvariantCulture) ?? string.Empty,
            CentsToDecimal(Convert.ToInt64(sale["subtotal_amount"], CultureInfo.InvariantCulture)),
            CentsToDecimal(Convert.ToInt64(sale["discount_amount"], CultureInfo.InvariantCulture)),
            CentsToDecimal(Convert.ToInt64(sale["total_amount"], CultureInfo.InvariantCulture)),
            items);
    }

    public async Task<OperationResult> OpenCashSessionAsync(CashOpenRequest request, CancellationToken cancellationToken = default)
    {
        if (request.CashierId <= 0)
        {
            throw new InvalidOperationException("Le caissier est obligatoire.");
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var cashierName = await GetActiveUserNameAsync(connection, request.CashierId, cancellationToken);
        if (string.IsNullOrWhiteSpace(cashierName))
        {
            throw new InvalidOperationException("Le caissier selectionne est introuvable ou inactif.");
        }

        var existingSession = await ScalarLongAsync(
            connection,
            "SELECT id FROM cash_sessions WHERE cashier_id = $cashierId AND status = 1 LIMIT 1;",
            p => p.AddWithValue("$cashierId", request.CashierId),
            cancellationToken);

        if (existingSession > 0)
        {
            return new OperationResult(false, "Une session de caisse est deja ouverte pour ce caissier.");
        }

        var openingAmount = DecimalToCents(request.OpeningAmount);
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
            p =>
            {
                p.AddWithValue("$cashierId", request.CashierId);
                p.AddWithValue("$openedAt", Now());
                p.AddWithValue("$openingAmount", openingAmount);
            },
            cancellationToken);

        var sessionId = await LastInsertRowIdAsync(connection, transaction, cancellationToken);
        await WriteAuditAsync(connection, transaction, adminId, "OPEN_CASH_SESSION_WEB", "cash_sessions", sessionId, cashierName, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, $"Session ouverte pour {cashierName}.");
    }

    public async Task<OperationResult> CloseCashSessionAsync(CashCloseRequest request, CancellationToken cancellationToken = default)
    {
        if (request.CashSessionId <= 0)
        {
            throw new InvalidOperationException("La session de caisse est obligatoire.");
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var session = await QuerySingleRowAsync(
            connection,
            null,
            """
            SELECT cs.id, cs.opening_amount, cs.status, u.full_name
            FROM cash_sessions cs
            INNER JOIN users u ON u.id = cs.cashier_id
            WHERE cs.id = $sessionId;
            """,
            p => p.AddWithValue("$sessionId", request.CashSessionId),
            cancellationToken);

        if (session.Count == 0)
        {
            throw new InvalidOperationException("La session de caisse est introuvable.");
        }

        if (Convert.ToInt64(session["status"], CultureInfo.InvariantCulture) != 1)
        {
            return new OperationResult(false, "Cette session de caisse est deja cloturee.");
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
            p => p.AddWithValue("$sessionId", request.CashSessionId),
            cancellationToken);

        var declaredAmount = DecimalToCents(request.DeclaredClosingAmount);
        var differenceAmount = declaredAmount - expectedClosingAmount;
        var cashierName = Convert.ToString(session["full_name"], CultureInfo.InvariantCulture) ?? string.Empty;

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE cash_sessions
            SET closed_at = $closedAt,
                expected_closing_amount = $expectedAmount,
                declared_closing_amount = $declaredAmount,
                difference_amount = $differenceAmount,
                status = 2
            WHERE id = $sessionId;
            """,
            p =>
            {
                p.AddWithValue("$closedAt", Now());
                p.AddWithValue("$expectedAmount", expectedClosingAmount);
                p.AddWithValue("$declaredAmount", declaredAmount);
                p.AddWithValue("$differenceAmount", differenceAmount);
                p.AddWithValue("$sessionId", request.CashSessionId);
            },
            cancellationToken);

        await WriteAuditAsync(connection, transaction, adminId, "CLOSE_CASH_SESSION_WEB", "cash_sessions", request.CashSessionId, cashierName, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, $"Session cloturee pour {cashierName}. Ecart : {CentsToDecimal(differenceAmount):N2}.");
    }

    public async Task<OperationResult> CreateSaleAsync(SaleCreateRequest request, CancellationToken cancellationToken = default)
    {
        return await CreateSaleCartAsync(
            new SaleCartCreateRequest(
                request.CashSessionId,
                null,
                request.PaymentMethod,
                request.DiscountAmount,
                [new SaleCartItemRequest(request.ProductId, request.Quantity)]),
            cancellationToken);
    }

    public async Task<OperationResult> CreateSaleCartAsync(SaleCartCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request.CashSessionId <= 0)
        {
            throw new InvalidOperationException("La session de caisse est obligatoire.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("Le panier de vente est vide.");
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var session = await QuerySingleRowAsync(
            connection,
            null,
            "SELECT id, cashier_id, status FROM cash_sessions WHERE id = $sessionId AND is_hidden = 0;",
            p => p.AddWithValue("$sessionId", request.CashSessionId),
            cancellationToken);

        if (session.Count == 0 || Convert.ToInt64(session["status"], CultureInfo.InvariantCulture) != 1)
        {
            throw new InvalidOperationException("Selectionne une session de caisse ouverte avant de vendre.");
        }

        var requestedItems = request.Items
            .Where(item => item.ProductId > 0 && item.Quantity > 0)
            .GroupBy(item => item.ProductId)
            .Select(group => new SaleCartItemRequest(group.Key, group.Sum(item => item.Quantity)))
            .ToList();
        if (requestedItems.Count == 0)
        {
            throw new InvalidOperationException("Le panier de vente ne contient aucun produit valide.");
        }

        var lines = new List<SaleCartLine>();
        foreach (var item in requestedItems)
        {
            var product = await QuerySingleRowAsync(
                connection,
                null,
                """
                SELECT p.id, p.name, p.sale_price, p.is_stock_managed, COALESCE(sl.quantity, 0) AS quantity
                FROM products p
                LEFT JOIN stock_levels sl ON sl.product_id = p.id
                WHERE p.id = $productId AND p.is_active = 1 AND p.is_hidden = 0;
                """,
                p => p.AddWithValue("$productId", item.ProductId),
                cancellationToken);

            if (product.Count == 0)
            {
                throw new InvalidOperationException("Un produit du panier est introuvable ou inactif.");
            }

            var productName = Convert.ToString(product["name"], CultureInfo.InvariantCulture) ?? string.Empty;
            var unitPrice = Convert.ToInt64(product["sale_price"], CultureInfo.InvariantCulture);
            var isStockManaged = Convert.ToInt64(product["is_stock_managed"], CultureInfo.InvariantCulture) == 1;
            var stockQuantity = Convert.ToDouble(product["quantity"], CultureInfo.InvariantCulture);
            if (isStockManaged && stockQuantity < item.Quantity)
            {
                throw new InvalidOperationException($"Stock insuffisant pour {productName}.");
            }

            var lineTotal = Convert.ToInt64(decimal.Round(unitPrice * (decimal)item.Quantity, 0, MidpointRounding.AwayFromZero));
            lines.Add(new SaleCartLine(item.ProductId, productName, item.Quantity, unitPrice, isStockManaged, lineTotal));
        }

        var subtotal = lines.Sum(line => line.TotalAmount);
        var discount = Math.Max(0, DecimalToCents(request.DiscountAmount));
        if (discount > subtotal)
        {
            throw new InvalidOperationException("La remise ne peut pas depasser le total de la vente.");
        }

        var total = subtotal - discount;
        var cashierId = Convert.ToInt64(session["cashier_id"], CultureInfo.InvariantCulture);
        var customerName = string.IsNullOrWhiteSpace(request.CustomerName) ? "Client comptoir" : request.CustomerName.Trim();
        var ticketNumber = $"LP2M-{DateTime.Now:yyyyMMdd-HHmmssfff}";
        var saleDate = Now();
        var paymentMethod = request.PaymentMethod is >= 1 and <= 4 ? request.PaymentMethod : 1;

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO sales
                (ticket_number, cash_session_id, cashier_id, customer_name, sale_date, subtotal_amount, discount_amount, total_amount, payment_method, status)
            VALUES
                ($ticketNumber, $sessionId, $cashierId, $customerName, $saleDate, $subtotal, $discount, $total, $paymentMethod, 1);
            """,
            p =>
            {
                p.AddWithValue("$ticketNumber", ticketNumber);
                p.AddWithValue("$sessionId", request.CashSessionId);
                p.AddWithValue("$cashierId", cashierId);
                p.AddWithValue("$customerName", customerName);
                p.AddWithValue("$saleDate", saleDate);
                p.AddWithValue("$subtotal", subtotal);
                p.AddWithValue("$discount", discount);
                p.AddWithValue("$total", total);
                p.AddWithValue("$paymentMethod", paymentMethod);
            },
            cancellationToken);

        var saleId = await LastInsertRowIdAsync(connection, transaction, cancellationToken);
        foreach (var line in lines)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO sale_items
                    (sale_id, product_id, product_name, quantity, unit_price, discount_amount, total_amount)
                VALUES
                    ($saleId, $productId, $productName, $quantity, $unitPrice, 0, $total);
                """,
                p =>
                {
                    p.AddWithValue("$saleId", saleId);
                    p.AddWithValue("$productId", line.ProductId);
                    p.AddWithValue("$productName", line.ProductName);
                    p.AddWithValue("$quantity", line.Quantity);
                    p.AddWithValue("$unitPrice", line.UnitPrice);
                    p.AddWithValue("$total", line.TotalAmount);
                },
                cancellationToken);
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO cash_movements
                (cash_session_id, movement_type, amount, description, user_id, created_at)
            VALUES
                ($sessionId, 1, $amount, $description, $userId, $createdAt);
            """,
            p =>
            {
                p.AddWithValue("$sessionId", request.CashSessionId);
                p.AddWithValue("$amount", total);
                p.AddWithValue("$description", $"Paiement ticket {ticketNumber}");
                p.AddWithValue("$userId", cashierId);
                p.AddWithValue("$createdAt", saleDate);
            },
            cancellationToken);

        foreach (var line in lines.Where(line => line.IsStockManaged))
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO stock_levels (product_id, quantity)
                VALUES ($productId, $delta)
                ON CONFLICT(product_id) DO UPDATE SET quantity = quantity + $delta;
                """,
                p =>
                {
                    p.AddWithValue("$productId", line.ProductId);
                    p.AddWithValue("$delta", -line.Quantity);
                },
                cancellationToken);

            await InsertStockMovementAsync(connection, transaction, line.ProductId, 2, line.Quantity, $"Vente {ticketNumber}", saleId, cashierId, cancellationToken);
        }

        await WriteAuditAsync(connection, transaction, cashierId, "CREATE_SALE_CART_WEB", "sales", saleId, $"{ticketNumber} - {customerName}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, $"Vente panier validee pour {customerName} : ticket {ticketNumber}.");
    }

    public async Task<ProductDto> SaveProductAsync(ProductSaveRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Name, "Le nom du produit est obligatoire.");
        if (request.CategoryId <= 0)
        {
            throw new InvalidOperationException("La categorie du produit est obligatoire.");
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var salePrice = DecimalToCents(request.SalePrice);
        var costPrice = DecimalToCents(request.CostPrice);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        long productId;
        if (request.Id is > 0)
        {
            productId = request.Id.Value;
            await ExecuteAsync(
                connection,
                transaction,
                """
                UPDATE products
                SET category_id = $categoryId,
                    name = $name,
                    sku = $sku,
                    barcode = $barcode,
                    sale_price = $salePrice,
                    cost_price = $costPrice,
                    is_stock_managed = $isStockManaged,
                    low_stock_threshold = $lowStockThreshold,
                    is_active = $isActive,
                    image_data = $imageData
                WHERE id = $id;
                """,
                p =>
                {
                    BindProduct(p, request, salePrice, costPrice);
                    p.AddWithValue("$id", productId);
                },
                cancellationToken);
        }
        else
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO products
                    (category_id, name, sku, barcode, sale_price, cost_price, is_stock_managed, low_stock_threshold, is_active, image_data, created_at)
                VALUES
                    ($categoryId, $name, $sku, $barcode, $salePrice, $costPrice, $isStockManaged, $lowStockThreshold, $isActive, $imageData, $createdAt);
                """,
                p =>
                {
                    BindProduct(p, request, salePrice, costPrice);
                    p.AddWithValue("$createdAt", Now());
                },
                cancellationToken);

            productId = await LastInsertRowIdAsync(connection, transaction, cancellationToken);
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO stock_levels (product_id, quantity) VALUES ($productId, $quantity);",
                p =>
                {
                    p.AddWithValue("$productId", productId);
                    p.AddWithValue("$quantity", request.IsStockManaged ? request.InitialQuantity : 0);
                },
                cancellationToken);

            if (request.IsStockManaged && request.InitialQuantity > 0)
            {
                await InsertStockMovementAsync(connection, transaction, productId, 1, request.InitialQuantity, "Stock initial via formulaire web", null, adminId, cancellationToken);
            }
        }

        await WriteAuditAsync(connection, transaction, adminId, "SAVE_PRODUCT_WEB", "products", productId, request.Name, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (await GetProductsAsync(connection, cancellationToken)).First(p => p.Id == productId);
    }

    public async Task<CategoryDto> CreateCategoryAsync(CategoryCreateRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Name, "Le nom de categorie est obligatoire.");
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO product_categories (name, is_active, image_data) VALUES ($name, 1, $imageData);",
            p =>
            {
                p.AddWithValue("$name", request.Name.Trim());
                p.AddWithValue("$imageData", string.IsNullOrWhiteSpace(request.ImageData) ? DBNull.Value : request.ImageData);
            },
            cancellationToken);

        var id = await LastInsertRowIdAsync(connection, transaction, cancellationToken);
        await WriteAuditAsync(connection, transaction, adminId, "CREATE_CATEGORY_WEB", "product_categories", id, request.Name, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CategoryDto(id, request.Name.Trim(), true, false, request.ImageData);
    }

    public async Task<StockItemDto> AdjustStockAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProductId <= 0)
        {
            throw new InvalidOperationException("Le produit est obligatoire.");
        }

        if (Math.Abs(request.QuantityDelta) < 0.0001)
        {
            throw new InvalidOperationException("La quantite d'ajustement doit etre differente de zero.");
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var movementType = request.QuantityDelta >= 0 ? 1 : 3;

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO stock_levels (product_id, quantity)
            VALUES ($productId, $delta)
            ON CONFLICT(product_id) DO UPDATE SET quantity = quantity + $delta;
            """,
            p =>
            {
                p.AddWithValue("$productId", request.ProductId);
                p.AddWithValue("$delta", request.QuantityDelta);
            },
            cancellationToken);

        await InsertStockMovementAsync(
            connection,
            transaction,
            request.ProductId,
            movementType,
            Math.Abs(request.QuantityDelta),
            string.IsNullOrWhiteSpace(request.Reason) ? "Ajustement via formulaire web" : request.Reason.Trim(),
            null,
            adminId,
            cancellationToken);
        await WriteAuditAsync(connection, transaction, adminId, "ADJUST_STOCK_WEB", "products", request.ProductId, request.Reason, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (await GetStockAsync(connection, cancellationToken)).First(s => s.ProductId == request.ProductId);
    }

    public async Task<ExpenseDto> CreateExpenseAsync(ExpenseCreateRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Description, "La description de depense est obligatoire.");
        if (request.CategoryId <= 0)
        {
            throw new InvalidOperationException("La categorie de depense est obligatoire.");
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var amount = DecimalToCents(request.Amount);
        long? sessionId = null;

        if (request.PaidFromCashRegister)
        {
            var openSession = await ScalarLongAsync(
                connection,
                "SELECT id FROM cash_sessions WHERE status = 1 AND is_hidden = 0 ORDER BY opened_at DESC LIMIT 1;",
                null,
                cancellationToken);

            if (openSession == 0)
            {
                throw new InvalidOperationException("Aucune session de caisse ouverte pour payer cette depense.");
            }

            sessionId = openSession;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO expenses
                (category_id, cash_session_id, user_id, amount, description, expense_date, paid_from_cash_register, created_at, status)
            VALUES
                ($categoryId, $sessionId, $userId, $amount, $description, $expenseDate, $paidFromCash, $createdAt, 'VALIDATED');
            """,
            p =>
            {
                p.AddWithValue("$categoryId", request.CategoryId);
                p.AddWithValue("$sessionId", (object?)sessionId ?? DBNull.Value);
                p.AddWithValue("$userId", adminId);
                p.AddWithValue("$amount", amount);
                p.AddWithValue("$description", request.Description.Trim());
                p.AddWithValue("$expenseDate", Now());
                p.AddWithValue("$paidFromCash", request.PaidFromCashRegister ? 1 : 0);
                p.AddWithValue("$createdAt", Now());
            },
            cancellationToken);

        var expenseId = await LastInsertRowIdAsync(connection, transaction, cancellationToken);

        if (sessionId is not null)
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
                p =>
                {
                    p.AddWithValue("$sessionId", sessionId.Value);
                    p.AddWithValue("$amount", amount);
                    p.AddWithValue("$description", request.Description.Trim());
                    p.AddWithValue("$userId", adminId);
                    p.AddWithValue("$createdAt", Now());
                },
                cancellationToken);
        }

        await WriteAuditAsync(connection, transaction, adminId, "CREATE_EXPENSE_WEB", "expenses", expenseId, request.Description, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (await GetExpensesAsync(connection, cancellationToken)).First(e => e.Id == expenseId);
    }

    public async Task<UserDto> SaveUserAsync(UserSaveRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Username, "Le nom utilisateur est obligatoire.");
        ValidateRequired(request.FullName, "Le nom complet est obligatoire.");
        if (request.RoleId <= 0)
        {
            throw new InvalidOperationException("Le role est obligatoire.");
        }

        if (request.Id is null or <= 0)
        {
            ValidateRequired(request.Password, "Le mot de passe est obligatoire.");
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        long userId;
        var twoFactorSecret = request.TwoFactorEnabled ? _totpService.GenerateSecret() : null;
        string? twoFactorConfiguredAt = request.TwoFactorEnabled ? Now() : null;
        if (request.Id is > 0)
        {
            userId = request.Id.Value;
            var existingUsername = await ScalarStringAsync(
                connection,
                "SELECT username FROM users WHERE id = $id;",
                p => p.AddWithValue("$id", userId),
                cancellationToken);

            if (string.Equals(existingUsername, "admin", StringComparison.OrdinalIgnoreCase) && !request.IsActive)
            {
                throw new InvalidOperationException("Impossible de desactiver le compte admin.");
            }

            var existingTwoFactorSecret = await ScalarStringAsync(
                connection,
                "SELECT two_factor_secret FROM users WHERE id = $id;",
                p => p.AddWithValue("$id", userId),
                cancellationToken);
            var existingTwoFactorConfiguredAt = await ScalarStringAsync(
                connection,
                "SELECT two_factor_configured_at FROM users WHERE id = $id;",
                p => p.AddWithValue("$id", userId),
                cancellationToken);

            if (request.TwoFactorEnabled && !request.ResetTwoFactorSecret && !string.IsNullOrWhiteSpace(existingTwoFactorSecret))
            {
                twoFactorSecret = existingTwoFactorSecret;
                twoFactorConfiguredAt = existingTwoFactorConfiguredAt;
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    UPDATE users
                    SET username = $username,
                        full_name = $fullName,
                        role_id = $roleId,
                        is_active = $isActive,
                        two_factor_enabled = $twoFactorEnabled,
                        two_factor_secret = $twoFactorSecret,
                        two_factor_configured_at = $twoFactorConfiguredAt
                    WHERE id = $id;
                    """,
                    p =>
                    {
                        p.AddWithValue("$username", request.Username.Trim());
                        p.AddWithValue("$fullName", request.FullName.Trim());
                        p.AddWithValue("$roleId", request.RoleId);
                        p.AddWithValue("$isActive", request.IsActive ? 1 : 0);
                        p.AddWithValue("$twoFactorEnabled", request.TwoFactorEnabled ? 1 : 0);
                        p.AddWithValue("$twoFactorSecret", (object?)twoFactorSecret ?? DBNull.Value);
                        p.AddWithValue("$twoFactorConfiguredAt", (object?)twoFactorConfiguredAt ?? DBNull.Value);
                        p.AddWithValue("$id", userId);
                    },
                    cancellationToken);
            }
            else
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    UPDATE users
                    SET username = $username,
                        password_hash = $passwordHash,
                        full_name = $fullName,
                        role_id = $roleId,
                        is_active = $isActive,
                        two_factor_enabled = $twoFactorEnabled,
                        two_factor_secret = $twoFactorSecret,
                        two_factor_configured_at = $twoFactorConfiguredAt
                    WHERE id = $id;
                    """,
                    p =>
                    {
                        p.AddWithValue("$username", request.Username.Trim());
                        p.AddWithValue("$passwordHash", _passwordHasher.HashPassword(request.Password));
                        p.AddWithValue("$fullName", request.FullName.Trim());
                        p.AddWithValue("$roleId", request.RoleId);
                        p.AddWithValue("$isActive", request.IsActive ? 1 : 0);
                        p.AddWithValue("$twoFactorEnabled", request.TwoFactorEnabled ? 1 : 0);
                        p.AddWithValue("$twoFactorSecret", (object?)twoFactorSecret ?? DBNull.Value);
                        p.AddWithValue("$twoFactorConfiguredAt", (object?)twoFactorConfiguredAt ?? DBNull.Value);
                        p.AddWithValue("$id", userId);
                    },
                    cancellationToken);
            }
        }
        else
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO users
                    (username, password_hash, full_name, role_id, is_active, two_factor_enabled, two_factor_secret, two_factor_configured_at, created_at)
                VALUES
                    ($username, $passwordHash, $fullName, $roleId, $isActive, $twoFactorEnabled, $twoFactorSecret, $twoFactorConfiguredAt, $createdAt);
                """,
                p =>
                {
                    p.AddWithValue("$username", request.Username.Trim());
                    p.AddWithValue("$passwordHash", _passwordHasher.HashPassword(request.Password ?? string.Empty));
                    p.AddWithValue("$fullName", request.FullName.Trim());
                    p.AddWithValue("$roleId", request.RoleId);
                    p.AddWithValue("$isActive", request.IsActive ? 1 : 0);
                    p.AddWithValue("$twoFactorEnabled", request.TwoFactorEnabled ? 1 : 0);
                    p.AddWithValue("$twoFactorSecret", (object?)twoFactorSecret ?? DBNull.Value);
                    p.AddWithValue("$twoFactorConfiguredAt", (object?)twoFactorConfiguredAt ?? DBNull.Value);
                    p.AddWithValue("$createdAt", Now());
                },
                cancellationToken);

            userId = await LastInsertRowIdAsync(connection, transaction, cancellationToken);
        }

        await WriteAuditAsync(connection, transaction, adminId, "SAVE_USER_WEB", "users", userId, request.Username, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (await GetUsersAsync(connection, cancellationToken)).First(u => u.Id == userId);
    }

    public async Task<TwoFactorSetupDto> ResetTwoFactorSecretAsync(long userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var username = await ScalarStringAsync(
            connection,
            "SELECT username FROM users WHERE id = $id AND is_hidden = 0;",
            p => p.AddWithValue("$id", userId),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException("Utilisateur introuvable.");
        }

        var secret = _totpService.GenerateSecret();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE users
            SET two_factor_enabled = 1,
                two_factor_secret = $secret,
                two_factor_configured_at = $configuredAt
            WHERE id = $id;
            """,
            p =>
            {
                p.AddWithValue("$secret", secret);
                p.AddWithValue("$configuredAt", Now());
                p.AddWithValue("$id", userId);
            },
            cancellationToken);

        await WriteAuditAsync(connection, transaction, adminId, "RESET_USER_2FA", "users", userId, username, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TwoFactorSetupDto(userId, username, secret, _totpService.BuildOtpAuthUri("LP2M_Bar_Mngt", username, secret));
    }

    public async Task<OperationResult> SetProductActiveAsync(long productId, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            "UPDATE products SET is_active = $active WHERE id = $id;",
            p =>
            {
                p.AddWithValue("$active", isActive ? 1 : 0);
                p.AddWithValue("$id", productId);
            },
            cancellationToken);

        await WriteAuditAsync(connection, transaction, adminId, "SET_PRODUCT_ACTIVE_WEB", "products", productId, isActive ? "Actif" : "Inactif", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, isActive ? "Produit active." : "Produit desactive.");
    }

    public async Task<OperationResult> SetUserActiveAsync(long userId, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        var username = await ScalarStringAsync(
            connection,
            "SELECT username FROM users WHERE id = $id;",
            p => p.AddWithValue("$id", userId),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException("Utilisateur introuvable.");
        }

        if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase) && !isActive)
        {
            throw new InvalidOperationException("Impossible de desactiver le compte admin.");
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            "UPDATE users SET is_active = $active WHERE id = $id;",
            p =>
            {
                p.AddWithValue("$active", isActive ? 1 : 0);
                p.AddWithValue("$id", userId);
            },
            cancellationToken);

        await WriteAuditAsync(connection, transaction, adminId, "SET_USER_ACTIVE_WEB", "users", userId, isActive ? "Actif" : "Inactif", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, isActive ? "Utilisateur active." : "Utilisateur desactive.");
    }

    public Task<OperationResult> SetObjectHiddenAsync(ObjectVisibilityRequest request, CancellationToken cancellationToken = default)
    {
        return SetHiddenStateAsync(request.ObjectType, request.Id, request.IsHidden, request.IsHidden ? "Masque" : "Affiche", cancellationToken);
    }

    public async Task<OperationResult> SetObjectsHiddenAsync(BulkObjectRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Objects.Count == 0)
        {
            return new OperationResult(false, "Aucun objet selectionne.");
        }

        foreach (var item in request.Objects)
        {
            await SetHiddenStateAsync(item.ObjectType, item.Id, request.IsHidden, request.IsHidden ? "Masque" : "Affiche", cancellationToken);
        }

        return new OperationResult(true, $"{request.Objects.Count} objet(s) {(request.IsHidden ? "masque(s)" : "affiche(s)")}.");
    }

    public Task<OperationResult> DeleteObjectAsync(string objectType, long id, CancellationToken cancellationToken = default)
    {
        return SetHiddenStateAsync(objectType, id, true, "Supprime", cancellationToken, deactivate: true);
    }

    public async Task<OperationResult> DeleteObjectsAsync(IReadOnlyList<ObjectReferenceDto> objects, CancellationToken cancellationToken = default)
    {
        if (objects.Count == 0)
        {
            return new OperationResult(false, "Aucun objet selectionne.");
        }

        foreach (var item in objects)
        {
            await SetHiddenStateAsync(item.ObjectType, item.Id, true, "Supprime", cancellationToken, deactivate: true);
        }

        return new OperationResult(true, $"{objects.Count} objet(s) supprime(s) de l'affichage.");
    }


    private async Task<OperationResult> SetHiddenStateAsync(string objectType, long id, bool isHidden, string actionLabel, CancellationToken cancellationToken, bool deactivate = false)
    {
        if (id <= 0)
        {
            throw new InvalidOperationException("L'objet selectionne est invalide.");
        }

        var target = ResolveObjectTarget(objectType);
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var adminId = await GetAdminUserIdAsync(connection, cancellationToken);
        if (!await ObjectExistsAsync(connection, target, id, cancellationToken))
        {
            throw new InvalidOperationException("Objet introuvable.");
        }

        await EnsureObjectCanBeHiddenAsync(connection, target, id, isHidden, cancellationToken);

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var sql = deactivate && target.ActiveColumn is not null
            ? $"UPDATE {target.TableName} SET is_hidden = $hidden, {target.ActiveColumn} = 0 WHERE id = $id;"
            : $"UPDATE {target.TableName} SET is_hidden = $hidden WHERE id = $id;";

        await ExecuteAsync(
            connection,
            transaction,
            sql,
            p =>
            {
                p.AddWithValue("$hidden", isHidden ? 1 : 0);
                p.AddWithValue("$id", id);
            },
            cancellationToken);

        await WriteAuditAsync(connection, transaction, adminId, $"{actionLabel.ToUpperInvariant()}_OBJECT_WEB", target.TableName, id, target.Label, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationResult(true, $"{target.Label} {actionLabel.ToLowerInvariant()}.");
    }

    private static async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT id, name, is_active, is_hidden, image_data FROM {tableName} ORDER BY name;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<CategoryDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CategoryDto(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt64(2) == 1,
                reader.GetInt64(3) == 1,
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return result;
    }

    private static ObjectTarget ResolveObjectTarget(string objectType)
    {
        return objectType.Trim().ToLowerInvariant() switch
        {
            "product" or "products" => new ObjectTarget("products", "Produit", "is_active"),
            "category" or "productcategory" or "product-category" => new ObjectTarget("product_categories", "Categorie produit", "is_active"),
            "expensecategory" or "expense-category" => new ObjectTarget("expense_categories", "Categorie depense", "is_active"),
            "expense" or "expenses" => new ObjectTarget("expenses", "Depense", null),
            "user" or "users" => new ObjectTarget("users", "Utilisateur", "is_active"),
            "sale" or "sales" => new ObjectTarget("sales", "Vente", null),
            "cashsession" or "cash-session" or "cash" => new ObjectTarget("cash_sessions", "Session de caisse", null),
            _ => throw new InvalidOperationException("Type d'objet non pris en charge.")
        };
    }

    private static async Task EnsureObjectCanBeHiddenAsync(SqliteConnection connection, ObjectTarget target, long id, bool isHidden, CancellationToken cancellationToken)
    {
        if (!string.Equals(target.TableName, "users", StringComparison.OrdinalIgnoreCase) || !isHidden)
        {
            return;
        }

        var username = await ScalarStringAsync(
            connection,
            "SELECT username FROM users WHERE id = $id;",
            p => p.AddWithValue("$id", id),
            cancellationToken);

        if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Impossible de masquer ou supprimer le compte admin.");
        }
    }

    private static async Task<bool> ObjectExistsAsync(SqliteConnection connection, ObjectTarget target, long id, CancellationToken cancellationToken)
    {
        var count = await ScalarLongAsync(
            connection,
            $"SELECT COUNT(1) FROM {target.TableName} WHERE id = $id;",
            p => p.AddWithValue("$id", id),
            cancellationToken);

        return count > 0;
    }

    private static async Task<IReadOnlyList<RoleDto>> GetRolesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name FROM roles ORDER BY id;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<RoleDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new RoleDto(reader.GetInt64(0), reader.GetString(1)));
        }

        return result;
    }

    private static async Task<IReadOnlyList<ProductDto>> GetProductsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                p.id, p.category_id, c.name, p.name, p.sku, p.barcode, p.sale_price, p.cost_price,
                p.is_stock_managed, p.low_stock_threshold, COALESCE(sl.quantity, 0), p.is_active, p.is_hidden, p.image_data
            FROM products p
            INNER JOIN product_categories c ON c.id = p.category_id
            LEFT JOIN stock_levels sl ON sl.product_id = p.id
            ORDER BY p.name;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<ProductDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ProductDto(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                CentsToDecimal(reader.GetInt64(6)),
                CentsToDecimal(reader.GetInt64(7)),
                reader.GetInt64(8) == 1,
                reader.GetDouble(9),
                reader.GetDouble(10),
                reader.GetInt64(11) == 1,
                reader.GetInt64(12) == 1,
                reader.IsDBNull(13) ? null : reader.GetString(13)));
        }

        return result;
    }

    private static async Task<IReadOnlyList<StockItemDto>> GetStockAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.id, p.name, c.name, COALESCE(sl.quantity, 0), p.low_stock_threshold, p.is_hidden
            FROM products p
            INNER JOIN product_categories c ON c.id = p.category_id
            LEFT JOIN stock_levels sl ON sl.product_id = p.id
            WHERE p.is_stock_managed = 1
            ORDER BY COALESCE(sl.quantity, 0), p.name;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<StockItemDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var quantity = reader.GetDouble(3);
            var threshold = reader.GetDouble(4);
            result.Add(new StockItemDto(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), quantity, threshold, quantity <= threshold, reader.GetInt64(5) == 1));
        }

        return result;
    }

    private static async Task<IReadOnlyList<ExpenseDto>> GetExpensesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.id, ec.name, e.description, e.amount, e.paid_from_cash_register, e.expense_date, e.status, e.is_hidden
            FROM expenses e
            INNER JOIN expense_categories ec ON ec.id = e.category_id
            ORDER BY e.expense_date DESC
            LIMIT 100;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<ExpenseDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ExpenseDto(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                CentsToDecimal(reader.GetInt64(3)),
                reader.GetInt64(4) == 1,
                DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                reader.GetString(6),
                reader.GetInt64(7) == 1));
        }

        return result;
    }

    private static async Task<IReadOnlyList<UserDto>> GetUsersAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT u.id, u.username, u.full_name, u.role_id, r.name, u.is_active, u.created_at, u.is_hidden, u.two_factor_enabled
            FROM users u
            INNER JOIN roles r ON r.id = u.role_id
            ORDER BY u.username;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<UserDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new UserDto(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetString(4),
                reader.GetInt64(5) == 1,
                DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture),
                reader.GetInt64(7) == 1,
                reader.GetInt64(8) == 1));
        }

        return result;
    }

    private static async Task<IReadOnlyList<SaleDto>> GetSalesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                s.id, s.ticket_number, u.full_name, COALESCE(NULLIF(s.customer_name, ''), 'Client comptoir'), s.total_amount,
                CASE s.payment_method WHEN 1 THEN 'Especes' WHEN 2 THEN 'Carte' WHEN 3 THEN 'Mobile money' ELSE 'Credit client' END,
                CASE s.status WHEN 1 THEN 'Validee' ELSE 'Annulee' END,
                s.sale_date,
                s.is_hidden
            FROM sales s
            INNER JOIN users u ON u.id = s.cashier_id
            ORDER BY s.sale_date DESC
            LIMIT 100;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<SaleDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new SaleDto(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                CentsToDecimal(reader.GetInt64(4)),
                reader.GetString(5),
                reader.GetString(6),
                DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture),
                reader.GetInt64(8) == 1));
        }

        return result;
    }

    private static async Task<IReadOnlyList<CashSessionDto>> GetCashSessionsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                cs.id, u.full_name, cs.opened_at, cs.closed_at, cs.opening_amount, cs.expected_closing_amount,
                cs.declared_closing_amount, cs.difference_amount,
                CASE cs.status WHEN 1 THEN 'Ouverte' WHEN 2 THEN 'Cloturee' ELSE 'Annulee' END,
                cs.is_hidden
            FROM cash_sessions cs
            INNER JOIN users u ON u.id = cs.cashier_id
            ORDER BY cs.opened_at DESC
            LIMIT 100;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<CashSessionDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CashSessionDto(
                reader.GetInt64(0),
                reader.GetString(1),
                DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
                reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                CentsToDecimal(reader.GetInt64(4)),
                CentsToDecimal(reader.GetInt64(5)),
                reader.IsDBNull(6) ? null : CentsToDecimal(reader.GetInt64(6)),
                reader.IsDBNull(7) ? null : CentsToDecimal(reader.GetInt64(7)),
                reader.GetString(8),
                reader.GetInt64(9) == 1));
        }

        return result;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction transaction, string sql, Action<SqliteParameterCollection>? bind, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        bind?.Invoke(command.Parameters);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> ScalarLongAsync(SqliteConnection connection, string sql, Action<SqliteParameterCollection>? bind, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind?.Invoke(command.Parameters);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async Task<string?> ScalarStringAsync(SqliteConnection connection, string sql, Action<SqliteParameterCollection>? bind, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind?.Invoke(command.Parameters);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToString(result, CultureInfo.InvariantCulture);
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
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < reader.FieldCount; index++)
        {
            row[reader.GetName(index)] = reader.IsDBNull(index) ? DBNull.Value : reader.GetValue(index);
        }

        return row;
    }

    private static async Task<IReadOnlyList<WebDashboardPointDto>> GetSalesChartAsync(
        SqliteConnection connection,
        DateTime startDay,
        DateTime endDay,
        CancellationToken cancellationToken)
    {
        var start = startDay.ToString("O", CultureInfo.InvariantCulture);
        var end = endDay.ToString("O", CultureInfo.InvariantCulture);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT substr(sale_date, 1, 10) AS sale_day, COALESCE(SUM(total_amount), 0) AS amount, COUNT(1) AS ticket_count
            FROM sales
            WHERE status = 1 AND is_hidden = 0 AND sale_date >= $start AND sale_date < $end
            GROUP BY substr(sale_date, 1, 10)
            ORDER BY sale_day;
            """;
        command.Parameters.AddWithValue("$start", start);
        command.Parameters.AddWithValue("$end", end);

        var rows = new Dictionary<string, (long Amount, int Count)>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows[reader.GetString(0)] = (reader.GetInt64(1), (int)Math.Min(int.MaxValue, reader.GetInt64(2)));
        }

        var result = new List<WebDashboardPointDto>();
        for (var day = startDay.Date; day < endDay.Date; day = day.AddDays(1))
        {
            var key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            rows.TryGetValue(key, out var value);
            result.Add(new WebDashboardPointDto(day.ToString("dd/MM", CultureInfo.InvariantCulture), CentsToDecimal(value.Amount), value.Count));
        }

        return result;
    }

    private static async Task<IReadOnlyList<WebTopProductDto>> GetTopProductsAsync(
        SqliteConnection connection,
        DateTime startDay,
        DateTime endDay,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT si.product_name, COALESCE(SUM(si.quantity), 0) AS quantity_sold, COALESCE(SUM(si.total_amount), 0) AS amount
            FROM sale_items si
            INNER JOIN sales s ON s.id = si.sale_id
            WHERE s.status = 1 AND s.is_hidden = 0 AND s.sale_date >= $start AND s.sale_date < $end
            GROUP BY si.product_id, si.product_name
            ORDER BY quantity_sold DESC, amount DESC
            LIMIT 5;
            """;
        command.Parameters.AddWithValue("$start", startDay.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$end", endDay.ToString("O", CultureInfo.InvariantCulture));

        var result = new List<WebTopProductDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new WebTopProductDto(reader.GetString(0), reader.GetDouble(1), CentsToDecimal(reader.GetInt64(2))));
        }

        return result;
    }

    private static async Task<long> LastInsertRowIdAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT last_insert_rowid();";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async Task<long> GetAdminUserIdAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        return await ScalarLongAsync(connection, "SELECT id FROM users WHERE username = 'admin' LIMIT 1;", null, cancellationToken);
    }

    private static Task<string?> GetActiveUserNameAsync(SqliteConnection connection, long userId, CancellationToken cancellationToken)
    {
        return ScalarStringAsync(
            connection,
            "SELECT full_name FROM users WHERE id = $id AND is_active = 1 AND is_hidden = 0 LIMIT 1;",
            p => p.AddWithValue("$id", userId),
            cancellationToken);
    }

    private static async Task InsertStockMovementAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long productId,
        int movementType,
        double quantity,
        string reason,
        long? saleId,
        long userId,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO stock_movements (product_id, movement_type, quantity, reason, sale_id, user_id, created_at)
            VALUES ($productId, $movementType, $quantity, $reason, $saleId, $userId, $createdAt);
            """,
            p =>
            {
                p.AddWithValue("$productId", productId);
                p.AddWithValue("$movementType", movementType);
                p.AddWithValue("$quantity", quantity);
                p.AddWithValue("$reason", reason);
                p.AddWithValue("$saleId", (object?)saleId ?? DBNull.Value);
                p.AddWithValue("$userId", userId);
                p.AddWithValue("$createdAt", Now());
            },
            cancellationToken);
    }

    private static Task WriteAuditAsync(SqliteConnection connection, SqliteTransaction transaction, long? userId, string action, string entityType, long? entityId, string? details, CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO audit_logs (user_id, action, entity_type, entity_id, details, created_at)
            VALUES ($userId, $action, $entityType, $entityId, $details, $createdAt);
            """,
            p =>
            {
                p.AddWithValue("$userId", (object?)userId ?? DBNull.Value);
                p.AddWithValue("$action", action);
                p.AddWithValue("$entityType", entityType);
                p.AddWithValue("$entityId", (object?)entityId ?? DBNull.Value);
                p.AddWithValue("$details", details ?? string.Empty);
                p.AddWithValue("$createdAt", Now());
            },
            cancellationToken);
    }

    private static void BindProduct(SqliteParameterCollection p, ProductSaveRequest request, long salePrice, long costPrice)
    {
        p.AddWithValue("$categoryId", request.CategoryId);
        p.AddWithValue("$name", request.Name.Trim());
        p.AddWithValue("$sku", string.IsNullOrWhiteSpace(request.Sku) ? DBNull.Value : request.Sku.Trim());
        p.AddWithValue("$barcode", string.IsNullOrWhiteSpace(request.Barcode) ? DBNull.Value : request.Barcode.Trim());
        p.AddWithValue("$salePrice", salePrice);
        p.AddWithValue("$costPrice", costPrice);
        p.AddWithValue("$isStockManaged", request.IsStockManaged ? 1 : 0);
        p.AddWithValue("$lowStockThreshold", request.LowStockThreshold);
        p.AddWithValue("$isActive", request.IsActive ? 1 : 0);
        p.AddWithValue("$imageData", string.IsNullOrWhiteSpace(request.ImageData) ? DBNull.Value : request.ImageData);
    }

    private static void BindPeriod(SqliteParameterCollection p, string start, string end)
    {
        p.AddWithValue("$start", start);
        p.AddWithValue("$end", end);
    }

    private static string Now()
    {
        return DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
    }

    private static long DecimalToCents(decimal value)
    {
        return Convert.ToInt64(decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero));
    }

    private static decimal CentsToDecimal(long cents)
    {
        return cents / 100m;
    }

    private static BusinessProfileDto DefaultBusinessProfile()
    {
        return new BusinessProfileDto(
            "La pause de Medoune",
            "LP2M",
            "Abidjan, Cote d'Ivoire",
            "Contact a renseigner",
            null,
            null,
            "Merci pour votre visite. Paiement en FCFA.");
    }

    private sealed record SaleCartLine(
        long ProductId,
        string ProductName,
        double Quantity,
        long UnitPrice,
        bool IsStockManaged,
        long TotalAmount);

    private static void ValidateRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record ObjectTarget(string TableName, string Label, string? ActiveColumn);
}
