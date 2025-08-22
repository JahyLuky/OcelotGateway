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
    logger.Info("Log4net configured successfully");

    // Ensure logs directory exists
    var logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    if (!Directory.Exists(logsDirectory))
    {
        Directory.CreateDirectory(logsDirectory);
        logger.Info($"Created logs directory at: {logsDirectory}");
    }

    // Configure URLs from appsettings.json
    var urls = builder.Configuration.GetSection("Urls").Get<string[]>();
    if (urls != null && urls.Any())
    {
        builder.WebHost.UseUrls(urls);
        logger.Info($"Configured URLs: {string.Join(", ", urls)}");
    }
    else
    {
        logger.Error("No URLs defined in configuration");
        throw new InvalidOperationException("No URLs defined in configuration");
    }

    // Load ocelot.json
    try
    {
        builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
        logger.Info("Loaded ocelot.json configuration");
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
        logger.Info("JWT settings configured successfully");
    }
    catch (Exception ex)
    {
        logger.Error($"Failed to configure JWT settings: {ex.Message}", ex);
        throw;
    }

    var key = Encoding.UTF8.GetBytes(jwtSettings.Key);

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer("Bearer", x =>
        {
            x.RequireHttpsMetadata = true;
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
    logger.Info("Authentication configured successfully");

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddScoped<IJwtService, JwtService>();
    builder.Services.AddSingleton<GatewaySecretDelegatingHandler>();
    builder.Services.AddOcelot()
        .AddDelegatingHandler<GatewaySecretDelegatingHandler>(true);

    logger.Info("Services configured successfully");

    var app = builder.Build();
    logger.Info("Application built successfully");

    app.UseAuthentication();
    app.UseAuthorization();
    logger.Info("Authentication and Authorization middleware configured");

    app.Use(async (context, next) =>
    {
        var logger = LogManager.GetLogger("OcelotGateway.HeaderLogger");
        logger.Info($"Incoming request: {context.Request.Method} {context.Request.Path}");
        foreach (var header in context.Request.Headers)
        {
            logger.Info($"Header: {header.Key} = {header.Value}");
        }
        await next();
    });

    app.MapControllers();
    logger.Info("Controllers mapped");

    // Only call UseOcelot once, excluding specific paths like /api/auth/token
    app.MapWhen(context => !context.Request.Path.StartsWithSegments("/api/auth/token"),
        appBuilder =>
        {
            appBuilder.UseOcelot().Wait();
        });

    logger.Info("Ocelot middleware configured successfully");

    logger.Info("Starting application...");
    app.Run();
}
catch (Exception ex)
{
    logger.Fatal($"Application failed to start: {ex.Message}", ex);
    throw;
}
