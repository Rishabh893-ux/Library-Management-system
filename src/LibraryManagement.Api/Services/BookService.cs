using LibraryManagement.Api.Common;
using LibraryManagement.Api.Data;
using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Api.Services;

public interface IBookService
{
    Task<PagedResult<BookDto>> SearchAsync(BookSearchQuery query, CancellationToken ct = default);
    Task<BookDto> GetAsync(int id, CancellationToken ct = default);
    Task<BookDto> CreateAsync(CreateBookRequest request, CancellationToken ct = default);
    Task<BookDto> UpdateAsync(int id, UpdateBookRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class BookService(
    LibraryDbContext db,
    IReservationService reservations) : IBookService
{
    public async Task<PagedResult<BookDto>> SearchAsync(BookSearchQuery query, CancellationToken ct = default)
    {
        var books = db.Books.AsNoTracking();

        // Case-insensitive substring matching (uses SQL Server's default case-insensitive collation).
        if (!string.IsNullOrWhiteSpace(query.Title))
            books = books.Where(b => b.Title.Contains(query.Title.Trim()));
        if (!string.IsNullOrWhiteSpace(query.Author))
            books = books.Where(b => b.Author.Contains(query.Author.Trim()));
        if (!string.IsNullOrWhiteSpace(query.Category))
            books = books.Where(b => b.Category.Contains(query.Category.Trim()));

        var total = await books.CountAsync(ct);
        var items = await books
            .OrderBy(b => b.Title).ThenBy(b => b.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(b => new BookDto(b.Id, b.Title, b.Author, b.Isbn, b.Category, b.TotalCopies, b.AvailableCopies))
            .ToListAsync(ct);

        return new PagedResult<BookDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<BookDto> GetAsync(int id, CancellationToken ct = default) =>
        (await FindAsync(id, ct, tracking: false)).ToDto();

    public async Task<BookDto> CreateAsync(CreateBookRequest request, CancellationToken ct = default)
    {
        var isbn = NormalizeIsbn(request.Isbn);
        await EnsureIsbnUniqueAsync(isbn, excludeBookId: null, ct);

        var book = new Book
        {
            Title = request.Title.Trim(),
            Author = request.Author.Trim(),
            Isbn = isbn,
            Category = request.Category.Trim(),
            TotalCopies = request.TotalCopies,
            AvailableCopies = request.TotalCopies
        };
        db.Books.Add(book);
        await db.SaveChangesAsync(ct);
        return book.ToDto();
    }

    public async Task<BookDto> UpdateAsync(int id, UpdateBookRequest request, CancellationToken ct = default)
    {
        var book = await FindAsync(id, ct);
        var isbn = NormalizeIsbn(request.Isbn);
        await EnsureIsbnUniqueAsync(isbn, excludeBookId: id, ct);

        var onLoan = book.TotalCopies - book.AvailableCopies;
        if (request.TotalCopies < onLoan)
            throw new BusinessRuleException(
                $"TotalCopies cannot be less than the {onLoan} copies currently on loan.");

        book.Title = request.Title.Trim();
        book.Author = request.Author.Trim();
        book.Isbn = isbn;
        book.Category = request.Category.Trim();
        book.TotalCopies = request.TotalCopies;
        book.AvailableCopies = request.TotalCopies - onLoan;

        // New copies may be able to serve people already waiting in the queue.
        await reservations.ProcessQueueAsync(book, ct);
        await db.SaveChangesAsync(ct);

        return book.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var book = await FindAsync(id, ct);
        if (await db.Loans.AnyAsync(l => l.BookId == id && l.ReturnDate == null, ct))
            throw new BusinessRuleException("Cannot delete a book that has copies on loan.");

        // Loan history and reservations are removed by cascade delete.
        db.Books.Remove(book);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Book> FindAsync(int id, CancellationToken ct, bool tracking = true)
    {
        var query = tracking ? db.Books : db.Books.AsNoTracking();
        return await query.SingleOrDefaultAsync(b => b.Id == id, ct)
               ?? throw new NotFoundException($"Book {id} was not found.");
    }

    private async Task EnsureIsbnUniqueAsync(string isbn, int? excludeBookId, CancellationToken ct)
    {
        if (await db.Books.AnyAsync(b => b.Isbn == isbn && b.Id != excludeBookId, ct))
            throw new BusinessRuleException($"A book with ISBN {isbn} already exists.");
    }

    internal static string NormalizeIsbn(string isbn) =>
        new string(isbn.Where(c => c != '-' && c != ' ').ToArray()).ToUpperInvariant();
}
