// Controllers/AuthController.cs
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using SurveyApi.Models;

namespace SurveyApi.Controllers;

[ApiController]
[Route("api")]
public class AuthController : ControllerBase
{
    private readonly IDbConnection _connection;
    private readonly IConfiguration _config;

    public AuthController(IDbConnection connection, IConfiguration config)
    {
        _connection = connection;
        _config = config;
    }

    // POST /api/register
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length < 6)
        {
            return BadRequest(new
            {
                error = "Email and password required (min 6 characters)."
            });
        }

        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        const string sql = @"
            INSERT INTO users (email, password)
            VALUES (@Email, @Password);
        ";

        try
        {
            var affected = await _connection.ExecuteAsync(sql, new
            {
                Email = request.Email,
                Password = hashedPassword
            });

            if (affected == 0)
            {
                return StatusCode(500, new { error = "Failed to register user." });
            }

            return StatusCode(201, new { message = "User registered successfully." });
        }
        catch (MySqlException ex) when (ex.Number == 1062) // ER_DUP_ENTRY
        {
            return BadRequest(new { error = "Email already in use." });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Registration error: {ex}");
            return StatusCode(500, new { error = "Failed to register user." });
        }
    }

    // POST /api/login
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        const string sql = @"
            SELECT id, password
            FROM users
            WHERE email = @Email;
        ";

        try
        {
            var user = await _connection.QuerySingleOrDefaultAsync<(int Id, string Password)>(
                sql,
                new { Email = request.Email }
            );

            if (user == default)
            {
                return Unauthorized(new { error = "Invalid credentials" });
            }

            var passwordMatch = BCrypt.Net.BCrypt.Verify(request.Password, user.Password);
            if (!passwordMatch)
            {
                return Unauthorized(new { error = "Invalid credentials" });
            }

            // Build JWT
            var token = GenerateJwtToken(user.Id);

            return Ok(new LoginResponse { Token = token });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Login error: {ex}");
            return StatusCode(500, new { error = "Login failed." });
        }
    }

    private string GenerateJwtToken(int userId)
    {
        var jwtSecret = _config["JWT_SECRET"];
        if (string.IsNullOrEmpty(jwtSecret))
        {
            throw new Exception("JWT_SECRET environment variable is not set.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(jwtSecret);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // Default expiry: 1h (like Node)
        var expiresInConfig = _config["JWT_EXPIRES_IN"]; // e.g. "1h"
        // For simplicity, just support hours (e.g. "1h", "2h"):
        var expires = DateTime.UtcNow.AddHours(ParseExpiresToHours(expiresInConfig));

        var claims = new[]
        {
            // Equivalent to payload { user_id: user.id }
            new Claim("user_id", userId.ToString()),
            // Also set standard name identifier so [Authorize] helpers can use it
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var tokenDescriptor = new JwtSecurityToken(
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }

    private static double ParseExpiresToHours(string? expiresIn)
    {
        if (string.IsNullOrWhiteSpace(expiresIn))
            return 1; // default 1 hour

        // crude parser: "1h" -> 1, "2h" -> 2
        if (expiresIn.EndsWith("h", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(expiresIn.TrimEnd('h', 'H'), out var hours))
        {
            return hours;
        }

        // If they put "3600s" or whatever, you can expand this.
        return 1;
    }
}
