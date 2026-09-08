using FoodTraceability.Modules.Identity.Application.Memberships;
using FoodTraceability.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.Modules.Identity.Infrastructure.Memberships;

internal sealed class MembershipWriter(IdentityDbContext dbContext) : IMembershipWriter
{
    private const string MembershipPrimaryKey = "pk_organization_membership";
    private const string MembershipUserForeignKey =
        "fk_organization_membership_user_user_id";
    private const string MembershipOrganizationForeignKey =
        "fk_org_membership_org_organization";
    private const string RoleAssignmentUniqueIndex =
        "ix_organization_role_assignment_user_id_organization_id_role_i";
    private const string RoleAssignmentMembershipForeignKey =
        "fk_organization_role_assignment_organization_membership_user_i";
    private const string RoleAssignmentRoleScopeForeignKey =
        "fk_organization_role_assignment_roles_role_id_assignment_scope";

    public async Task AddMemberAsync(
        OrganizationMembership membership,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(membership);

        dbContext.OrganizationMemberships.Add(membership);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsConstraintViolation(
                exception,
                PostgresErrorCodes.UniqueViolation,
                MembershipPrimaryKey))
        {
            throw new MembershipConflictException(
                "The user is already a member of this organization.");
        }
        catch (DbUpdateException exception)
            when (IsConstraintViolation(
                exception,
                PostgresErrorCodes.ForeignKeyViolation,
                MembershipUserForeignKey))
        {
            throw new MembershipValidationException(
                "The referenced user does not exist.");
        }
        catch (DbUpdateException exception)
            when (IsConstraintViolation(
                exception,
                PostgresErrorCodes.ForeignKeyViolation,
                MembershipOrganizationForeignKey))
        {
            throw new MembershipValidationException(
                "The referenced organization does not exist.");
        }
    }

    public async Task AssignRoleAsync(
        OrganizationRoleAssignment assignment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        dbContext.OrganizationRoleAssignments.Add(assignment);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsConstraintViolation(
                exception,
                PostgresErrorCodes.UniqueViolation,
                RoleAssignmentUniqueIndex))
        {
            throw new MembershipConflictException(
                "The role is already assigned in this organization.");
        }
        catch (DbUpdateException exception)
            when (IsConstraintViolation(
                exception,
                PostgresErrorCodes.ForeignKeyViolation,
                RoleAssignmentMembershipForeignKey))
        {
            throw new MembershipNotFoundException(
                "The user is not a member of this organization.");
        }
        catch (DbUpdateException exception)
            when (IsConstraintViolation(
                exception,
                PostgresErrorCodes.ForeignKeyViolation,
                RoleAssignmentRoleScopeForeignKey))
        {
            throw new MembershipValidationException(
                "The referenced role does not exist or is not assignable within an organization.");
        }
    }

    private static bool IsConstraintViolation(
        DbUpdateException exception,
        string sqlState,
        string constraintName)
    {
        return exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == sqlState
            && postgresException.ConstraintName == constraintName;
    }
}
