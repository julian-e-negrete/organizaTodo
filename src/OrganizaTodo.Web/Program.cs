using Microsoft.AspNetCore.Authentication.Cookies;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Repositories;
using OrganizaTodo.Web.Services;

// Load .env into environment variables (local dev; Docker sets them directly)
// Walk up from CWD to find .env — works regardless of where dotnet run is invoked from
static string? FindEnvFile()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir != null)
    {
        var path = Path.Combine(dir.FullName, ".env");
        if (File.Exists(path)) return path;
        dir = dir.Parent;
    }
    return null;
}

var envFile = FindEnvFile();
if (envFile != null)
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
        var idx = line.IndexOf('=');
        if (idx > 0)
            Environment.SetEnvironmentVariable(line[..idx].Trim(), line[(idx + 1)..].Trim());
    }
}

// Build ConnectionStrings__DefaultConnection from POSTGRES_* vars if not already set
if (Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") is null)
{
    var host = Environment.GetEnvironmentVariable("POSTGRES_HOST");
    var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
    var db   = Environment.GetEnvironmentVariable("POSTGRES_DB");
    var user = Environment.GetEnvironmentVariable("POSTGRES_USER");
    var pass = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
    if (host is not null)
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            $"Host={host};Port={port};Database={db};Username={user};Password={pass};Ssl Mode=Disable");
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();

builder.Services.AddScoped<IUserRepository,            UserRepository>();
builder.Services.AddScoped<IHousingServiceRepository,  HousingServiceRepository>();
builder.Services.AddScoped<ICreditCardRepository,      CreditCardRepository>();
builder.Services.AddScoped<IFixedLiabilityRepository,  FixedLiabilityRepository>();
builder.Services.AddScoped<IIncomeRepository,          IncomeRepository>();
builder.Services.AddScoped<IOtherExpenseRepository,    OtherExpenseRepository>();
builder.Services.AddScoped<IShoppingRepository,        ShoppingRepository>();
builder.Services.AddScoped<IMockProductRepository,     MockProductRepository>();
builder.Services.AddScoped<ISavingRepository,          SavingRepository>();

builder.Services.AddScoped<IAuthService,    AuthService>();
builder.Services.AddScoped<IBalanceService, BalanceService>();
builder.Services.AddScoped<IAdminService,   AdminService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath        = "/Account/Login";
        options.LogoutPath       = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan   = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
