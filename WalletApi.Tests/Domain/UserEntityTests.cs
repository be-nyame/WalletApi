using System;
using WalletApi.Domain.Entities;
using Xunit;

namespace WalletApi.Tests.Domain;

public class UserEntityTests
{
    // Initial state for a newly created User
 
    [Fact]
    public void NewUser_HasUniqueId()
    {
        var u1 = new User();
        var u2 = new User();
        Assert.NotEqual(u1.Id, u2.Id);
    }

    [Fact]
    public void NewUser_IdIsNotEmpty()
    {
        var user = new User();
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Fact]
    public void NewUser_CreatedAtIsSet()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var user   = new User();
        var after  = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(user.CreatedAt, before, after);
    }

    [Fact]
    public void NewUser_UpdatedAtIsNullByDefault()
    {
        var user = new User();
        Assert.Null(user.UpdatedAt);
    }
    
    // Default values for new User properties

    [Fact]
    public void NewUser_EmailDefaultsToEmptyString()
    {
        var user = new User();
        Assert.Equal(string.Empty, user.Email);
    }

    [Fact]
    public void NewUser_FirstNameDefaultsToEmptyString()
    {
        var user = new User();
        Assert.Equal(string.Empty, user.FirstName);
    }

    [Fact]
    public void NewUser_LastNameDefaultsToEmptyString()
    {
        var user = new User();
        Assert.Equal(string.Empty, user.LastName);
    }

    [Fact]
    public void NewUser_PasswordHashDefaultsToEmptyString()
    {
        var user = new User();
        Assert.Equal(string.Empty, user.PasswordHash);
    }

    [Fact]
    public void NewUser_IsNotVerifiedByDefault()
    {
        var user = new User();
        Assert.False(user.IsVerified);
    }

    [Fact]
    public void NewUser_RefreshTokenIsNullByDefault()
    {
        var user = new User();
        Assert.Null(user.RefreshToken);
    }

    [Fact]
    public void NewUser_RefreshTokenExpiryIsNullByDefault()
    {
        var user = new User();
        Assert.Null(user.RefreshTokenExpiry);
    }
    
    // Assigning values to User properties

    [Fact]
    public void User_EmailCanBeAssigned()
    {
        var user = new User { Email = "test@example.com" };
        Assert.Equal("test@example.com", user.Email);
    }

    [Fact]
    public void User_FirstNameCanBeAssigned()
    {
        var user = new User { FirstName = "Jane" };
        Assert.Equal("Jane", user.FirstName);
    }

    [Fact]
    public void User_LastNameCanBeAssigned()
    {
        var user = new User { LastName = "Doe" };
        Assert.Equal("Doe", user.LastName);
    }

    [Fact]
    public void User_PasswordHashCanBeAssigned()
    {
        const string hash = "$2a$12$examplehashvalue";
        var user = new User { PasswordHash = hash };
        Assert.Equal(hash, user.PasswordHash);
    }

    [Fact]
    public void User_CanBeVerified()
    {
        var user = new User { IsVerified = true };
        Assert.True(user.IsVerified);
    }
    
    // Refresh token expiry behavior
    
    [Fact]
    public void User_RefreshTokenCanBeAssigned()
    {
        const string token = "some-refresh-token-value";
        var user = new User { RefreshToken = token };
        Assert.Equal(token, user.RefreshToken);
    }

    [Fact]
    public void User_RefreshTokenExpiryCanBeAssigned()
    {
        var expiry = DateTime.UtcNow.AddDays(7);
        var user   = new User { RefreshTokenExpiry = expiry };
        Assert.Equal(expiry, user.RefreshTokenExpiry);
    }

    [Fact]
    public void User_RefreshTokenExpiryInThePast_IsExpired()
    {
        var user = new User
        {
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(-1)
        };
        Assert.True(user.RefreshTokenExpiry < DateTime.UtcNow);
    }

    [Fact]
    public void User_RefreshTokenExpiryInTheFuture_IsNotExpired()
    {
        var user = new User
        {
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(7)
        };
        Assert.True(user.RefreshTokenExpiry > DateTime.UtcNow);
    }
}