using LibraryManagement.Api.Data;
using LibraryManagement.Api.Entities;
using LibraryManagement.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibraryManagement.Tests.Infrastructure;

/// <summary>A controllable clock.</summary>
public sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; private set; } = start;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
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

    public TestLibrary()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<LibraryDbContext>().UseSqlite(_connection).Options;
        using var db = NewDb();
        db.Database.EnsureCreated();
    }

    public LibraryDbContext NewDb() => new(_options);

    // Services that share work must share the DbContext, just as they do within one HTTP request.
    public LoanService Loans()
    {
        var db = NewDb();
        return new LoanService(db, NewReservationService(db), Clock);
    }

    public ReservationService Reservations() => NewReservationService(NewDb());

    public BookService Books()
    {
        var db = NewDb();
        return new BookService(db, NewReservationService(db));
    }

    public ReportService Reports() => new(NewDb(), Clock);

    private ReservationService NewReservationService(LibraryDbContext db) =>
        new(db, Clock, NullLogger<ReservationService>.Instance);

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
