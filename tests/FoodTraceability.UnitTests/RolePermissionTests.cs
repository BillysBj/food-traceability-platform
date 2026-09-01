using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.UnitTests;

public sealed class RolePermissionTests
{
    private static readonly Guid RoleId = Guid.Parse("a66dc50f-cd91-4e36-8cfc-068466b44d9a");
    private static readonly Guid PermissionId = Guid.Parse("169a6f74-985a-4502-b279-59285251f476");

    [Fact]
    public void ValidRolePermissionIsCreated()
    {
        var rolePermission = RolePermission.Create(RoleId, PermissionId);

        Assert.Equal(RoleId, rolePermission.RoleId);
        Assert.Equal(PermissionId, rolePermission.PermissionId);
    }

    [Fact]
    public void EmptyRoleIdIsRejected()
    {
        Assert.Throws<IdentityDomainException>(() => RolePermission.Create(
            Guid.Empty,
            PermissionId));
    }

    [Fact]
    public void EmptyPermissionIdIsRejected()
    {
        Assert.Throws<IdentityDomainException>(() => RolePermission.Create(
            RoleId,
            Guid.Empty));
    }
}
