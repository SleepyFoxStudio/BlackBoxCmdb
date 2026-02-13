using Amazon;
using Amazon.S3;
using BlackBoxCmdb;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    return new AmazonS3Client(RegionEndpoint.EUWest1);
});

// Register DataService as singleton
builder.Services.AddSingleton<DataService>(sp =>
{
    var s3Client = sp.GetRequiredService<IAmazonS3>();
    string bucketName = "ceat-defaults";
    string s3Key = "cloud-ops-aws-accounts.json";

    var service = new DataService(s3Client, bucketName, s3Key);
    service.LoadAsync().GetAwaiter().GetResult(); // Load at startup
    return service;
});

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