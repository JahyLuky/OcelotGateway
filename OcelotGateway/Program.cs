using log4net;
using log4net.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using OcelotGateway.Models;
using OcelotGateway.Services;
using System.Text;

var logger = LogManager.GetLogger(typeof(Program));
JwtSettings jwtSettings = null;

try
{
    logger.Info("Starting Ocelot Gateway application...");

    var builder = WebApplication.CreateBuilder(args);

    // Configure log4net
    var logRepository = LogManager.GetRepository(typeof(Program).Assembly);
    XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

    // Ensure logs directory exists
    var logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    if (!Directory.Exists(logsDirectory))
    {
        Directory.CreateDirectory(logsDirectory);
    }

    // Load ocelot.json
    try
    {
        builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
    }
    catch (Exception ex)
    {
        logger.Error($"Failed to load ocelot.json: {ex.Message}", ex);
        throw;
    }

    // Configure JWT settings
    try
    {
        jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
        if (jwtSettings == null)
        {
            logger.Error("JWT settings not found in configuration");
            throw new InvalidOperationException("JWT settings not found in configuration");
        }
        builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
        builder.Services.AddSingleton(jwtSettings);
    }
    catch (Exception ex)
    {
        logger.Error($"Failed to configure JWT settings: {ex.Message}", ex);
        throw;
    }

    // Kestrel configuration is automatically loaded from appsettings.json by CreateBuilder(args)

    var key = Encoding.UTF8.GetBytes(jwtSettings.Key);

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer("Bearer", x =>
        {
            x.RequireHttpsMetadata = false;
            x.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddScoped<IJwtService, JwtService>();
    builder.Services.AddSingleton<GatewaySecretDelegatingHandler>();
    builder.Services.AddOcelot().AddDelegatingHandler<GatewaySecretDelegatingHandler>(true);


    var app = builder.Build();

    app.UsePathBase("/api");

    app.UseAuthentication();
    app.UseAuthorization();

    app.Use(async (context, next) =>
    {
        var logger = LogManager.GetLogger("OcelotGateway.HeaderLogger");

        logger.Info($"Incoming request: {context.Request.Method} {context.Request.Path}");

        await next();
    });

    app.MapControllers();

    // Only call UseOcelot once, excluding specific paths like /api/auth/token and /health
    app.MapWhen(context => 
        !context.Request.Path.StartsWithSegments("/auth/token") && 
        !context.Request.Path.StartsWithSegments("/health"),
        appBuilder =>
        {
            appBuilder.UseOcelot().Wait();
        });

    app.Run();
}
catch (Exception ex)
{
    logger.Fatal($"Application failed to start: {ex.Message}", ex);
    throw;
}

