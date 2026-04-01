using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WalletApi.Infrastructure.Data;

namespace WalletApi.IntegrationTests.Commons;

/// <summary>
/// Base class that every auth test class inherits.
/// Provides a shared factory, a pre-configured HttpClient, and helpers
/// for database reset and registration.
/// </summary>
public abstract class AuthTestBase : IClassFixture<AuthApiFactory>
{
    protected readonly HttpClient            Client;
    protected readonly AuthApiFactory        Factory;
    protected readonly JsonSerializerOptions JsonOptions;

    protected AuthTestBase(AuthApiFactory factory)
    {
        Factory     = factory;
        Client      = factory.CreateClient();
        JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    /// <summary>Wipes and recreates the In-Memory database before each test.</summary>
    protected async Task ResetDatabaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    /// <summary>Registers a new user and returns the full auth response.</summary>
    protected async Task<TestAuthResponse> RegisterAsync(
        string email     = "test@example.com",
        string password  = "Password123!",
        string firstName = "Test",
        string lastName  = "User")
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email     = email,
            Password  = password,
            FirstName = firstName,
            LastName  = lastName
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TestAuthResponse>(JsonOptions))!;
    }
}

/// <summary>
/// Base class that every wallet test class inherits.
/// Same structure as AuthTestBase.
/// </summary>
public abstract class WalletTestBase : IClassFixture<WalletApiFactory>
{
    protected readonly HttpClient              Client;
    protected readonly WalletApiFactory        Factory;
    protected readonly JsonSerializerOptions   JsonOptions;

    protected WalletTestBase(WalletApiFactory factory)
    {
        Factory     = factory;
        Client      = factory.CreateClient();
        JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }
    
    protected async Task ResetDatabaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }
    
    protected async Task<(string AccessToken, Guid UserId)> RegisterAndLoginAsync(
        string email    = "wallet@example.com",
        string password = "Password123!")
    {
        var payload = new
        {
            Email     = email,
            Password  = password,
            FirstName = "Test",
            LastName  = "User"
        };

        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", payload);
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<TestAuthResponse>(JsonOptions);
        return (auth!.AccessToken, auth.UserId);
    }

    /// <summary>Creates an <see cref="HttpClient"/> with the Bearer token pre-set.</summary>
    protected HttpClient AuthenticatedClient(string accessToken)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}