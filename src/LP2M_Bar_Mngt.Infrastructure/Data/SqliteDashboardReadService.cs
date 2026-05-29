using LP2M_Bar_Mngt.Application.Abstractions;
using LP2M_Bar_Mngt.Application.DTOs;
using Microsoft.Data.Sqlite;

namespace LP2M_Bar_Mngt.Infrastructure.Data;

public sealed class SqliteDashboardReadService : IDashboardReadService
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteDashboardReadService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var todayStart = DateTime.Now.Date;
        var tomorrowStart = todayStart.AddDays(1);

        var todayRevenueCents = await ScalarLongAsync(
            connection,
            """
            SELECT COALESCE(SUM(total_amount), 0)
            FROM sales
            WHERE status = 1 AND sale_date >= $start AND sale_date < $end;
            """,
            parameters =>
            {
                parameters.AddWithValue("$start", todayStart.ToString("O"));
                parameters.AddWithValue("$end", tomorrowStart.ToString("O"));
            },
            cancellationToken);

        var todayTicketCount = (int)await ScalarLongAsync(
            connection,
            """
            SELECT COUNT(1)
            FROM sales
            WHERE status = 1 AND sale_date >= $start AND sale_date < $end;
            """,
            parameters =>
            {
                parameters.AddWithValue("$start", todayStart.ToString("O"));
                parameters.AddWithValue("$end", tomorrowStart.ToString("O"));
            },
            cancellationToken);

        var openCashSessionCount = (int)await ScalarLongAsync(
            connection,
            "SELECT COUNT(1) FROM cash_sessions WHERE status = 1;",
            null,
            cancellationToken);

        var lowStockCount = (int)await ScalarLongAsync(
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

        var activeProductCount = (int)await ScalarLongAsync(
            connection,
            "SELECT COUNT(1) FROM products WHERE is_active = 1;",
            null,
            cancellationToken);

        var activeUserCount = (int)await ScalarLongAsync(
            connection,
            "SELECT COUNT(1) FROM users WHERE is_active = 1;",
            null,
            cancellationToken);

        var recentSales = await QueryRecentSalesAsync(connection, cancellationToken);
        var lowStockAlerts = await QueryLowStockAlertsAsync(connection, cancellationToken);

        return new DashboardSummary(
            todayRevenueCents,
            todayTicketCount,
            openCashSessionCount,
            lowStockCount,
            activeProductCount,
            activeUserCount,
            recentSales,
            lowStockAlerts);
    }

    private static async Task<IReadOnlyList<RecentSaleDto>> QueryRecentSalesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                s.ticket_number,
                s.sale_date,
                u.full_name,
                s.total_amount,
                CASE s.payment_method
                    WHEN 1 THEN 'Especes'
                    WHEN 2 THEN 'Carte'
                    WHEN 3 THEN 'Mobile money'
                    ELSE 'Autre'
                END AS payment_method
            FROM sales s
            INNER JOIN users u ON u.id = s.cashier_id
            ORDER BY s.sale_date DESC
            LIMIT 8;
            """;

        var result = new List<RecentSaleDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new RecentSaleDto(
                reader.GetString(0),
                DateTime.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.GetInt64(3) / 100m,
                reader.GetString(4)));
        }

        return result;
    }

    private static async Task<IReadOnlyList<LowStockAlertDto>> QueryLowStockAlertsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                p.name,
                c.name,
                COALESCE(sl.quantity, 0),
                p.low_stock_threshold
            FROM products p
            INNER JOIN product_categories c ON c.id = p.category_id
            LEFT JOIN stock_levels sl ON sl.product_id = p.id
            WHERE p.is_active = 1
              AND p.is_stock_managed = 1
              AND COALESCE(sl.quantity, 0) <= p.low_stock_threshold
            ORDER BY COALESCE(sl.quantity, 0) ASC, p.name ASC
            LIMIT 8;
            """;

        var result = new List<LowStockAlertDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new LowStockAlertDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDouble(2),
                reader.GetDouble(3)));
        }

        return result;
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
        return result is null or DBNull ? 0 : Convert.ToInt64(result);
    }
}
