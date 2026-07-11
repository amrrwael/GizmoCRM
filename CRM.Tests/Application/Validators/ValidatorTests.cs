using CRM.Application.Features.Auth.Commands;
using CRM.Application.Features.Contacts.Commands;
using CRM.Application.Features.Deals.Commands;
using CRM.Application.Features.Activities.Commands;
using CRM.Domain.Enums;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace CRM.Tests.Application.Validators;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Valid_Login_ShouldPass()
    {
        var result = _validator.TestValidate(new LoginCommand("alice@acme.com", "password123"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "password")]          // empty email
    [InlineData("not-an-email", "pass")]  // bad email format
    [InlineData("a@b.com", "")]           // empty password
    public void Invalid_Login_ShouldFail(string email, string password)
    {
        var result = _validator.TestValidate(new LoginCommand(email, password));
        result.ShouldHaveAnyValidationError();
    }
}

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void Valid_Register_ShouldPass()
    {
        var cmd = new RegisterCommand("alice@acme.com", "Secure@123", "Alice", "Johnson", UserRole.Sales);
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("not-email", "Secure@123", "Alice", "Johnson")]   // bad email
    [InlineData("a@b.com", "short1A", "Alice", "Johnson")]        // password too short
    [InlineData("a@b.com", "nouppercase1", "Alice", "Johnson")]   // no uppercase
    [InlineData("a@b.com", "NoDigitHere!", "Alice", "Johnson")]   // no digit
    [InlineData("a@b.com", "Secure@123", "", "Johnson")]          // empty first name
    [InlineData("a@b.com", "Secure@123", "Alice", "")]            // empty last name
    public void Invalid_Register_ShouldFail(string email, string password, string firstName, string lastName)
    {
        var cmd = new RegisterCommand(email, password, firstName, lastName, UserRole.Sales);
        _validator.TestValidate(cmd).ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Password_MissingUppercase_ShouldFailWithCorrectMessage()
    {
        var cmd = new RegisterCommand("a@b.com", "alllowercase1", "Alice", "Johnson", UserRole.Sales);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void Password_MissingDigit_ShouldFailWithCorrectMessage()
    {
        var cmd = new RegisterCommand("a@b.com", "NoDigitsHere!", "Alice", "Johnson", UserRole.Sales);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one digit.");
    }

    [Fact]
    public void Email_TooLong_ShouldFail()
    {
        var longEmail = new string('a', 195) + "@b.com";
        var cmd = new RegisterCommand(longEmail, "Secure@123", "Alice", "Johnson", UserRole.Sales);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Email);
    }
}

public class CreateContactValidatorTests
{
    private readonly CreateContactValidator _validator = new();

    [Fact]
    public void Valid_Contact_ShouldPass()
    {
        var cmd = new CreateContactCommand(
            "Alice", "Johnson", "alice@acme.com",
            "+1-555-0101", "Acme Corp", "CEO",
            "High-priority lead", ["enterprise"]);
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyFirstName_ShouldFail()
    {
        var cmd = new CreateContactCommand("", "Johnson", "a@b.com", null, null, null, null, null);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void EmptyLastName_ShouldFail()
    {
        var cmd = new CreateContactCommand("Alice", "", "a@b.com", null, null, null, null, null);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-valid-email")]
    [InlineData("@missing-local.com")]
    public void InvalidEmail_ShouldFail(string email)
    {
        var cmd = new CreateContactCommand("Alice", "Johnson", email, null, null, null, null, null);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void PhoneTooLong_ShouldFail()
    {
        var cmd = new CreateContactCommand("Alice", "Johnson", "a@b.com",
            new string('1', 51), null, null, null, null);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void NullPhone_ShouldPass()
    {
        var cmd = new CreateContactCommand("Alice", "Johnson", "a@b.com", null, null, null, null, null);
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }
}

public class CreateDealValidatorTests
{
    private readonly CreateDealValidator _validator = new();

    [Fact]
    public void Valid_Deal_ShouldPass()
    {
        var cmd = new CreateDealCommand(
            "Acme Enterprise Deal", 75000m,
            Guid.NewGuid(), null,
            DateTime.UtcNow.AddMonths(2), "Notes");
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyTitle_ShouldFail()
    {
        var cmd = new CreateDealCommand("", 1000m, Guid.NewGuid(), null, null, null);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void NegativeValue_ShouldFail()
    {
        var cmd = new CreateDealCommand("Deal", -1m, Guid.NewGuid(), null, null, null);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Value);
    }

    [Fact]
    public void ZeroValue_ShouldPass()
    {
        var cmd = new CreateDealCommand("Free Pilot", 0m, Guid.NewGuid(), null, null, null);
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyContactId_ShouldFail()
    {
        var cmd = new CreateDealCommand("Deal", 1000m, Guid.Empty, null, null, null);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.ContactId);
    }
}

public class MoveDealStageValidatorTests
{
    private readonly MoveDealStageValidator _validator = new();

    [Theory]
    [InlineData(DealStage.Lead)]
    [InlineData(DealStage.Qualified)]
    [InlineData(DealStage.Proposal)]
    [InlineData(DealStage.Negotiation)]
    [InlineData(DealStage.Won)]
    public void ValidStage_WithoutLostReason_ShouldPass(DealStage stage)
    {
        var cmd = new MoveDealStageCommand(Guid.NewGuid(), stage, null);
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void LostStage_WithReason_ShouldPass()
    {
        var cmd = new MoveDealStageCommand(Guid.NewGuid(), DealStage.Lost, "Went with competitor.");
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void LostStage_WithoutReason_ShouldFail()
    {
        var cmd = new MoveDealStageCommand(Guid.NewGuid(), DealStage.Lost, null);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.LostReason)
            .WithErrorMessage("Lost reason is required when marking a deal as lost.");
    }

    [Fact]
    public void LostStage_EmptyReason_ShouldFail()
    {
        var cmd = new MoveDealStageCommand(Guid.NewGuid(), DealStage.Lost, "");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.LostReason);
    }
}

public class CreateActivityValidatorTests
{
    private readonly CreateActivityValidator _validator = new();

    [Fact]
    public void Valid_Activity_LinkedToContact_ShouldPass()
    {
        var cmd = new CreateActivityCommand(
            ActivityType.Call, "Discovery Call", "Notes",
            DateTime.UtcNow.AddDays(1),
            null, Guid.NewGuid(), null, null);
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Valid_Activity_LinkedToDeal_ShouldPass()
    {
        var cmd = new CreateActivityCommand(
            ActivityType.Meeting, "Product Demo", null,
            DateTime.UtcNow.AddDays(2),
            null, null, Guid.NewGuid(), null);
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Activity_NotLinkedToContactOrDeal_ShouldFail()
    {
        var cmd = new CreateActivityCommand(
            ActivityType.Task, "Orphan Task", null,
            DateTime.UtcNow.AddDays(1),
            null, null, null, null);  // both null
        _validator.TestValidate(cmd).ShouldHaveAnyValidationError();
    }

    [Fact]
    public void EmptyTitle_ShouldFail()
    {
        var cmd = new CreateActivityCommand(
            ActivityType.Call, "", null, null,
            null, Guid.NewGuid(), null, null);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void TitleTooLong_ShouldFail()
    {
        var cmd = new CreateActivityCommand(
            ActivityType.Call, new string('x', 201), null, null,
            null, Guid.NewGuid(), null, null);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Title);
    }
}
