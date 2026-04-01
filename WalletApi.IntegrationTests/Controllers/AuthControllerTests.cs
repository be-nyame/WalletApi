using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WalletApi.Infrastructure.Data;
using WalletApi.IntegrationTests.Commons;

namespace WalletApi.IntegrationTests.Controllers;

public class RegisterTests : AuthTestBase
{
    public RegisterTests(AuthApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Register_ValidRequest_Returns201WithTokens()
    {
        await ResetDatabaseAsync();

        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email     = "newuser@example.com",
            Password  = "Password123!",
            FirstName = "Jane",
            LastName  = "Doe"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var auth = await response.Content.ReadFromJsonAsync<TestAuthResponse>(JsonOptions);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.NotEqual(Guid.Empty, auth.UserId);
        Assert.True(auth.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        await ResetDatabaseAsync();

        await RegisterAsync("dup@example.com");

        var secondResponse = await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email     = "dup@example.com",
            Password  = "AnotherPass1!",
            FirstName = "Jane",
            LastName  = "Doe"
        });

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Register_EmailIsCaseInsensitive_DuplicateIsRejected()
    {
        await ResetDatabaseAsync();

        await RegisterAsync("CaseTest@example.com");

        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email     = "CASETEST@EXAMPLE.COM",
            Password  = "Password123!",
            FirstName = "Same",
            LastName  = "Person"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_MissingEmail_ReturnsBadRequest()
    {
        await ResetDatabaseAsync();

        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email     = "",
            Password  = "Password123!",
            FirstName = "No",
            LastName  = "Email"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_MissingPassword_ReturnsBadRequest()
    {
        await ResetDatabaseAsync();

        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email     = "nopass@example.com",
            Password  = "",
            FirstName = "No",
            LastName  = "Password"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_UniqueUserIds_AreDistinctForDifferentUsers()
    {
        await ResetDatabaseAsync();

        var first  = await RegisterAsync("user1@example.com");
        var second = await RegisterAsync("user2@example.com");

        Assert.NotEqual(first.UserId, second.UserId);
    }

    [Fact]
    public async Task Register_AccessTokensAreDistinctForDifferentUsers()
    {
        await ResetDatabaseAsync();

        var first  = await RegisterAsync("distinct1@example.com");
        var second = await RegisterAsync("distinct2@example.com");

        Assert.NotEqual(first.AccessToken, second.AccessToken);
    }
}

public class LoginTests : AuthTestBase
{
    public LoginTests(AuthApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithTokens()
    {
        await ResetDatabaseAsync();
        await RegisterAsync("login@example.com", "Password123!");

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email    = "login@example.com",
            Password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var auth = await response.Content.ReadFromJsonAsync<TestAuthResponse>(JsonOptions);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.NotEqual(Guid.Empty, auth.UserId);
        Assert.True(auth.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        await ResetDatabaseAsync();
        await RegisterAsync("wrongpass@example.com", "Correct1!");

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email    = "wrongpass@example.com",
            Password = "WrongPassword!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_NonExistentEmail_Returns401()
    {
        await ResetDatabaseAsync();

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email    = "ghost@example.com",
            Password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive_Succeeds()
    {
        await ResetDatabaseAsync();
        await RegisterAsync("mixedcase@example.com", "Password123!");

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email    = "MIXEDCASE@EXAMPLE.COM",
            Password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_MissingEmail_ReturnsBadRequest()
    {
        await ResetDatabaseAsync();

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email    = "",
            Password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_MissingPassword_ReturnsBadRequest()
    {
        await ResetDatabaseAsync();

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email    = "nopwd@example.com",
            Password = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_UserIdMatchesRegistration()
    {
        await ResetDatabaseAsync();
        var registered = await RegisterAsync("idmatch@example.com", "Password123!");

        var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email    = "idmatch@example.com",
            Password = "Password123!"
        });

        var loggedIn = await loginResponse.Content
            .ReadFromJsonAsync<TestAuthResponse>(JsonOptions);

        Assert.Equal(registered.UserId, loggedIn!.UserId);
    }
}

public class RefreshTokenTests : AuthTestBase
{
    public RefreshTokenTests(AuthApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Refresh_ValidToken_Returns200WithNewTokens()
    {
        await ResetDatabaseAsync();
        var registered = await RegisterAsync("refresh@example.com");

        var response = await Client.PostAsJsonAsync(
            "/api/v1/auth/refresh", registered.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var auth = await response.Content.ReadFromJsonAsync<TestAuthResponse>(JsonOptions);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.NotEqual(Guid.Empty, auth.UserId);
        Assert.True(auth.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Refresh_ValidToken_RotatesRefreshToken()
    {
        await ResetDatabaseAsync();
        var registered = await RegisterAsync("rotate@example.com");

        var response = await Client.PostAsJsonAsync(
            "/api/v1/auth/refresh", registered.RefreshToken);

        var refreshed = await response.Content
            .ReadFromJsonAsync<TestAuthResponse>(JsonOptions);

        Assert.NotEqual(registered.RefreshToken, refreshed!.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ReusedOldToken_Returns401()
    {
        await ResetDatabaseAsync();
        var registered = await RegisterAsync("reuse@example.com");

        // Rotate once — the original token should now be invalid.
        await Client.PostAsJsonAsync("/api/v1/auth/refresh", registered.RefreshToken);

        var replayResponse = await Client.PostAsJsonAsync(
            "/api/v1/auth/refresh", registered.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_InvalidToken_Returns401()
    {
        await ResetDatabaseAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/v1/auth/refresh", "this-is-not-a-valid-refresh-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_EmptyToken_Returns401()
    {
        await ResetDatabaseAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/v1/auth/refresh", string.Empty);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_TokenFromOneUser_CannotImpersonateAnother()
    {
        await ResetDatabaseAsync();

        var userA = await RegisterAsync("userA-refresh@example.com");
        var userB = await RegisterAsync("userB-refresh@example.com");

        // userA's token must not yield userB's identity.
        var response = await Client.PostAsJsonAsync(
            "/api/v1/auth/refresh", userA.RefreshToken);

        var refreshed = await response.Content
            .ReadFromJsonAsync<TestAuthResponse>(JsonOptions);

        Assert.Equal(userA.UserId, refreshed!.UserId);
        Assert.NotEqual(userB.UserId, refreshed.UserId);
    }

    [Fact]
    public async Task Refresh_CanBeUsedMultipleTimesSequentially()
    {
        await ResetDatabaseAsync();
        var auth = await RegisterAsync("chain@example.com");

        for (var i = 0; i < 3; i++)
        {
            var response = await Client.PostAsJsonAsync(
                "/api/v1/auth/refresh", auth.RefreshToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            auth = (await response.Content
                .ReadFromJsonAsync<TestAuthResponse>(JsonOptions))!;
        }

        // After three rotations the latest token should still be valid.
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
    }
}