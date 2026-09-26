using LibraryManagement.Api.Entities;

namespace LibraryManagement.Api.Services;

public interface INotificationService
{
    /// <summary>Tells a member that a copy of the book they reserved is being held for them.</summary>
    Task ReservationAvailableAsync(Reservation reservation, CancellationToken ct = default);
}

public static class NotificationServiceExtensions
{
    public static async Task NotifyAllAsync(this INotificationService service,
        IEnumerable<Reservation> reservations, CancellationToken ct = default)
    {
        foreach (var reservation in reservations)
            await service.ReservationAvailableAsync(reservation, ct);
    }
}

/// <summary>
/// Placeholder that writes notifications to the log. Replace it with an email/SMS implementation.
/// The reservation's Status/NotifiedDate is the source of truth, so a failed send loses nothing.
/// </summary>
public class LoggingNotificationService(ILogger<LoggingNotificationService> logger) : INotificationService
{
    public Task ReservationAvailableAsync(Reservation reservation, CancellationToken ct = default)
    {
        logger.LogInformation(
            "NOTIFY {Email}: a copy of '{Title}' is ready for pickup (reservation {ReservationId}).",
            reservation.Member.Email, reservation.Book.Title, reservation.Id);
        return Task.CompletedTask;
    }
}
