namespace Domain.Entities;

public class RoleClaim:BaseEntity
{
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public string ClaimType { get; set; } = null!;
    public string ClaimValue { get; set; } = null!;
}