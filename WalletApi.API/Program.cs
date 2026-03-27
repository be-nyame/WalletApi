using Microsoft.EntityFrameworkCore;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.HttpOverrides;
using System.Text;
using WalletApi.Application.Interfaces;
using WalletApi.Application.Services;
using WalletApi.Application.Validators;
using WalletApi.Infrastructure.Data;
using WalletApi.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// Application services
builder.Services.AddScoped<IAuthService, AuthService>();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.Zero
        };
    });

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "Wallet API",
        Version = "v1"
    });

    c.AddServer(new OpenApiServer { Url = "/" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter your JWT access token. Example: eyJhbGci..."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var isTesting = builder.Environment.IsEnvironment("Testing");

builder.Services.AddControllers();

var app = builder.Build();

// Migrations
// Skipped in Testing — InMemory does not support migrations.
// The test base class calls EnsureDeleted/EnsureCreated before each test.
if (!isTesting)
{
    using var scope = app.Services.CreateScope();
    var db     = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Failed to apply migrations. Shutting down.");
        throw;
    }
}

// Middleware pipeline
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
                       | ForwardedHeaders.XForwardedProto
});
// Convert unhandled exceptions to structured JSON error responses.
app.UseMiddleware<ExceptionMiddleware>();

// Decode JWT → populate HttpContext.User.
app.UseAuthentication();

// Enforce [Authorize] using the identity set by UseAuthentication.
app.UseAuthorization();

// Swagger (development only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();