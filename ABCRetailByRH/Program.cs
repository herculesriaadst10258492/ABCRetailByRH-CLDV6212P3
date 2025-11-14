using System;
using ABCRetailByRH.Services;
using ABCRetailByRH.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// ✨ Needed for Session (in-memory cache)
builder.Services.AddDistributedMemoryCache();

// --- Session (lightweight store for UserName/UserRole) ---
builder.Services.AddSession(o =>
{
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.IdleTimeout = TimeSpan.FromHours(1);
});

// ✨ Needed because _Layout injects IHttpContextAccessor
builder.Services.AddHttpContextAccessor();

// ---- SQL (AuthDatabase) ----
var sqlConn =
    builder.Configuration.GetConnectionString("AuthDatabase")
    ?? Environment.GetEnvironmentVariable("SQLCONNSTR_AuthDatabase");

if (string.IsNullOrWhiteSpace(sqlConn))
    throw new InvalidOperationException("Missing ConnectionStrings:AuthDatabase.");

builder.Services.AddDbContext<AuthDbContext>(opt => opt.UseSqlServer(sqlConn));

// ---- Cart SQL DB ----
builder.Services.AddDbContext<CartDbContext>(opt => opt.UseSqlServer(sqlConn));

// ---- Azure Storage (unchanged) ----
var storageConnection =
    builder.Configuration["AzureStorage:ConnectionString"]
    ?? builder.Configuration.GetConnectionString("AzureStorage")
    ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(storageConnection))
    throw new InvalidOperationException(
        "Missing Azure Storage connection string. " +
        "Set AzureStorage:ConnectionString in appsettings.json OR ConnectionStrings:AzureStorage " +
        "OR AZURE_STORAGE_CONNECTION_STRING as an environment variable.");

builder.Services.AddSingleton<IAzureStorageService>(_ => new AzureStorageService(storageConnection));

// ---- Functions typed HttpClient ----
var functionsBaseUrl =
    builder.Configuration["Functions:BaseUrl"]
    ?? builder.Configuration["FunctionApi:BaseUrl"];

if (string.IsNullOrWhiteSpace(functionsBaseUrl))
    throw new InvalidOperationException("Missing Functions:BaseUrl (or FunctionApi:BaseUrl).");

builder.Services.Configure<FunctionsOptions>(opts =>
{
    opts.Key = builder.Configuration["Functions:Key"] ?? builder.Configuration["FunctionApi:Key"];
});

builder.Services.AddHttpClient<IFunctionsClient, FunctionsClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<FunctionsOptions>>().Value;
    client.BaseAddress = new Uri(functionsBaseUrl.TrimEnd('/'));
    if (!string.IsNullOrWhiteSpace(opts.Key))
    {
        client.DefaultRequestHeaders.Remove("x-functions-key");
        client.DefaultRequestHeaders.Add("x-functions-key", opts.Key);
    }
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Session must be before authz
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
