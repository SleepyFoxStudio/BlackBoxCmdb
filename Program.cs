using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddOpenIdConnect(options =>
{
    var azureAd = builder.Configuration.GetSection("AzureAd");

    options.Authority = $"{azureAd["Instance"]}{azureAd["TenantId"]}/v2.0";
    options.ClientId = azureAd["ClientId"];
    options.ClientSecret = azureAd["ClientSecret"];
    options.ResponseType = "code";
    options.SaveTokens = true;
});

builder.Services.AddAuthorization(options =>
{
    // Require login for entire site
    options.FallbackPolicy = options.DefaultPolicy;
});

var app = builder.Build();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();