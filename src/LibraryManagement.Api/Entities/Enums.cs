namespace LibraryManagement.Api.Entities;

public enum MemberRole
{
    Member = 0,
    Librarian = 1
}

public enum ReservationStatus
{
    /// <summary>Waiting in the queue for a copy.</summary>
    Pending = 0,

    /// <summary>A returned copy is being held for this member.</summary>
    Notified = 1,

    /// <summary>Cancelled by the member or a librarian.</summary>
    Cancelled = 2,

    /// <summary>The member borrowed the held copy. The reservation is closed.</summary>
    Fulfilled = 3
}
