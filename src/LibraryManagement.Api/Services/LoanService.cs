using LibraryManagement.Api.Common;
using LibraryManagement.Api.Data;
using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Api.Services;

public interface ILoanService
{
    Task<LoanDto> BorrowAsync(int bookId, int memberId, CancellationToken ct = default);

    /// <param name="actingMemberId">The caller. Members can only return their own loans.</param>
    /// <param name="actingIsLibrarian">Librarians can return any loan.</param>
    Task<LoanDto> ReturnAsync(int loanId, int actingMemberId, bool actingIsLibrarian, CancellationToken ct = default);

    Task<IReadOnlyList<LoanDto>> GetActiveLoansAsync(int memberId, CancellationToken ct = default);
}

public class LoanService(
    LibraryDbContext db,
    IReservationService reservations,
    TimeProvider clock) : ILoanService
{
    public async Task<LoanDto> BorrowAsync(int bookId, int memberId, CancellationToken ct = default)
    {
        var member = await db.Members.FindAsync([memberId], ct)
                     ?? throw new NotFoundException($"Member {memberId} was not found.");
        var book = await db.Books.FindAsync([bookId], ct)
                   ?? throw new NotFoundException($"Book {bookId} was not found.");

        if (await db.Loans.AnyAsync(l => l.BookId == bookId && l.MemberId == memberId && l.ReturnDate == null, ct))
            throw new BusinessRuleException($"Member already has '{book.Title}' on loan.");

        // The member's own open reservation for this book, if any. If it is Notified, a copy is held for them.
        var ownReservation = await db.Reservations.SingleOrDefaultAsync(r =>
            r.BookId == bookId && r.MemberId == memberId &&
            (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Notified), ct);

        var hasHeldCopy = ownReservation?.Status == ReservationStatus.Notified;
        if (!hasHeldCopy)
        {
            // Copies held for other members' reservations can't be borrowed by anyone else.
            var held = await reservations.CountHeldCopiesAsync(bookId, ct);
            if (book.AvailableCopies - held <= 0)
                throw new BusinessRuleException(
                    $"No copies of '{book.Title}' are available. Reserve it instead (POST /api/reservations).");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var loan = new Loan
        {
            Book = book,
            Member = member,
            BorrowDate = now,
            DueDate = FineCalculator.CalculateDueDate(now),
            FineAmount = 0m
        };
        db.Loans.Add(loan);
        book.AvailableCopies--;
        if (ownReservation is not null)
            ownReservation.Status = ReservationStatus.Fulfilled;

        // AvailableCopies is a concurrency token. If another request took the last copy first,
        // this throws DbUpdateConcurrencyException, which the API returns as 409.
        await db.SaveChangesAsync(ct);
        return loan.ToDto(now);
    }

    public async Task<LoanDto> ReturnAsync(int loanId, int actingMemberId, bool actingIsLibrarian, CancellationToken ct = default)
    {
        var loan = await db.Loans
                       .Include(l => l.Book)
                       .Include(l => l.Member)
                       .SingleOrDefaultAsync(l => l.Id == loanId, ct)
                   ?? throw new NotFoundException($"Loan {loanId} was not found.");

        if (!actingIsLibrarian && loan.MemberId != actingMemberId)
            throw new ForbiddenException("You can only return your own loans.");
        if (loan.ReturnDate is not null)
            throw new BusinessRuleException($"Loan {loanId} was already returned on {loan.ReturnDate:yyyy-MM-dd}.");

        var now = clock.GetUtcNow().UtcDateTime;
        loan.ReturnDate = now;
        loan.FineAmount = FineCalculator.CalculateFine(loan.DueDate, now);
        loan.Book.AvailableCopies = Math.Min(loan.Book.AvailableCopies + 1, loan.Book.TotalCopies);

        // Hold the returned copy for the next member in the queue, if there is one.
        await reservations.ProcessQueueAsync(loan.Book, ct);

        await db.SaveChangesAsync(ct);
        return loan.ToDto(now);
    }

    public async Task<IReadOnlyList<LoanDto>> GetActiveLoansAsync(int memberId, CancellationToken ct = default)
    {
        if (!await db.Members.AnyAsync(m => m.Id == memberId, ct))
            throw new NotFoundException($"Member {memberId} was not found.");

        var now = clock.GetUtcNow().UtcDateTime;
        var loans = await db.Loans.AsNoTracking()
            .Include(l => l.Book)
            .Include(l => l.Member)
            .Where(l => l.MemberId == memberId && l.ReturnDate == null)
            .OrderBy(l => l.DueDate)
            .ToListAsync(ct);

        return loans.Select(l => l.ToDto(now)).ToList();
    }
}
