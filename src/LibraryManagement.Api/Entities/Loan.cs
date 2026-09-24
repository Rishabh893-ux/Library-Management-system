namespace LibraryManagement.Api.Entities;

public class Loan
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int MemberId { get; set; }
    public DateTime BorrowDate { get; set; }
    public DateTime DueDate { get; set; }

    /// <summary>Null while the book is still out.</summary>
    public DateTime? ReturnDate { get; set; }

    /// <summary>Fine charged at return time. Zero while the loan is active.</summary>
    public decimal FineAmount { get; set; }

    public Book Book { get; set; } = null!;
    public Member Member { get; set; } = null!;

    public bool IsActive => ReturnDate is null;
}
