using LibraryManagement.Api.Common;
using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = Roles.Librarian)]
[Produces("application/json")]
public class ReportsController(IReportService reports) : ControllerBase
{
    /// <summary>The 5 most-borrowed books of all time, by number of loans.</summary>
    [HttpGet("most-borrowed")]
    [ProducesResponseType<IReadOnlyList<MostBorrowedBookDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MostBorrowedBookDto>>> MostBorrowed(CancellationToken ct) =>
        Ok(await reports.GetMostBorrowedBooksAsync(5, ct));

    /// <summary>Unreturned loans past their due date, with the fine owed so far (₹5/day).</summary>
    [HttpGet("overdue-loans")]
    [ProducesResponseType<OverdueReportDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OverdueReportDto>> Overdue(CancellationToken ct) =>
        Ok(await reports.GetOverdueLoansAsync(ct));
}
