using InvoicesErp.Data;
using InvoicesErp.Interfaces;
using InvoicesErp.Middleware;
using InvoicesErp.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// MVC + API Controllers (same controllers serve /api/*)
builder.Services
    .AddControllersWithViews()
    .ConfigureApiBehaviorOptions(options =>
    {
        // لا تجعل ASP.NET يرجع 400 تلقائيًا قبل وصول الطلب للـ Controller.
        options.SuppressModelStateInvalidFilter = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// SOLID: depend on abstractions (DIP)
builder.Services.AddSingleton<InvoicesErp.Interfaces.IIdGenerator, InvoicesErp.Services.GuidIdGenerator>();
builder.Services.AddScoped<InvoicesErp.Interfaces.IAuthService, InvoicesErp.Services.AuthService>();
builder.Services.AddScoped<InvoicesErp.Interfaces.IUserStore, InvoicesErp.Services.UserStore>();
builder.Services.AddScoped<InvoicesErp.Interfaces.IBootstrapService, InvoicesErp.Services.BootstrapService>();
builder.Services.AddScoped<InvoicesErp.Interfaces.IOwnerService, InvoicesErp.Services.OwnerService>();
builder.Services.AddScoped<InvoicesErp.Interfaces.ICostControlCalculator, InvoicesErp.Services.Calculations.CostControlCalculator>();
// Concrete still available for any legacy resolution
builder.Services.AddScoped<InvoicesErp.Services.BootstrapService>();
builder.Services.AddScoped<InvoicesErp.Services.UserStore>();
builder.Services.AddScoped<InvoicesErp.Services.AuthService>();
builder.Services.AddScoped<ISectorService, SectorService>();
builder.Services.AddScoped<ISectorManagerService, SectorManagerService>();


// CORS — kept for optional separate frontend during transition; same-origin preferred
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var configuredOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
            ?? ["http://localhost:5173", "http://localhost:5180", "http://localhost:5174", "http://localhost:3000", "http://localhost:5000", "http://localhost:5080"];

        policy
            .WithOrigins(configuredOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");

app.UseStaticFiles();
app.UseRouting();

// Auth: parse Bearer token → HttpContext.Items["AppUser"]
app.UseMiddleware<AuthMiddleware>();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Verify DB connectivity, initialize the supply catalog schema, and seed the default admin.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (await db.Database.CanConnectAsync())
    {
        // The standalone supply catalog is an authorized feature schema addition.
        // Apply its idempotent SQL batches when the configured database is available.
        var supplySchemaPath = Path.Combine(app.Environment.ContentRootPath, "Database", "SupplyCatalogSchema.sql");
        if (File.Exists(supplySchemaPath))
        {
            var supplySchema = await File.ReadAllTextAsync(supplySchemaPath);
            foreach (var batch in Regex.Split(supplySchema, @"(?im)^\s*GO\s*;?\s*$")
                         .Where(sql => !string.IsNullOrWhiteSpace(sql)))
            {
                await db.Database.ExecuteSqlRawAsync(batch);
            }
            app.Logger.LogInformation("Supply catalog schema is ready.");
        }
        else
        {
            app.Logger.LogWarning("Supply catalog schema script was not found at {SchemaPath}.", supplySchemaPath);
        }

        await UserStore.SeedAdminAsync(db);
    }
    else
    {
        app.Logger.LogWarning("Cannot connect to SQL Server. API data endpoints will fail until DefaultConnection is configured.");
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Database initialization skipped. Configure DefaultConnection in appsettings.json.");
}

// Production safety: Auth signing key must not be empty or the well-known dev default.
if (app.Environment.IsProduction())
{
    var signKey = app.Configuration["Auth:SigningKey"] ?? "";
    if (string.IsNullOrWhiteSpace(signKey) ||
        signKey.Contains("Change-In-Production", StringComparison.OrdinalIgnoreCase) ||
        signKey.Contains("Dev-Signing-Key", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Auth:SigningKey must be set to a strong secret via environment/configuration in Production.");
    }
    var cs = app.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(cs))
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured in Production.");
}

app.Run();
