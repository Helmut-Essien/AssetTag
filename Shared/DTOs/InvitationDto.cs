using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs
{
    public class CreateInvitationDTO
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        public string? Role { get; set; } = "User";
    }

    public class InvitationResponseDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? UsedAt { get; set; }
        public string? Role { get; set; }
        public string InvitedByUserName { get; set; } = string.Empty;
    }

    public class RegisterWithInvitationDTO
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Compare("Password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? FirstName { get; set; }
        public string? Surname { get; set; }
        public string? OtherNames { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Address { get; set; }
        public string? JobRole { get; set; }
        public string? DepartmentId { get; set; }
    }

    public class ValidateInvitationDTO
    {
        public string Token { get; set; } = string.Empty;
    }
}