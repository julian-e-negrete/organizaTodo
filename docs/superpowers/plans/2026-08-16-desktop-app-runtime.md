# OrganizaTodo Desktop App Runtime — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `OrganizaTodo.Desktop`, a self-contained native app that bundles PostgreSQL, hosts the existing `OrganizaTodo.Web` app's Kestrel server in-process, and opens it in a Photino.NET window — runnable and fully testable via `dotnet run`/`dotnet publish` on a dev machine, ahead of OS installer packaging (a separate follow-up plan).

**Architecture:** `OrganizaTodo.Desktop` owns a strict startup sequence (paths → ports → bundled Postgres → schema bootstrap → in-process Kestrel → Photino window) and the reverse on shutdown. `OrganizaTodo.Web` is refactored to expose `Program.BuildApp(args, configure)` so Desktop can host it in-process without touching its application logic; the Web project's own `Main()`/Docker path is unaffected.

**Tech Stack:** .NET 8, C# nullable-enabled, Dapper + Npgsql (no EF Core), Photino.NET, xUnit.

**Spec:** [docs/superpowers/specs/2026-08-16-self-hosted-offline-installer-design.md](../specs/2026-08-16-self-hosted-offline-installer-design.md)

## Global Constraints

- Kestrel and PostgreSQL bind to `127.0.0.1` only — never `0.0.0.0`.
- No EF Core anywhere in this plan — use Dapper for any new SQL access code, per the project's existing convention.
- Photino.NET, not Electron.
- `OrganizaTodo.Web`'s existing Docker/server deployment path (external Postgres, `.env`-driven config, `Main()` behavior) must remain unchanged and fully functional.
- Bundled-Postgres binary vendoring, WebView2 redistributable bundling, and OS installer packaging (Inno Setup / shell installer) are **out of scope** for this plan — covered by the follow-up installer-packaging plan. This plan uses whatever Postgres binaries are discoverable on the dev/CI machine (via `pg_config --bindir` or `ORGANIZATODO_PG_BIN_DIR`), not vendored ones.
- Known, accepted limitation (per spec): Pinecone and the scraped shopping catalog (`carrefour_products`/`coto_products` — not created by any SQL file in this repo) don't work in the offline desktop build. Not solved by this plan.
- Follow the project's superpowers TDD workflow: write the failing test before the implementation for every task below.

---

### Task 1: Scaffold `OrganizaTodo.Desktop` and its test project

**Files:**
- Create: `src/OrganizaTodo.Desktop/OrganizaTodo.Desktop.csproj`
- Create: `src/OrganizaTodo.Desktop/Program.cs` (placeholder `Console.WriteLine` for now — real wiring is Task 11)
- Create: `tests/OrganizaTodo.Desktop.Tests/OrganizaTodo.Desktop.Tests.csproj`
- Create: `tests/OrganizaTodo.Desktop.Tests/ScaffoldingTests.cs`
- Modify: `OrganizaTodo.sln`

**Interfaces:** None — pure scaffolding. Later tasks add real files under `src/OrganizaTodo.Desktop/Lifecycle/`.

- [ ] **Step 1: Create the Desktop project and add it to the solution**

```bash
dotnet new console -o src/OrganizaTodo.Desktop -n OrganizaTodo.Desktop
dotnet sln OrganizaTodo.sln add src/OrganizaTodo.Desktop/OrganizaTodo.Desktop.csproj
dotnet add src/OrganizaTodo.Desktop/OrganizaTodo.Desktop.csproj reference src/OrganizaTodo.Web/OrganizaTodo.Web.csproj
dotnet add src/OrganizaTodo.Desktop/OrganizaTodo.Desktop.csproj package Photino.NET
```

- [ ] **Step 2: Enable nullable/implicit usings and vendor the SQL files as content**

Edit `src/OrganizaTodo.Desktop/OrganizaTodo.Desktop.csproj` so it reads:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>OrganizaTodo.Desktop</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Photino.NET" Version="*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\OrganizaTodo.Web\OrganizaTodo.Web.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="..\OrganizaTodo.Web\Database\*.sql">
      <Link>Database\%(Filename)%(Extension)</Link>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>

</Project>
```

Leave the `Version="*"` from `dotnet add package` if it pinned a specific version instead — either is fine, just don't hand-edit it to a guessed number.

- [ ] **Step 3: Create the test project**

```bash
dotnet new xunit -o tests/OrganizaTodo.Desktop.Tests -n OrganizaTodo.Desktop.Tests
dotnet sln OrganizaTodo.sln add tests/OrganizaTodo.Desktop.Tests/OrganizaTodo.Desktop.Tests.csproj
dotnet add tests/OrganizaTodo.Desktop.Tests/OrganizaTodo.Desktop.Tests.csproj reference src/OrganizaTodo.Desktop/OrganizaTodo.Desktop.csproj
rm tests/OrganizaTodo.Desktop.Tests/UnitTest1.cs
```

- [ ] **Step 4: Write the scaffolding smoke test**

`tests/OrganizaTodo.Desktop.Tests/ScaffoldingTests.cs`:

```csharp
namespace OrganizaTodo.Desktop.Tests;

public class ScaffoldingTests
{
    [Fact]
    public void TestProjectIsWiredUp()
    {
        Assert.True(true);
    }
}
```

- [ ] **Step 5: Run the test suite**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests`
Expected: PASS (1 test)

- [ ] **Step 6: Commit**

```bash
git add OrganizaTodo.sln src/OrganizaTodo.Desktop tests/OrganizaTodo.Desktop.Tests
git commit -m "scaffold OrganizaTodo.Desktop and its test project"
```

---

### Task 2: `AppPaths` — per-OS data directory layout

**Files:**
- Create: `src/OrganizaTodo.Desktop/Lifecycle/AppPaths.cs`
- Test: `tests/OrganizaTodo.Desktop.Tests/AppPathsTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record AppPaths(string DataDir, string PgDataDir, string LogsDir, string ConfigDir)`
  - `public static AppPaths AppPaths.From(string appDataRoot)`
  - `public void AppPaths.EnsureDirectoriesExist()`
  - `public static class AppDataRoot { public static string ResolveForCurrentOS(); }`

- [ ] **Step 1: Write the failing test**

`tests/OrganizaTodo.Desktop.Tests/AppPathsTests.cs`:

```csharp
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class AppPathsTests
{
    [Fact]
    public void From_ComposesSubdirectoriesUnderOrganizaTodoFolder()
    {
        var paths = AppPaths.From("/fake/root");

        Assert.Equal(Path.Combine("/fake/root", "organizatodo"), paths.DataDir);
        Assert.Equal(Path.Combine("/fake/root", "organizatodo", "pgdata"), paths.PgDataDir);
        Assert.Equal(Path.Combine("/fake/root", "organizatodo", "logs"), paths.LogsDir);
        Assert.Equal(Path.Combine("/fake/root", "organizatodo", "config"), paths.ConfigDir);
    }

    [Fact]
    public void EnsureDirectoriesExist_CreatesAllFourDirectories()
    {
        var root = Directory.CreateTempSubdirectory("organizatodo-apppaths-test-").FullName;
        try
        {
            var paths = AppPaths.From(root);
            paths.EnsureDirectoriesExist();

            Assert.True(Directory.Exists(paths.DataDir));
            Assert.True(Directory.Exists(paths.PgDataDir));
            Assert.True(Directory.Exists(paths.LogsDir));
            Assert.True(Directory.Exists(paths.ConfigDir));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter AppPathsTests`
