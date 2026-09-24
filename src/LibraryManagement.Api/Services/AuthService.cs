using LibraryManagement.Api.Common;
using LibraryManagement.Api.Data;
using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public class AuthService(
    LibraryDbContext db,
    IPasswordHasher<Member> hasher,
    ITokenService tokens,
    TimeProvider clock) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        if (await db.Members.AnyAsync(m => m.Email == email, ct))
            throw new BusinessRuleException("An account with this email already exists.");

        // Self-registration always creates a Member. Librarian accounts come from seeding or an existing librarian.
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
        var (token, expires) = tokens.CreateToken(member);
        return new AuthResponse(token, expires, member.ToDto());
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
