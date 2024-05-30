using System.Reflection;
using System.Security.Claims;
using Domain.DTOs.RolePermissionDTOs;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Helpers;

public static class ClaimsHelper
{
    public static void GetPermissions(this List<RoleClaimsDto> allPermissions, Type policy)
    {
        var nestedTypes = policy.GetNestedTypes(BindingFlags.Public);
        if (nestedTypes.Length > 0)
        {
            foreach (var nested in nestedTypes)
            {
                FieldInfo[] fields = nested.GetFields(BindingFlags.Static | BindingFlags.Public);

                foreach (FieldInfo fi in fields)
                {
                    allPermissions.Add(new RoleClaimsDto("Permissions", fi.GetValue(null).ToString()));
                }
            }
        }
        else
        {
            FieldInfo[] fields = policy.GetFields(BindingFlags.Static | BindingFlags.Public);

            foreach (FieldInfo fi in fields)
            {
                allPermissions.Add(new RoleClaimsDto(fi.GetValue(null).ToString(), "Permissions"));
            }
        }
    }

    public static async Task AddPermissionClaim(this DataContext context, Role role, string permission)
    {
        var allClaims = await context.RoleClaims.Where(x=>x.RoleId==role.Id).ToListAsync();
        if (!allClaims.Any(a => a.ClaimType == "Permission" && a.ClaimValue == permission))
        {
            await context.RoleClaims.AddAsync(new RoleClaim()
            {
                ClaimType = "Permission",
                Role = role,
                RoleId = role.Id,
                ClaimValue = permission,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
          await  context.SaveChangesAsync();
        }
    }

 
}