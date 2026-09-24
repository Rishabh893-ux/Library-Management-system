using LibraryManagement.Api.Entities;

namespace LibraryManagement.Api.Common;

/// <summary>Role names used in [Authorize(Roles = ...)]. They match the <see cref="MemberRole"/> enum names.</summary>
public static class Roles
{
    public const string Librarian = nameof(MemberRole.Librarian);
    public const string Member = nameof(MemberRole.Member);
}
