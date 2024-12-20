﻿using TD.KCN.WebApi.Application.Common.Exceptions;
using TD.KCN.WebApi.Application.Identity.Users;
using TD.KCN.WebApi.Domain.Identity;
using TD.KCN.WebApi.Shared.Authorization;
using TD.KCN.WebApi.Shared.Multitenancy;
using Microsoft.EntityFrameworkCore;
using DocumentFormat.OpenXml.VariantTypes;
using TD.KCN.WebApi.Application.Identity.Roles;
using Microsoft.AspNetCore.Identity;

namespace TD.KCN.WebApi.Infrastructure.Identity;

internal partial class UserService
{
    public async Task<List<UserRoleDto>> GetRolesAsync(string userId, CancellationToken cancellationToken)
    {
        var userRoles = new List<UserRoleDto>();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) throw new NotFoundException("User Not Found.");
        var roles = await _roleManager.Roles.AsNoTracking().ToListAsync(cancellationToken);
        if (roles is null) throw new NotFoundException("Roles Not Found.");
        foreach (var role in roles)
        {
            userRoles.Add(new UserRoleDto
            {
                RoleId = role.Id,
                RoleName = role.Name,
                Description = role.Description,
                Enabled = await _userManager.IsInRoleAsync(user, role.Name!)
            });
        }

        return userRoles;
    }

    public async Task<string> AssignRolesAsync(string userId, UserRolesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var user = await _userManager.Users.Where(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new NotFoundException(_t["User Not Found."]);

        // Check if the user is an admin for which the admin role is getting disabled
        if (await _userManager.IsInRoleAsync(user, TDRoles.Admin)
            && request.UserRoles.Any(a => !a.Enabled && a.RoleName == TDRoles.Admin))
        {
            // Get count of users in Admin Role
            int adminCount = (await _userManager.GetUsersInRoleAsync(TDRoles.Admin)).Count;

            // Check if user is not Root Tenant Admin
            // Edge Case : there are chances for other tenants to have users with the same email as that of Root Tenant Admin. Probably can add a check while User Registration
            if (user.Email == MultitenancyConstants.Root.EmailAddress)
            {
                if (_currentTenant.Id == MultitenancyConstants.Root.Id)
                {
                    throw new ConflictException(_t["Cannot Remove Admin Role From Root Tenant Admin."]);
                }
            }
            else if (adminCount <= 2)
            {
                throw new ConflictException(_t["Tenant should have at least 2 Admins."]);
            }
        }

        foreach (var userRole in request.UserRoles)
        {
            // Check if Role Exists
            if (await _roleManager.FindByNameAsync(userRole.RoleName!) is not null)
            {
                if (userRole.Enabled)
                {
                    if (!await _userManager.IsInRoleAsync(user, userRole.RoleName!))
                    {
                        await _userManager.AddToRoleAsync(user, userRole.RoleName!);
                    }
                }
                else
                {
                    await _userManager.RemoveFromRoleAsync(user, userRole.RoleName!);
                }
            }
        }

        await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id, true));

        return _t["User Roles Updated Successfully."];
    }

    public async Task<UserRoleDetailDto> GetRolesByUserNameAsync(string username, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByNameAsync(username);
        _ = user ?? throw new NotFoundException(_t["User Not Found."]);

        var userRole = await _db.UserRoles.SingleOrDefaultAsync(x => x.UserId == user.Id);
        _ = userRole ?? throw new NotFoundException(_t["UserRole Not Found."]);
        return new UserRoleDetailDto()
        {
            Username = username,
            RoleId = userRole.RoleId,
        };
    }

    public async Task<UserPermissionDto> GetPermissionsByUserNameAsync(string username, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByNameAsync(username);
        _ = user ?? throw new NotFoundException(_t["User Not Found."]);

#pragma warning disable CS8601
        var userPermissionDto = new UserPermissionDto()
        {
            UserName = user.UserName,
            Permissions =
            [
                .. GetPermissionsAsync(user.Id, cancellationToken).Result,
                .. _db.UserClaims.Where(x => x.UserId == user.Id).Select(x => x.ClaimValue),
            ]
        };
#pragma warning restore CS8601
        return userPermissionDto;
    }

    public async Task<string> CreateOrUpdateUserRoleAsync(CreateOrUpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await _db.UserRoles.SingleOrDefaultAsync(x => x.UserId == request.UserId);
        if (user == null)
        {
            _db.UserRoles.Add(new IdentityUserRole<string>
            {
                UserId = request.UserId,
                RoleId = request.RoleId
            });
            await _db.SaveChangesAsync();
            return _t["Permissions Create."];
        }
        else
        {
            _db.UserRoles.Remove(user);
            await _db.SaveChangesAsync(cancellationToken);

            _db.UserRoles.Add(new IdentityUserRole<string>
            {
                UserId = request.UserId,
                RoleId = request.RoleId
            });
            await _db.SaveChangesAsync(cancellationToken);
            return _t["Permissions Update."];
        }
    }
}