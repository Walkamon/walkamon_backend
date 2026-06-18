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

builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
#endregion
builder.Services.AddScoped<IItemTypeService, ItemTypeService>();

builder.Services.AddScoped<IShopItemRepository, ShopItemRepository>();

builder.Services.AddScoped<IItemService, ItemService>();

builder.Services.AddScoped<IFriendService, FriendService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddHttpContextAccessor();


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

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
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

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();

app.UseAuthorization();

#endregion

app.MapControllers();

app.Run();
