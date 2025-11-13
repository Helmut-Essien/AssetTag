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

    public class CreateMultipleInvitationsDTO
    {
        [Required(ErrorMessage = "At least one email is required")]
        public List<string> Emails { get; set; } = new List<string>();

        public string? Role { get; set; } = "User";
    }

    public class BulkInvitationResponseDTO
    {
        public List<InvitationResponseDTO> SuccessfulInvitations { get; set; } = new List<InvitationResponseDTO>();
        public List<FailedInvitationDTO> FailedInvitations { get; set; } = new List<FailedInvitationDTO>();
        public int TotalProcessed { get; set; }
        public int SuccessfulCount { get; set; }
        public int FailedCount { get; set; }
    }

    public class FailedInvitationDTO
    {
        public string Email { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    public class InvitationValidationResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public InvitationResponseDTO? Data { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }
}