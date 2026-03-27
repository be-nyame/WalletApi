using FluentValidation.TestHelper;
using WalletApi.Application.DTOs.Auth;
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