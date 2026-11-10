using NexMed360.Application.Access;
using NexMed360.Domain.Rules;
using NexMed360Api.Common.Auth;
using NexMed360Api.Dtos.OrganizationMember;
using NexMed360Api.Dtos.PractitionerRole;
using NexMed360Api.Dtos.User;
using NexMed360Api.Services;

namespace NexMed360.API.Old.Tests;

public class OrganizationRulePlacementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaffCreationRequiresPermissionBeforeUsingAnyRepository(bool practitioner)
    {
        var organizationId = Guid.NewGuid();
        var access = new WorkspaceAccess();
        access.Select(new() { Mode = WorkspaceModes.Work, OrganizationId = organizationId });
        var service = new OrganizationService(null!, null!, null!, null!, null!, access);

        // Any dependency call before the permission check would fail this test.
        var result = practitioner
            ? await service.AddPractitionerRole(new CreatePractitionerRoleDto { OrganizationId = organizationId.ToString() })
            : await service.AddOrganizationMember(new CreateOrganizationMemberDto { OrganizationId = organizationId.ToString() });

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.Error!.HttpStatus);
        Assert.Equal("You need permission to manage staff.", result.Error.Description);
    }

    [Fact]
    public async Task AssigningAnAdministratorRequiresPlatformModeBeforeUsingAnyRepository()
    {
        var access = new WorkspaceAccess();
        access.Select(new() { Mode = WorkspaceModes.Work, Permissions = [Permissions.MembersManage] });
        var service = new OrganizationService(null!, null!, null!, null!, null!, access);

        var result = await service.AssignAdmin(new CreateOrganizationManagerDto());

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.Error!.HttpStatus);
    }

    [Fact]
    public void OrganizationOwnershipRejectsAnEmptyOrganizationInDomain()
    {
        Assert.Throws<InvalidOperationException>(() => OrganizationRules.ValidateOwnership(Guid.Empty));
    }

    [Fact]
    public void OrganizationOwnershipAcceptsNewAndUnchangedRecords()
    {
        var organizationId = Guid.NewGuid();
        OrganizationRules.ValidateOwnership(organizationId);
        OrganizationRules.ValidateOwnership(organizationId, organizationId);
    }

    [Fact]
    public void OrganizationOwnershipRejectsMovingASavedRecordInDomain()
    {
        Assert.Throws<InvalidOperationException>(() => OrganizationRules.ValidateOwnership(Guid.NewGuid(), Guid.NewGuid()));
    }
}
