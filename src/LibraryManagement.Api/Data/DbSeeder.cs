using LibraryManagement.Api.Entities;
using LibraryManagement.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Api.Data;

/// <summary>
/// Inserts demo data (accounts, books, loan history, overdue loans and a reservation queue) into an empty database.
/// Does nothing if any member already exists.
/// </summary>
public class DbSeeder(
    LibraryDbContext db,
    IPasswordHasher<Member> hasher,
    IFineCalculator fines,
    TimeProvider clock,
    ILogger<DbSeeder> logger)
{
    public const string LibrarianEmail = "admin@library.local";
    public const string LibrarianPassword = "Admin@123";
    public const string MemberPassword = "Member@123";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Members.AnyAsync(ct))
        {
            logger.LogInformation("Database already contains data; skipping seed.");
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;

        Member NewMember(string name, string email, MemberRole role, string password, int joinedDaysAgo)
        {
            var m = new Member { Name = name, Email = email, Role = role, JoinDate = now.AddDays(-joinedDaysAgo) };
            m.PasswordHash = hasher.HashPassword(m, password);
            return m;
        }

        var librarian = NewMember("Head Librarian", LibrarianEmail, MemberRole.Librarian, LibrarianPassword, 365);
        var alice = NewMember("Alice Sharma", "alice@example.com", MemberRole.Member, MemberPassword, 200);
        var bob = NewMember("Bob Verma", "bob@example.com", MemberRole.Member, MemberPassword, 150);
        var carol = NewMember("Carol Iyer", "carol@example.com", MemberRole.Member, MemberPassword, 90);
        db.Members.AddRange(librarian, alice, bob, carol);

        Book NewBook(string title, string author, string isbn, string category, int copies) =>
            new() { Title = title, Author = author, Isbn = isbn, Category = category, TotalCopies = copies, AvailableCopies = copies };

        var cleanCode = NewBook("Clean Code", "Robert C. Martin", "9780132350884", "Software", 3);
        var pragmatic = NewBook("The Pragmatic Programmer", "David Thomas, Andrew Hunt", "9780135957059", "Software", 2);
        var ddia = NewBook("Designing Data-Intensive Applications", "Martin Kleppmann", "9781449373320", "Software", 2);
        var sapiens = NewBook("Sapiens", "Yuval Noah Harari", "9780062316097", "History", 2);
        var godOfSmallThings = NewBook("The God of Small Things", "Arundhati Roy", "9780679457312", "Fiction", 1);
        var midnightsChildren = NewBook("Midnight's Children", "Salman Rushdie", "9780812976533", "Fiction", 1);
        var wingsOfFire = NewBook("Wings of Fire", "A. P. J. Abdul Kalam", "9788173711466", "Biography", 2);
        var briefHistory = NewBook("A Brief History of Time", "Stephen Hawking", "9780553380163", "Science", 2);
        var atomicHabits = NewBook("Atomic Habits", "James Clear", "9780735211292", "Self-Help", 3);
        var alchemist = NewBook("The Alchemist", "Paulo Coelho", "9780062315007", "Fiction", 2);
        var books = new[] { cleanCode, pragmatic, ddia, sapiens, godOfSmallThings, midnightsChildren, wingsOfFire, briefHistory, atomicHabits, alchemist };
        db.Books.AddRange(books);

        var loans = new List<Loan>();

        // Returned loans. daysLate > 0 means the member paid a fine.
        void Returned(Book book, Member member, int borrowedDaysAgo, int daysLate = 0)
        {
            var borrowed = now.AddDays(-borrowedDaysAgo);
            var due = fines.CalculateDueDate(borrowed);
            var returned = daysLate > 0 ? due.AddDays(daysLate) : borrowed.AddDays(7);
            loans.Add(new Loan
            {
                Book = book, Member = member, BorrowDate = borrowed, DueDate = due,
                ReturnDate = returned, FineAmount = fines.CalculateFine(due, returned)
            });
        }

        // Active loans (not yet returned).
        void Active(Book book, Member member, int borrowedDaysAgo)
        {
            var borrowed = now.AddDays(-borrowedDaysAgo);
            loans.Add(new Loan { Book = book, Member = member, BorrowDate = borrowed, DueDate = fines.CalculateDueDate(borrowed) });
        }

        // History that gives the "most borrowed" report a clear ranking.
        Returned(cleanCode, alice, 180); Returned(cleanCode, bob, 150, daysLate: 3); Returned(cleanCode, carol, 80);
        Returned(cleanCode, bob, 60); Returned(cleanCode, carol, 45);
        Returned(atomicHabits, alice, 170); Returned(atomicHabits, bob, 120); Returned(atomicHabits, carol, 70, daysLate: 2);
        Returned(atomicHabits, alice, 50);
        Returned(sapiens, bob, 140); Returned(sapiens, alice, 100);
        Returned(alchemist, carol, 85); Returned(alchemist, alice, 75); Returned(alchemist, bob, 40);
        Returned(ddia, alice, 130); Returned(ddia, carol, 65);
        Returned(wingsOfFire, bob, 55);

        // Current loans: two overdue (for the overdue report), two on time.
        Active(cleanCode, alice, borrowedDaysAgo: 20);         // 6 days overdue → ₹30
        Active(godOfSmallThings, bob, borrowedDaysAgo: 30);    // 16 days overdue → ₹80, no copies left
        Active(sapiens, carol, borrowedDaysAgo: 3);
        Active(midnightsChildren, alice, borrowedDaysAgo: 5);  // no copies left
        db.Loans.AddRange(loans);

        // Every copy of these books is out, so there is a queue to demonstrate FIFO processing on return.
        db.Reservations.AddRange(
            new Reservation { Book = godOfSmallThings, Member = carol, ReservationDate = now.AddDays(-10), Status = ReservationStatus.Pending },
            new Reservation { Book = godOfSmallThings, Member = alice, ReservationDate = now.AddDays(-4), Status = ReservationStatus.Pending },
            new Reservation { Book = midnightsChildren, Member = bob, ReservationDate = now.AddDays(-2), Status = ReservationStatus.Pending });

        // AvailableCopies must match the active loans.
        foreach (var book in books)
            book.AvailableCopies = book.TotalCopies - loans.Count(l => l.Book == book && l.ReturnDate is null);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Members} members, {Books} books, {Loans} loans.", 4, books.Length, loans.Count);
    }
}
