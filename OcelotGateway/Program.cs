using log4net;
using log4net.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.LoadBalancer.LoadBalancers;
using Ocelot.Middleware;
using OcelotGateway.LoadBalancers;
using OcelotGateway.Middleware;
using OcelotGateway.Models;
using OcelotGateway.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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

    // Load swagger-services.json for unified Swagger UI
    try
    {
        builder.Configuration.AddJsonFile("swagger-services.json", optional: true, reloadOnChange: true);
    }
    catch (Exception ex)
    {
        logger.Warn($"Failed to load swagger-services.json: {ex.Message}", ex);
        // Continue without swagger services configuration
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
                ClockSkew = TimeSpan.Zero,
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = ClaimTypes.Role
            };
        });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Gateway API", Version = "v1" });
        c.DocInclusionPredicate((docName, apiDesc) =>
        {
            var path = apiDesc.RelativePath.Trim('/');
            return path.Equals("health", StringComparison.OrdinalIgnoreCase) ||
                   path.Equals("auth/token", StringComparison.OrdinalIgnoreCase);
        });
    });
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddHttpClient(); // Add HTTP client factory for Swagger proxy service

    // Register existing services
    builder.Services.AddScoped<IJwtService, JwtService>();
    builder.Services.AddSingleton<GatewaySecretDelegatingHandler>();

    logger.Info("Swagger services registered successfully");

    // Register custom load balancer factory
    builder.Services.AddSingleton<ILoadBalancerFactory, PrimaryBackupLoadBalancerFactory>();

    builder.Services.AddOcelot()
        .AddDelegatingHandler<GatewaySecretDelegatingHandler>(true);


    var app = builder.Build();

    app.UsePathBase("/api");

    // Enable static files for custom Swagger UI assets
    app.UseStaticFiles();

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "Gateway API");

        // Configure other Swagger endpoints from appsettings.json
        var swaggerEndpoints = builder.Configuration.GetSection("SwaggerEndpoints").Get<List<SwaggerEndpointConfig>>();
        if (swaggerEndpoints != null)
        {
            foreach (var endpoint in swaggerEndpoints)
            {
                c.SwaggerEndpoint(endpoint.Url, endpoint.Name);
            }
        }

        c.RoutePrefix = "swagger";

        c.ConfigObject.AdditionalItems["validatorUrl"] = "none";

        // Set default expanded state
        c.DefaultModelsExpandDepth(1);
        c.DefaultModelExpandDepth(1);
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);

        // Enable request/response interceptors for better debugging
        c.EnableDeepLinking();
        // c.EnableFilter(); // Commented out to remove the Filter (by tag) section
        c.EnableValidator();

        // Inject custom JavaScript for enhanced UI
        c.InjectJavascript("/swagger-custom.js");

        // Set custom document title
        c.DocumentTitle = "API Documentation - Gateway";
    });

    app.UseAuthentication();
    app.UseAuthorization();

    app.Use(async (context, next) =>
    {
        var logger = LogManager.GetLogger("OcelotGateway.HeaderLogger");

        if (context.Request.Path != "/health")
            logger.Info($"Incoming request: {context.Request.Method} {context.Request.Path}");

        await next();
    });

    app.MapControllers();

    // Only call UseOcelot once, excluding specific paths like /auth/token and /health
    app.MapWhen(context =>
        !context.Request.Path.StartsWithSegments("/auth/token") &&
        !context.Request.Path.StartsWithSegments("/health") &&
        !context.Request.Path.StartsWithSegments("/swagger"),
        appBuilder =>
        {
            appBuilder.UseMiddleware<AuthorizationMiddleware>();
            appBuilder.UseOcelot().Wait();
        });

    app.Run();
}
catch (Exception ex)
{
    logger.Fatal($"Application failed to start: {ex.Message}", ex);
    throw;
}