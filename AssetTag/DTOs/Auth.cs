namespace AssetTag.DTOs
{
    public record RegisterDTO (string Username, string Email, string Password, string FirstName, string Surname);
    public record LoginDTO (string Email, string Password);
    public record TokenRequestDTO(string AccessToken, string RefreshToken);
    public record TokenResponseDTO(string AccessToken, string RefreshToken);
    public record AssignRoleDTO(string Email, string RoleName);
    public record CreateRoleDTO(string RoleName);
}
