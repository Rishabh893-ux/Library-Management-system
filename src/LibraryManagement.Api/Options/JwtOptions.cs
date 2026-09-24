using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Api.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; set; } = string.Empty;
    [Required] public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 signing key. Must be at least 32 characters (256 bits).</summary>
    [Required, MinLength(32)] public string Key { get; set; } = string.Empty;

    [Range(1, 1440)] public int ExpiryMinutes { get; set; } = 60;
}
