using LibraryManagement.Api.Common;
using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Api.Controllers;

[ApiController]
[Route("api/books")]
[Authorize]
[Produces("application/json")]
public class BooksController(IBookService books) : ControllerBase
{
    /// <summary>Search books by title, author and/or category (case-insensitive partial match). Results are paged.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<BookDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BookDto>>> Search([FromQuery] BookSearchQuery query, CancellationToken ct) =>
        Ok(await books.SearchAsync(query, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<BookDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookDto>> Get(int id, CancellationToken ct) =>
        Ok(await books.GetAsync(id, ct));

    /// <summary>Add a book to the catalogue. AvailableCopies starts equal to TotalCopies. Librarian only.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType<BookDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookDto>> Create(CreateBookRequest request, CancellationToken ct)
    {
        var book = await books.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = book.Id }, book);
    }

    /// <summary>Replace a book's details. Librarian only.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType<BookDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookDto>> Update(int id, UpdateBookRequest request, CancellationToken ct) =>
        Ok(await books.UpdateAsync(id, request, ct));

    /// <summary>Delete a book and its loan history. Refused while any copy is on loan. Librarian only.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Librarian)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await books.DeleteAsync(id, ct);
        return NoContent();
    }
}
