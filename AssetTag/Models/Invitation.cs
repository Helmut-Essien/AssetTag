using AssetTag.Models;
using NUlid;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Invitation
{
    [Key]
    public string Id { get; set; } = Ulid.NewUlid().ToString();

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = Guid.NewGuid().ToString();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7); // 7 days expiry
    public bool IsUsed { get; set; } = false;
    public DateTime? UsedAt { get; set; }

    // Foreign key to the user who sent the invitation
    public string InvitedByUserId { get; set; } = string.Empty;
    [ForeignKey("InvitedByUserId")]
    public ApplicationUser? InvitedByUser { get; set; }

    // Role to assign to the user upon registration
    public string? Role { get; set; } = "User";
}