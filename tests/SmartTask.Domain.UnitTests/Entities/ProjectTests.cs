using Shouldly;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Exceptions;

namespace SmartTask.Domain.UnitTests.Entities;

public sealed class ProjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Start = new(2026, 11, 1);
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    public void Create_EndDateBeforeStartDate_ThrowsDomainException()
    {
        Should.Throw<DomainException>(() => Project.Create("Website", null, Start, Start.AddDays(-1), OwnerId));
    }

    [Fact]
    public void Create_EndDateEqualsStartDate_Succeeds()
    {
        var project = Project.Create("Website", null, Start, Start, OwnerId);

        project.EndDate.ShouldBe(Start);
    }

    [Fact]
    public void Create_ValidInput_TrimsTextAndSetsOwner()
    {
        var project = Project.Create("  Website  ", "  Relaunch  ", Start, null, OwnerId);

        project.Name.ShouldBe("Website");
        project.Description.ShouldBe("Relaunch");
        project.OwnerId.ShouldBe(OwnerId);
        project.IsOwnedBy(OwnerId).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void Create_InvalidName_ThrowsDomainException(string name)
    {
        Should.Throw<DomainException>(() => Project.Create(name, null, Start, null, OwnerId));
    }

    [Fact]
    public void Create_EmptyOwner_ThrowsDomainException()
    {
        Should.Throw<DomainException>(() => Project.Create("Website", null, Start, null, Guid.Empty));
    }

    [Fact]
    public void Update_EndDateBeforeStartDate_ThrowsDomainException()
    {
        var project = Project.Create("Website", null, Start, null, OwnerId);

        Should.Throw<DomainException>(() => project.Update("Website", null, Start, Start.AddDays(-1)));
    }

    [Fact]
    public void RemoveMember_Owner_ThrowsDomainException()
    {
        var project = Project.Create("Website", null, Start, null, OwnerId);

        Should.Throw<DomainException>(() => project.RemoveMember(OwnerId));
    }

    [Fact]
    public void AddMember_SameUserTwice_AddsOnce()
    {
        var project = Project.Create("Website", null, Start, null, OwnerId);
        var userId = Guid.NewGuid();

        project.AddMember(userId, Now);
        project.AddMember(userId, Now);

        project.Members.Count.ShouldBe(1);
        project.HasMember(userId).ShouldBeTrue();
    }

    [Fact]
    public void AddMember_Owner_IsNoOpAndOwnerCountsAsMember()
    {
        var project = Project.Create("Website", null, Start, null, OwnerId);

        project.AddMember(OwnerId, Now);

        project.Members.ShouldBeEmpty();
        project.HasMember(OwnerId).ShouldBeTrue();
    }

    [Fact]
    public void RemoveMember_ExistingMember_RemovesIt()
    {
        var project = Project.Create("Website", null, Start, null, OwnerId);
        var userId = Guid.NewGuid();
        project.AddMember(userId, Now);

        project.RemoveMember(userId);

        project.HasMember(userId).ShouldBeFalse();
    }
}
