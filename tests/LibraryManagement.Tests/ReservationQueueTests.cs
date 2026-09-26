using LibraryManagement.Api.Common;
using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Entities;
using LibraryManagement.Tests.Infrastructure;

namespace LibraryManagement.Tests;

public class ReservationQueueTests : IDisposable
{
    private readonly TestLibrary _lib = new();

    public void Dispose() => _lib.Dispose();

    /// <summary>One-copy book lent to "Holder", with Alice and then Bob in the queue.</summary>
    private async Task<(int Book, int Holder, int LoanId, ReservationDto Alice, ReservationDto Bob)> BookWithQueueAsync()
    {
        var book = _lib.AddBook(copies: 1);
        var holder = _lib.AddMember("Holder");
        var alice = _lib.AddMember("Alice");
        var bob = _lib.AddMember("Bob");

        var loan = await _lib.Loans().BorrowAsync(book, holder);
        _lib.Clock.Advance(TimeSpan.FromMinutes(1));
        var aliceRes = await _lib.Reservations().ReserveAsync(book, alice);
        _lib.Clock.Advance(TimeSpan.FromMinutes(1));
        var bobRes = await _lib.Reservations().ReserveAsync(book, bob);

        return (book, holder, loan.Id, aliceRes, bobRes);
    }

    [Fact]
    public async Task Reservations_get_FIFO_queue_positions()
    {
        var (_, _, _, alice, bob) = await BookWithQueueAsync();

        Assert.Equal(1, alice.QueuePosition);
        Assert.Equal(2, bob.QueuePosition);
        Assert.Equal(nameof(ReservationStatus.Pending), alice.Status);
    }

    [Fact]
    public async Task Returning_a_book_notifies_only_the_oldest_pending_reservation()
    {
        var (book, holder, loanId, alice, bob) = await BookWithQueueAsync();

        await _lib.Loans().ReturnAsync(loanId, holder, actingIsLibrarian: false);

        var aliceRes = _lib.GetReservation(alice.Id);
        Assert.Equal(ReservationStatus.Notified, aliceRes.Status);
        Assert.Equal(_lib.Clock.Now.UtcDateTime, aliceRes.NotifiedDate);
        Assert.Equal(ReservationStatus.Pending, _lib.GetReservation(bob.Id).Status);
        Assert.Equal(1, _lib.GetBook(book).AvailableCopies); // on the shelf, but held for Alice
    }

    [Fact]
    public async Task Held_copy_cannot_be_borrowed_by_someone_else_but_the_notified_member_can_borrow_it()
    {
        var (book, holder, loanId, alice, bob) = await BookWithQueueAsync();
        await _lib.Loans().ReturnAsync(loanId, holder, actingIsLibrarian: false);

        // Bob (2nd in queue) and a member not in the queue can't take Alice's held copy.
        await Assert.ThrowsAsync<BusinessRuleException>(() => _lib.Loans().BorrowAsync(book, bob.MemberId));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _lib.Loans().BorrowAsync(book, _lib.AddMember("Walkin")));

        var loan = await _lib.Loans().BorrowAsync(book, alice.MemberId);

        Assert.Equal(alice.MemberId, loan.MemberId);
        Assert.Equal(ReservationStatus.Fulfilled, _lib.GetReservation(alice.Id).Status);
        Assert.Equal(0, _lib.GetBook(book).AvailableCopies);
    }

    [Fact]
    public async Task Queue_advances_through_successive_returns()
    {
        var (book, holder, loanId, alice, bob) = await BookWithQueueAsync();

        await _lib.Loans().ReturnAsync(loanId, holder, actingIsLibrarian: false);
        var aliceLoan = await _lib.Loans().BorrowAsync(book, alice.MemberId);
        await _lib.Loans().ReturnAsync(aliceLoan.Id, alice.MemberId, actingIsLibrarian: false);

        Assert.Equal(ReservationStatus.Notified, _lib.GetReservation(bob.Id).Status);
        Assert.Equal(ReservationStatus.Fulfilled, _lib.GetReservation(alice.Id).Status);
    }

    [Fact]
    public async Task Cancelling_a_notified_reservation_passes_the_copy_to_the_next_in_queue()
    {
        var (_, holder, loanId, alice, bob) = await BookWithQueueAsync();
        await _lib.Loans().ReturnAsync(loanId, holder, actingIsLibrarian: false);

        await _lib.Reservations().CancelAsync(alice.Id, alice.MemberId, actingIsLibrarian: false);

        Assert.Equal(ReservationStatus.Cancelled, _lib.GetReservation(alice.Id).Status);
        Assert.Equal(ReservationStatus.Notified, _lib.GetReservation(bob.Id).Status);
    }

    [Fact]
    public async Task Cannot_reserve_a_book_that_has_free_copies()
    {
        var book = _lib.AddBook(copies: 1);
        var alice = _lib.AddMember("Alice");

        await Assert.ThrowsAsync<BusinessRuleException>(() => _lib.Reservations().ReserveAsync(book, alice));
    }

    [Fact]
    public async Task Cannot_hold_two_open_reservations_for_the_same_book()
    {
        var (book, _, _, alice, _) = await BookWithQueueAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => _lib.Reservations().ReserveAsync(book, alice.MemberId));
    }

    [Fact]
    public async Task Adding_copies_to_a_book_serves_waiting_reservations()
    {
        var (book, _, _, alice, bob) = await BookWithQueueAsync();
        var current = _lib.GetBook(book);

        await _lib.Books().UpdateAsync(book, new UpdateBookRequest
        {
            Title = current.Title, Author = current.Author, Isbn = "9780132350884",
            Category = current.Category, TotalCopies = 3 // +2 copies
        });

        Assert.Equal(ReservationStatus.Notified, _lib.GetReservation(alice.Id).Status);
        Assert.Equal(ReservationStatus.Notified, _lib.GetReservation(bob.Id).Status);
        Assert.Equal(2, _lib.GetBook(book).AvailableCopies);
    }
}
