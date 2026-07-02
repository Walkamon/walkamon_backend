using BLL.Interfaces;
using BLL.Options;
using BLL.Service;
using BLL.Validations;
using DAL.Data;
using DAL.GenericRepository;
using DAL.Interfaces;
using DAL.Repository;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

#region Controllers

builder.Services.AddControllers();

builder.Services.AddFluentValidationAutoValidation();


builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

#endregion

#region Database

builder.Services.AddDbContext<WalkamonContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

#endregion
#region CORS

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.Equals(
                    origin,
                    "https://purple-island-059194d00.7.azurestaticapps.net",
                    StringComparison.OrdinalIgnoreCase))
                 {
                    return true;
                }

                if (string.Equals(origin, "null", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                var isAllowedScheme = uri.Scheme is "http" or "https" or "capacitor" or "ionic";
                var isAllowedHost = uri.Host is "localhost" or "127.0.0.1" or "::1";

                return isAllowedScheme && isAllowedHost;
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
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

app.UseSwagger();

app.UseSwaggerUI();

// Local Android devices connect through `adb reverse` to the HTTP launch
// profile. Redirecting that request to the ASP.NET development HTTPS
// certificate makes the device reject the connection.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();

app.UseAuthorization();

#endregion

app.MapControllers();

app.Run();
