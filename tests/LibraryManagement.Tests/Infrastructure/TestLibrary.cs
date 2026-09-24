using LibraryManagement.Api.Data;
using LibraryManagement.Api.Entities;
using LibraryManagement.Api.Options;
using LibraryManagement.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Tests.Infrastructure;

/// <summary>A controllable clock.</summary>
public sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; private set; } = start;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}

/// <summary>Records notifications instead of sending them.</summary>
public sealed class RecordingNotificationService : INotificationService
{
    public List<int> NotifiedReservationIds { get; } = new();

    public Task ReservationAvailableAsync(Reservation reservation, CancellationToken ct = default)
    {
        NotifiedReservationIds.Add(reservation.Id);
        return Task.CompletedTask;
    }
}

/// <summary>
/// An isolated library backed by an in-memory SQLite database, which enforces FKs, check constraints
/// and concurrency tokens like a real database.
/// Each service call gets a fresh DbContext, as each HTTP request would, so stale change tracking
/// can't hide bugs.
/// </summary>
public sealed class TestLibrary : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LibraryDbContext> _options;

    public FakeClock Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));
    public RecordingNotificationService Notifications { get; } = new();
    public FineCalculator Fines { get; } = new(Microsoft.Extensions.Options.Options.Create(new LoanPolicyOptions()));

    public TestLibrary()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<LibraryDbContext>().UseSqlite(_connection).Options;
        using var db = NewDb();
        db.Database.EnsureCreated();
    }

    public LibraryDbContext NewDb() => new(_options);

    public LoanService Loans()
    {
        var db = NewDb();
        return new LoanService(db, Fines, new ReservationQueue(db, Clock), Notifications, Clock);
    }

    public ReservationService Reservations()
    {
        var db = NewDb();
        return new ReservationService(db, new ReservationQueue(db, Clock), Notifications, Clock);
    }

    public BookService Books()
    {
        var db = NewDb();
        return new BookService(db, new ReservationQueue(db, Clock), Notifications);
    }

    public ReportService Reports() => new(NewDb(), Fines, Clock);

    public int AddBook(int copies, string title = "Test Book")
    {
        using var db = NewDb();
        var book = new Book
        {
            Title = title, Author = "Author", Category = "Category",
            Isbn = Guid.NewGuid().ToString("N")[..13], TotalCopies = copies, AvailableCopies = copies
        };
        db.Books.Add(book);
        db.SaveChanges();
        return book.Id;
    }

    public int AddMember(string name)
    {
        using var db = NewDb();
        var member = new Member
        {
            Name = name, Email = $"{name.ToLowerInvariant()}@test.local",
            PasswordHash = "x", Role = MemberRole.Member, JoinDate = Clock.Now.UtcDateTime
        };
        db.Members.Add(member);
        db.SaveChanges();
        return member.Id;
    }

    public Book GetBook(int id)
    {
        using var db = NewDb();
        return db.Books.AsNoTracking().Single(b => b.Id == id);
    }

    public Reservation GetReservation(int id)
    {
        using var db = NewDb();
        return db.Reservations.AsNoTracking().Single(r => r.Id == id);
    }

    public void Dispose() => _connection.Dispose();
}
