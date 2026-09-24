using LibraryManagement.Api.Common;
using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Api.Controllers;

[ApiController]
[Route("api/loans")]
[Authorize]
[Produces("application/json")]
public class LoansController(ILoanService loans) : ControllerBase
{
    /// <summary>
    /// Borrow a book for 14 days. Fails with 409 if no copy is free; reserve the book instead.
    /// Librarians may pass MemberId to lend on behalf of a member.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<LoanDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoanDto>> Borrow(BorrowRequest request, CancellationToken ct)
    {
        var memberId = User.ResolveTargetMemberId(request.MemberId);
        var loan = await loans.BorrowAsync(request.BookId, memberId, ct);
        return StatusCode(StatusCodes.Status201Created, loan);
    }

    /// <summary>
    /// Return a borrowed book. Charges ₹5 per day late and hands the copy to the next reservation in the queue, if any.
    /// </summary>
    [HttpPost("{id:int}/return")]
    [ProducesResponseType<LoanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoanDto>> Return(int id, CancellationToken ct) =>
        Ok(await loans.ReturnAsync(id, User.GetMemberId(), User.IsLibrarian(), ct));

    /// <summary>
    /// A member's active (unreturned) loans. Members see their own. Librarians may pass memberId to see anyone's.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType<IReadOnlyList<LoanDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<LoanDto>>> Active([FromQuery] int? memberId, CancellationToken ct) =>
        Ok(await loans.GetActiveLoansAsync(User.ResolveTargetMemberId(memberId), ct));
}
