using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Api.DTOs;

public class CreateReservationRequest
{
    [Range(1, int.MaxValue)]
    public int BookId { get; init; }

    /// <summary>Librarians only: reserve on behalf of this member. Members always reserve for themselves.</summary>
    [Range(1, int.MaxValue)]
    public int? MemberId { get; init; }
}

/// <param name="QueuePosition">1-based position among Pending reservations. Null for other statuses.</param>
public record ReservationDto(
    int Id,
    int BookId,
    string BookTitle,
    int MemberId,
    string MemberName,
    DateTime ReservationDate,
    string Status,
    DateTime? NotifiedDate,
    int? QueuePosition);
