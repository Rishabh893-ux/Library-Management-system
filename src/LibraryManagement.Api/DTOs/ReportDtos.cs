namespace LibraryManagement.Api.DTOs;

public record MostBorrowedBookDto(int BookId, string Title, string Author, int BorrowCount);

public record OverdueLoanDto(
    int LoanId,
    int BookId,
    string BookTitle,
    int MemberId,
    string MemberName,
    string MemberEmail,
    DateTime BorrowDate,
    DateTime DueDate,
    int DaysOverdue,
    decimal FineOwed);

public record OverdueReportDto(int Count, decimal TotalFinesOwed, IReadOnlyList<OverdueLoanDto> Loans);
