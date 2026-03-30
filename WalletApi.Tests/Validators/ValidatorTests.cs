using FluentValidation.TestHelper;
using WalletApi.Application.DTOs.Auth;
using WalletApi.Application.DTOs.Wallet;
using WalletApi.Application.Validators;

namespace WalletApi.Tests.Validators;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_PassesValidation()
    {
        var req = new RegisterRequest("John", "Doe", "john@example.com", "P@ssw0rd!");
        var result = _validator.TestValidate(req);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    public void InvalidEmail_FailsValidation(string email)
    {
        var req = new RegisterRequest("John", "Doe", email, "P@ssw0rd!");
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("short")]          // length too short
    [InlineData("alllowercase1!")] // missing uppercase letter
    [InlineData("ALLUPPERCASE1!")] // missing lowercase letter
    [InlineData("NoSpecialChar1")] // missing special character
    [InlineData("NoNumber!Abc")]   // missing number
    public void WeakPassword_FailsValidation(string password)
    {
        var req = new RegisterRequest("John", "Doe", "john@example.com", password);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void EmptyFirstName_FailsValidation()
    {
        var req = new RegisterRequest("", "Doe", "john@example.com", "P@ssw0rd!");
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void EmptyLastName_FailsValidation()
    {
        var req = new RegisterRequest("John", "", "john@example.com", "P@ssw0rd!");
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void FirstName_ExceedingMaxLength_FailsValidation()
    {
        var longName = new string('A', 101);
        var req = new RegisterRequest(longName, "Doe", "john@example.com", "P@ssw0rd!");
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }
}

public class TopUpRequestValidatorTests
{
    private readonly TopUpRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var req = new TopUpRequest(100m, "Top up");
        var result = _validator.TestValidate(req);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void NonPositiveAmount_FailsValidation(decimal amount)
    {
        var req = new TopUpRequest(amount, null);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void ExcessiveAmount_FailsValidation()
    {
        var req = new TopUpRequest(1_000_001m, null);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void NullDescription_PassesValidation()
    {
        var req = new TopUpRequest(50m, null);
        var result = _validator.TestValidate(req);
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class TransferRequestValidatorTests
{
    private readonly TransferRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var req = new TransferRequest(Guid.NewGuid(), 50m, "Payment");
        var result = _validator.TestValidate(req);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyRecipientId_FailsValidation()
    {
        var req = new TransferRequest(Guid.Empty, 50m, null);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.RecipientWalletId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void NonPositiveAmount_FailsValidation(decimal amount)
    {
        var req = new TransferRequest(Guid.NewGuid(), amount, null);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }
}