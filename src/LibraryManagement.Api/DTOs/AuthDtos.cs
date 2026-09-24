using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Api.DTOs;

public class RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    /// <summary>At least 8 characters with an uppercase letter, a lowercase letter and a digit.</summary>
    [Required, StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "Password must contain an uppercase letter, a lowercase letter and a digit.")]
    public string Password { get; init; } = string.Empty;
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public record AuthResponse(string Token, DateTime ExpiresAtUtc, MemberDto Member);

public record MemberDto(int Id, string Name, string Email, string Role, DateTime JoinDate);
