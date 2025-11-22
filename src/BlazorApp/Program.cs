using Azure.Identity;
using BlazorApp.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add authentication only if AzureAd is configured
var clientId = builder.Configuration["AzureAd:ClientId"];
if (!string.IsNullOrEmpty(clientId))
{
    builder.Services.AddMicrosoftIdentityWebAppAuthentication(builder.Configuration, "AzureAd");
    builder.Services.AddControllersWithViews()
        .AddMicrosoftIdentityUI();
}
else
{
    builder.Services.AddControllersWithViews();
}

builder.Services.AddHttpContextAccessor();

// Configure authentication
builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters.NameClaimType = "name";
});

// Register application services
builder.Services.AddSingleton<PdfService>();
builder.Services.AddSingleton<TimeService>();
builder.Services.AddSingleton<TableStorageService>();
builder.Services.AddScoped<AzureOpenAIService>();

// Add HttpClient
builder.Services.AddHttpClient();

var app = builder.Build();

// Initialize table storage on startup
_ = Task.Run(async () =>
{
    try
    {
        var tableService = app.Services.GetRequiredService<TableStorageService>();
        await tableService.GetOrCreateTodayAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to initialize table storage");
    }
});

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

if (!string.IsNullOrEmpty(builder.Configuration["AzureAd:ClientId"]))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapControllers();

app.Run();
