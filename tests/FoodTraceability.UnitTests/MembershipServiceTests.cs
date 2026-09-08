using FoodTraceability.Modules.Identity.Application.Memberships;
using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.UnitTests;

public sealed class AddMemberServiceTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 9, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidCommandCreatesMembershipWithSuppliedIdentifiers()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var writer = new CapturingMembershipWriter();
        var service = new AddMemberService(writer, new FixedTimeProvider(CreatedAt));

        var result = await service.AddAsync(
            new AddMemberCommand(organizationId, userId),
            CancellationToken.None);

        var membership = Assert.IsType<OrganizationMembership>(writer.Membership);
        Assert.Equal(organizationId, membership.OrganizationId);
        Assert.Equal(userId, membership.UserId);
        Assert.Equal(CreatedAt, membership.CreatedAt);
        Assert.Equal(organizationId, result.OrganizationId);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(CreatedAt, result.CreatedAt);
        Assert.Empty(result.Roles);
    }

    private sealed class CapturingMembershipWriter : IMembershipWriter
    {
        public OrganizationMembership? Membership { get; private set; }

        public Task AddMemberAsync(
            OrganizationMembership membership,
            CancellationToken cancellationToken)
        {
            Membership = membership;
            return Task.CompletedTask;
        }

        public Task AssignRoleAsync(
            OrganizationRoleAssignment assignment,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

public sealed class AssignRoleServiceTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 9, 9, 15, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidCommandCreatesOrganizationWideRoleAssignment()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var writer = new CapturingMembershipWriter();
        var service = new AssignRoleService(writer, new FixedTimeProvider(CreatedAt));

        await service.AssignAsync(
            new AssignRoleCommand(organizationId, userId, roleId),
            CancellationToken.None);

        var assignment = Assert.IsType<OrganizationRoleAssignment>(writer.Assignment);
        Assert.Equal(organizationId, assignment.OrganizationId);
        Assert.Equal(userId, assignment.UserId);
        Assert.Equal(roleId, assignment.RoleId);
        Assert.Null(assignment.LocationId);
        Assert.Equal(CreatedAt, assignment.CreatedAt);
    }

    [Fact]
    public async Task ValidCommandCreatesNewNonEmptyAssignmentIdentifier()
    {
        var writer = new CapturingMembershipWriter();
        var service = new AssignRoleService(writer, new FixedTimeProvider(CreatedAt));

        await service.AssignAsync(
            new AssignRoleCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        var assignment = Assert.IsType<OrganizationRoleAssignment>(writer.Assignment);
        Assert.NotEqual(Guid.Empty, assignment.Id);
    }

    private sealed class CapturingMembershipWriter : IMembershipWriter
    {
        public OrganizationRoleAssignment? Assignment { get; private set; }

        public Task AddMemberAsync(
            OrganizationMembership membership,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AssignRoleAsync(
            OrganizationRoleAssignment assignment,
            CancellationToken cancellationToken)
        {
            Assignment = assignment;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

public sealed class MembershipQueryServiceTests
{
    [Fact]
    public async Task EmptyOrganizationOrUserIdentifierReturnsNullWithoutCallingReader()
    {
        var reader = new StubMembershipReader();
        var service = new MembershipQueryService(reader);

        var emptyOrganizationResult = await service.FindAsync(
            Guid.Empty,
            Guid.NewGuid(),
            CancellationToken.None);
        var emptyUserResult = await service.FindAsync(
            Guid.NewGuid(),
            Guid.Empty,
            CancellationToken.None);

        Assert.Null(emptyOrganizationResult);
        Assert.Null(emptyUserResult);
        Assert.Equal(0, reader.CallCount);
    }

    private sealed class StubMembershipReader : IMembershipReader
    {
        public int CallCount { get; private set; }

        public Task<MembershipDetails?> FindAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<MembershipDetails?>(null);
        }
    }
}
