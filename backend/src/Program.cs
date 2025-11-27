// Program.cs -> equivalent to index.ts in typescript
// the appsettings.json file is also meant to work with this
using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Controllers
builder.Services.AddControllers();

// MySQL connection factory (simple, per-request)
builder.Services.AddScoped<IDbConnection>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config.GetConnectionString("DefaultConnection");
    return new MySqlConnection(connectionString);
});

// TODO: configure JWT auth to match your existing tokens
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Fill these in to match your auth.routes.ts token issuing logic
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            // IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("your_secret_here")),
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Root endpoint
app.MapGet("/", () => "Survey API is running!");

app.Run();
