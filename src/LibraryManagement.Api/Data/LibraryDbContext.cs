using LibraryManagement.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Api.Data;

public class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Book>(b =>
        {
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Author).HasMaxLength(150).IsRequired();
            b.Property(x => x.Isbn).HasMaxLength(17).IsRequired();
            b.Property(x => x.Category).HasMaxLength(100).IsRequired();
            b.Property(x => x.AvailableCopies).IsConcurrencyToken();
            b.HasIndex(x => x.Isbn).IsUnique();
            b.HasIndex(x => x.Title);
            b.HasIndex(x => x.Author);
            b.HasIndex(x => x.Category);
            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Books_TotalCopies", "[TotalCopies] >= 0");
                t.HasCheckConstraint("CK_Books_AvailableCopies", "[AvailableCopies] >= 0 AND [AvailableCopies] <= [TotalCopies]");
            });
        });

        modelBuilder.Entity<Member>(m =>
        {
            m.Property(x => x.Name).HasMaxLength(100).IsRequired();
            m.Property(x => x.Email).HasMaxLength(256).IsRequired();
            m.Property(x => x.PasswordHash).IsRequired();
            m.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            m.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Loan>(l =>
        {
            l.Property(x => x.FineAmount).HasPrecision(10, 2);
            l.Ignore(x => x.IsActive);
            // Deleting a book removes its loan history. The service refuses to delete a book with active loans.
            l.HasOne(x => x.Book).WithMany(b => b.Loans).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
            l.HasOne(x => x.Member).WithMany(m => m.Loans).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            l.HasIndex(x => new { x.MemberId, x.ReturnDate });
            l.HasIndex(x => new { x.ReturnDate, x.DueDate });
        });

        modelBuilder.Entity<Reservation>(r =>
        {
            r.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            r.HasOne(x => x.Book).WithMany(b => b.Reservations).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
            r.HasOne(x => x.Member).WithMany(m => m.Reservations).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            // Supports the queue lookup: "active reservations for book X in FIFO order".
            r.HasIndex(x => new { x.BookId, x.Status, x.ReservationDate });
        });
    }
}
