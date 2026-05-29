using System.Globalization;
using System.Security.Claims;
using System.Text;
using LP2M_Bar_Mngt.Application.Abstractions;
using LP2M_Bar_Mngt.Application.DTOs;
using LP2M_Bar_Mngt.Infrastructure.Data;
using LP2M_Bar_Mngt.Infrastructure.Security;
using LP2M_Bar_Mngt.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

var connectionFactory = SqliteConnectionFactory.CreateDefault();
var passwordHasher = new PasswordHasher();

builder.Services.AddSingleton(connectionFactory);
builder.Services.AddSingleton(passwordHasher);
builder.Services.AddSingleton<TotpService>();
builder.Services.AddSingleton<TwoFactorChallengeStore>();
builder.Services.AddSingleton<IApplicationDatabaseInitializer, SqliteDatabaseInitializer>();
builder.Services.AddSingleton<IDashboardReadService, SqliteDashboardReadService>();
builder.Services.AddSingleton<IOperationsService, SqliteOperationsService>();
builder.Services.AddSingleton<IWebManagementService, SqliteWebManagementService>();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "LP2M_Bar_Mngt.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(feature?.Error.Message ?? "Erreur de traitement.");
    });
});

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        !context.Request.Path.StartsWithSegments("/api/auth") &&
        context.User.Identity?.IsAuthenticated != true)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync("Authentification requise.");
        return;
    }

    await next();
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "OK",
    application = "LP2M_Bar_Mngt",
    time = DateTimeOffset.UtcNow
}));

