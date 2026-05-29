namespace LP2M_Bar_Mngt.Infrastructure.Data;

internal static class SqliteSchema
{
    public const string CreateTables = """
        CREATE TABLE IF NOT EXISTS roles (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL UNIQUE,
            is_system INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS users (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            username TEXT NOT NULL UNIQUE,
            password_hash TEXT NOT NULL,
            full_name TEXT NOT NULL,
            role_id INTEGER NOT NULL,
            is_active INTEGER NOT NULL DEFAULT 1,
            is_hidden INTEGER NOT NULL DEFAULT 0,
            two_factor_enabled INTEGER NOT NULL DEFAULT 0,
            two_factor_secret TEXT NULL,
            two_factor_configured_at TEXT NULL,
            created_at TEXT NOT NULL,
            last_login_at TEXT NULL,
            FOREIGN KEY (role_id) REFERENCES roles(id)
        );

        CREATE TABLE IF NOT EXISTS product_categories (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL UNIQUE,
            is_active INTEGER NOT NULL DEFAULT 1,
            is_hidden INTEGER NOT NULL DEFAULT 0,
            image_data TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS products (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            category_id INTEGER NOT NULL,
            name TEXT NOT NULL,
            sku TEXT NULL UNIQUE,
            barcode TEXT NULL UNIQUE,
            sale_price INTEGER NOT NULL,
            cost_price INTEGER NOT NULL,
            is_stock_managed INTEGER NOT NULL DEFAULT 1,
            low_stock_threshold REAL NOT NULL DEFAULT 0,
            is_active INTEGER NOT NULL DEFAULT 1,
            is_hidden INTEGER NOT NULL DEFAULT 0,
            image_data TEXT NULL,
            created_at TEXT NOT NULL,
            FOREIGN KEY (category_id) REFERENCES product_categories(id)
        );

        CREATE TABLE IF NOT EXISTS stock_levels (
            product_id INTEGER PRIMARY KEY,
            quantity REAL NOT NULL DEFAULT 0,
            FOREIGN KEY (product_id) REFERENCES products(id)
        );

        CREATE TABLE IF NOT EXISTS cash_sessions (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            cashier_id INTEGER NOT NULL,
            opened_at TEXT NOT NULL,
            closed_at TEXT NULL,
            opening_amount INTEGER NOT NULL DEFAULT 0,
            expected_closing_amount INTEGER NOT NULL DEFAULT 0,
            declared_closing_amount INTEGER NULL,
            difference_amount INTEGER NULL,
            is_hidden INTEGER NOT NULL DEFAULT 0,
            status INTEGER NOT NULL,
            FOREIGN KEY (cashier_id) REFERENCES users(id)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ux_cash_sessions_one_open_per_cashier
            ON cash_sessions(cashier_id)
            WHERE status = 1;

        CREATE TABLE IF NOT EXISTS cash_movements (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            cash_session_id INTEGER NOT NULL,
            movement_type INTEGER NOT NULL,
            amount INTEGER NOT NULL,
            description TEXT NOT NULL,
            user_id INTEGER NOT NULL,
            created_at TEXT NOT NULL,
            FOREIGN KEY (cash_session_id) REFERENCES cash_sessions(id),
            FOREIGN KEY (user_id) REFERENCES users(id)
        );

        CREATE TABLE IF NOT EXISTS sales (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            ticket_number TEXT NOT NULL UNIQUE,
            cash_session_id INTEGER NOT NULL,
            cashier_id INTEGER NOT NULL,
            customer_name TEXT NULL,
            sale_date TEXT NOT NULL,
            subtotal_amount INTEGER NOT NULL,
            discount_amount INTEGER NOT NULL DEFAULT 0,
            total_amount INTEGER NOT NULL,
            payment_method INTEGER NOT NULL,
            status INTEGER NOT NULL,
            is_hidden INTEGER NOT NULL DEFAULT 0,
            cancelled_at TEXT NULL,
            cancelled_by INTEGER NULL,
            cancel_reason TEXT NULL,
            FOREIGN KEY (cash_session_id) REFERENCES cash_sessions(id),
            FOREIGN KEY (cashier_id) REFERENCES users(id),
            FOREIGN KEY (cancelled_by) REFERENCES users(id)
        );

        CREATE TABLE IF NOT EXISTS sale_items (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            sale_id INTEGER NOT NULL,
            product_id INTEGER NOT NULL,
            product_name TEXT NOT NULL,
            quantity REAL NOT NULL,
            unit_price INTEGER NOT NULL,
            discount_amount INTEGER NOT NULL DEFAULT 0,
            total_amount INTEGER NOT NULL,
            FOREIGN KEY (sale_id) REFERENCES sales(id),
            FOREIGN KEY (product_id) REFERENCES products(id)
        );

        CREATE TABLE IF NOT EXISTS stock_movements (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            product_id INTEGER NOT NULL,
            movement_type INTEGER NOT NULL,
            quantity REAL NOT NULL,
            reason TEXT NOT NULL,
            sale_id INTEGER NULL,
            user_id INTEGER NOT NULL,
            created_at TEXT NOT NULL,
            FOREIGN KEY (product_id) REFERENCES products(id),
            FOREIGN KEY (sale_id) REFERENCES sales(id),
            FOREIGN KEY (user_id) REFERENCES users(id)
        );

        CREATE TABLE IF NOT EXISTS expense_categories (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL UNIQUE,
            is_active INTEGER NOT NULL DEFAULT 1,
            is_hidden INTEGER NOT NULL DEFAULT 0,
            image_data TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS business_profile (
            id INTEGER PRIMARY KEY CHECK (id = 1),
            name TEXT NOT NULL,
            sigle TEXT NOT NULL,
            address TEXT NOT NULL,
            contact TEXT NOT NULL,
            logo_data TEXT NULL,
            cover_image_data TEXT NULL,
            ticket_footer TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS app_settings (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS expenses (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            category_id INTEGER NOT NULL,
            cash_session_id INTEGER NULL,
            user_id INTEGER NOT NULL,
            amount INTEGER NOT NULL,
            description TEXT NOT NULL,
            expense_date TEXT NOT NULL,
            paid_from_cash_register INTEGER NOT NULL DEFAULT 0,
            created_at TEXT NOT NULL,
            status TEXT NOT NULL,
            is_hidden INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY (category_id) REFERENCES expense_categories(id),
            FOREIGN KEY (cash_session_id) REFERENCES cash_sessions(id),
            FOREIGN KEY (user_id) REFERENCES users(id)
        );

        CREATE TABLE IF NOT EXISTS audit_logs (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id INTEGER NULL,
            action TEXT NOT NULL,
            entity_type TEXT NOT NULL,
            entity_id INTEGER NULL,
            details TEXT NOT NULL,
            created_at TEXT NOT NULL,
            FOREIGN KEY (user_id) REFERENCES users(id)
        );

        CREATE INDEX IF NOT EXISTS ix_sales_sale_date ON sales(sale_date);
        CREATE INDEX IF NOT EXISTS ix_sales_ticket_number ON sales(ticket_number);
        CREATE INDEX IF NOT EXISTS ix_sales_cash_session_id ON sales(cash_session_id);
        CREATE INDEX IF NOT EXISTS ix_sale_items_sale_id ON sale_items(sale_id);
        CREATE INDEX IF NOT EXISTS ix_stock_movements_product_id ON stock_movements(product_id);
        CREATE INDEX IF NOT EXISTS ix_stock_movements_created_at ON stock_movements(created_at);
        CREATE INDEX IF NOT EXISTS ix_cash_sessions_cashier_id ON cash_sessions(cashier_id);
        CREATE INDEX IF NOT EXISTS ix_cash_sessions_status ON cash_sessions(status);
        CREATE INDEX IF NOT EXISTS ix_expenses_expense_date ON expenses(expense_date);
        CREATE INDEX IF NOT EXISTS ix_products_name ON products(name);
        CREATE INDEX IF NOT EXISTS ix_products_barcode ON products(barcode);
        """;
}
