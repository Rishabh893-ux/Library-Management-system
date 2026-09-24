using LibraryManagement.Api.Common;
using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Api.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
[Produces("application/json")]
public class ReservationsController(IReservationService reservations) : ControllerBase
{
    /// <summary>
    /// Join the queue for a book with no free copies. When a copy is returned, the oldest Pending reservation
    /// becomes Notified and the copy is held for that member until they borrow it.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<ReservationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationDto>> Reserve(CreateReservationRequest request, CancellationToken ct)
    {
        var memberId = User.ResolveTargetMemberId(request.MemberId);
        var reservation = await reservations.ReserveAsync(request.BookId, memberId, ct);
        return StatusCode(StatusCodes.Status201Created, reservation);
    }

    /// <summary>Cancel a Pending or Notified reservation. A held copy passes to the next member in the queue.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType<ReservationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationDto>> Cancel(int id, CancellationToken ct) =>
        Ok(await reservations.CancelAsync(id, User.GetMemberId(), User.IsLibrarian(), ct));

    /// <summary>A member's reservations (all statuses). Librarians may pass memberId.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReservationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReservationDto>>> Mine([FromQuery] int? memberId, CancellationToken ct) =>
        Ok(await reservations.GetForMemberAsync(User.ResolveTargetMemberId(memberId), ct));

    /// <summary>The reservation queue for a book. Librarian only.</summary>
    [HttpGet("book/{bookId:int}")]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType<IReadOnlyList<ReservationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ReservationDto>>> QueueForBook(int bookId, CancellationToken ct) =>
        Ok(await reservations.GetQueueForBookAsync(bookId, ct));
}
