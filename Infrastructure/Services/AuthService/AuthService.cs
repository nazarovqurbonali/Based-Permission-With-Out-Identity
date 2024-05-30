using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Domain.Constants;
using Domain.DTOs.AuthDTOs;
using Domain.Entities;
using Domain.Responses;
using Infrastructure.Data;
using Infrastructure.Services.HashService;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services.AuthService;

public class AuthService(IConfiguration configuration, DataContext context, IHashService hashService) : IAuthService
{
    public async Task<Response<string>> Register(RegisterDto model)
    {
        try
        {
            var result = await context.Users.FirstOrDefaultAsync(x => x.UserName == model.UserName);
            if (result != null) return new Response<string>(HttpStatusCode.BadRequest, "Such a user already exists!");
            var user = new User()
            {
                UserName = model.UserName,
                Email = model.Email,
                PhoneNumber = string.Empty,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                PasswordHash = hashService.ConvertToHash(model.Password)
            };

            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();

            var existingUser = await context.Users.FirstOrDefaultAsync(x => x.UserName == model.UserName);
            var existingRole = await context.Roles.FirstOrDefaultAsync(x => x.Name == Roles.User);
            if (existingUser is not null && existingRole is not null)
            {
                var userRole = new UserRole()
                {
                    RoleId = existingRole.Id,
                    UserId = existingUser.Id,
                    Role = existingRole,
                    User = existingUser,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                await context.UserRoles.AddAsync(userRole);
                await context.SaveChangesAsync();
            }


            return new Response<string>($"Done.  Your registered by id {user.Id}");
        }
        catch (Exception e)
        {
            return new Response<string>(HttpStatusCode.InternalServerError, e.Message);
        }
    }

    public async Task<Response<string>> Login(LoginDto model)
    {
        try
        {
            var user = await context.Users.FirstOrDefaultAsync(x=>x.UserName==model.UserName && x.PasswordHash==hashService.ConvertToHash(model.Password));
            if (user is not null)
            {
                    return new Response<string>(await GenerateJwtToken(user));
            }

            return new Response<string>(HttpStatusCode.BadRequest, "Your username or password is incorrect!!!");
        }
        catch (Exception e)
        {
            return new Response<string>(HttpStatusCode.InternalServerError, e.Message);
        }
    }

    private async Task<string> GenerateJwtToken(User user)
    {
        var key = Encoding.UTF8.GetBytes(configuration["JWT:Key"]!);
        var securityKey = new SymmetricSecurityKey(key);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>()
        {
            new(JwtRegisteredClaimNames.Sid, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, user.UserName!),
            new(JwtRegisteredClaimNames.Email, user.Email!),
        };
        //add roles

        var roles = await context.UserRoles.Where(x => x.UserId == user.Id).Include(x => x.Role)
            .Select(x => x.Role).ToListAsync();
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role!.Name));
            var roleClaims = await context.RoleClaims.Where(x => x.RoleId == role.Id).ToListAsync();
            foreach (var roleClaim in roleClaims)
            {
                claims.Add(new Claim("Permissions", roleClaim.ClaimValue));
            }
        }

        var token = new JwtSecurityToken(
            issuer: configuration["JWT:Issuer"],
            audience: configuration["JWT:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        var securityTokenHandler = new JwtSecurityTokenHandler();
        var tokenString = securityTokenHandler.WriteToken(token);
        return tokenString;
    }
}