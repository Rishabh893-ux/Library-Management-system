using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Entities;

namespace LibraryManagement.Api.Services;

/// <summary>Entity → DTO mapping, kept in one place so controllers never see entities.</summary>
public static class Mapping
{
    public static MemberDto ToDto(this Member m) =>
        new(m.Id, m.Name, m.Email, m.Role.ToString(), m.JoinDate);

    public static BookDto ToDto(this Book b) =>
        new(b.Id, b.Title, b.Author, b.Isbn, b.Category, b.TotalCopies, b.AvailableCopies);

    /// <remarks>Requires <see cref="Loan.Book"/> and <see cref="Loan.Member"/> to be loaded.</remarks>
    public static LoanDto ToDto(this Loan l, DateTime utcNow) =>
        new(l.Id, l.BookId, l.Book.Title, l.MemberId, l.Member.Name,
            l.BorrowDate, l.DueDate, l.ReturnDate, l.FineAmount,
            IsOverdue: l.ReturnDate is null && utcNow.Date > l.DueDate.Date);

    /// <remarks>Requires <see cref="Reservation.Book"/> and <see cref="Reservation.Member"/> to be loaded.</remarks>
    public static ReservationDto ToDto(this Reservation r, int? queuePosition = null) =>
        new(r.Id, r.BookId, r.Book.Title, r.MemberId, r.Member.Name,
            r.ReservationDate, r.Status.ToString(), r.NotifiedDate, queuePosition);
}
