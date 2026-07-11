using CRM.Api.Middleware;
using CRM.Api.Services;
using CRM.Application;
using CRM.Application.Common.Interfaces;
using CRM.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// JWT Authentication - Token Only (No "Bearer " prefix)
var jwtSection = builder.Configuration.GetSection("Jwt");
var secret = jwtSection["Secret"] ?? throw new InvalidOperationException("JWT Secret is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // NOTE: This API deliberately expects the RAW JWT with no "Bearer " prefix
        // (see api/client.ts on the frontend, which sends it the same way).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

                if (string.IsNullOrEmpty(authHeader))
                {
                    return Task.CompletedTask;
                }

                // If it contains "Bearer ", reject it with a clear error
                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = 400;
                    context.Response.ContentType = "application/json";
                    context.Response.WriteAsync("{\"error\": \"Use token without 'Bearer ' prefix\"}");
                    return Task.CompletedTask;
                }

                // Accept ONLY the raw token (no prefix)
                context.Token = authHeader;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Swagger with Token-only support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GizmoCRM API",
        Version = "v1",
        Description = "A professional single-company CRM backend built with Clean Architecture and CQRS."
    });

    c.AddSecurityDefinition("Token", new OpenApiSecurityScheme
    {
        Description = "Enter your JWT token WITHOUT 'Bearer ' prefix. Just the token itself.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Token"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Token" }
            },
            Array.Empty<string>()
        }
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("CrmPolicy", policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:3000", "http://localhost:5173"])
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

// Build
var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "GizmoCRM API v1");
        c.DisplayRequestDuration();
    });
}

app.UseHttpsRedirection();
app.UseCors("CrmPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Seed database on startup (dev only)
if (app.Environment.IsDevelopment())
{
    await CRM.Infrastructure.Migrations.DatabaseSeeder.SeedAsync(app.Services);
}

app.Run();