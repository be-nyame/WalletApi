using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using WalletApi.Application.DTOs.Auth;
using WalletApi.Application.Interfaces;
using WalletApi.Domain.Entities;
using WalletApi.Infrastructure.Data;

namespace WalletApi.Application.Services;

public class AuthService : IAuthService
{
    private readonly WalletDbContext      _db;
    private readonly IConfiguration      _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        WalletDbContext      db,
        IConfiguration       config,
        ILogger<AuthService> logger)
    {
        _db     = db;
        _config = config;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest req, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Registration attempt | Email: {Email}", req.Email);

        if (string.IsNullOrWhiteSpace(req.Email))
            throw new ArgumentException("Email is required.");

        if (string.IsNullOrWhiteSpace(req.Password))
            throw new ArgumentException("Password is required.");

        if (await _db.Users.AnyAsync(u => u.Email == req.Email.ToLowerInvariant(), ct))
        {
            _logger.LogWarning(
                "Registration failed — email already exists | Email: {Email}",
                req.Email);
            throw new InvalidOperationException("Email already registered.");
        }

        var user = new User
        {
            Email        = req.Email.ToLowerInvariant(),
            FirstName    = req.FirstName,
            LastName     = req.LastName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12)
        };

        try
        {
            await _db.Users.AddAsync(user, ct);
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex,
                "Registration failed — database error | Email: {Email}", req.Email);
            throw;
        }

        // Generate the token pair first so we can hash and persist the
        // refresh token. Without this step, RefreshTokenAsync cannot find a
        // matching hash and returns 401 immediately after registration.
        var tokens = GenerateTokens(user);

        try
        {
            user.RefreshToken       = BCrypt.Net.BCrypt.HashPassword(tokens.RefreshToken);
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex,
                "Registration failed — could not persist refresh token | Email: {Email}",
                req.Email);
            throw;
        }

        _logger.LogInformation(
            "Registration successful | UserId: {UserId} | Email: {Email}",
            user.Id, user.Email);

        return tokens;
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest req, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Login attempt | Email: {Email}", req.Email);

        if (string.IsNullOrWhiteSpace(req.Email))
            throw new ArgumentException("Email is required.");

        if (string.IsNullOrWhiteSpace(req.Password))
            throw new ArgumentException("Password is required.");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant(), ct);

        if (user is null)
        {
            _logger.LogWarning(
                "Login failed — user not found | Email: {Email}", req.Email);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        bool passwordValid;
        try
        {
            passwordValid = BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Login failed — password verification error | Email: {Email}",
                req.Email);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (!passwordValid)
        {
            _logger.LogWarning(
                "Login failed — wrong password | Email: {Email} | UserId: {UserId}",
                req.Email, user.Id);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        AuthResponse tokens;
        try
        {
            tokens = GenerateTokens(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Login failed — token generation error | UserId: {UserId}", user.Id);
            throw;
        }

        try
        {
            user.RefreshToken       = BCrypt.Net.BCrypt.HashPassword(tokens.RefreshToken);
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex,
                "Login failed — could not persist refresh token | UserId: {UserId}",
                user.Id);
            throw;
        }

        _logger.LogInformation(
            "Login successful | UserId: {UserId} | Email: {Email}",
            user.Id, user.Email);

        return tokens;
    }

    public async Task<AuthResponse> RefreshTokenAsync(
        string refreshToken, CancellationToken ct = default)
    {
        _logger.LogInformation("Refresh token attempt");

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning("Refresh token failed — empty token provided");
            throw new UnauthorizedAccessException("Refresh token is required.");
        }

        List<User> users;
        try
        {
            users = await _db.Users
                .Where(u => u.RefreshTokenExpiry > DateTime.UtcNow)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh token failed — database error");
            throw;
        }

        User? user;
        try
        {
            user = users.FirstOrDefault(u =>
                u.RefreshToken != null &&
                BCrypt.Net.BCrypt.Verify(refreshToken, u.RefreshToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Refresh token failed — token verification error");
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        if (user is null)
        {
            _logger.LogWarning(
                "Refresh token failed — no matching valid token found");
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        AuthResponse tokens;
        try
        {
            tokens = GenerateTokens(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Refresh token failed — token generation error | UserId: {UserId}",
                user.Id);
            throw;
        }

        try
        {
            user.RefreshToken       = BCrypt.Net.BCrypt.HashPassword(tokens.RefreshToken);
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex,
                "Refresh token failed — could not persist new token | UserId: {UserId}",
                user.Id);
            throw;
        }

        _logger.LogInformation(
            "Refresh token successful | UserId: {UserId}", user.Id);

        return tokens;
    }

    private AuthResponse GenerateTokens(User user)
    {
        var jwtSecret = _config["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
            throw new InvalidOperationException(
                "JWT secret is not configured. Check Jwt:Secret in appsettings.");

        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(
                         int.Parse(_config["Jwt:ExpiryMinutes"] ?? "60"));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim("firstName",                   user.FirstName)
        };

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            expiry,
            signingCredentials: creds);

        var refreshToken = Convert.ToBase64String(
                               RandomNumberGenerator.GetBytes(64));

        _logger.LogDebug(
            "Tokens generated | UserId: {UserId} | Expiry: {Expiry}",
            user.Id, expiry);

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            refreshToken,
            expiry,
            user.Id);
    }
}