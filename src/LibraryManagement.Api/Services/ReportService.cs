using LibraryManagement.Api.Data;
using LibraryManagement.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Api.Services;

public interface IReportService
{
    Task<IReadOnlyList<MostBorrowedBookDto>> GetMostBorrowedBooksAsync(int top = 5, CancellationToken ct = default);
    Task<OverdueReportDto> GetOverdueLoansAsync(CancellationToken ct = default);
}

public class ReportService(LibraryDbContext db, IFineCalculator fines, TimeProvider clock) : IReportService
{
    public async Task<IReadOnlyList<MostBorrowedBookDto>> GetMostBorrowedBooksAsync(int top = 5, CancellationToken ct = default)
    {
        // Hand-written SQL (SQL Server dialect) for the aggregate. The interpolated value becomes a
        // parameter (@p0), so this is not open to SQL injection.
        var rows = await db.Database.SqlQuery<MostBorrowedRow>($"""
            SELECT TOP ({top})
                   b.Id        AS BookId,
                   b.Title     AS Title,
                   b.Author    AS Author,
                   COUNT(l.Id) AS BorrowCount
            FROM   Loans l
                   INNER JOIN Books b ON b.Id = l.BookId
            GROUP  BY b.Id, b.Title, b.Author
            ORDER  BY COUNT(l.Id) DESC, b.Title ASC
            """).ToListAsync(ct);

        return rows.Select(r => new MostBorrowedBookDto(r.BookId, r.Title, r.Author, r.BorrowCount)).ToList();
    }

    public async Task<OverdueReportDto> GetOverdueLoansAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var startOfToday = now.Date; // due any time before today → at least 1 day overdue

        var rows = await db.Loans.AsNoTracking()
            .Where(l => l.ReturnDate == null && l.DueDate < startOfToday)
            .OrderBy(l => l.DueDate)
            .Select(l => new
            {
                l.Id, l.BookId, BookTitle = l.Book.Title,
                l.MemberId, MemberName = l.Member.Name, MemberEmail = l.Member.Email,
                l.BorrowDate, l.DueDate
            })
            .ToListAsync(ct);

        var loans = rows.Select(r => new OverdueLoanDto(
                r.Id, r.BookId, r.BookTitle, r.MemberId, r.MemberName, r.MemberEmail,
                r.BorrowDate, r.DueDate,
                DaysOverdue: fines.DaysOverdue(r.DueDate, now),
                FineOwed: fines.CalculateFine(r.DueDate, now)))
            .ToList();

        return new OverdueReportDto(loans.Count, loans.Sum(l => l.FineOwed), loans);
    }

    /// <summary>Result shape for the raw SQL query. Column aliases must match these property names.</summary>
    internal sealed class MostBorrowedRow
    {
        public int BookId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public int BorrowCount { get; set; }
    }
}
