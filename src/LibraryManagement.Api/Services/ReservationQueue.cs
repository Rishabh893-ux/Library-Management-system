using LibraryManagement.Api.Data;
using LibraryManagement.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Api.Services;

/// <summary>
/// FIFO reservation queue for a single book.
/// <para>
/// A <see cref="ReservationStatus.Notified"/> reservation holds one of the book's available copies for that member.
/// The copy stays in <see cref="Book.AvailableCopies"/> but other members can't borrow it.
/// So the copies anyone can borrow = AvailableCopies − (number of Notified reservations).
/// </para>
/// </summary>
public interface IReservationQueue
{
    /// <summary>
    /// Gives any unheld available copies to the oldest Pending reservations (FIFO) by marking them Notified.
    /// Changes tracked entities only. The caller must call SaveChanges, then send notifications
    /// for the reservations this method returns.
    /// </summary>
    Task<IReadOnlyList<Reservation>> ProcessAsync(Book book, CancellationToken ct = default);

    /// <summary>Number of available copies currently held for Notified reservations.</summary>
    Task<int> CountHeldCopiesAsync(int bookId, CancellationToken ct = default);
}

public class ReservationQueue(LibraryDbContext db, TimeProvider clock) : IReservationQueue
{
    public async Task<IReadOnlyList<Reservation>> ProcessAsync(Book book, CancellationToken ct = default)
    {
        // Filter on the database status, then re-check statuses in memory. Tracked entities keep their
        // in-memory values, so a reservation cancelled earlier in this unit of work counts as cancelled.
        var active = await db.Reservations
            .Include(r => r.Member)
            .Include(r => r.Book)
            .Where(r => r.BookId == book.Id &&
                        (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Notified))
            .OrderBy(r => r.ReservationDate).ThenBy(r => r.Id)
            .ToListAsync(ct);

        var held = active.Count(r => r.Status == ReservationStatus.Notified);
        var free = book.AvailableCopies - held;
        if (free <= 0) return Array.Empty<Reservation>();

        var now = clock.GetUtcNow().UtcDateTime;
        var notified = new List<Reservation>();
        foreach (var reservation in active.Where(r => r.Status == ReservationStatus.Pending).Take(free))
        {
            reservation.Status = ReservationStatus.Notified;
            reservation.NotifiedDate = now;
            notified.Add(reservation);
        }
        return notified;
    }

    public Task<int> CountHeldCopiesAsync(int bookId, CancellationToken ct = default) =>
        db.Reservations.CountAsync(r => r.BookId == bookId && r.Status == ReservationStatus.Notified, ct);
}