Expected: FAIL (compile error — `AppPaths` doesn't exist yet)

- [ ] **Step 3: Implement `AppPaths`**

`src/OrganizaTodo.Desktop/Lifecycle/AppPaths.cs`:

```csharp
namespace OrganizaTodo.Desktop.Lifecycle;

public sealed record AppPaths(string DataDir, string PgDataDir, string LogsDir, string ConfigDir)
{
    public static AppPaths From(string appDataRoot)
    {
        var dataDir = Path.Combine(appDataRoot, "organizatodo");
        return new AppPaths(
            DataDir: dataDir,
            PgDataDir: Path.Combine(dataDir, "pgdata"),
            LogsDir: Path.Combine(dataDir, "logs"),
            ConfigDir: Path.Combine(dataDir, "config"));
    }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(PgDataDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(ConfigDir);
    }
}

public static class AppDataRoot
{
    public static string ResolveForCurrentOS()
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share");
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter AppPathsTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add src/OrganizaTodo.Desktop/Lifecycle/AppPaths.cs tests/OrganizaTodo.Desktop.Tests/AppPathsTests.cs
git commit -m "add AppPaths for per-OS data directory layout"
```

---

### Task 3: `PortAllocator` — loopback port selection with fallback

**Files:**
- Create: `src/OrganizaTodo.Desktop/Lifecycle/PortAllocator.cs`
- Test: `tests/OrganizaTodo.Desktop.Tests/PortAllocatorTests.cs`

**Interfaces:**
- Produces:
  - `public static bool PortAllocator.IsPortAvailable(int port)`
  - `public static int PortAllocator.FindAvailablePort(int preferredPort)`
  - `public static int PortAllocator.FindAvailablePort()`

- [ ] **Step 1: Write the failing test**

`tests/OrganizaTodo.Desktop.Tests/PortAllocatorTests.cs`:

```csharp
using System.Net;
using System.Net.Sockets;
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class PortAllocatorTests
{
    [Fact]
    public void FindAvailablePort_WithPreferredPortFree_ReturnsIt()
    {
        var preferred = PortAllocator.FindAvailablePort();

        var result = PortAllocator.FindAvailablePort(preferred);

        Assert.Equal(preferred, result);
    }

    [Fact]
    public void FindAvailablePort_WithPreferredPortTaken_ReturnsDifferentAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var takenPort = ((IPEndPoint)listener.LocalEndpoint).Port;

        var result = PortAllocator.FindAvailablePort(takenPort);

        Assert.NotEqual(takenPort, result);
        Assert.True(PortAllocator.IsPortAvailable(result));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter PortAllocatorTests`
Expected: FAIL (compile error — `PortAllocator` doesn't exist yet)

- [ ] **Step 3: Implement `PortAllocator`**

`src/OrganizaTodo.Desktop/Lifecycle/PortAllocator.cs`:

```csharp
using System.Net;
using System.Net.Sockets;

namespace OrganizaTodo.Desktop.Lifecycle;

public static class PortAllocator
{
    public static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public static int FindAvailablePort(int preferredPort) =>
        IsPortAvailable(preferredPort) ? preferredPort : FindAvailablePort();

    public static int FindAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter PortAllocatorTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add src/OrganizaTodo.Desktop/Lifecycle/PortAllocator.cs tests/OrganizaTodo.Desktop.Tests/PortAllocatorTests.cs
git commit -m "add PortAllocator for loopback port selection"
```

---

### Task 4: `PostgresConnectionStringBuilder`

**Files:**
- Create: `src/OrganizaTodo.Desktop/Lifecycle/PostgresConnectionStringBuilder.cs`
- Test: `tests/OrganizaTodo.Desktop.Tests/PostgresConnectionStringBuilderTests.cs`

**Interfaces:**
- Produces: `public static string PostgresConnectionStringBuilder.Build(int port, string database, string username, string password)`

- [ ] **Step 1: Write the failing test**

`tests/OrganizaTodo.Desktop.Tests/PostgresConnectionStringBuilderTests.cs`:

```csharp
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class PostgresConnectionStringBuilderTests
{
    [Fact]
    public void Build_ProducesLoopbackConnectionString()
    {
        var result = PostgresConnectionStringBuilder.Build(55432, "organizatodo", "app_user", "s3cret");

        Assert.Equal(
            "Host=127.0.0.1;Port=55432;Database=organizatodo;Username=app_user;Password=s3cret;Ssl Mode=Disable",
            result);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter PostgresConnectionStringBuilderTests`
Expected: FAIL (compile error)

- [ ] **Step 3: Implement it**

`src/OrganizaTodo.Desktop/Lifecycle/PostgresConnectionStringBuilder.cs`:

```csharp
namespace OrganizaTodo.Desktop.Lifecycle;

public static class PostgresConnectionStringBuilder
{
    public static string Build(int port, string database, string username, string password) =>
        $"Host=127.0.0.1;Port={port};Database={database};Username={username};Password={password};Ssl Mode=Disable";
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter PostgresConnectionStringBuilderTests`
Expected: PASS (1 test)

- [ ] **Step 5: Commit**

```bash
git add src/OrganizaTodo.Desktop/Lifecycle/PostgresConnectionStringBuilder.cs tests/OrganizaTodo.Desktop.Tests/PostgresConnectionStringBuilderTests.cs
git commit -m "add PostgresConnectionStringBuilder"
```

---

### Task 5: `DbCredentialStore` — generated local superuser credentials

**Files:**
- Create: `src/OrganizaTodo.Desktop/Lifecycle/DbCredentialStore.cs`
- Test: `tests/OrganizaTodo.Desktop.Tests/DbCredentialStoreTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record DbCredentials(string Username, string Password)`
  - `public sealed class DbCredentialStore { public DbCredentialStore(string configDir); public DbCredentials LoadOrCreate(); }`

- [ ] **Step 1: Write the failing test**

`tests/OrganizaTodo.Desktop.Tests/DbCredentialStoreTests.cs`:

```csharp
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class DbCredentialStoreTests
{
    [Fact]
    public void LoadOrCreate_FirstCall_PersistsCredentialsToDisk()
    {
        var configDir = Directory.CreateTempSubdirectory("organizatodo-creds-test-").FullName;
        try
        {
            var store = new DbCredentialStore(configDir);

            var credentials = store.LoadOrCreate();

            Assert.False(string.IsNullOrWhiteSpace(credentials.Username));
            Assert.False(string.IsNullOrWhiteSpace(credentials.Password));
            Assert.True(File.Exists(Path.Combine(configDir, "db-credentials.json")));
        }
        finally
        {
            Directory.Delete(configDir, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreate_SecondCall_ReturnsSameCredentials()
    {
        var configDir = Directory.CreateTempSubdirectory("organizatodo-creds-test-").FullName;
        try
        {
            var first = new DbCredentialStore(configDir).LoadOrCreate();
            var second = new DbCredentialStore(configDir).LoadOrCreate();

            Assert.Equal(first, second);
        }
        finally
        {
            Directory.Delete(configDir, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter DbCredentialStoreTests`
Expected: FAIL (compile error)

- [ ] **Step 3: Implement it**

`src/OrganizaTodo.Desktop/Lifecycle/DbCredentialStore.cs`:

```csharp
using System.Security.Cryptography;
using System.Text.Json;

namespace OrganizaTodo.Desktop.Lifecycle;

public sealed record DbCredentials(string Username, string Password);

public sealed class DbCredentialStore(string configDir)
{
    private string CredentialsPath => Path.Combine(configDir, "db-credentials.json");

    public DbCredentials LoadOrCreate()
    {
        if (File.Exists(CredentialsPath))
        {
            var json = File.ReadAllText(CredentialsPath);
            return JsonSerializer.Deserialize<DbCredentials>(json)
                   ?? throw new InvalidOperationException($"{CredentialsPath} is corrupt");
        }

        Directory.CreateDirectory(configDir);
        var credentials = new DbCredentials("organizatodo_app", GenerateRandomPassword());
        File.WriteAllText(CredentialsPath, JsonSerializer.Serialize(credentials));
        RestrictToCurrentUser(CredentialsPath);
        return credentials;
    }

    private static string GenerateRandomPassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

    private static void RestrictToCurrentUser(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter DbCredentialStoreTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add src/OrganizaTodo.Desktop/Lifecycle/DbCredentialStore.cs tests/OrganizaTodo.Desktop.Tests/DbCredentialStoreTests.cs
git commit -m "add DbCredentialStore for generated local superuser credentials"
```

---

### Task 6: Refactor `OrganizaTodo.Web` `Program.cs` to expose `BuildApp`

**Files:**
- Modify: `src/OrganizaTodo.Web/Program.cs`
- Modify: `tests/OrganizaTodo.Desktop.Tests/OrganizaTodo.Desktop.Tests.csproj` (add a reference to `OrganizaTodo.Web`)
- Test: `tests/OrganizaTodo.Desktop.Tests/WebProgramTests.cs`

**Interfaces:**
- Produces (namespace `OrganizaTodo.Web`):
  - `public static class Program { public static void Main(string[] args); public static WebApplication BuildApp(string[] args, Action<WebApplicationBuilder>? configure = null); }`
  - Configuration keys `BuildApp`'s `configure` delegate is expected to set: `"ConnectionStrings:DefaultConnection"`, `"ConnectionStrings:ShoppingConnection"`, `"Desktop:IsHosted"` (`"true"`/`"false"`), and `builder.WebHost.UseUrls(...)`.

- [ ] **Step 1: Add the Web project reference to the test project**

```bash
dotnet add tests/OrganizaTodo.Desktop.Tests/OrganizaTodo.Desktop.Tests.csproj reference src/OrganizaTodo.Web/OrganizaTodo.Web.csproj
```

- [ ] **Step 2: Write the failing test**

`tests/OrganizaTodo.Desktop.Tests/WebProgramTests.cs`:

```csharp
namespace OrganizaTodo.Desktop.Tests;

public class WebProgramTests
{
    [Fact]
    public void BuildApp_ConfigureDelegate_OverridesConnectionStringsAndDesktopFlag()
    {
        var app = OrganizaTodo.Web.Program.BuildApp(Array.Empty<string>(), builder =>
        {
            builder.Configuration["ConnectionStrings:DefaultConnection"] = "Host=test;Port=1;Database=d;Username=u;Password=p";
            builder.Configuration["ConnectionStrings:ShoppingConnection"] = "Host=test;Port=1;Database=s;Username=u;Password=p";
            builder.Configuration["Desktop:IsHosted"] = "true";
        });

        Assert.Equal(
            "Host=test;Port=1;Database=d;Username=u;Password=p",
            app.Configuration["ConnectionStrings:DefaultConnection"]);
        Assert.Equal("true", app.Configuration["Desktop:IsHosted"]);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter WebProgramTests`
Expected: FAIL (compile error — `Program.BuildApp` doesn't exist yet; top-level statements currently generate an internal, method-less `Program`)

- [ ] **Step 4: Refactor `Program.cs`**

Replace the full contents of `src/OrganizaTodo.Web/Program.cs` with:

```csharp
using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Repositories;
using OrganizaTodo.Web.Services;

namespace OrganizaTodo.Web;

public static class Program
{
    public static void Main(string[] args) => BuildApp(args).Run();

    public static WebApplication BuildApp(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        SqlMapper.AddTypeHandler(DateOnlyTypeHandler.Instance);

        LoadDotEnvIfPresent();
        ApplyPostgresEnvironmentDefaults();

        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);

        builder.Services.AddControllersWithViews();

        builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        builder.Services.AddSingleton<IShoppingDbConnectionFactory, ShoppingDbConnectionFactory>();

        builder.Services.AddScoped<IUserRepository,            UserRepository>();
        builder.Services.AddScoped<IHousingServiceRepository,  HousingServiceRepository>();
        builder.Services.AddScoped<ICreditCardRepository,      CreditCardRepository>();
        builder.Services.AddScoped<IFixedLiabilityRepository,  FixedLiabilityRepository>();
        builder.Services.AddScoped<IIncomeRepository,          IncomeRepository>();
        builder.Services.AddScoped<IOtherExpenseRepository,    OtherExpenseRepository>();
        builder.Services.AddScoped<IShoppingRepository,        ShoppingRepository>();
        builder.Services.AddScoped<IMockProductRepository,     MockProductRepository>();
        builder.Services.AddScoped<IProductCatalogRepository,  ProductCatalogRepository>();
        builder.Services.AddScoped<ISavingRepository,          SavingRepository>();
        builder.Services.AddScoped<IAssetPurchaseRepository,   AssetPurchaseRepository>();
        builder.Services.AddScoped<IVehicleRepository,          VehicleRepository>();
        builder.Services.AddScoped<IRecurringIncomeRepository,  RecurringIncomeRepository>();
        builder.Services.AddScoped<ILedgerRepository,           LedgerRepository>();

        builder.Services.AddScoped<IAuthService,    AuthService>();
        builder.Services.AddScoped<IBalanceService, BalanceService>();
        builder.Services.AddScoped<IAdminService,   AdminService>();
        builder.Services.AddScoped<ILedgerInterestService, LedgerInterestService>();
        builder.Services.AddHostedService<LedgerInterestBackgroundService>();

        builder.Services.AddHttpClient<IDolarService,    DolarService>();
        builder.Services.AddHttpClient<IFuelPriceService, FuelPriceService>();

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

        var isDesktopHost = app.Configuration.GetValue<bool>("Desktop:IsHosted");

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            if (!isDesktopHost)
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
        }

        if (!isDesktopHost)
        {
            app.UseHttpsRedirection();
        }

        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        return app;
    }

    // Load .env into environment variables (local dev; Docker sets them directly)
    // Walk up from CWD to find .env — works regardless of where dotnet run is invoked from
    private static string? FindEnvFile()
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

    private static void LoadDotEnvIfPresent()
    {
        var envFile = FindEnvFile();
        if (envFile is null) return;

        foreach (var line in File.ReadAllLines(envFile))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var idx = line.IndexOf('=');
            if (idx > 0)
                Environment.SetEnvironmentVariable(line[..idx].Trim(), line[(idx + 1)..].Trim());
        }
    }

    private static void ApplyPostgresEnvironmentDefaults()
    {
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

        // Build ShoppingConnection from POSTGRES_DB_Shopping (same host/user/pass, different DB)
        if (Environment.GetEnvironmentVariable("ConnectionStrings__ShoppingConnection") is null)
        {
            var host = Environment.GetEnvironmentVariable("POSTGRES_HOST");
            var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
            var db   = Environment.GetEnvironmentVariable("POSTGRES_DB_Shopping");
            var user = Environment.GetEnvironmentVariable("POSTGRES_USER");
            var pass = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
            if (host is not null && db is not null)
                Environment.SetEnvironmentVariable(
                    "ConnectionStrings__ShoppingConnection",
                    $"Host={host};Port={port};Database={db};Username={user};Password={pass};Ssl Mode=Disable");
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter WebProgramTests`
Expected: PASS (1 test)

- [ ] **Step 6: Confirm the existing Docker/server path still builds and runs unchanged**

Run: `dotnet build OrganizaTodo.sln`
Expected: Build succeeds for all projects. (Full Docker verification happens manually before this plan is considered done — see the final task.)

- [ ] **Step 7: Commit**

```bash
git add src/OrganizaTodo.Web/Program.cs tests/OrganizaTodo.Desktop.Tests
git commit -m "refactor OrganizaTodo.Web Program.cs to expose BuildApp for in-process hosting"
```

---

### Task 7: `PostgresManager` — bundled Postgres process lifecycle

**Files:**
- Create: `src/OrganizaTodo.Desktop/Lifecycle/PostgresManager.cs`
- Test: `tests/OrganizaTodo.Desktop.Tests/PostgresManagerIntegrationTests.cs`

**Interfaces:**
- Consumes: `DbCredentials` (Task 5), `PostgresConnectionStringBuilder.Build` (Task 4), `PortAllocator.FindAvailablePort()` (Task 3)
- Produces:
  ```csharp
  public interface IPostgresManager
  {
      Task InitializeIfNeededAsync(CancellationToken ct = default);
      Task StartAsync(CancellationToken ct = default);
      Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken ct = default);
      Task StopAsync(CancellationToken ct = default);
  }

  public sealed class PostgresManager : IPostgresManager
  {
      public PostgresManager(string pgBinDirectory, string pgDataDir, int port, DbCredentials credentials, string logsDir);
  }
  ```

This task requires PostgreSQL client tools (`initdb`, `postgres`, `pg_ctl`, `pg_config`) discoverable on the dev/CI machine — already a prerequisite for running this project per the existing README.

- [ ] **Step 1: Add the Npgsql package reference to the test project**

```bash
dotnet add tests/OrganizaTodo.Desktop.Tests/OrganizaTodo.Desktop.Tests.csproj package Npgsql
```

- [ ] **Step 2: Write the failing integration test**

`tests/OrganizaTodo.Desktop.Tests/PostgresManagerIntegrationTests.cs`:

```csharp
using System.Diagnostics;
using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class PostgresManagerIntegrationTests : IAsyncLifetime
{
    private string _pgBinDirectory = "";
    private string _tempDir = "";

    public Task InitializeAsync()
    {
        _pgBinDirectory = ResolvePgBinDirectory();
        _tempDir = Directory.CreateTempSubdirectory("organizatodo-pgmanager-test-").FullName;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Directory.Delete(_tempDir, recursive: true);
        return Task.CompletedTask;
    }

    private static string ResolvePgBinDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable("ORGANIZATODO_PG_BIN_DIR");
        if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath;

        using var process = Process.Start(new ProcessStartInfo("pg_config", "--bindir")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false
        })!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException(
                "pg_config not found on PATH and ORGANIZATODO_PG_BIN_DIR not set — install PostgreSQL client tools to run this test.");

        return output;
    }

    [Fact]
    public async Task InitializeStartWaitStop_FullLifecycle_Succeeds()
    {
        var pgDataDir = Path.Combine(_tempDir, "pgdata");
        var logsDir = Path.Combine(_tempDir, "logs");
        Directory.CreateDirectory(logsDir);
        var port = PortAllocator.FindAvailablePort();
        var credentials = new DbCredentials("organizatodo_app", "test-password-123");

        var manager = new PostgresManager(_pgBinDirectory, pgDataDir, port, credentials, logsDir);

        await manager.InitializeIfNeededAsync();
        await manager.StartAsync();
        await manager.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));

        await using var conn = new NpgsqlConnection(
            PostgresConnectionStringBuilder.Build(port, "postgres", credentials.Username, credentials.Password));
        await conn.OpenAsync();
        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
        await conn.CloseAsync();

        await manager.StopAsync();
        Assert.True(PortAllocator.IsPortAvailable(port));
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter PostgresManagerIntegrationTests`
Expected: FAIL (compile error — `PostgresManager` doesn't exist yet)

- [ ] **Step 4: Implement `PostgresManager`**

`src/OrganizaTodo.Desktop/Lifecycle/PostgresManager.cs`:

```csharp
using System.Diagnostics;
using Npgsql;

namespace OrganizaTodo.Desktop.Lifecycle;

public interface IPostgresManager
{
    Task InitializeIfNeededAsync(CancellationToken ct = default);
    Task StartAsync(CancellationToken ct = default);
    Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}

public sealed class PostgresManager(
    string pgBinDirectory,
    string pgDataDir,
    int port,
    DbCredentials credentials,
    string logsDir) : IPostgresManager
{
    private Process? _process;

    public Task InitializeIfNeededAsync(CancellationToken ct = default)
    {
        if (File.Exists(Path.Combine(pgDataDir, "PG_VERSION")))
            return Task.CompletedTask;

        Directory.CreateDirectory(pgDataDir);

        var pwFile = Path.GetTempFileName();
        File.WriteAllText(pwFile, credentials.Password);
        try
        {
            RunToCompletion(
                Path.Combine(pgBinDirectory, "initdb"),
                $"-D \"{pgDataDir}\" -U {credentials.Username} --pwfile=\"{pwFile}\" --auth=scram-sha-256");
        }
        finally
        {
            File.Delete(pwFile);
        }

        File.AppendAllText(
            Path.Combine(pgDataDir, "postgresql.conf"),
            $"\nlisten_addresses = '127.0.0.1'\nport = {port}\n");

        return Task.CompletedTask;
    }

    private const long MaxLogFileSizeBytes = 10 * 1024 * 1024;

    public Task StartAsync(CancellationToken ct = default)
    {
        _process = new Process
        {
            StartInfo = new ProcessStartInfo(Path.Combine(pgBinDirectory, "postgres"), $"-D \"{pgDataDir}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        _process.Start();

        var logPath = Path.Combine(logsDir, "postgres.log");
        TruncateLogIfOversized(logPath);
        _ = PipeStreamToFileAsync(_process.StandardOutput, logPath, ct);
        _ = PipeStreamToFileAsync(_process.StandardError, logPath, ct);

        return Task.CompletedTask;
    }

    private static void TruncateLogIfOversized(string logPath)
    {
        var info = new FileInfo(logPath);
        if (info.Exists && info.Length > MaxLogFileSizeBytes)
        {
            File.Delete(logPath);
        }
    }

    public async Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_process is { HasExited: true })
                throw new InvalidOperationException($"postgres exited early with code {_process.ExitCode}");

            try
            {
                await using var conn = new NpgsqlConnection(
                    PostgresConnectionStringBuilder.Build(port, "postgres", credentials.Username, credentials.Password));
                await conn.OpenAsync(ct);
                return;
            }
            catch
            {
                await Task.Delay(200, ct);
            }
        }

        throw new TimeoutException($"postgres did not become ready within {timeout}");
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_process is null || _process.HasExited) return;

        RunToCompletion(Path.Combine(pgBinDirectory, "pg_ctl"), $"stop -D \"{pgDataDir}\" -m fast -w");
        await _process.WaitForExitAsync(ct);
    }

    private static void RunToCompletion(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        })!;
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var stderr = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"{fileName} exited with {process.ExitCode}: {stderr}");
        }
    }

    private static async Task PipeStreamToFileAsync(StreamReader reader, string path, CancellationToken ct)
    {
        await using var writer = new StreamWriter(path, append: true) { AutoFlush = true };
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            await writer.WriteLineAsync(line);
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter PostgresManagerIntegrationTests`
Expected: PASS (1 test). Note: this test takes several seconds (real `initdb` + real Postgres startup).

- [ ] **Step 6: Commit**

```bash
git add src/OrganizaTodo.Desktop/Lifecycle/PostgresManager.cs tests/OrganizaTodo.Desktop.Tests/PostgresManagerIntegrationTests.cs tests/OrganizaTodo.Desktop.Tests/OrganizaTodo.Desktop.Tests.csproj
git commit -m "add PostgresManager for bundled Postgres process lifecycle"
```

---

### Task 8: `MigrationPlanner` and `SchemaBootstrapper`

**Files:**
- Create: `src/OrganizaTodo.Desktop/Lifecycle/MigrationPlanner.cs`
- Create: `src/OrganizaTodo.Desktop/Lifecycle/SchemaBootstrapper.cs`
- Test: `tests/OrganizaTodo.Desktop.Tests/MigrationPlannerTests.cs`
- Test: `tests/OrganizaTodo.Desktop.Tests/SchemaBootstrapperIntegrationTests.cs`

**Interfaces:**
- Consumes: `IPostgresManager`, `PostgresManager`, `PostgresConnectionStringBuilder.Build`, `DbCredentials`, `PortAllocator` (Tasks 3–5, 7)
- Produces:
  ```csharp
  public static class MigrationPlanner
  {
      public static IReadOnlyList<string> PendingMainMigrations(string databaseDir, IReadOnlySet<string> alreadyApplied);
      public static IReadOnlyList<string> PendingShoppingMigrations(string databaseDir, IReadOnlySet<string> alreadyApplied);
  }

  public interface ISchemaBootstrapper
  {
      Task BootstrapAsync(NpgsqlConnection adminConnection, string targetDatabase, bool isShoppingDatabase, CancellationToken ct = default);
  }

  public sealed class SchemaBootstrapper : ISchemaBootstrapper
  {
      public SchemaBootstrapper(string databaseSqlDir);
  }
  ```
- Known limitation carried into this task (per spec/design discussion): `migrate_shopping_*.sql` creates `LANGUAGE sql` functions referencing `carrefour_products`/`coto_products`, tables that no SQL file in this repo creates. `SchemaBootstrapper` applies the shopping migrations as-is (function creation succeeds — Postgres doesn't validate `LANGUAGE sql` function bodies against existing objects at creation time); calling those functions on an empty offline install will error until the tables are populated by an external process. This is accepted, not fixed, by this plan.

- [ ] **Step 1: Write the failing `MigrationPlanner` test**

`tests/OrganizaTodo.Desktop.Tests/MigrationPlannerTests.cs`:

```csharp
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class MigrationPlannerTests
{
    [Fact]
    public void PendingMainMigrations_ReturnsUnappliedFilesInNumericOrder()
    {
        var dir = Directory.CreateTempSubdirectory("organizatodo-planner-test-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "migrate_002_savings.sql"), "-- noop");
            File.WriteAllText(Path.Combine(dir, "migrate_001_fixed_liabilities_paid.sql"), "-- noop");
            File.WriteAllText(Path.Combine(dir, "migrate_010_recurring_income.sql"), "-- noop");
            File.WriteAllText(Path.Combine(dir, "migrate_shopping_001_product_functions.sql"), "-- noop");

            var pending = MigrationPlanner.PendingMainMigrations(dir, new HashSet<string> { "migrate_001_fixed_liabilities_paid.sql" });

            Assert.Equal(
                new[] { "migrate_002_savings.sql", "migrate_010_recurring_income.sql" },
                pending);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PendingShoppingMigrations_OnlyMatchesShoppingPrefixedFiles()
    {
        var dir = Directory.CreateTempSubdirectory("organizatodo-planner-test-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "migrate_001_fixed_liabilities_paid.sql"), "-- noop");
            File.WriteAllText(Path.Combine(dir, "migrate_shopping_002_fix_function_types.sql"), "-- noop");
            File.WriteAllText(Path.Combine(dir, "migrate_shopping_001_product_functions.sql"), "-- noop");

            var pending = MigrationPlanner.PendingShoppingMigrations(dir, new HashSet<string>());

            Assert.Equal(
                new[] { "migrate_shopping_001_product_functions.sql", "migrate_shopping_002_fix_function_types.sql" },
                pending);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter MigrationPlannerTests`
Expected: FAIL (compile error)

- [ ] **Step 3: Implement `MigrationPlanner`**

`src/OrganizaTodo.Desktop/Lifecycle/MigrationPlanner.cs`:

```csharp
using System.Text.RegularExpressions;

namespace OrganizaTodo.Desktop.Lifecycle;

public static partial class MigrationPlanner
{
    [GeneratedRegex(@"^migrate_(\d+)_.*\.sql$")]
    private static partial Regex MainMigrationPattern();

    [GeneratedRegex(@"^migrate_shopping_(\d+)_.*\.sql$")]
    private static partial Regex ShoppingMigrationPattern();

    public static IReadOnlyList<string> PendingMainMigrations(string databaseDir, IReadOnlySet<string> alreadyApplied) =>
        PendingMatching(databaseDir, MainMigrationPattern(), alreadyApplied);

    public static IReadOnlyList<string> PendingShoppingMigrations(string databaseDir, IReadOnlySet<string> alreadyApplied) =>
        PendingMatching(databaseDir, ShoppingMigrationPattern(), alreadyApplied);

    private static IReadOnlyList<string> PendingMatching(string dir, Regex pattern, IReadOnlySet<string> alreadyApplied)
    {
        return Directory.GetFiles(dir)
            .Select(Path.GetFileName)
            .Where(name => name is not null && pattern.IsMatch(name) && !alreadyApplied.Contains(name))
            .Select(name => name!)
            .OrderBy(name => int.Parse(pattern.Match(name).Groups[1].Value))
            .ToList();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter MigrationPlannerTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Write the failing `SchemaBootstrapper` integration test**

`tests/OrganizaTodo.Desktop.Tests/SchemaBootstrapperIntegrationTests.cs`:

```csharp
using Dapper;
using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class SchemaBootstrapperIntegrationTests : IAsyncLifetime
{
    private string _pgBinDirectory = "";
    private string _tempDir = "";
    private PostgresManager _manager = null!;
    private int _port;
    private DbCredentials _credentials = null!;

    public async Task InitializeAsync()
    {
        _pgBinDirectory = PostgresTestEnvironment.ResolvePgBinDirectory();
        _tempDir = Directory.CreateTempSubdirectory("organizatodo-bootstrap-test-").FullName;
        var logsDir = Path.Combine(_tempDir, "logs");
        Directory.CreateDirectory(logsDir);
        _port = PortAllocator.FindAvailablePort();
        _credentials = new DbCredentials("organizatodo_app", "test-password-123");

        _manager = new PostgresManager(_pgBinDirectory, Path.Combine(_tempDir, "pgdata"), _port, _credentials, logsDir);
        await _manager.InitializeIfNeededAsync();
        await _manager.StartAsync();
        await _manager.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));
    }

    public async Task DisposeAsync()
    {
        await _manager.StopAsync();
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task BootstrapAsync_FreshDatabase_CreatesSchemaAndIsIdempotent()
    {
        var sqlDir = Path.Combine(AppContext.BaseDirectory, "Database");
        var bootstrapper = new SchemaBootstrapper(sqlDir);

        await using var adminConnection = new NpgsqlConnection(
            PostgresConnectionStringBuilder.Build(_port, "postgres", _credentials.Username, _credentials.Password));
        await adminConnection.OpenAsync();

        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo", isShoppingDatabase: false);
        // Re-running must not fail or re-apply already-applied files.
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo", isShoppingDatabase: false);

        await using var appConnection = new NpgsqlConnection(
            PostgresConnectionStringBuilder.Build(_port, "organizatodo", _credentials.Username, _credentials.Password));
        await appConnection.OpenAsync();

        var usersTableExists = await appConnection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users')");
        Assert.True(usersTableExists);
    }
}
```

- [ ] **Step 6: Add the shared Postgres test-environment helper**

`tests/OrganizaTodo.Desktop.Tests/PostgresTestEnvironment.cs` (extracted so both integration test classes share one resolver instead of duplicating it):

```csharp
using System.Diagnostics;

namespace OrganizaTodo.Desktop.Tests;

internal static class PostgresTestEnvironment
{
    public static string ResolvePgBinDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable("ORGANIZATODO_PG_BIN_DIR");
        if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath;

        using var process = Process.Start(new ProcessStartInfo("pg_config", "--bindir")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false
        })!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException(
                "pg_config not found on PATH and ORGANIZATODO_PG_BIN_DIR not set — install PostgreSQL client tools to run this test.");

        return output;
    }
}
```

Update `PostgresManagerIntegrationTests.ResolvePgBinDirectory` (Task 7) to call `PostgresTestEnvironment.ResolvePgBinDirectory()` and delete its private copy, so there's one implementation.

- [ ] **Step 7: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SchemaBootstrapperIntegrationTests`
Expected: FAIL (compile error — `SchemaBootstrapper` doesn't exist yet)

- [ ] **Step 8: Implement `SchemaBootstrapper`**

`src/OrganizaTodo.Desktop/Lifecycle/SchemaBootstrapper.cs`:

```csharp
using Dapper;
using Npgsql;

namespace OrganizaTodo.Desktop.Lifecycle;

public interface ISchemaBootstrapper
{
    Task BootstrapAsync(NpgsqlConnection adminConnection, string targetDatabase, bool isShoppingDatabase, CancellationToken ct = default);
}

public sealed class SchemaBootstrapper(string databaseSqlDir) : ISchemaBootstrapper
{
    public async Task BootstrapAsync(NpgsqlConnection adminConnection, string targetDatabase, bool isShoppingDatabase, CancellationToken ct = default)
    {
        await EnsureDatabaseExistsAsync(adminConnection, targetDatabase);

        var targetConnectionString = new NpgsqlConnectionStringBuilder(adminConnection.ConnectionString)
        {
            Database = targetDatabase
        }.ConnectionString;

        await using var dbConnection = new NpgsqlConnection(targetConnectionString);
        await dbConnection.OpenAsync(ct);

        await EnsureMigrationsTableAsync(dbConnection);
        var applied = await LoadAppliedMigrationsAsync(dbConnection);

        if (!applied.Contains("init.sql"))
        {
            await ApplyFileAsync(dbConnection, Path.Combine(databaseSqlDir, "init.sql"));
            await RecordAppliedAsync(dbConnection, "init.sql");
        }

        var pending = isShoppingDatabase
            ? MigrationPlanner.PendingShoppingMigrations(databaseSqlDir, applied)
            : MigrationPlanner.PendingMainMigrations(databaseSqlDir, applied);

        foreach (var file in pending)
        {
            await ApplyFileAsync(dbConnection, Path.Combine(databaseSqlDir, file));
            await RecordAppliedAsync(dbConnection, file);
        }
    }

    private static async Task EnsureDatabaseExistsAsync(NpgsqlConnection adminConnection, string database)
    {
        var exists = await adminConnection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @database)",
            new { database });
        if (!exists)
        {
            await adminConnection.ExecuteAsync($"CREATE DATABASE \"{database}\"");
        }
    }

    private static Task EnsureMigrationsTableAsync(NpgsqlConnection connection) =>
        connection.ExecuteAsync(
            "CREATE TABLE IF NOT EXISTS schema_migrations (filename TEXT PRIMARY KEY, applied_at TIMESTAMPTZ NOT NULL DEFAULT now())");

    private static async Task<HashSet<string>> LoadAppliedMigrationsAsync(NpgsqlConnection connection)
    {
        var rows = await connection.QueryAsync<string>("SELECT filename FROM schema_migrations");
        return rows.ToHashSet();
    }

    private static async Task ApplyFileAsync(NpgsqlConnection connection, string path)
    {
        var sql = await File.ReadAllTextAsync(path);
        await connection.ExecuteAsync(sql);
    }

    private static Task RecordAppliedAsync(NpgsqlConnection connection, string filename) =>
        connection.ExecuteAsync("INSERT INTO schema_migrations (filename) VALUES (@filename)", new { filename });
}
```

- [ ] **Step 9: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter "SchemaBootstrapperIntegrationTests|MigrationPlannerTests|PostgresManagerIntegrationTests"`
Expected: PASS (5 tests total)

- [ ] **Step 10: Commit**

```bash
git add src/OrganizaTodo.Desktop/Lifecycle/MigrationPlanner.cs src/OrganizaTodo.Desktop/Lifecycle/SchemaBootstrapper.cs tests/OrganizaTodo.Desktop.Tests
git commit -m "add MigrationPlanner and SchemaBootstrapper for first-run schema setup"
```

---

### Task 9: `SingleInstanceGuard`

**Files:**
- Create: `src/OrganizaTodo.Desktop/Lifecycle/SingleInstanceGuard.cs`
- Test: `tests/OrganizaTodo.Desktop.Tests/SingleInstanceGuardTests.cs`

**Interfaces:**
- Produces: `public sealed class SingleInstanceGuard : IDisposable { public SingleInstanceGuard(string name); public bool TryAcquire(); }`

- [ ] **Step 1: Write the failing test**

`tests/OrganizaTodo.Desktop.Tests/SingleInstanceGuardTests.cs`:

```csharp
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class SingleInstanceGuardTests
{
    [Fact]
    public void SecondGuardWithSameName_FailsToAcquireWhileFirstHoldsIt()
    {
        var name = $"organizatodo-test-{Guid.NewGuid():N}";
        using var first = new SingleInstanceGuard(name);
        Assert.True(first.TryAcquire());

        using var second = new SingleInstanceGuard(name);
        Assert.False(second.TryAcquire());
    }

    [Fact]
    public void GuardWithDifferentNames_BothAcquireIndependently()
    {
        using var first = new SingleInstanceGuard($"organizatodo-test-{Guid.NewGuid():N}");
        using var second = new SingleInstanceGuard($"organizatodo-test-{Guid.NewGuid():N}");

        Assert.True(first.TryAcquire());
        Assert.True(second.TryAcquire());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SingleInstanceGuardTests`
Expected: FAIL (compile error)

- [ ] **Step 3: Implement `SingleInstanceGuard`**

`src/OrganizaTodo.Desktop/Lifecycle/SingleInstanceGuard.cs`:

```csharp
namespace OrganizaTodo.Desktop.Lifecycle;

public sealed class SingleInstanceGuard(string name) : IDisposable
{
    private Mutex? _mutex;

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        return createdNew;
    }

    public void Dispose() => _mutex?.Dispose();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter SingleInstanceGuardTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add src/OrganizaTodo.Desktop/Lifecycle/SingleInstanceGuard.cs tests/OrganizaTodo.Desktop.Tests/SingleInstanceGuardTests.cs
git commit -m "add SingleInstanceGuard to prevent a second concurrent launch"
```

---

### Task 10: `DesktopHost` orchestrator with ordered startup/shutdown and failure cleanup

**Files:**
- Create: `src/OrganizaTodo.Desktop/Lifecycle/IWebAppRunner.cs`
- Create: `src/OrganizaTodo.Desktop/Lifecycle/DesktopHost.cs`
- Test: `tests/OrganizaTodo.Desktop.Tests/DesktopHostTests.cs`

**Interfaces:**
- Consumes: `IPostgresManager` (Task 7), `ISchemaBootstrapper` (Task 8), `DbCredentials` (Task 5), `PostgresConnectionStringBuilder.Build` (Task 4)
- Produces:
  ```csharp
  public interface IWebAppRunner
  {
      Task<string> StartAsync(string defaultConnectionString, string shoppingConnectionString, CancellationToken ct = default);
      Task StopAsync(CancellationToken ct = default);
  }

  public sealed class DesktopHost
  {
      public DesktopHost(
          IPostgresManager postgresManager,
          ISchemaBootstrapper schemaBootstrapper,
          IWebAppRunner webAppRunner,
          int postgresPort,
          DbCredentials credentials,
          string defaultDatabase,
          string shoppingDatabase);

      public Task<string> StartAsync(CancellationToken ct = default); // returns the URL to open in the window
      public Task ShutdownAsync(CancellationToken ct = default);
  }
  ```

- [ ] **Step 1: Write the failing test with fakes for the three collaborators**

`tests/OrganizaTodo.Desktop.Tests/DesktopHostTests.cs`:

```csharp
using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class DesktopHostTests
{
    private sealed class FakePostgresManager : IPostgresManager
    {
        public List<string> Calls { get; } = [];
        public Task InitializeIfNeededAsync(CancellationToken ct = default) { Calls.Add("Initialize"); return Task.CompletedTask; }
        public Task StartAsync(CancellationToken ct = default) { Calls.Add("Start"); return Task.CompletedTask; }
        public Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken ct = default) { Calls.Add("WaitUntilReady"); return Task.CompletedTask; }
        public Task StopAsync(CancellationToken ct = default) { Calls.Add("Stop"); return Task.CompletedTask; }
    }

    private sealed class FakeSchemaBootstrapper : ISchemaBootstrapper
    {
        public List<string> BootstrappedDatabases { get; } = [];
        public Task BootstrapAsync(NpgsqlConnection adminConnection, string targetDatabase, bool isShoppingDatabase, CancellationToken ct = default)
        {
            BootstrappedDatabases.Add(targetDatabase);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWebAppRunner(bool throwOnStart = false) : IWebAppRunner
    {
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }

        public Task<string> StartAsync(string defaultConnectionString, string shoppingConnectionString, CancellationToken ct = default)
        {
            if (throwOnStart) throw new InvalidOperationException("simulated Kestrel bind failure");
            Started = true;
            return Task.FromResult("http://127.0.0.1:5157");
        }

        public Task StopAsync(CancellationToken ct = default) { Stopped = true; return Task.CompletedTask; }
    }

    private static DbCredentials TestCredentials => new("organizatodo_app", "test-password");

    [Fact]
    public async Task StartAsync_HappyPath_RunsStepsInOrderAndReturnsUrl()
    {
        var postgres = new FakePostgresManager();
        var schema = new FakeSchemaBootstrapper();
        var webApp = new FakeWebAppRunner();
        var host = new DesktopHost(postgres, schema, webApp, 55432, TestCredentials, "organizatodo", "organizatodo_shopping");

        var url = await host.StartAsync();

        Assert.Equal("http://127.0.0.1:5157", url);
        Assert.Equal(["Initialize", "Start", "WaitUntilReady"], postgres.Calls);
        Assert.Equal(["organizatodo", "organizatodo_shopping"], schema.BootstrappedDatabases);
        Assert.True(webApp.Started);
    }

    [Fact]
    public async Task StartAsync_WebAppFailsToStart_StopsPostgresBeforeRethrowing()
    {
        var postgres = new FakePostgresManager();
        var schema = new FakeSchemaBootstrapper();
        var webApp = new FakeWebAppRunner(throwOnStart: true);
        var host = new DesktopHost(postgres, schema, webApp, 55432, TestCredentials, "organizatodo", "organizatodo_shopping");

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.Contains("Stop", postgres.Calls);
    }

    [Fact]
    public async Task ShutdownAsync_StopsWebAppThenPostgres()
    {
        var postgres = new FakePostgresManager();
        var schema = new FakeSchemaBootstrapper();
        var webApp = new FakeWebAppRunner();
        var host = new DesktopHost(postgres, schema, webApp, 55432, TestCredentials, "organizatodo", "organizatodo_shopping");
        await host.StartAsync();
        postgres.Calls.Clear();

        await host.ShutdownAsync();

        Assert.True(webApp.Stopped);
        Assert.Equal(["Stop"], postgres.Calls);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter DesktopHostTests`
Expected: FAIL (compile error — `DesktopHost`/`IWebAppRunner` don't exist yet)

- [ ] **Step 3: Implement `IWebAppRunner` and `DesktopHost`**

`src/OrganizaTodo.Desktop/Lifecycle/IWebAppRunner.cs`:

```csharp
namespace OrganizaTodo.Desktop.Lifecycle;

public interface IWebAppRunner
{
    Task<string> StartAsync(string defaultConnectionString, string shoppingConnectionString, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
```

`src/OrganizaTodo.Desktop/Lifecycle/DesktopHost.cs`:

```csharp
using Npgsql;

namespace OrganizaTodo.Desktop.Lifecycle;

public sealed class DesktopHost(
    IPostgresManager postgresManager,
    ISchemaBootstrapper schemaBootstrapper,
    IWebAppRunner webAppRunner,
    int postgresPort,
    DbCredentials credentials,
    string defaultDatabase,
    string shoppingDatabase)
{
    public async Task<string> StartAsync(CancellationToken ct = default)
    {
        await postgresManager.InitializeIfNeededAsync(ct);

        try
        {
            await postgresManager.StartAsync(ct);
            await postgresManager.WaitUntilReadyAsync(TimeSpan.FromSeconds(30), ct);

            var adminConnectionString = PostgresConnectionStringBuilder.Build(postgresPort, "postgres", credentials.Username, credentials.Password);
            await using (var adminConnection = new NpgsqlConnection(adminConnectionString))
            {
                await adminConnection.OpenAsync(ct);
                await schemaBootstrapper.BootstrapAsync(adminConnection, defaultDatabase, isShoppingDatabase: false, ct);
                await schemaBootstrapper.BootstrapAsync(adminConnection, shoppingDatabase, isShoppingDatabase: true, ct);
            }

            var defaultConnectionString = PostgresConnectionStringBuilder.Build(postgresPort, defaultDatabase, credentials.Username, credentials.Password);
            var shoppingConnectionString = PostgresConnectionStringBuilder.Build(postgresPort, shoppingDatabase, credentials.Username, credentials.Password);

            return await webAppRunner.StartAsync(defaultConnectionString, shoppingConnectionString, ct);
        }
        catch
        {
            await postgresManager.StopAsync(ct);
            throw;
        }
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        await webAppRunner.StopAsync(ct);
        await postgresManager.StopAsync(ct);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter DesktopHostTests`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add src/OrganizaTodo.Desktop/Lifecycle/IWebAppRunner.cs src/OrganizaTodo.Desktop/Lifecycle/DesktopHost.cs tests/OrganizaTodo.Desktop.Tests/DesktopHostTests.cs
git commit -m "add DesktopHost orchestrator with ordered startup and failure cleanup"
```

---

### Task 11: `KestrelWebAppRunner`, Photino window wiring, and end-to-end verification

**Files:**
- Create: `src/OrganizaTodo.Desktop/Lifecycle/KestrelWebAppRunner.cs`
- Modify: `src/OrganizaTodo.Desktop/Program.cs`
- Test: `tests/OrganizaTodo.Desktop.Tests/KestrelWebAppRunnerIntegrationTests.cs`

**Interfaces:**
- Consumes: `IWebAppRunner` (Task 10), `OrganizaTodo.Web.Program.BuildApp` (Task 6), `PortAllocator` (Task 3), `DesktopHost`, `AppPaths`/`AppDataRoot` (Task 2), `DbCredentialStore` (Task 5), `SingleInstanceGuard` (Task 9), `PostgresManager`/`SchemaBootstrapper` (Tasks 7–8)
- Produces: `public sealed class KestrelWebAppRunner : IWebAppRunner` (constructor takes no arguments — it self-allocates a Kestrel port via `PortAllocator`)

- [ ] **Step 1: Write the failing end-to-end integration test**

`tests/OrganizaTodo.Desktop.Tests/KestrelWebAppRunnerIntegrationTests.cs`:

```csharp
using Npgsql;
using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class KestrelWebAppRunnerIntegrationTests : IAsyncLifetime
{
    private string _tempDir = "";
    private PostgresManager _postgres = null!;
    private int _pgPort;
    private DbCredentials _credentials = null!;

    public async Task InitializeAsync()
    {
        var pgBinDirectory = PostgresTestEnvironment.ResolvePgBinDirectory();
        _tempDir = Directory.CreateTempSubdirectory("organizatodo-e2e-test-").FullName;
        var logsDir = Path.Combine(_tempDir, "logs");
        Directory.CreateDirectory(logsDir);
        _pgPort = PortAllocator.FindAvailablePort();
        _credentials = new DbCredentials("organizatodo_app", "test-password-123");

        _postgres = new PostgresManager(pgBinDirectory, Path.Combine(_tempDir, "pgdata"), _pgPort, _credentials, logsDir);
        await _postgres.InitializeIfNeededAsync();
        await _postgres.StartAsync();
        await _postgres.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));

        var sqlDir = Path.Combine(AppContext.BaseDirectory, "Database");
        var bootstrapper = new SchemaBootstrapper(sqlDir);
        await using var adminConnection = new NpgsqlConnection(
            PostgresConnectionStringBuilder.Build(_pgPort, "postgres", _credentials.Username, _credentials.Password));
        await adminConnection.OpenAsync();
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo", isShoppingDatabase: false);
        await bootstrapper.BootstrapAsync(adminConnection, "organizatodo_shopping", isShoppingDatabase: true);
    }

    public async Task DisposeAsync()
    {
        await _postgres.StopAsync();
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task StartAsync_ServesLoginPageOverHttp_ThenStopsCleanly()
    {
        var defaultConnectionString = PostgresConnectionStringBuilder.Build(_pgPort, "organizatodo", _credentials.Username, _credentials.Password);
        var shoppingConnectionString = PostgresConnectionStringBuilder.Build(_pgPort, "organizatodo_shopping", _credentials.Username, _credentials.Password);

        var runner = new KestrelWebAppRunner();
        var url = await runner.StartAsync(defaultConnectionString, shoppingConnectionString);

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync($"{url}/Account/Login");
        Assert.True(response.IsSuccessStatusCode);

        await runner.StopAsync();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter KestrelWebAppRunnerIntegrationTests`
Expected: FAIL (compile error — `KestrelWebAppRunner` doesn't exist yet)

- [ ] **Step 3: Implement `KestrelWebAppRunner`**

`src/OrganizaTodo.Desktop/Lifecycle/KestrelWebAppRunner.cs`:

```csharp
namespace OrganizaTodo.Desktop.Lifecycle;

public sealed class KestrelWebAppRunner : IWebAppRunner
{
    private WebApplication? _app;
    private Task? _runTask;

    public async Task<string> StartAsync(string defaultConnectionString, string shoppingConnectionString, CancellationToken ct = default)
    {
        var port = PortAllocator.FindAvailablePort(preferredPort: 5157);

        _app = OrganizaTodo.Web.Program.BuildApp(Array.Empty<string>(), builder =>
        {
            builder.Configuration["ConnectionStrings:DefaultConnection"] = defaultConnectionString;
            builder.Configuration["ConnectionStrings:ShoppingConnection"] = shoppingConnectionString;
            builder.Configuration["Desktop:IsHosted"] = "true";
            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        });

        var started = new TaskCompletionSource();
        _app.Lifetime.ApplicationStarted.Register(() => started.TrySetResult());

        _runTask = _app.RunAsync(ct);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

        return $"http://127.0.0.1:{port}";
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_app is null) return;
        await _app.StopAsync(ct);
        if (_runTask is not null) await _runTask;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/OrganizaTodo.Desktop.Tests --filter KestrelWebAppRunnerIntegrationTests`
Expected: PASS (1 test)

- [ ] **Step 5: Wire up `Program.cs` with the Photino window**

Replace `src/OrganizaTodo.Desktop/Program.cs` with:

```csharp
using OrganizaTodo.Desktop.Lifecycle;
using Photino.NET;

using var guard = new SingleInstanceGuard("OrganizaTodo.Desktop.SingleInstance");
if (!guard.TryAcquire())
{
    Console.Error.WriteLine("OrganizaTodo is already running.");
    return;
}

var paths = AppPaths.From(AppDataRoot.ResolveForCurrentOS());
paths.EnsureDirectoriesExist();

var credentials = new DbCredentialStore(paths.ConfigDir).LoadOrCreate();
var pgPort = PortAllocator.FindAvailablePort(preferredPort: 55432);
var pgBinDirectory = ResolvePgBinDirectory();

var postgresManager = new PostgresManager(pgBinDirectory, paths.PgDataDir, pgPort, credentials, paths.LogsDir);
var schemaBootstrapper = new SchemaBootstrapper(Path.Combine(AppContext.BaseDirectory, "Database"));
var webAppRunner = new KestrelWebAppRunner();
var host = new DesktopHost(postgresManager, schemaBootstrapper, webAppRunner, pgPort, credentials, "organizatodo", "organizatodo_shopping");

string url;
try
{
    url = await host.StartAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to start OrganizaTodo: {ex}");
    return;
}

var window = new PhotinoWindow()
    .SetTitle("OrganizaTodo")
    .SetUseOsDefaultSize(false)
    .SetSize(1280, 800)
    .Load(url);

window.WindowClosing += (sender, args) =>
{
    host.ShutdownAsync().GetAwaiter().GetResult();
    return false;
};

window.WaitForClose();

static string ResolvePgBinDirectory()
{
    var overridePath = Environment.GetEnvironmentVariable("ORGANIZATODO_PG_BIN_DIR");
    if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath;
    return Path.Combine(AppContext.BaseDirectory, "pgbin", "bin");
}
```

Note: `PhotinoWindow`'s exact fluent method names (`SetTitle`, `SetUseOsDefaultSize`, `SetSize`, `Load`, `WindowClosing`, `WaitForClose`) come from Photino.NET's documented sample usage — confirm them against IntelliSense for the version `dotnet add package Photino.NET` actually resolved in Task 1, and adjust names if that version's API differs.

- [ ] **Step 6: Build the whole solution**

Run: `dotnet build OrganizaTodo.sln`
Expected: Build succeeds.

- [ ] **Step 7: Manual end-to-end verification**

```bash
export ORGANIZATODO_PG_BIN_DIR=$(pg_config --bindir)
dotnet run --project src/OrganizaTodo.Desktop
```

Expected: a native window opens showing the OrganizaTodo login page. Create an account, confirm the dashboard loads. Close the window, then confirm both the app process and the spawned `postgres` process have exited (`ps aux | grep -E 'postgres|OrganizaTodo.Desktop'` shows nothing). Re-run the same command a second time and confirm data persists (the account created in the first run still logs in) — proves `pgdata` survived across restarts.

- [ ] **Step 8: Run the full test suite one last time**

Run: `dotnet test`
Expected: PASS (all tests across the solution)

- [ ] **Step 9: Commit**

```bash
git add src/OrganizaTodo.Desktop tests/OrganizaTodo.Desktop.Tests
git commit -m "wire up Photino window and KestrelWebAppRunner for end-to-end desktop launch"
```

---

## Follow-up (separate plan, not part of this one)

Once this plan is verified working on the dev machine, a follow-up plan covers: vendoring Postgres binaries for `win-x64`/`linux-x64` into the publish output, bundling the WebView2 offline redistributable, the Inno Setup script for Windows, and the self-extracting shell installer for Linux — per the "Build & packaging" section of the design spec.

It should also address: suppressing the console window on Windows (conditional `OutputType=WinExe` for the `win-x64` publish, vs `Exe` for `linux-x64` — kept as plain `Exe` in this plan since a console is useful during `dotnet run` development) and, if request/error logs beyond Postgres's own log turn out to be worth capturing to `logs/`, wiring ASP.NET Core's logging providers to a size-capped file there.
