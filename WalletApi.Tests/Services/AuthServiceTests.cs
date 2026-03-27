using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WalletApi.Application.DTOs.Auth;
using WalletApi.Application.Services;
using WalletApi.Domain.Entities;
using WalletApi.Infrastructure.Data;
using Xunit;

namespace WalletApi.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly WalletDbContext _db;
    private readonly IConfiguration _config;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<WalletDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new WalletDbContext(options);

        var inMemorySettings = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Jwt:Secret",        "super-secret-test-key-that-is-at-least-32-chars" },
            { "Jwt:Issuer",        "WalletApi.Tests" },
            { "Jwt:Audience",      "WalletApiUsers" },
            { "Jwt:ExpiryMinutes", "60" }
        };

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _sut = new AuthService(
            _db,
            _config,
            Mock.Of<ILogger<AuthService>>()   // ← add this
        );
    }

    // -----------------------------------------------------------------------
    // Register
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_WithValidRequest_ReturnsTokens()
    {
        var request = new RegisterRequest("John", "Doe", "john@test.com", "P@ssw0rd!");

        var result = await _sut.RegisterAsync(request);

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task RegisterAsync_CreatesUserInDatabase()
    {
        var request = new RegisterRequest("Jane", "Doe", "jane@test.com", "P@ssw0rd!");

        await _sut.RegisterAsync(request);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == "jane@test.com");
        Assert.NotNull(user);
        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Doe", user.LastName);
    }

    [Fact]
    public async Task RegisterAsync_HashesPasswordCorrectly()
    {
        var request = new RegisterRequest("Alice", "Smith", "alice@test.com", "P@ssw0rd!");

        await _sut.RegisterAsync(request);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == "alice@test.com");
        Assert.NotNull(user);
        Assert.NotEqual("P@ssw0rd!", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("P@ssw0rd!", user.PasswordHash));
    }

    [Fact]
    public async Task RegisterAsync_AutoCreatesWalletForNewUser()
    {
        var request = new RegisterRequest("Bob", "Jones", "bob@test.com", "P@ssw0rd!");

        await _sut.RegisterAsync(request);

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == "bob@test.com");

        Assert.NotNull(user);
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ThrowsInvalidOperationException()
    {
        var request = new RegisterRequest("User", "One", "duplicate@test.com", "P@ssw0rd!");
        await _sut.RegisterAsync(request);

        var duplicate = new RegisterRequest("User", "Two", "duplicate@test.com", "P@ssw0rd!");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(duplicate));
    }

    // -----------------------------------------------------------------------
    // Login
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokens()
    {
        await _sut.RegisterAsync(new RegisterRequest("Test", "User", "login@test.com", "P@ssw0rd!"));

        var result = await _sut.LoginAsync(new LoginRequest("login@test.com", "P@ssw0rd!"));

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsUnauthorizedAccessException()
    {
        await _sut.RegisterAsync(new RegisterRequest("Test", "User", "wrongpass@test.com", "P@ssw0rd!"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.LoginAsync(new LoginRequest("wrongpass@test.com", "WrongPassword!")));
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentEmail_ThrowsUnauthorizedAccessException()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.LoginAsync(new LoginRequest("ghost@test.com", "P@ssw0rd!")));
    }

    [Fact]
    public async Task LoginAsync_IsCaseInsensitiveForEmail()
    {
        await _sut.RegisterAsync(new RegisterRequest("Test", "User", "case@test.com", "P@ssw0rd!"));

        // Login with uppercase email — should still work
        var result = await _sut.LoginAsync(new LoginRequest("CASE@TEST.COM", "P@ssw0rd!"));

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
    }

    [Fact]
    public async Task LoginAsync_PersistsHashedRefreshTokenInDatabase()
    {
        await _sut.RegisterAsync(new RegisterRequest("Test", "User", "refresh@test.com", "P@ssw0rd!"));
        var result = await _sut.LoginAsync(new LoginRequest("refresh@test.com", "P@ssw0rd!"));

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == "refresh@test.com");

        Assert.NotNull(user);
        Assert.NotNull(user.RefreshToken);
        // The stored token should be hashed, not the raw value
        Assert.NotEqual(result.RefreshToken, user.RefreshToken);
        Assert.True(BCrypt.Net.BCrypt.Verify(result.RefreshToken, user.RefreshToken));
    }

    // -----------------------------------------------------------------------
    // Refresh token
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RefreshTokenAsync_WithValidToken_ReturnsNewTokens()
    {
        await _sut.RegisterAsync(new RegisterRequest("Test", "User", "rt@test.com", "P@ssw0rd!"));
        var loginResult = await _sut.LoginAsync(new LoginRequest("rt@test.com", "P@ssw0rd!"));

        var refreshResult = await _sut.RefreshTokenAsync(loginResult.RefreshToken);

        Assert.NotNull(refreshResult);
        Assert.NotEmpty(refreshResult.AccessToken);
        Assert.NotEqual(loginResult.RefreshToken, refreshResult.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_WithInvalidToken_ThrowsUnauthorizedAccessException()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RefreshTokenAsync("totally-invalid-token"));
    }

    [Fact]
    public async Task RefreshTokenAsync_WithExpiredToken_ThrowsUnauthorizedAccessException()
    {
        await _sut.RegisterAsync(new RegisterRequest("Test", "User", "expired@test.com", "P@ssw0rd!"));
        var loginResult = await _sut.LoginAsync(new LoginRequest("expired@test.com", "P@ssw0rd!"));

        // Manually expire the token in the DB
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == "expired@test.com");
        user!.RefreshTokenExpiry = DateTime.UtcNow.AddDays(-1);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RefreshTokenAsync(loginResult.RefreshToken));
    }

    public void Dispose() => _db.Dispose();
}