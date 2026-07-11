using CRM.Domain.Entities;
using CRM.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CRM.Tests.Domain;

public class UserEntityTests
{
    [Fact]
    public void Create_ShouldNormaliseEmailToLowercase()
    {
        var user = User.Create("ALICE@ACME.COM", "hash", "Alice", "Smith", UserRole.Sales);
        user.Email.Should().Be("alice@acme.com");
    }

    [Fact]
    public void Create_ShouldTrimWhitespaceFromNames()
    {
        var user = User.Create("a@b.com", "hash", "  Alice  ", "  Smith  ", UserRole.Sales);
        user.FirstName.Should().Be("Alice");
        user.LastName.Should().Be("Smith");
    }

    [Fact]
    public void Create_ShouldDefaultToActive()
    {
        var user = User.Create("a@b.com", "hash", "Alice", "Smith", UserRole.Sales);
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void FullName_ShouldCombineFirstAndLastName()
    {
        var user = User.Create("a@b.com", "hash", "Alice", "Smith", UserRole.Sales);
        user.FullName.Should().Be("Alice Smith");
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalseAndClearTokens()
    {
        var user = User.Create("a@b.com", "hash", "Alice", "Smith", UserRole.Sales);
        user.SetRefreshToken("some-token", DateTime.UtcNow.AddDays(7));
        user.Deactivate();

        user.IsActive.Should().BeFalse();
        user.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiryTime.Should().BeNull();
    }

    [Fact]
    public void Activate_ShouldRestoreIsActiveTrue()
    {
        var user = User.Create("a@b.com", "hash", "Alice", "Smith", UserRole.Sales);
        user.Deactivate();
        user.Activate();
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ChangeRole_ShouldUpdateRole()
    {
        var user = User.Create("a@b.com", "hash", "Alice", "Smith", UserRole.Sales);
        user.ChangeRole(UserRole.Manager);
        user.Role.Should().Be(UserRole.Manager);
    }

    [Fact]
    public void RecordLogin_ShouldSetLastLoginAt()
    {
        var user = User.Create("a@b.com", "hash", "Alice", "Smith", UserRole.Sales);
        var before = DateTime.UtcNow.AddSeconds(-1);
        user.RecordLogin();
        user.LastLoginAt.Should().BeAfter(before);
    }

    [Fact]
    public void SetRefreshToken_ShouldPersistTokenAndExpiry()
    {
        var user = User.Create("a@b.com", "hash", "Alice", "Smith", UserRole.Sales);
        var expiry = DateTime.UtcNow.AddDays(7);
        user.SetRefreshToken("refresh-abc-123", expiry);

        user.RefreshToken.Should().Be("refresh-abc-123");
        user.RefreshTokenExpiryTime.Should().BeCloseTo(expiry, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateProfile_ShouldUpdateNamesAndSetUpdatedAt()
    {
        var user = User.Create("a@b.com", "hash", "Alice", "Smith", UserRole.Sales);
        user.UpdateProfile("Alicia", "Johnson");

        user.FirstName.Should().Be("Alicia");
        user.LastName.Should().Be("Johnson");
        user.UpdatedAt.Should().NotBeNull();
    }
}

public class ContactEntityTests
{
    [Fact]
    public void Create_ShouldNormaliseEmail()
    {
        var contact = Contact.Create("Alice", "Smith", "ALICE@ACME.COM", null, null, null, Guid.NewGuid());
        contact.Email.Should().Be("alice@acme.com");
    }

    [Fact]
    public void Tags_ShouldBeEmptyByDefault()
    {
        var contact = Contact.Create("Alice", "Smith", "a@b.com", null, null, null, Guid.NewGuid());
        contact.Tags.Should().BeEmpty();
    }

    [Fact]
    public void SetTags_ShouldStoreLowerCaseDeduplicatedTags()
    {
        var contact = Contact.Create("Alice", "Smith", "a@b.com", null, null, null, Guid.NewGuid());
        contact.SetTags(["Enterprise", "HOT-LEAD", "enterprise"]);

        contact.Tags.Should().HaveCount(2);
        contact.Tags.Should().Contain("enterprise");
        contact.Tags.Should().Contain("hot-lead");
    }

    [Fact]
    public void AddTag_ShouldAddWithoutDuplicates()
    {
        var contact = Contact.Create("Alice", "Smith", "a@b.com", null, null, null, Guid.NewGuid());
        contact.SetTags(["vip"]);
        contact.AddTag("enterprise");
        contact.AddTag("enterprise"); // duplicate

        contact.Tags.Should().HaveCount(2);
    }

    [Fact]
    public void RemoveTag_ShouldRemoveExistingTag()
    {
        var contact = Contact.Create("Alice", "Smith", "a@b.com", null, null, null, Guid.NewGuid());
        contact.SetTags(["vip", "enterprise"]);
        contact.RemoveTag("vip");

        contact.Tags.Should().ContainSingle().Which.Should().Be("enterprise");
    }

    [Fact]
    public void AssignTo_ShouldSetAssignedToId()
    {
        var contact = Contact.Create("Alice", "Smith", "a@b.com", null, null, null, Guid.NewGuid());
        var userId = Guid.NewGuid();
        contact.AssignTo(userId);

        contact.AssignedToId.Should().Be(userId);
        contact.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void AssignTo_Null_ShouldUnassignContact()
    {
        var contact = Contact.Create("Alice", "Smith", "a@b.com", null, null, null, Guid.NewGuid());
        contact.AssignTo(Guid.NewGuid());
        contact.AssignTo(null);
        contact.AssignedToId.Should().BeNull();
    }

    [Fact]
    public void FullName_ShouldCombineNames()
    {
        var contact = Contact.Create("Alice", "Johnson", "a@b.com", null, null, null, Guid.NewGuid());
        contact.FullName.Should().Be("Alice Johnson");
    }

    [Fact]
    public void UpdateNotes_ShouldSetNotes()
    {
        var contact = Contact.Create("Alice", "Smith", "a@b.com", null, null, null, Guid.NewGuid());
        contact.UpdateNotes("Interested in enterprise tier.");
        contact.Notes.Should().Be("Interested in enterprise tier.");
    }
}

public class DealEntityTests
{
    [Fact]
    public void Create_ShouldDefaultToLeadStageWithProbability10()
    {
        var deal = Deal.Create("Test Deal", 5000m, Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid());
        deal.Stage.Should().Be(DealStage.Lead);
        deal.Probability.Should().Be(10);
    }

    [Theory]
    [InlineData(DealStage.Lead, 10)]
    [InlineData(DealStage.Qualified, 30)]
    [InlineData(DealStage.Proposal, 60)]
    [InlineData(DealStage.Negotiation, 80)]
    [InlineData(DealStage.Won, 100)]
    [InlineData(DealStage.Lost, 0)]
    public void MoveToStage_ShouldSetCorrectProbability(DealStage stage, int expectedProbability)
    {
        var deal = Deal.Create("Deal", 1000m, Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid());
        deal.MoveToStage(stage, stage == DealStage.Lost ? "Budget cut" : null);
        deal.Probability.Should().Be(expectedProbability);
    }

    [Fact]
    public void MoveToStage_Won_ShouldSetClosedAtAndIsOpenFalse()
    {
        var deal = Deal.Create("Deal", 1000m, Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid());
        deal.MoveToStage(DealStage.Won);

        deal.ClosedAt.Should().NotBeNull();
        deal.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void MoveToStage_Lost_ShouldSetLostReasonAndClosedAt()
    {
        var deal = Deal.Create("Deal", 1000m, Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid());
        deal.MoveToStage(DealStage.Lost, "Went with competitor.");

        deal.LostReason.Should().Be("Went with competitor.");
        deal.ClosedAt.Should().NotBeNull();
        deal.IsOpen.Should().BeFalse();
    }

    [Theory]
    [InlineData(DealStage.Lead)]
    [InlineData(DealStage.Qualified)]
    [InlineData(DealStage.Proposal)]
    [InlineData(DealStage.Negotiation)]
    public void IsOpen_ShouldBeTrueForOpenStages(DealStage stage)
    {
        var deal = Deal.Create("Deal", 1000m, Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid());
        deal.MoveToStage(stage);
        deal.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void Reassign_ShouldChangeOwnerId()
    {
        var originalOwner = Guid.NewGuid();
        var newOwner = Guid.NewGuid();
        var deal = Deal.Create("Deal", 1000m, originalOwner, Guid.NewGuid(), null, null, Guid.NewGuid());
        deal.Reassign(newOwner);
        deal.OwnerId.Should().Be(newOwner);
    }

    [Fact]
    public void UpdateDetails_ShouldUpdateAllFields()
    {
        var deal = Deal.Create("Old Title", 1000m, Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid());
        var closeDate = DateTime.UtcNow.AddMonths(1);
        deal.UpdateDetails("New Title", 50000m, closeDate, "Updated description.");

        deal.Title.Should().Be("New Title");
        deal.Value.Should().Be(50000m);
        deal.ExpectedCloseDate.Should().BeCloseTo(closeDate, TimeSpan.FromSeconds(1));
        deal.Description.Should().Be("Updated description.");
        deal.UpdatedAt.Should().NotBeNull();
    }
}

public class ActivityEntityTests
{
    [Fact]
    public void Create_ShouldDefaultToPendingStatus()
    {
        var activity = Activity.Create(
            ActivityType.Call, "Test Call", null, null,
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid());

        activity.Status.Should().Be(ActivityStatus.Pending);
    }

    [Fact]
    public void Complete_ShouldSetStatusAndCompletedAt()
    {
        var activity = Activity.Create(
            ActivityType.Call, "Test Call", null, null,
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid());

        activity.Complete("Deal closed.");

        activity.Status.Should().Be(ActivityStatus.Completed);
        activity.CompletedAt.Should().NotBeNull();
        activity.Outcome.Should().Be("Deal closed.");
    }

    [Fact]
    public void Cancel_ShouldSetStatusToCancelled()
    {
        var activity = Activity.Create(
            ActivityType.Task, "Test Task", null, null,
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid());

        activity.Cancel();
        activity.Status.Should().Be(ActivityStatus.Cancelled);
    }

    [Fact]
    public void IsOverdue_ShouldBeTrueWhenPendingAndPastDueDate()
    {
        var activity = Activity.Create(
            ActivityType.Task, "Overdue Task", null,
            DateTime.UtcNow.AddDays(-1), // past due
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid());

        activity.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void IsOverdue_ShouldBeFalseWhenCompleted()
    {
        var activity = Activity.Create(
            ActivityType.Task, "Done Task", null,
            DateTime.UtcNow.AddDays(-1),
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid());
        activity.Complete();

        activity.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_ShouldBeFalseWhenFutureDueDate()
    {
        var activity = Activity.Create(
            ActivityType.Task, "Future Task", null,
            DateTime.UtcNow.AddDays(3),
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid());

        activity.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void SetReminder_ShouldSetHasReminderAndReminderAt()
    {
        var activity = Activity.Create(
            ActivityType.Meeting, "Team Meeting", null, null,
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid());

        var reminderTime = DateTime.UtcNow.AddHours(2);
        activity.SetReminder(reminderTime);

        activity.HasReminder.Should().BeTrue();
        activity.ReminderAt.Should().BeCloseTo(reminderTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ClearReminder_ShouldRemoveReminder()
    {
        var activity = Activity.Create(
            ActivityType.Meeting, "Team Meeting", null, null,
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid());

        activity.SetReminder(DateTime.UtcNow.AddHours(2));
        activity.ClearReminder();

        activity.HasReminder.Should().BeFalse();
        activity.ReminderAt.Should().BeNull();
    }
}
