using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs
{
    public record RegisterDTO (string Username, string Email, string Password, string FirstName, string Surname);
    public record LoginDTO (string Email, string Password);
    public record TokenRequestDTO(string AccessToken, string RefreshToken);
    public record TokenResponseDTO(string AccessToken, string RefreshToken);
    public record AssignRoleDTO(string Email, string RoleName);
    public record CreateRoleDTO(string RoleName);
    public class ForgotPasswordDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare("NewPassword")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
