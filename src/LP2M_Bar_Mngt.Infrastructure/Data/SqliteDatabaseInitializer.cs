using LP2M_Bar_Mngt.Application.Abstractions;
using LP2M_Bar_Mngt.Infrastructure.Security;
using Microsoft.Data.Sqlite;

namespace LP2M_Bar_Mngt.Infrastructure.Data;

public sealed class SqliteDatabaseInitializer : IApplicationDatabaseInitializer
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly PasswordHasher _passwordHasher;

    public SqliteDatabaseInitializer(SqliteConnectionFactory connectionFactory, PasswordHasher passwordHasher)
    {
        _connectionFactory = connectionFactory;
        _passwordHasher = passwordHasher;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteAsync(connection, transaction, SqliteSchema.CreateTables, cancellationToken: cancellationToken);
        await EnsureHiddenColumnsAsync(connection, transaction, cancellationToken);
        await SeedRolesAsync(connection, transaction, cancellationToken);
        await SeedAdminUserAsync(connection, transaction, cancellationToken);
        await SeedBusinessProfileAsync(connection, transaction, cancellationToken);
        await SeedCatalogAsync(connection, transaction, cancellationToken);
        await SeedExpenseCategoriesAsync(connection, transaction, cancellationToken);
        await ApplyIvorianThemeCatalogAsync(connection, transaction, cancellationToken);
        await ApplyProductImagesAsync(connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task EnsureHiddenColumnsAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var hiddenTables = new[]
        {
            "users",
            "product_categories",
            "products",
            "cash_sessions",
            "sales",
            "expense_categories",
            "expenses"
        };

        foreach (var table in hiddenTables)
        {
            if (!await ColumnExistsAsync(connection, transaction, table, "is_hidden", cancellationToken))
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    $"ALTER TABLE {table} ADD COLUMN is_hidden INTEGER NOT NULL DEFAULT 0;",
                    cancellationToken: cancellationToken);
            }
        }

        var imageTables = new[]
        {
            "product_categories",
            "products",
            "expense_categories"
        };

        foreach (var table in imageTables)
        {
            if (!await ColumnExistsAsync(connection, transaction, table, "image_data", cancellationToken))
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    $"ALTER TABLE {table} ADD COLUMN image_data TEXT NULL;",
                    cancellationToken: cancellationToken);
            }
        }

        if (!await ColumnExistsAsync(connection, transaction, "users", "two_factor_enabled", cancellationToken))
        {
            await ExecuteAsync(
                connection,
                transaction,
                "ALTER TABLE users ADD COLUMN two_factor_enabled INTEGER NOT NULL DEFAULT 0;",
                cancellationToken: cancellationToken);
        }

        if (!await ColumnExistsAsync(connection, transaction, "users", "two_factor_secret", cancellationToken))
        {
            await ExecuteAsync(
                connection,
                transaction,
                "ALTER TABLE users ADD COLUMN two_factor_secret TEXT NULL;",
                cancellationToken: cancellationToken);
        }

        if (!await ColumnExistsAsync(connection, transaction, "users", "two_factor_configured_at", cancellationToken))
        {
            await ExecuteAsync(
                connection,
                transaction,
                "ALTER TABLE users ADD COLUMN two_factor_configured_at TEXT NULL;",
                cancellationToken: cancellationToken);
        }

        if (!await ColumnExistsAsync(connection, transaction, "sales", "customer_name", cancellationToken))
        {
            await ExecuteAsync(
                connection,
                transaction,
                "ALTER TABLE sales ADD COLUMN customer_name TEXT NULL;",
                cancellationToken: cancellationToken);
        }
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, SqliteTransaction transaction, string tableName, string columnName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info({tableName});";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task SeedRolesAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT OR IGNORE INTO roles (name, is_system) VALUES
            ('Admin', 1),
            ('Gerant', 1),
            ('Caissier', 1),
            ('Lecture', 1);
            """;

        await ExecuteAsync(connection, transaction, sql, cancellationToken: cancellationToken);
    }

    private async Task SeedAdminUserAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var adminRoleId = await GetIdByNameAsync(connection, transaction, "roles", "Admin", cancellationToken);
        var passwordHash = _passwordHasher.HashPassword("admin123");

        const string sql = """
            INSERT OR IGNORE INTO users (username, password_hash, full_name, role_id, is_active, created_at)
            VALUES ($username, $passwordHash, $fullName, $roleId, 1, $createdAt);
            """;

        await ExecuteAsync(
            connection,
            transaction,
            sql,
            parameters =>
            {
                parameters.AddWithValue("$username", "admin");
                parameters.AddWithValue("$passwordHash", passwordHash);
                parameters.AddWithValue("$fullName", "Administrateur");
                parameters.AddWithValue("$roleId", adminRoleId);
                parameters.AddWithValue("$createdAt", DateTime.Now.ToString("O"));
            },
            cancellationToken);
    }

    private static async Task SeedBusinessProfileAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT OR IGNORE INTO business_profile
                (id, name, sigle, address, contact, logo_data, cover_image_data, ticket_footer)
            VALUES
                (1, 'La pause de Medoune', 'LP2M', 'Abidjan, Cote d''Ivoire', 'Contact a renseigner', NULL, NULL, 'Merci pour votre visite. Paiement en FCFA.');
            """;

        await ExecuteAsync(connection, transaction, sql, cancellationToken: cancellationToken);
    }

    private async Task SeedCatalogAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        const string categoriesSql = """
            INSERT OR IGNORE INTO product_categories (name, is_active) VALUES
            ('Boissons', 1),
            ('Cuisine', 1),
            ('Snacks', 1);
            """;

        await ExecuteAsync(connection, transaction, categoriesSql, cancellationToken: cancellationToken);

        var drinksId = await GetIdByNameAsync(connection, transaction, "product_categories", "Boissons", cancellationToken);
        var kitchenId = await GetIdByNameAsync(connection, transaction, "product_categories", "Cuisine", cancellationToken);
        var snacksId = await GetIdByNameAsync(connection, transaction, "product_categories", "Snacks", cancellationToken);
        var adminUserId = await GetIdByNameAsync(connection, transaction, "users", "admin", cancellationToken, "username");

        await SeedProductAsync(connection, transaction, drinksId, adminUserId, "Biere locale", "BR-001", "600000000001", 200, 130, true, 10, 48, cancellationToken);
        await SeedProductAsync(connection, transaction, drinksId, adminUserId, "Eau minerale", "EAU-001", "600000000002", 100, 60, true, 20, 120, cancellationToken);
        await SeedProductAsync(connection, transaction, drinksId, adminUserId, "Soda", "SD-001", "600000000003", 150, 90, true, 15, 96, cancellationToken);
        await SeedProductAsync(connection, transaction, kitchenId, adminUserId, "Brochette", "CU-001", null, 300, 180, false, 0, 0, cancellationToken);
        await SeedProductAsync(connection, transaction, snacksId, adminUserId, "Chips", "SN-001", "600000000004", 100, 55, true, 12, 8, cancellationToken);
    }

    private async Task SeedProductAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long categoryId,
        long userId,
        string name,
        string sku,
        string? barcode,
        long salePriceCents,
        long costPriceCents,
        bool isStockManaged,
        double lowStockThreshold,
        double initialQuantity,
        CancellationToken cancellationToken)
    {
        const string productSql = """
            INSERT OR IGNORE INTO products
                (category_id, name, sku, barcode, sale_price, cost_price, is_stock_managed, low_stock_threshold, is_active, created_at)
            VALUES
                ($categoryId, $name, $sku, $barcode, $salePrice, $costPrice, $isStockManaged, $lowStockThreshold, 1, $createdAt);
            """;

        await ExecuteAsync(
            connection,
            transaction,
            productSql,
            parameters =>
            {
                parameters.AddWithValue("$categoryId", categoryId);
                parameters.AddWithValue("$name", name);
                parameters.AddWithValue("$sku", sku);
                parameters.AddWithValue("$barcode", (object?)barcode ?? DBNull.Value);
                parameters.AddWithValue("$salePrice", salePriceCents);
                parameters.AddWithValue("$costPrice", costPriceCents);
                parameters.AddWithValue("$isStockManaged", isStockManaged ? 1 : 0);
                parameters.AddWithValue("$lowStockThreshold", lowStockThreshold);
                parameters.AddWithValue("$createdAt", DateTime.Now.ToString("O"));
            },
            cancellationToken);

        var productId = await GetIdByNameAsync(connection, transaction, "products", name, cancellationToken);

        const string stockLevelSql = """
            INSERT OR IGNORE INTO stock_levels (product_id, quantity)
            VALUES ($productId, $quantity);
            """;

        await ExecuteAsync(
            connection,
            transaction,
            stockLevelSql,
            parameters =>
            {
                parameters.AddWithValue("$productId", productId);
                parameters.AddWithValue("$quantity", initialQuantity);
            },
            cancellationToken);

        var movementCount = await ScalarLongAsync(
            connection,
            transaction,
            "SELECT COUNT(1) FROM stock_movements WHERE product_id = $productId;",
            parameters => parameters.AddWithValue("$productId", productId),
            cancellationToken);

        if (isStockManaged && movementCount == 0)
        {
            const string movementSql = """
                INSERT INTO stock_movements (product_id, movement_type, quantity, reason, sale_id, user_id, created_at)
                VALUES ($productId, 1, $quantity, $reason, NULL, $userId, $createdAt);
                """;

            await ExecuteAsync(
                connection,
                transaction,
                movementSql,
                parameters =>
                {
                    parameters.AddWithValue("$productId", productId);
                    parameters.AddWithValue("$quantity", initialQuantity);
                    parameters.AddWithValue("$reason", "Stock initial");
                    parameters.AddWithValue("$userId", userId);
                    parameters.AddWithValue("$createdAt", DateTime.Now.ToString("O"));
                },
                cancellationToken);
        }
    }

    private async Task SeedExpenseCategoriesAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT OR IGNORE INTO expense_categories (name, is_active) VALUES
            ('Achats fournisseurs', 1),
            ('Transport', 1),
            ('Charges', 1),
            ('Autres', 1);
            """;

        await ExecuteAsync(connection, transaction, sql, cancellationToken: cancellationToken);
    }

    private async Task ApplyIvorianThemeCatalogAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        const string migrationKey = "theme_catalog_ci_v1";
        var applied = await ScalarStringAsync(
            connection,
            transaction,
            "SELECT value FROM app_settings WHERE key = $key LIMIT 1;",
            parameters => parameters.AddWithValue("$key", migrationKey),
            cancellationToken);

        if (string.Equals(applied, "applied", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        const string profileSql = """
            INSERT INTO business_profile
                (id, name, sigle, address, contact, logo_data, cover_image_data, ticket_footer)
            VALUES
                (1, 'La pause de Medoune', 'LP2M', 'Abidjan, Cote d''Ivoire', 'Contact a renseigner', NULL, NULL, 'Merci pour votre visite. Paiement en FCFA.')
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                sigle = excluded.sigle,
                address = excluded.address,
                contact = excluded.contact,
                ticket_footer = excluded.ticket_footer;
            """;
        await ExecuteAsync(connection, transaction, profileSql, cancellationToken: cancellationToken);

        await UpsertProductCategoryAsync(connection, transaction, "Cafe & chocolat", cancellationToken);
        await UpsertProductCategoryAsync(connection, transaction, "Boissons fraiches", cancellationToken);
        await UpsertProductCategoryAsync(connection, transaction, "Plats ivoiriens", cancellationToken);
        await UpsertProductCategoryAsync(connection, transaction, "Snacks & accompagnements", cancellationToken);
        await UpsertProductCategoryAsync(connection, transaction, "Spiritueux", cancellationToken);

        var cafeId = await GetIdByNameAsync(connection, transaction, "product_categories", "Cafe & chocolat", cancellationToken);
        var freshId = await GetIdByNameAsync(connection, transaction, "product_categories", "Boissons fraiches", cancellationToken);
        var mealsId = await GetIdByNameAsync(connection, transaction, "product_categories", "Plats ivoiriens", cancellationToken);
        var snacksId = await GetIdByNameAsync(connection, transaction, "product_categories", "Snacks & accompagnements", cancellationToken);
        var spiritsId = await GetIdByNameAsync(connection, transaction, "product_categories", "Spiritueux", cancellationToken);
        var adminUserId = await GetIdByNameAsync(connection, transaction, "users", "admin", cancellationToken, "username");

        await UpsertIvorianProductAsync(connection, transaction, cafeId, adminUserId, "Cafe noir", "LP2M-CF-001", "618000100001", 250, 120, true, 20, 80, cancellationToken);
        await UpsertIvorianProductAsync(connection, transaction, cafeId, adminUserId, "Cafe au lait LP2M", "LP2M-CF-002", "618000100002", 500, 250, true, 15, 60, cancellationToken);
        await UpsertIvorianProductAsync(connection, transaction, cafeId, adminUserId, "Chocolat chaud", "LP2M-CF-003", "618000100003", 600, 300, true, 12, 40, cancellationToken);
        await UpsertIvorianProductAsync(connection, transaction, cafeId, adminUserId, "The Lipton", "LP2M-CF-004", "618000100004", 300, 120, true, 15, 50, cancellationToken);

        await UpsertIvorianProductAsync(connection, transaction, freshId, adminUserId, "Eau minerale 0,5L", "LP2M-BF-001", "618000200001", 300, 150, true, 24, 120, cancellationToken);
        await UpsertIvorianProductAsync(connection, transaction, freshId, adminUserId, "Eau minerale 1,5L", "LP2M-BF-002", "618000200002", 600, 300, true, 18, 72, cancellationToken);
        await UpsertIvorianProductAsync(connection, transaction, freshId, adminUserId, "Soda 33cl", "LP2M-BF-003", "618000200003", 500, 300, true, 24, 96, cancellationToken);
        await UpsertIvorianProductAsync(connection, transaction, freshId, adminUserId, "Jus de bissap", "LP2M-BF-004", "618000200004", 500, 220, true, 10, 40, cancellationToken);
        await UpsertIvorianProductAsync(connection, transaction, freshId, adminUserId, "Jus de gingembre", "LP2M-BF-005", "618000200005", 500, 220, true, 10, 40, cancellationToken);
        await UpsertIvorianProductAsync(connection, transaction, freshId, adminUserId, "Biere locale 65cl", "LP2M-BF-006", "618000200006", 1000, 650, true, 12, 48, cancellationToken);

        await UpsertIvorianProductAsync(connection, transaction, mealsId, adminUserId, "Garba", "LP2M-PI-001", "618000300001", 1500, 950, false, 0, 0, cancellationToken);
        await UpsertIvorianProductAsync(connection, transaction, mealsId, adminUserId, "Attieke poisson", "LP2M-PI-002", "618000300002", 3000, 2000, false, 0, 0, cancellationToken);
        await UpsertIvorianProductAsync(connection, transaction, mealsId, adminUserId, "Alloco poisson", "LP2M-PI-003", "618000300003", 2500, 1600, false, 0, 0, cancellationToken);
        await UpsertIvorianProductAsync(connection, transaction, mealsId, adminUserId, "Poulet braise", "LP2M-PI-004", "618000300004", 3500, 2400, false, 0, 0, cancellationToken);
        await UpsertIvorianProductAsync(connection, transaction, mealsId, adminUserId, "Brochette de viande", "LP2M-PI-005", "618000300005", 1000, 650, false, 0, 0, cancellationToken);

        await UpsertIvorianProductAsync(connection, transaction, snacksId, adminUserId, "Chips plantain", "LP2M-SN-001", "618000400001", 500, 250, true, 15, 60, cancellationToken);
        await UpsertIvorianProductAsync(connection, transaction, snacksId, adminUserId, "Arachides grillees", "LP2M-SN-002", "618000400002", 300, 150, true, 20, 80, cancellationToken);
        await UpsertIvorianProductAsync(connection, transaction, snacksId, adminUserId, "Pastels", "LP2M-SN-003", "618000400003", 500, 220, true, 15, 50, cancellationToken);

        await UpsertIvorianProductAsync(connection, transaction, spiritsId, adminUserId, "Whisky verre", "LP2M-SP-001", "618000500001", 1500, 900, true, 8, 30, cancellationToken);
        await UpsertIvorianProductAsync(connection, transaction, spiritsId, adminUserId, "Gin tonic", "LP2M-SP-002", "618000500002", 2000, 1200, true, 8, 30, cancellationToken);

        await HideLegacyDemoCatalogAsync(connection, transaction, cancellationToken);

        const string setMigrationSql = """
            INSERT INTO app_settings (key, value, updated_at)
            VALUES ($key, 'applied', $updatedAt)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;
            """;
        await ExecuteAsync(
            connection,
            transaction,
            setMigrationSql,
            parameters =>
            {
                parameters.AddWithValue("$key", migrationKey);
                parameters.AddWithValue("$updatedAt", DateTime.Now.ToString("O"));
            },
            cancellationToken);
    }

    private static async Task UpsertProductCategoryAsync(SqliteConnection connection, SqliteTransaction transaction, string name, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO product_categories (name, is_active, is_hidden)
            VALUES ($name, 1, 0)
            ON CONFLICT(name) DO UPDATE SET is_active = 1, is_hidden = 0;
            """;

        await ExecuteAsync(connection, transaction, sql, parameters => parameters.AddWithValue("$name", name), cancellationToken);
    }

    private async Task UpsertIvorianProductAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long categoryId,
        long userId,
        string name,
        string sku,
        string barcode,
        long salePriceFcfa,
        long costPriceFcfa,
        bool isStockManaged,
        double lowStockThreshold,
        double initialQuantity,
        CancellationToken cancellationToken)
    {
        const string productSql = """
            INSERT INTO products
                (category_id, name, sku, barcode, sale_price, cost_price, is_stock_managed, low_stock_threshold, is_active, is_hidden, created_at)
            VALUES
                ($categoryId, $name, $sku, $barcode, $salePrice, $costPrice, $isStockManaged, $lowStockThreshold, 1, 0, $createdAt)
            ON CONFLICT(sku) DO UPDATE SET
                category_id = excluded.category_id,
                name = excluded.name,
                barcode = excluded.barcode,
                sale_price = excluded.sale_price,
                cost_price = excluded.cost_price,
                is_stock_managed = excluded.is_stock_managed,
                low_stock_threshold = excluded.low_stock_threshold,
                is_active = 1,
                is_hidden = 0;
            """;

        await ExecuteAsync(
            connection,
            transaction,
            productSql,
            parameters =>
            {
                parameters.AddWithValue("$categoryId", categoryId);
                parameters.AddWithValue("$name", name);
                parameters.AddWithValue("$sku", sku);
                parameters.AddWithValue("$barcode", barcode);
                parameters.AddWithValue("$salePrice", FcfaToStoredAmount(salePriceFcfa));
                parameters.AddWithValue("$costPrice", FcfaToStoredAmount(costPriceFcfa));
                parameters.AddWithValue("$isStockManaged", isStockManaged ? 1 : 0);
                parameters.AddWithValue("$lowStockThreshold", lowStockThreshold);
                parameters.AddWithValue("$createdAt", DateTime.Now.ToString("O"));
            },
            cancellationToken);

        var productId = await ScalarLongAsync(
            connection,
            transaction,
            "SELECT id FROM products WHERE sku = $sku LIMIT 1;",
            parameters => parameters.AddWithValue("$sku", sku),
            cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            "INSERT OR IGNORE INTO stock_levels (product_id, quantity) VALUES ($productId, $quantity);",
            parameters =>
            {
                parameters.AddWithValue("$productId", productId);
                parameters.AddWithValue("$quantity", initialQuantity);
            },
            cancellationToken);

        var movementCount = await ScalarLongAsync(
            connection,
            transaction,
            "SELECT COUNT(1) FROM stock_movements WHERE product_id = $productId;",
            parameters => parameters.AddWithValue("$productId", productId),
            cancellationToken);

        if (isStockManaged && movementCount == 0)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO stock_movements (product_id, movement_type, quantity, reason, sale_id, user_id, created_at)
                VALUES ($productId, 1, $quantity, $reason, NULL, $userId, $createdAt);
                """,
                parameters =>
                {
                    parameters.AddWithValue("$productId", productId);
                    parameters.AddWithValue("$quantity", initialQuantity);
                    parameters.AddWithValue("$reason", "Stock initial catalogue CI");
                    parameters.AddWithValue("$userId", userId);
                    parameters.AddWithValue("$createdAt", DateTime.Now.ToString("O"));
                },
                cancellationToken);
        }
    }

    private static async Task HideLegacyDemoCatalogAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        const string hideProductsSql = """
            UPDATE products
            SET is_active = 0, is_hidden = 1
            WHERE sku IN ('BR-001', 'EAU-001', 'SD-001', 'CU-001', 'SN-001')
               OR name IN ('Biere locale', 'Eau minerale', 'Soda', 'Brochette', 'Chips');
            """;
        await ExecuteAsync(connection, transaction, hideProductsSql, cancellationToken: cancellationToken);

        const string hideCategoriesSql = """
            UPDATE product_categories
            SET is_active = 0, is_hidden = 1
            WHERE name IN ('Boissons', 'Cuisine', 'Snacks');
            """;
        await ExecuteAsync(connection, transaction, hideCategoriesSql, cancellationToken: cancellationToken);
    }

    private static long FcfaToStoredAmount(long amount)
    {
        return amount * 100;
    }

    private static async Task ApplyProductImagesAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        const string migrationKey = "product_images_ci_v1";
        var applied = await ScalarStringAsync(
            connection,
            transaction,
            "SELECT value FROM app_settings WHERE key = $key LIMIT 1;",
            parameters => parameters.AddWithValue("$key", migrationKey),
            cancellationToken);

        if (string.Equals(applied, "applied", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var images = new Dictionary<string, string>
        {
            ["LP2M-CF-001"] = ProductImage("Cafe noir", "cup", "#3a2016", "#d9a441"),
            ["LP2M-CF-002"] = ProductImage("Cafe au lait", "cup", "#7a3f1d", "#f3dec3"),
            ["LP2M-CF-003"] = ProductImage("Chocolat", "cup", "#4b2418", "#e0b27a"),
            ["LP2M-CF-004"] = ProductImage("The Lipton", "cup", "#9b742b", "#f1d48f"),
            ["LP2M-BF-001"] = ProductImage("Eau 0,5L", "bottle", "#3b82c4", "#dff3ff"),
            ["LP2M-BF-002"] = ProductImage("Eau 1,5L", "bottle", "#2f6fa3", "#dff3ff"),
            ["LP2M-BF-003"] = ProductImage("Soda 33cl", "bottle", "#b33b2f", "#ffd2c9"),
            ["LP2M-BF-004"] = ProductImage("Bissap", "bottle", "#7d1538", "#f4b1c4"),
            ["LP2M-BF-005"] = ProductImage("Gingembre", "bottle", "#c4932f", "#ffe6a7"),
            ["LP2M-BF-006"] = ProductImage("Biere 65cl", "bottle", "#b77722", "#f3d37a"),
            ["LP2M-PI-001"] = ProductImage("Garba", "plate", "#d6b26a", "#6b4b28"),
            ["LP2M-PI-002"] = ProductImage("Attieke poisson", "plate", "#eadbba", "#3e6b6b"),
            ["LP2M-PI-003"] = ProductImage("Alloco poisson", "plate", "#d9972b", "#496e58"),
            ["LP2M-PI-004"] = ProductImage("Poulet braise", "plate", "#a84f2a", "#f1c27d"),
            ["LP2M-PI-005"] = ProductImage("Brochette", "plate", "#8f3d1f", "#d9a441"),
            ["LP2M-SN-001"] = ProductImage("Chips plantain", "snack", "#d49b26", "#fff0b8"),
            ["LP2M-SN-002"] = ProductImage("Arachides", "snack", "#9a642f", "#f3dec3"),
            ["LP2M-SN-003"] = ProductImage("Pastels", "snack", "#c87932", "#ffe1a8"),
            ["LP2M-SP-001"] = ProductImage("Whisky verre", "glass", "#a46021", "#f2cf7e"),
            ["LP2M-SP-002"] = ProductImage("Gin tonic", "glass", "#98c7c9", "#e8fbff")
        };

        foreach (var item in images)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "UPDATE products SET image_data = $imageData WHERE sku = $sku;",
                parameters =>
                {
                    parameters.AddWithValue("$sku", item.Key);
                    parameters.AddWithValue("$imageData", item.Value);
                },
                cancellationToken);
        }

        var categoryImages = new Dictionary<string, string>
        {
            ["Cafe & chocolat"] = ProductImage("Cafe", "cup", "#4b2418", "#f3dec3"),
            ["Boissons fraiches"] = ProductImage("Boissons", "bottle", "#2f6fa3", "#dff3ff"),
            ["Plats ivoiriens"] = ProductImage("Plats", "plate", "#d9972b", "#496e58"),
            ["Snacks & accompagnements"] = ProductImage("Snacks", "snack", "#d49b26", "#fff0b8"),
            ["Spiritueux"] = ProductImage("Bar", "glass", "#a46021", "#f2cf7e")
        };

        foreach (var item in categoryImages)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "UPDATE product_categories SET image_data = $imageData WHERE name = $name;",
                parameters =>
                {
                    parameters.AddWithValue("$name", item.Key);
                    parameters.AddWithValue("$imageData", item.Value);
                },
                cancellationToken);
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO app_settings (key, value, updated_at)
            VALUES ($key, 'applied', $updatedAt)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;
            """,
            parameters =>
            {
                parameters.AddWithValue("$key", migrationKey);
                parameters.AddWithValue("$updatedAt", DateTime.Now.ToString("O"));
            },
            cancellationToken);
    }

    private static string ProductImage(string title, string shape, string primary, string secondary)
    {
        var drawing = shape switch
        {
            "bottle" => $"""
                <rect x="48" y="24" width="32" height="18" rx="5" fill="{secondary}"/>
                <rect x="44" y="38" width="40" height="66" rx="12" fill="{primary}"/>
                <rect x="50" y="58" width="28" height="24" rx="5" fill="{secondary}" opacity=".9"/>
                """,
            "plate" => $"""
                <ellipse cx="64" cy="72" rx="42" ry="24" fill="{secondary}"/>
                <ellipse cx="64" cy="70" rx="30" ry="15" fill="{primary}"/>
                <circle cx="79" cy="63" r="8" fill="#fff7df" opacity=".85"/>
                """,
            "snack" => $"""
                <path d="M36 42h56l-8 62H44z" fill="{primary}"/>
                <path d="M44 50h40l-4 22H48z" fill="{secondary}" opacity=".9"/>
                <circle cx="54" cy="86" r="7" fill="{secondary}"/>
                <circle cx="72" cy="88" r="7" fill="{secondary}"/>
                """,
            "glass" => $"""
                <path d="M42 32h44l-7 64H49z" fill="{secondary}" opacity=".95"/>
                <path d="M49 62h30l-4 28H53z" fill="{primary}"/>
                <rect x="57" y="96" width="14" height="14" rx="4" fill="{primary}"/>
                """,
            _ => $"""
                <path d="M36 46h50v28a25 25 0 0 1-50 0z" fill="{primary}"/>
                <path d="M86 54h12a13 13 0 0 1 0 26H86" fill="none" stroke="{primary}" stroke-width="7"/>
                <rect x="42" y="52" width="38" height="12" rx="6" fill="{secondary}" opacity=".9"/>
                """
        };

        var safeTitle = System.Security.SecurityElement.Escape(title) ?? title;
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128">
              <defs>
                <linearGradient id="bg" x1="0" x2="1" y1="0" y2="1">
                  <stop offset="0" stop-color="#fffaf2"/>
                  <stop offset="1" stop-color="#f3dec3"/>
                </linearGradient>
              </defs>
              <rect width="128" height="128" rx="18" fill="url(#bg)"/>
              <rect y="96" width="128" height="32" fill="#8a5a34" opacity=".2"/>
              {drawing}
              <text x="64" y="119" text-anchor="middle" font-family="Segoe UI, Arial" font-size="11" font-weight="700" fill="#2b1710">{safeTitle}</text>
            </svg>
            """;

        return $"data:image/svg+xml;charset=utf-8,{Uri.EscapeDataString(svg)}";
    }

    private static async Task<long> GetIdByNameAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string value,
        CancellationToken cancellationToken,
        string columnName = "name")
    {
        return await ScalarLongAsync(
            connection,
            transaction,
            $"SELECT id FROM {tableName} WHERE {columnName} = $value LIMIT 1;",
            parameters => parameters.AddWithValue("$value", value),
            cancellationToken);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        Action<SqliteParameterCollection>? bind = null,
        CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        bind?.Invoke(command.Parameters);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> ScalarLongAsync(
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

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0 : Convert.ToInt64(result);
    }

    private static async Task<string?> ScalarStringAsync(
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

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToString(result);
    }
}
