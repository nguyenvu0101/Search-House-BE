namespace TD.KCN.WebApi.Application.Identity.Roles;

public interface IRoleService : ITransientService
{
    Task<PaginationResponse<RoleDetailsDto>> SearchAsync(RoleListFilter filter, CancellationToken cancellationToken);

    Task<List<RoleDto>> GetListAsync(CancellationToken cancellationToken);

    Task<int> GetCountAsync(CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string roleName, string? excludeId);

    Task<RoleDto> GetByIdAsync(string id);

    Task<RoleDto> GetByIdWithPermissionsAsync(string roleId, CancellationToken cancellationToken);

    Task<string> CreateOrUpdateAsync(CreateOrUpdateRoleRequest request, CancellationToken cancellationToken);

    Task<string> UpdatePermissionsAsync(UpdateRolePermissionsRequest request, CancellationToken cancellationToken);

    Task<string> DeleteAsync(string id);

    Task<RoleWithPermissionGroupsDto> GetByIdWithPermissionGroupsAsync(string roleId, CancellationToken cancellationToken);

}