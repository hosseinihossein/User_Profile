using System.Net;
using System.Security.Cryptography.X509Certificates;
using AspApp.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;

namespace AspApp;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);



        //******************* Certs *******************
        string certsDirPath = Path.Combine(builder.Environment.ContentRootPath, "Certs");
        Directory.CreateDirectory(certsDirPath);



        //******************* Kestrel *******************
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Any, 80);
            options.Limits.MaxRequestBodySize = 16 * 1024;// 16 KB
        });



        // Identity DbContext
        builder.Services.AddDbContext<Identity_DbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration["ConnectionStrings_Postgres:IdentityConnection"]);
        });



        //******************* Identity service *******************
        builder.Services.AddIdentity<Identity_UserDbModel, Identity_RoleDbModel>(options =>
        {
            options.User.AllowedUserNameCharacters =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._";

            options.User.RequireUniqueEmail = true;

            options.SignIn.RequireConfirmedEmail = false;

            options.Password.RequireDigit = false;
            options.Password.RequiredLength = 5;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredUniqueChars = 1;
        })
        .AddEntityFrameworkStores<Identity_DbContext>()
        .AddDefaultTokenProviders();



        builder.Services.AddOpenIddict()
        .AddValidation(options =>
        {
            // Note: the validation handler uses OpenID Connect discovery
            // to retrieve the issuer signing keys used to validate tokens.
            options.SetIssuer(builder.Configuration["HostName"] ?? "https://192.168.1.251/");

            // Register the encryption credentials. This sample uses a symmetric
            // encryption key that is shared between the server and the API project.
            //
            // Note: in a real world application, this encryption key should be
            // stored in a safe place (e.g in Azure KeyVault, stored as a secret).
            options.AddEncryptionKey(new SymmetricSecurityKey(
                Convert.FromBase64String(builder.Configuration["OidcServerEncryptionKey"]!)
            ));
            //options.AddEncryptionCertificate();

            // Register the System.Net.Http integration.
            options.UseSystemNetHttp()
            .ConfigureHttpClientHandler(handler =>
            {
                //accept self-signed tls
                handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            });

            // Register the ASP.NET Core host.
            options.UseAspNetCore();

        });

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });



        builder.Services.AddControllersWithViews();



        //******************* AntiForgery *******************
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            //options.Cookie.Name = "XSRF-TOKEN";//swap error
        });



        var app = builder.Build();



        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            KnownIPNetworks = { new System.Net.IPNetwork(IPAddress.Parse("172.18.0.0"), 16) }
        });



        //app.UseHttpsRedirection();
        //app.UseStaticFiles(new StaticFileOptions { ServeUnknownFileTypes = true });
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapDefaultControllerRoute();



        //******************* app.Run ******************
        Console.WriteLine($"\n*** App is running on all networks port '80'");
        app.Run();
    }
}
