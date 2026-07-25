using BLL.Interfaces;
using BLL.Options;
using BLL.Service;
using BLL.Validations;
using DAL.Data;
using DAL.GenericRepository;
using DAL.Interfaces;
using DAL.Repository;
using Walkamon.BackgroundServices;
using Walkamon.Health;
using Walkamon.Hubs;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

#region Controllers

builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddFluentValidationAutoValidation();


builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

#endregion

#region Database

builder.Services.AddDbContext<WalkamonContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()
    ));

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

#endregion
#region CORS

var configuredCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];
var corsOrigins = new HashSet<string>(
    configuredCorsOrigins.Where(origin => !string.IsNullOrWhiteSpace(origin)),
    StringComparer.OrdinalIgnoreCase);
var allowNullCorsOrigin = builder.Configuration.GetValue<bool>("Cors:AllowNullOrigin");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (corsOrigins.Contains(origin))
                {
                    return true;
                }

                if (allowNullCorsOrigin
                    && string.Equals(origin, "null", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                var isLocalScheme = uri.Scheme is "http" or "https" or "capacitor" or "ionic";
                var isLocalhostName = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
                var isLoopbackAddress = IPAddress.TryParse(uri.Host, out var address)
                    && IPAddress.IsLoopback(address);

                // Allow local web/mobile development on any port in every environment.
                // Authentication is still enforced by the API and SignalR hub.
                return isLocalScheme && (isLocalhostName || isLoopbackAddress);
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    foreach (var configuredProxy in builder.Configuration
                 .GetSection("ReverseProxy:KnownProxies")
                 .Get<string[]>() ?? [])
    {
        if (IPAddress.TryParse(configuredProxy, out var proxyAddress))
        {
            options.KnownProxies.Add(proxyAddress);
        }
    }
});

var rateLimitPermitCount = builder.Configuration.GetValue("RateLimiting:PermitLimit", 120);
var rateLimitWindowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var clientAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            clientAddress,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitPermitCount,
                Window = TimeSpan.FromSeconds(rateLimitWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

#endregion
#region Dependency Injection
builder.Services.AddScoped(typeof(IGenericRepository<>),
                           typeof(GenericRepository<>));

builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.Configure<SmtpOptions>(
    builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<PvpRealtimeOptions>(
    builder.Configuration.GetSection(PvpRealtimeOptions.SectionName));
builder.Services.Configure<StepValidationOptions>(
    builder.Configuration.GetSection(StepValidationOptions.SectionName));
builder.Services.Configure<MotionValidationOptions>(
    builder.Configuration.GetSection(MotionValidationOptions.SectionName));

builder.Services.Configure<FirebaseOptions>(
    builder.Configuration.GetSection(FirebaseOptions.SectionName));

builder.Services.Configure<DailyLoginRewardOptions>(
    builder.Configuration.GetSection(DailyLoginRewardOptions.SectionName));

builder.Services.AddScoped<IEmailSender, GmailSmtpEmailSender>();

builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();

builder.Services.AddScoped<IShopItemService, ShopItemService>();

builder.Services.AddScoped<IUserFeedbackService, UserFeedbackService>();

builder.Services.AddScoped<IInventoryService, InventoryService>();

builder.Services.AddScoped<IShopService, ShopService>();

builder.Services.AddScoped<IAdminChallengeService, AdminChallengeService>();

builder.Services.AddScoped<IAdminMissionService, AdminMissionService>();

builder.Services.AddScoped<IPlayerChallengeService, PlayerChallengeService>();

builder.Services.AddScoped<IPlayerMissionService, PlayerMissionService>();

builder.Services.AddScoped<IAdminAchievementService, AdminAchievementService>();

builder.Services.AddScoped<IPlayerAchievementService, PlayerAchievementService>();
builder.Services.AddScoped<IMissionProgressService, MissionProgressService>();
builder.Services.AddScoped<IAchievementProgressService, AchievementProgressService>();

builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();

builder.Services.AddScoped<IItemTypeService, ItemTypeService>();

builder.Services.AddScoped<IShopItemRepository, ShopItemRepository>();

builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<IDailyStepRepository, DailyStepRepository>();
builder.Services.AddScoped<IFriendRepository, FriendRepository>();
builder.Services.AddScoped<IDailyStepService, DailyStepService>();
builder.Services.AddScoped<IFriendService, FriendService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IStepGoalRepository, StepGoalRepository>();
builder.Services.AddScoped<IStepGoalService, StepGoalService>();
builder.Services.AddScoped<IStreakRewardService, StreakRewardService>();
builder.Services.AddScoped<IStreakRewardRepository, StreakRewardRepository>();
builder.Services.AddScoped<IDailyLoginRewardRepository, DailyLoginRewardRepository>();
builder.Services.AddScoped<IDailyLoginRewardService, DailyLoginRewardService>();
builder.Services.AddScoped<IPetService, PetService>();
builder.Services.AddScoped<IPetRepository, PetRepository>();

builder.Services.AddScoped<IPetInteractionRepository, PetInteractionRepository>();
builder.Services.AddScoped<  IPetEvolutionHistoryRepository, PetEvolutionHistoryRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<IFcmPushService, FcmPushService>();
builder.Services.AddScoped<IPvpSprintService, PvpSprintService>();
builder.Services.AddScoped<IValidatedStepService, ValidatedStepService>();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IAppAttestationVerifier>(
        new DevelopmentAttestationVerifier(builder.Environment.EnvironmentName));
}
else
{
    var stepValidation = builder.Configuration
        .GetSection(StepValidationOptions.SectionName)
        .Get<StepValidationOptions>() ?? new StepValidationOptions();
    if (!stepValidation.StrictAttestation)
        throw new InvalidOperationException("Strict Play Integrity validation is mandatory outside Development.");
    if (string.IsNullOrWhiteSpace(stepValidation.AndroidPackageName))
        throw new InvalidOperationException("StepValidation:AndroidPackageName is required outside Development.");
    if (string.IsNullOrWhiteSpace(stepValidation.GoogleCredentialPath))
        throw new InvalidOperationException("StepValidation:GoogleCredentialPath is required outside Development.");
    if (string.Equals(
            stepValidation.AppRecognitionMode,
            "certificate_allowlist",
            StringComparison.OrdinalIgnoreCase) &&
        string.IsNullOrWhiteSpace(stepValidation.AllowedCertificateSha256Hex))
        throw new InvalidOperationException(
            "StepValidation:AllowedCertificateSha256Hex is required in certificate_allowlist mode.");
    var credentialPath = Path.IsPathRooted(stepValidation.GoogleCredentialPath)
        ? stepValidation.GoogleCredentialPath
        : Path.Combine(builder.Environment.ContentRootPath, stepValidation.GoogleCredentialPath);
    if (!File.Exists(credentialPath))
        throw new InvalidOperationException($"Play Integrity credential file was not found: {credentialPath}");
    builder.Services.PostConfigure<StepValidationOptions>(options =>
        options.GoogleCredentialPath = credentialPath);
    builder.Services.AddHttpClient<IAppAttestationVerifier, PlayIntegrityAttestationVerifier>();
}
var backgroundServicesEnabled = builder.Configuration.GetValue(
    "BackgroundServices:Enabled",
    true);
if (backgroundServicesEnabled)
{
    builder.Services.AddHostedService<NotificationSchedulerService>();
    builder.Services.AddHostedService<PvpSprintLifecycleService>();
    builder.Services.AddHostedService<PvpOutboxDispatcherService>();
}
builder.Services.AddScoped<IAdminDashboardRepository, AdminDashboardRepository>();
builder.Services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<ISystemSettingService, SystemSettingService>();
builder.Services.AddHttpContextAccessor();
#endregion



#region JWT

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            builder.Configuration["Jwt:Key"]!
                        ))
            };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs/pvp-sprint"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Unauthorized"
                });
            },

            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Access denied"
                });
            }
        };
    });