app.MapGet("/api/auth/session", (ClaimsPrincipal user) =>
{
    return Results.Ok(CreateSession(user));
});

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    HttpContext httpContext,
    IApplicationDatabaseInitializer initializer,
    SqliteConnectionFactory connections,
    PasswordHasher hasher,
    TwoFactorChallengeStore twoFactorChallenges,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);

    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.Text("Identifiant et mot de passe obligatoires.", statusCode: StatusCodes.Status400BadRequest);
    }

    await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT
            u.id, u.username, u.password_hash, u.full_name, u.is_active, r.name, u.is_hidden,
            u.two_factor_enabled, u.two_factor_secret
        FROM users u
        INNER JOIN roles r ON r.id = u.role_id
        WHERE lower(u.username) = lower($username)
        LIMIT 1;
        """;
    command.Parameters.AddWithValue("$username", request.Username.Trim());
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);

    if (!await reader.ReadAsync(cancellationToken))
    {
        return Results.Text("Identifiant ou mot de passe incorrect.", statusCode: StatusCodes.Status401Unauthorized);
    }

    var isActive = reader.GetInt64(4) == 1;
    var passwordHash = reader.GetString(2);
    var isHidden = reader.GetInt64(6) == 1;
    if (!isActive || isHidden || !hasher.Verify(request.Password, passwordHash))
    {
        return Results.Text("Identifiant ou mot de passe incorrect.", statusCode: StatusCodes.Status401Unauthorized);
    }

    var userId = reader.GetInt64(0);
    var username = reader.GetString(1);
    var fullName = reader.GetString(3);
    var role = reader.GetString(5);
    var twoFactorEnabled = reader.GetInt64(7) == 1;
    var twoFactorSecret = reader.IsDBNull(8) ? null : reader.GetString(8);
    if (twoFactorEnabled && !string.IsNullOrWhiteSpace(twoFactorSecret))
    {
        var challenge = twoFactorChallenges.Create(userId, username, fullName, role, twoFactorSecret, request.RememberMe);
        return Results.Ok(new AuthSessionResponse(false, username, null, null, true, challenge.Id, "Code de double authentification requis."));
    }

    await SignInUserAsync(httpContext, userId, username, fullName, role, request.RememberMe);

    return Results.Ok(new AuthSessionResponse(true, username, fullName, role));
});

app.MapPost("/api/auth/two-factor", async (
    TwoFactorLoginRequest request,
    HttpContext httpContext,
    TwoFactorChallengeStore twoFactorChallenges,
    TotpService totpService) =>
{
    var challenge = twoFactorChallenges.Verify(request.ChallengeId, request.Code, totpService);
    if (challenge is null)
    {
        return Results.Text("Code de double authentification invalide ou expire.", statusCode: StatusCodes.Status401Unauthorized);
    }

    await SignInUserAsync(httpContext, challenge.UserId, challenge.Username, challenge.FullName, challenge.Role, challenge.RememberMe);
    return Results.Ok(new AuthSessionResponse(true, challenge.Username, challenge.FullName, challenge.Role));
});

app.MapPost("/api/auth/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new AuthSessionResponse(false, null, null, null));
});

app.MapGet("/api/data", async (
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.GetDataSetAsync(cancellationToken));
});

app.MapGet("/api/dashboard", async (
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.GetDashboardAsync(cancellationToken));
});

app.MapPost("/api/profile", async (
    BusinessProfileSaveRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.SaveBusinessProfileAsync(request, cancellationToken));
});

app.MapGet("/api/tickets/last", async (
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.GetTicketAsync(null, cancellationToken));
});

app.MapGet("/api/tickets/{saleId:long}", async (
    long saleId,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.GetTicketAsync(saleId, cancellationToken));
});

app.MapGet("/api/exports/{exportType}", async (
    string exportType,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    var data = await management.GetDataSetAsync(cancellationToken);
    var csv = BuildCsv(exportType, data);
    var fileName = $"lp2m-{exportType}-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
    return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
});

app.MapPost("/api/cash/open", async (IApplicationDatabaseInitializer initializer, IOperationsService operations, CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await operations.OpenCashSessionAsync(cancellationToken));
});

app.MapPost("/api/cash/open-session", async (
    CashOpenRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.OpenCashSessionAsync(request, cancellationToken));
});

app.MapPost("/api/cash/close", async (IApplicationDatabaseInitializer initializer, IOperationsService operations, CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await operations.CloseCashSessionAsync(cancellationToken));
});

app.MapPost("/api/cash/close-session", async (
    CashCloseRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.CloseCashSessionAsync(request, cancellationToken));
});

app.MapPost("/api/sales/quick", async (IApplicationDatabaseInitializer initializer, IOperationsService operations, CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await operations.CreateSaleAsync(cancellationToken));
});

app.MapPost("/api/sales", async (
    SaleCreateRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.CreateSaleAsync(request, cancellationToken));
});

app.MapPost("/api/sales/cart", async (
    SaleCartCreateRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.CreateSaleCartAsync(request, cancellationToken));
});

app.MapPost("/api/sales/cancel-last", async (IApplicationDatabaseInitializer initializer, IOperationsService operations, CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await operations.CancelLastSaleAsync(cancellationToken));
});

app.MapPost("/api/tickets/reprint-last", async (IApplicationDatabaseInitializer initializer, IOperationsService operations, CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await operations.ReprintLastTicketAsync(cancellationToken));
});

app.MapPost("/api/products", async (
    ProductSaveRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.SaveProductAsync(request, cancellationToken));
});

app.MapPut("/api/products/{id:long}", async (
    long id,
    ProductSaveRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    request = request with { Id = id };
    return Results.Ok(await management.SaveProductAsync(request, cancellationToken));
});

app.MapPost("/api/products/{id:long}/active", async (
    long id,
    ProductActiveRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.SetProductActiveAsync(id, request.IsActive, cancellationToken));
});

app.MapPost("/api/categories", async (
    CategoryCreateRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.CreateCategoryAsync(request, cancellationToken));
});

app.MapPost("/api/stock/adjust", async (
    StockAdjustmentRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.AdjustStockAsync(request, cancellationToken));
});

app.MapPost("/api/stock/restock-low", async (IApplicationDatabaseInitializer initializer, IOperationsService operations, CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await operations.RestockLowProductsAsync(cancellationToken));
});

app.MapPost("/api/expenses", async (
    ExpenseCreateRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.CreateExpenseAsync(request, cancellationToken));
});

app.MapPost("/api/users", async (
    UserSaveRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.SaveUserAsync(request, cancellationToken));
});

app.MapPut("/api/users/{id:long}", async (
    long id,
    UserSaveRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    request = request with { Id = id };
    return Results.Ok(await management.SaveUserAsync(request, cancellationToken));
});

app.MapPost("/api/users/{id:long}/active", async (
    long id,
    UserActiveRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.SetUserActiveAsync(id, request.IsActive, cancellationToken));
});

app.MapPost("/api/users/{id:long}/two-factor/setup", async (
    long id,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.ResetTwoFactorSecretAsync(id, cancellationToken));
});

app.MapPost("/api/objects/{objectType}/{id:long}/hidden", async (
    string objectType,
    long id,
    ObjectHiddenRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.SetObjectHiddenAsync(new ObjectVisibilityRequest(objectType, id, request.IsHidden), cancellationToken));
});

app.MapPost("/api/objects/{objectType}/{id:long}/delete", async (
    string objectType,
    long id,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.DeleteObjectAsync(objectType, id, cancellationToken));
});

app.MapPost("/api/objects/bulk/hidden", async (
    BulkObjectRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.SetObjectsHiddenAsync(request, cancellationToken));
});

app.MapPost("/api/objects/bulk/delete", async (
    BulkObjectRequest request,
    IApplicationDatabaseInitializer initializer,
    IWebManagementService management,
    CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await management.DeleteObjectsAsync(request.Objects, cancellationToken));
});

app.MapPost("/api/users/reset-admin", async (IApplicationDatabaseInitializer initializer, IOperationsService operations, CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await operations.ResetAdminPasswordAsync(cancellationToken));
});

app.MapPost("/api/audit/check", async (IApplicationDatabaseInitializer initializer, IOperationsService operations, CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await operations.WriteAuditEntryAsync(cancellationToken));
});

app.MapPost("/api/reports/daily", async (IApplicationDatabaseInitializer initializer, IOperationsService operations, CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await operations.GenerateDailyReportAsync(cancellationToken));
});

app.MapPost("/api/reports/export", async (IApplicationDatabaseInitializer initializer, IOperationsService operations, CancellationToken cancellationToken) =>
{
    await initializer.InitializeAsync(cancellationToken);
    return Results.Ok(await operations.ExportDailyReportAsync(cancellationToken));
});

app.MapFallbackToFile("index.html");

static string BuildCsv(string exportType, WebDataSetDto data)
{
    var rows = new List<string[]>();
    switch (exportType.Trim().ToLowerInvariant())
    {
        case "products":
            rows.Add(["Id", "Produit", "Categorie", "Prix", "Cout", "Stock", "Actif", "Masque"]);
            rows.AddRange(data.Products.Select(p => new[]
            {
                p.Id.ToString(CultureInfo.InvariantCulture),
                p.Name,
                p.CategoryName,
                p.SalePrice.ToString(CultureInfo.InvariantCulture),
                p.CostPrice.ToString(CultureInfo.InvariantCulture),
                p.Quantity.ToString(CultureInfo.InvariantCulture),
                p.IsActive ? "Oui" : "Non",
                p.IsHidden ? "Oui" : "Non"
            }));
            break;
        case "stock":
            rows.Add(["Produit", "Categorie", "Quantite", "Seuil", "Alerte", "Masque"]);
            rows.AddRange(data.Stock.Select(s => new[]
            {
                s.ProductName,
                s.CategoryName,
                s.Quantity.ToString(CultureInfo.InvariantCulture),
                s.LowStockThreshold.ToString(CultureInfo.InvariantCulture),
                s.IsLowStock ? "Oui" : "Non",
                s.IsHidden ? "Oui" : "Non"
            }));
            break;
        case "expenses":
            rows.Add(["Id", "Categorie", "Description", "Montant", "Depuis caisse", "Date", "Statut", "Masque"]);
            rows.AddRange(data.Expenses.Select(e => new[]
            {
                e.Id.ToString(CultureInfo.InvariantCulture),
                e.CategoryName,
                e.Description,
                e.Amount.ToString(CultureInfo.InvariantCulture),
                e.PaidFromCashRegister ? "Oui" : "Non",
                e.ExpenseDate.ToString("O", CultureInfo.InvariantCulture),
                e.Status,
                e.IsHidden ? "Oui" : "Non"
            }));
            break;
        case "users":
            rows.Add(["Id", "Identifiant", "Nom", "Role", "Actif", "Double auth", "Creation", "Masque"]);
            rows.AddRange(data.Users.Select(u => new[]
            {
                u.Id.ToString(CultureInfo.InvariantCulture),
                u.Username,
                u.FullName,
                u.RoleName,
                u.IsActive ? "Oui" : "Non",
                u.TwoFactorEnabled ? "Oui" : "Non",
                u.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
                u.IsHidden ? "Oui" : "Non"
            }));
            break;
        case "sales":
        default:
            rows.Add(["Id", "Ticket", "Client", "Caissier", "Total", "Paiement", "Statut", "Date", "Masque"]);
            rows.AddRange(data.Sales.Select(s => new[]
            {
                s.Id.ToString(CultureInfo.InvariantCulture),
                s.TicketNumber,
                s.CustomerName,
                s.CashierName,
                s.TotalAmount.ToString(CultureInfo.InvariantCulture),
                s.PaymentMethod,
                s.Status,
                s.SaleDate.ToString("O", CultureInfo.InvariantCulture),
                s.IsHidden ? "Oui" : "Non"
            }));
            break;
    }

    return string.Join(Environment.NewLine, rows.Select(row => string.Join(";", row.Select(EscapeCsv))));
}

static string EscapeCsv(string value)
{
    var safe = value.Replace("\"", "\"\"", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    return $"\"{safe}\"";
}

static AuthSessionResponse CreateSession(ClaimsPrincipal user)
{
    if (user.Identity?.IsAuthenticated != true)
    {
        return new AuthSessionResponse(false, null, null, null);
    }

    return new AuthSessionResponse(
        true,
        user.Identity.Name,
        user.FindFirstValue(ClaimTypes.GivenName),
        user.FindFirstValue(ClaimTypes.Role));
}

static Task SignInUserAsync(HttpContext httpContext, long userId, string username, string fullName, string role, bool rememberMe)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, userId.ToString(CultureInfo.InvariantCulture)),
        new(ClaimTypes.Name, username),
        new(ClaimTypes.GivenName, fullName),
        new(ClaimTypes.Role, role)
    };

    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    return httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(rememberMe ? 24 : 8)
        });
}

app.Run();

internal sealed record ProductActiveRequest(bool IsActive);
internal sealed record UserActiveRequest(bool IsActive);
internal sealed record ObjectHiddenRequest(bool IsHidden);
internal sealed record LoginRequest(string Username, string Password, bool RememberMe);
internal sealed record TwoFactorLoginRequest(string ChallengeId, string Code);
internal sealed record AuthSessionResponse(
    bool Authenticated,
    string? Username,
    string? FullName,
    string? Role,
    bool RequiresTwoFactor = false,
    string? ChallengeId = null,
    string? Message = null);
