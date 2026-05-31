

using DAL.Interfaces;


using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

//builder.Services.AddDbContext<WalkamonContext>(
//    options =>
//        options.UseSqlServer(
//            builder.Configuration
//            .GetConnectionString(
//                "DefaultConnection"
//            )
//        )
//);

//builder.Services.AddScoped(
//    typeof(IGenericRepository<>),
//   // typeof(GenericRepository<>)
//);

//builder.Services.AddScoped<
//    IUserRepository,
//    UserRepository>();

//builder.Services.AddScoped<
//    IAuthService,
//    AuthService>();

builder.Services
.AddAuthentication(
    JwtBearerDefaults.AuthenticationScheme
)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters =
        new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            IssuerSigningKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(
                        builder.Configuration["Jwt:Key"]
                    )
                )
        };
});

builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();

app.UseSwaggerUI();

app.UseAuthentication();

app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();