#endregion

#region Swagger

static string GetSwaggerTag(
    Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription api)
{
    var controllerName = api.ActionDescriptor.RouteValues["controller"] ?? "Other";
    var roles = api.ActionDescriptor.EndpointMetadata
        .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
        .SelectMany(attribute => (attribute.Roles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    var section = controllerName.Equals("Auth", StringComparison.OrdinalIgnoreCase)
        ? "00 - Authentication"
        : roles.Any(role => role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            ? "02 - Admin"
            : "01 - User";

    var displayName = controllerName
        .Replace("ControllerForAdmin", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace("ForAdmin", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace("Player", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace("Admin", string.Empty, StringComparison.OrdinalIgnoreCase);

    if (string.IsNullOrWhiteSpace(displayName))
    {
        displayName = "General";
    }

    return $"{section} / {displayName}";
}

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.TagActionsBy(api => [GetSwaggerTag(api)]);

    options.OrderActionsBy(api =>
        $"{GetSwaggerTag(api)}:{api.RelativePath}:{api.HttpMethod}");

    options.AddSecurityDefinition("Bearer",
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter JWT Token"
        });

    options.AddSecurityRequirement(
        new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference =
                        new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type =
                                Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                },
                Array.Empty<string>()
            }
        });
});

#endregion

var app = builder.Build();

#region Middleware

if (app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();
app.UseRateLimiter();

// Local Android devices connect through `adb reverse` to the HTTP launch
// profile. Redirecting that request to the ASP.NET development HTTPS
// certificate makes the device reject the connection.
if (!app.Environment.IsDevelopment())
{
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments("/health"),
        branch => branch.UseHttpsRedirection());
}

app.UseCors("AllowFrontend");

app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();

app.UseAuthorization();

#endregion

app.MapControllers();
app.MapHub<SprintHub>("/hubs/pvp-sprint");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Name == "database"
});

app.Run();
