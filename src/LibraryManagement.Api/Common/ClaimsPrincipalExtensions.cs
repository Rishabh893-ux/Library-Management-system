using System.Security.Claims;
using LibraryManagement.Api.Entities;
using Microsoft.IdentityModel.JsonWebTokens;

namespace LibraryManagement.Api.Common;

public static class ClaimsPrincipalExtensions
{
    public static int GetMemberId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedException("Token does not contain a valid member id.");
    }

    public static bool IsLibrarian(this ClaimsPrincipal user) => user.IsInRole(nameof(MemberRole.Librarian));

    /// <summary>
    /// Works out whose data a read request is about. Members can only see their own.
    /// Librarians can pass another member's id.
    /// </summary>
    public static int ResolveTargetMemberId(this ClaimsPrincipal user, int? requestedMemberId)
    {
        var self = user.GetMemberId();
        if (requestedMemberId is null || requestedMemberId == self) return self;
        if (!user.IsLibrarian())
            throw new ForbiddenException("Members can only act on their own account.");
        return requestedMemberId.Value;
    }
}
