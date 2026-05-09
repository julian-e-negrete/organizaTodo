using Microsoft.AspNetCore.Authentication.Cookies;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Repositories;
using OrganizaTodo.Web.Services;

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
