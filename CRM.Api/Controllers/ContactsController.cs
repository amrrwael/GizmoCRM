using CRM.Application.Features.Contacts.Commands;
using CRM.Application.Features.Contacts.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ContactsController(IMediator mediator) : ControllerBase
{
    /// <summary>Get paginated list of contacts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ContactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? tag = null,
        CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetContactsQuery(page, pageSize, search, tag), ct));

    /// <summary>Get a contact by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetContactByIdQuery(id), ct));

    /// <summary>Get the full activity/deal timeline for a contact.</summary>
    [HttpGet("{id:guid}/timeline")]
    [ProducesResponseType(typeof(PagedResult<TimelineItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTimeline(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetContactTimelineQuery(id, page, pageSize), ct));

    /// <summary>Create a new contact.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateContactCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update an existing contact.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContactRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateContactCommand(id, request.FirstName, request.LastName,
            request.Email, request.Phone, request.Company, request.Position, request.Notes, request.Tags), ct));

    /// <summary>Assign a contact to a user (Admin/Manager only).</summary>
    [HttpPatch("{id:guid}/assign")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new AssignContactCommand(id, request.AssignedToId), ct));

    /// <summary>Delete a contact (Admin/Manager only).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteContactCommand(id), ct);
        return NoContent();
    }
}

public record UpdateContactRequest(
    string FirstName, string LastName, string Email,
    string? Phone, string? Company, string? Position,
    string? Notes, List<string>? Tags);

public record AssignRequest(Guid? AssignedToId);