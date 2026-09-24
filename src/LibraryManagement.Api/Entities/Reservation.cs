namespace LibraryManagement.Api.Entities;

public class Reservation
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int MemberId { get; set; }

    /// <summary>Queue position is determined by this timestamp (FIFO), ties broken by Id.</summary>
    public DateTime ReservationDate { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    /// <summary>When a copy was set aside for this member. Null until the reservation is notified.</summary>
    public DateTime? NotifiedDate { get; set; }

    public Book Book { get; set; } = null!;
    public Member Member { get; set; } = null!;
}
