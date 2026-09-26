using LibraryManagement.Api.Common;
using LibraryManagement.Api.Data;
using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Api.Services;

public interface IReservationService
{
    Task<ReservationDto> ReserveAsync(int bookId, int memberId, CancellationToken ct = default);
    Task<ReservationDto> CancelAsync(int reservationId, int actingMemberId, bool actingIsLibrarian, CancellationToken ct = default);
    Task<IReadOnlyList<ReservationDto>> GetForMemberAsync(int memberId, CancellationToken ct = default);

    /// <summary>Open (Notified + Pending) reservations for a book in queue order.</summary>
    Task<IReadOnlyList<ReservationDto>> GetQueueForBookAsync(int bookId, CancellationToken ct = default);

    /// <summary>
    /// Gives any free copies of the book to the oldest Pending reservations (FIFO) by marking them Notified.
    /// Changes tracked entities only. The caller saves. Used after a return, a cancellation or adding copies.
    /// </summary>
    Task ProcessQueueAsync(Book book, CancellationToken ct = default);

    /// <summary>Number of available copies currently held for Notified reservations.</summary>
    Task<int> CountHeldCopiesAsync(int bookId, CancellationToken ct = default);
}

/// <remarks>
/// A Notified reservation holds one of the book's available copies for that member.
/// The copy stays in <see cref="Book.AvailableCopies"/> but no one else can borrow it,
/// so copies anyone can borrow = AvailableCopies − Notified reservations.
/// </remarks>
public class ReservationService(
    LibraryDbContext db,
    TimeProvider clock,
    ILogger<ReservationService> logger) : IReservationService
{
    // In-memory checks only. Queries spell out the comparison so it translates to plain SQL.
    private static readonly ReservationStatus[] OpenStatuses = [ReservationStatus.Pending, ReservationStatus.Notified];

    public async Task<ReservationDto> ReserveAsync(int bookId, int memberId, CancellationToken ct = default)
    {
        var member = await db.Members.FindAsync([memberId], ct)
                     ?? throw new NotFoundException($"Member {memberId} was not found.");
        var book = await db.Books.FindAsync([bookId], ct)
                   ?? throw new NotFoundException($"Book {bookId} was not found.");

        if (await db.Loans.AnyAsync(l => l.BookId == bookId && l.MemberId == memberId && l.ReturnDate == null, ct))
            throw new BusinessRuleException($"Member already has '{book.Title}' on loan.");

        if (await db.Reservations.AnyAsync(r =>
                r.BookId == bookId && r.MemberId == memberId &&
                (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Notified), ct))
            throw new BusinessRuleException($"Member already has an open reservation for '{book.Title}'.");

        var held = await CountHeldCopiesAsync(bookId, ct);
        if (book.AvailableCopies - held > 0)
            throw new BusinessRuleException($"'{book.Title}' has copies available. Borrow it instead of reserving.");

        var reservation = new Reservation
        {
            Book = book,
            Member = member,
            ReservationDate = clock.GetUtcNow().UtcDateTime,
            Status = ReservationStatus.Pending
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(ct);

        var position = await db.Reservations.CountAsync(r =>
            r.BookId == bookId && r.Status == ReservationStatus.Pending &&
            (r.ReservationDate < reservation.ReservationDate ||
             (r.ReservationDate == reservation.ReservationDate && r.Id <= reservation.Id)), ct);

        return reservation.ToDto(position);
    }

    public async Task<ReservationDto> CancelAsync(int reservationId, int actingMemberId, bool actingIsLibrarian, CancellationToken ct = default)
    {
        var reservation = await db.Reservations
                              .Include(r => r.Book)
                              .Include(r => r.Member)
                              .SingleOrDefaultAsync(r => r.Id == reservationId, ct)
                          ?? throw new NotFoundException($"Reservation {reservationId} was not found.");

        if (!actingIsLibrarian && reservation.MemberId != actingMemberId)
            throw new ForbiddenException("You can only cancel your own reservations.");
        if (!OpenStatuses.Contains(reservation.Status))
            throw new BusinessRuleException($"Reservation is already {reservation.Status} and cannot be cancelled.");

        var releasedHeldCopy = reservation.Status == ReservationStatus.Notified;
        reservation.Status = ReservationStatus.Cancelled;

        // A cancelled Notified reservation frees its held copy. Pass it to the next member in the queue.
        if (releasedHeldCopy)
            await ProcessQueueAsync(reservation.Book, ct);

        await db.SaveChangesAsync(ct);
        return reservation.ToDto();
    }

    public async Task ProcessQueueAsync(Book book, CancellationToken ct = default)
    {
        // Filter on the database status, then re-check statuses in memory. Tracked entities keep their
        // in-memory values, so a reservation cancelled earlier in this request counts as cancelled.
        var open = await db.Reservations
            .Include(r => r.Member)
            .Where(r => r.BookId == book.Id &&
                        (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Notified))
            .OrderBy(r => r.ReservationDate).ThenBy(r => r.Id)
            .ToListAsync(ct);

        var free = book.AvailableCopies - open.Count(r => r.Status == ReservationStatus.Notified);
        var now = clock.GetUtcNow().UtcDateTime;

        foreach (var reservation in open.Where(r => r.Status == ReservationStatus.Pending).Take(Math.Max(free, 0)))
        {
            reservation.Status = ReservationStatus.Notified;
            reservation.NotifiedDate = now;
            // Stand-in for an email/SMS: the Notified status is what the member sees in the API.
            logger.LogInformation("NOTIFY {Email}: a copy of '{Title}' is ready for pickup (reservation {ReservationId}).",
                reservation.Member.Email, book.Title, reservation.Id);
        }
    }

    public Task<int> CountHeldCopiesAsync(int bookId, CancellationToken ct = default) =>
        db.Reservations.CountAsync(r => r.BookId == bookId && r.Status == ReservationStatus.Notified, ct);

    public async Task<IReadOnlyList<ReservationDto>> GetForMemberAsync(int memberId, CancellationToken ct = default)
    {
        if (!await db.Members.AnyAsync(m => m.Id == memberId, ct))
            throw new NotFoundException($"Member {memberId} was not found.");

        var reservations = await db.Reservations.AsNoTracking()
            .Include(r => r.Book)
            .Include(r => r.Member)
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.ReservationDate)
            .ToListAsync(ct);

        // Queue positions for this member's pending reservations, computed from each book's full pending queue.
        var pendingBookIds = reservations
            .Where(r => r.Status == ReservationStatus.Pending)
            .Select(r => r.BookId)
            .Distinct()
            .ToList();
        var positions = await PendingQueuePositionsAsync(pendingBookIds, ct);

        return reservations
            .Select(r => r.ToDto(positions.GetValueOrDefault(r.Id)))
            .ToList();
    }

    public async Task<IReadOnlyList<ReservationDto>> GetQueueForBookAsync(int bookId, CancellationToken ct = default)
    {
        if (!await db.Books.AnyAsync(b => b.Id == bookId, ct))
            throw new NotFoundException($"Book {bookId} was not found.");

        var open = await db.Reservations.AsNoTracking()
            .Include(r => r.Book)
            .Include(r => r.Member)
            .Where(r => r.BookId == bookId && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Notified))
            .OrderBy(r => r.ReservationDate).ThenBy(r => r.Id)
            .ToListAsync(ct);

        // Notified reservations come first (they already have a copy), then Pending in FIFO order.
        var position = 0;
        return open
            .OrderBy(r => r.Status == ReservationStatus.Notified ? 0 : 1)
            .Select(r => r.ToDto(r.Status == ReservationStatus.Pending ? ++position : null))
            .ToList();
    }

    /// <summary>Maps reservation id → 1-based position in its book's Pending queue.</summary>
    private async Task<Dictionary<int, int?>> PendingQueuePositionsAsync(List<int> bookIds, CancellationToken ct)
    {
        if (bookIds.Count == 0) return new Dictionary<int, int?>();

        var pending = await db.Reservations.AsNoTracking()
            .Where(r => bookIds.Contains(r.BookId) && r.Status == ReservationStatus.Pending)
            .Select(r => new { r.Id, r.BookId, r.ReservationDate })
            .ToListAsync(ct);

        return pending
            .GroupBy(r => r.BookId)
            .SelectMany(g => g.OrderBy(r => r.ReservationDate).ThenBy(r => r.Id)
                              .Select((r, i) => (r.Id, Position: (int?)(i + 1))))
            .ToDictionary(x => x.Id, x => x.Position);
    }
}
