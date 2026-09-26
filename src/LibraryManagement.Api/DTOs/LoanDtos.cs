using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Api.DTOs;

public class BorrowRequest
{
    [Range(1, int.MaxValue)]
    public int BookId { get; init; }
}

public record LoanDto(
    int Id,
    int BookId,
    string BookTitle,
    int MemberId,
    string MemberName,
    DateTime BorrowDate,
    DateTime DueDate,
    DateTime? ReturnDate,
    decimal FineAmount,
    bool IsOverdue);
