using System.Net;
using System.Security.Cryptography.X509Certificates;
using AspApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server.AspNetCore;

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
            options.Listen(IPAddress.Any, builder.Configuration.GetValue<int>("TcpPort"), listenOptions =>
            {
                string pemFilePath = Path.Combine(certsDirPath, "HoLibz.com.pem");
                string keyFilePath = Path.Combine(certsDirPath, "HoLibz.com.key");
                if (System.IO.File.Exists(pemFilePath) && System.IO.File.Exists(keyFilePath))
                {
                    X509Certificate2 x509Certificate2 = X509Certificate2.CreateFromPemFile(pemFilePath, keyFilePath);
                    listenOptions.UseHttps(x509Certificate2);
                }
                else
                {
                    listenOptions.UseHttps();
                }
            });

            options.Limits.MaxRequestBodySize = 16 * 1024;// 16 KB
        });



        // Identity DbContext
        builder.Services.AddDbContext<Identity_DbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration["ConnectionStrings_Postgres:IdentityConnection"]);
        });



        builder.Services.AddOpenIddict()
        .AddValidation(options =>
        {
            // Note: the validation handler uses OpenID Connect discovery
            // to retrieve the issuer signing keys used to validate tokens.
            options.SetIssuer(builder.Configuration["OidcIssuer"] ?? "https://localhost:5443/");

            // Register the encryption credentials. This sample uses a symmetric
            // encryption key that is shared between the server and the API project.
            //
            // Note: in a real world application, this encryption key should be
            // stored in a safe place (e.g in Azure KeyVault, stored as a secret).
            options.AddEncryptionKey(new SymmetricSecurityKey(
                Convert.FromBase64String("DRjd/GnduI3Efzen9V9BvbNUfc/VKgXltV7Kbk9sMkY="))
            );

            // Register the System.Net.Http integration.
            options.UseSystemNetHttp();

            // Register the ASP.NET Core host.
            options.UseAspNetCore();
        });

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme;
        });



        builder.Services.AddControllersWithViews(options =>
        {
            options.Filters.Add(new RequireHttpsAttribute());
        });



        //******************* AntiForgery *******************
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            //options.Cookie.Name = "XSRF-TOKEN";//swap error
        });



        //**************************** Custom Services **************************
        builder.Services.AddSingleton<FileExtensionContentTypeProvider>();



        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigin", policy =>
                {
                    policy.WithOrigins("https://localhost:5443", "https://localhost:5444")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
                });
            });
        }



        var app = builder.Build();



        app.UseHttpsRedirection();
        app.UseStaticFiles(new StaticFileOptions { ServeUnknownFileTypes = true });
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapDefaultControllerRoute();

        app.Map("/{*catchAll}", async (HttpContext context, FileExtensionContentTypeProvider provider) =>
        {
            string? catchAll = context.Request.RouteValues["catchAll"]?.ToString();
            if (!string.IsNullOrWhiteSpace(catchAll))
            {
                string staticFilePath =
                Path.Combine(app.Environment.WebRootPath, "AngularApp", "browser", catchAll);
                if (File.Exists(staticFilePath))
                {
                    //var provider = new FileExtensionContentTypeProvider();
                    if (provider.TryGetContentType(staticFilePath, out string? contentType))
                    {
                        context.Response.ContentType = contentType;
                        await context.Response.SendFileAsync(staticFilePath);
                        return;
                    }
                    else
                    {
                        context.Response.ContentType = "application/octet-stream";
                        await context.Response.SendFileAsync(staticFilePath);
                        return;
                    }
                }
            }

            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(
                Path.Combine(app.Environment.WebRootPath, "AngularApp", "browser", "index.html")
            );
        });



        //******************* app.Run ******************
        Console.WriteLine($"\n*** App is running on all network interfaces on port '{builder.Configuration["TcpPort"]}'");
        app.Run();
    }
}
