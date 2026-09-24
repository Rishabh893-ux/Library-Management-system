using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Api.DTOs;

public class CreateBookRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; init; } = string.Empty;

    [Required, StringLength(150, MinimumLength = 1)]
    public string Author { get; init; } = string.Empty;

    /// <summary>ISBN-10 or ISBN-13. Hyphens and spaces are allowed and are stripped before saving.</summary>
    [Required, RegularExpression(@"^(?:[0-9][- ]?){9}[0-9Xx]$|^(?:[0-9][- ]?){12}[0-9]$",
        ErrorMessage = "ISBN must be a valid ISBN-10 or ISBN-13.")]
    public string Isbn { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string Category { get; init; } = string.Empty;

    [Range(1, 10_000)]
    public int TotalCopies { get; init; }
}

/// <summary>
/// Full replacement of a book's details. Changing TotalCopies adjusts AvailableCopies by the same amount.
/// TotalCopies cannot drop below the number of copies currently on loan.
/// </summary>
public class UpdateBookRequest : CreateBookRequest;

public class BookSearchQuery
{
    [StringLength(200)] public string? Title { get; init; }
    [StringLength(150)] public string? Author { get; init; }
    [StringLength(100)] public string? Category { get; init; }

    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}

public record BookDto(int Id, string Title, string Author, string Isbn, string Category, int TotalCopies, int AvailableCopies);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
