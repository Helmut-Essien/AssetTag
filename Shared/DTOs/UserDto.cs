using System;
using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs;

public record UserReadDTO
{
    public string Id { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public string? OtherNames { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string? Address { get; init; }
    public string? JobRole { get; init; }
    public string? ProfileImage { get; init; }
    public bool IsActive { get; init; }
    public DateTime DateCreated { get; init; }
    public string? DepartmentId { get; init; }
}

public record UserUpdateDTO
{
    [Required]
    public string Id { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? Surname { get; init; }
    public string? OtherNames { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string? Address { get; init; }
    public string? JobRole { get; init; }
    public string? ProfileImage { get; init; }
    public bool? IsActive { get; init; }
    public string? DepartmentId { get; init; }
}

// Additional DTOs
public record UserActivationDTO
{
    [Required]
    public bool IsActive { get; init; }
}

public record UserRolesDTO
{
    public string UserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public List<string> Roles { get; init; } = new();
}

public record AddUserToRoleDTO
{
    [Required]
    public string RoleName { get; init; } = string.Empty;
}

public record RemoveUserFromRoleDTO
{
    [Required]
    public string RoleName { get; init; } = string.Empty;
}

public record ResetPasswordDTO
{
    [Required]
    [MinLength(6)]
    public string NewPassword { get; init; } = string.Empty;
}