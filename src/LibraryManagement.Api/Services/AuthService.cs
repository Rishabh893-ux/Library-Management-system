using System.Security.Claims;
using System.Text;
using LibraryManagement.Api.Common;
using LibraryManagement.Api.Data;
using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Entities;
using LibraryManagement.Api.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LibraryManagement.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public class AuthService(
    LibraryDbContext db,
    IPasswordHasher<Member> hasher,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock) : IAuthService
{
    /// <summary>Claim that carries the member's role. Program.cs tells JWT validation to read roles from it.</summary>
    public const string RoleClaimType = "role";

    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        if (await db.Members.AnyAsync(m => m.Email == email, ct))
            throw new BusinessRuleException("An account with this email already exists.");

        // Self-registration always creates a Member. Librarian accounts come from seeding.
        var member = new Member
        {
            Name = request.Name.Trim(),
            Email = email,
            Role = MemberRole.Member,
            JoinDate = clock.GetUtcNow().UtcDateTime
        };
        member.PasswordHash = hasher.HashPassword(member, request.Password);

        db.Members.Add(member);
        await db.SaveChangesAsync(ct);

        return BuildResponse(member);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        var member = await db.Members.SingleOrDefaultAsync(m => m.Email == email, ct);

        // Same error for unknown email and wrong password, so the endpoint can't be used to discover accounts.
        if (member is null)
            throw new UnauthorizedException("Invalid email or password.");

        var result = hasher.VerifyHashedPassword(member, member.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedException("Invalid email or password.");

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            member.PasswordHash = hasher.HashPassword(member, request.Password);
            await db.SaveChangesAsync(ct);
        }

        return BuildResponse(member);
    }

    private AuthResponse BuildResponse(Member member)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_jwt.ExpiryMinutes);

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, member.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, member.Email),
                new Claim(JwtRegisteredClaimNames.Name, member.Name),
                new Claim(RoleClaimType, member.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
                SecurityAlgorithms.HmacSha256)
        });

        return new AuthResponse(token, expires, member.ToDto());
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
