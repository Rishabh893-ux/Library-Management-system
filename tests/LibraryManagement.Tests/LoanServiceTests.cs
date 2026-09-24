using LibraryManagement.Api.Common;
using LibraryManagement.Tests.Infrastructure;

namespace LibraryManagement.Tests;

public class LoanServiceTests : IDisposable
{
    private readonly TestLibrary _lib = new();

    public void Dispose() => _lib.Dispose();

    [Fact]
    public async Task Borrow_decrements_available_copies_and_sets_14_day_due_date()
    {
        var book = _lib.AddBook(copies: 2);
        var alice = _lib.AddMember("Alice");

        var loan = await _lib.Loans().BorrowAsync(book, alice);

        Assert.Equal(1, _lib.GetBook(book).AvailableCopies);
        Assert.Equal(_lib.Clock.Now.UtcDateTime, loan.BorrowDate);
        Assert.Equal(_lib.Clock.Now.UtcDateTime.AddDays(14), loan.DueDate);
        Assert.Null(loan.ReturnDate);
        Assert.Equal(0m, loan.FineAmount);
    }

    [Fact]
    public async Task Borrow_fails_when_no_copies_are_available()
    {
        var book = _lib.AddBook(copies: 1);
        var alice = _lib.AddMember("Alice");
        var bob = _lib.AddMember("Bob");
        await _lib.Loans().BorrowAsync(book, alice);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _lib.Loans().BorrowAsync(book, bob));

        Assert.Contains("Reserve it instead", ex.Message);
        Assert.Equal(0, _lib.GetBook(book).AvailableCopies);
    }

    [Fact]
    public async Task Return_after_due_date_charges_5_rupees_per_day_and_restores_copy()
    {
        var book = _lib.AddBook(copies: 1);
        var alice = _lib.AddMember("Alice");
        var loan = await _lib.Loans().BorrowAsync(book, alice);

        _lib.Clock.Advance(TimeSpan.FromDays(14 + 4)); // 4 days late
        var returned = await _lib.Loans().ReturnAsync(loan.Id, alice, actingIsLibrarian: false);

        Assert.Equal(20m, returned.FineAmount);
        Assert.NotNull(returned.ReturnDate);
        Assert.Equal(1, _lib.GetBook(book).AvailableCopies);
    }

    [Fact]
    public async Task Return_on_time_charges_no_fine()
    {
        var book = _lib.AddBook(copies: 1);
        var alice = _lib.AddMember("Alice");
        var loan = await _lib.Loans().BorrowAsync(book, alice);

        _lib.Clock.Advance(TimeSpan.FromDays(14));
        var returned = await _lib.Loans().ReturnAsync(loan.Id, alice, actingIsLibrarian: false);

        Assert.Equal(0m, returned.FineAmount);
    }

    [Fact]
    public async Task Returning_the_same_loan_twice_is_rejected()
    {
        var book = _lib.AddBook(copies: 1);
        var alice = _lib.AddMember("Alice");
        var loan = await _lib.Loans().BorrowAsync(book, alice);
        await _lib.Loans().ReturnAsync(loan.Id, alice, actingIsLibrarian: false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _lib.Loans().ReturnAsync(loan.Id, alice, actingIsLibrarian: false));
        Assert.Equal(1, _lib.GetBook(book).AvailableCopies); // not incremented twice
    }

    [Fact]
    public async Task Member_cannot_return_someone_elses_loan_but_librarian_can()
    {
        var book = _lib.AddBook(copies: 1);
        var alice = _lib.AddMember("Alice");
        var bob = _lib.AddMember("Bob");
        var loan = await _lib.Loans().BorrowAsync(book, alice);

        await Assert.ThrowsAsync<ForbiddenException>(() => _lib.Loans().ReturnAsync(loan.Id, bob, actingIsLibrarian: false));

        var returned = await _lib.Loans().ReturnAsync(loan.Id, bob, actingIsLibrarian: true);
        Assert.NotNull(returned.ReturnDate);
    }

    [Fact]
    public async Task Active_loans_exclude_returned_ones_and_flag_overdue()
    {
        var b1 = _lib.AddBook(copies: 1, "First");
        var b2 = _lib.AddBook(copies: 1, "Second");
        var alice = _lib.AddMember("Alice");
        var first = await _lib.Loans().BorrowAsync(b1, alice);
        await _lib.Loans().BorrowAsync(b2, alice);
        await _lib.Loans().ReturnAsync(first.Id, alice, actingIsLibrarian: false);

        _lib.Clock.Advance(TimeSpan.FromDays(15));
        var active = await _lib.Loans().GetActiveLoansAsync(alice);

        var loan = Assert.Single(active);
        Assert.Equal("Second", loan.BookTitle);
        Assert.True(loan.IsOverdue);
    }

    [Fact]
    public async Task Overdue_report_lists_unreturned_late_loans_with_fine_owed()
    {
        var book = _lib.AddBook(copies: 2);
        var alice = _lib.AddMember("Alice");
        var bob = _lib.AddMember("Bob");
        await _lib.Loans().BorrowAsync(book, alice);
        _lib.Clock.Advance(TimeSpan.FromDays(10));
        await _lib.Loans().BorrowAsync(book, bob); // not overdue yet

        _lib.Clock.Advance(TimeSpan.FromDays(7)); // Alice is now 3 days late, Bob has 7 days left
        var report = await _lib.Reports().GetOverdueLoansAsync();

        var overdue = Assert.Single(report.Loans);
        Assert.Equal(alice, overdue.MemberId);
        Assert.Equal(3, overdue.DaysOverdue);
        Assert.Equal(15m, overdue.FineOwed);
        Assert.Equal(15m, report.TotalFinesOwed);
    }
}
