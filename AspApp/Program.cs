using System.Net;
using AspApp.Helpers;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using OpenIddict.Validation.AspNetCore;

namespace AspApp;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);


        //******************* Kestrel *******************
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Any, 80);
            options.Limits.MaxRequestBodySize = 16 * 1024;// 16 KB
        });


        //******************* OpenIdDict *******************
        builder.Services.AddOpenIddict()
        .AddValidation(options =>
        {
            // Note: the validation handler uses OpenID Connect discovery
            // to retrieve the issuer signing keys used to validate tokens.
            options.SetIssuer(builder.Configuration["OidcIssuer"]!);

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


        //******************* Authentication *******************
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            //options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            //options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });


        //******************* Controllers *******************
        builder.Services.AddControllers();


        //******************* AntiForgery *******************
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            //options.Cookie.Name = "XSRF-TOKEN";//swap error
        });


        //**************************** Kafka Producer **************************
        KafkaProducerService kafkaService = new(builder.Configuration);
        builder.Services.AddSingleton<KafkaProducerService>(kafkaService);


        //**************************** Logger **************************
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(); // Keep Console for local visibility
        builder.Logging.AddKafka(kafkaService, builder.Configuration);


        // ************ MongoDb ***********
        // Register IMongoClient as a Singleton
        IMongoClient mongoClient = new MongoClient(builder.Configuration["MongoDb:ConnectionString"]);
        builder.Services.AddSingleton<IMongoClient>(mongoClient);
        // Register IMongoDatabase directly as a Singleton for quick access
        IMongoDatabase userDb = mongoClient.GetDatabase(builder.Configuration["MongoDb:UserDbName"]);
        builder.Services.AddSingleton<IMongoDatabase>(userDb);


        var app = builder.Build();


        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            KnownIPNetworks = { new System.Net.IPNetwork(IPAddress.Parse("172.18.0.0"), 16) }
        });


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
