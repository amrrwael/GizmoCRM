using CRM.Application.Features.Activities.Commands;
using CRM.Application.Features.Activities.Queries;
using CRM.Application.Features.Contacts.Commands;
using CRM.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ActivitiesController(IMediator mediator) : ControllerBase
{
    /// <summary>Get paginated list of activities with optional filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ActivityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? assignedToId = null,
        [FromQuery] Guid? contactId = null,
        [FromQuery] Guid? dealId = null,
        [FromQuery] ActivityStatus? status = null,
        [FromQuery] bool onlyOverdue = false,
        CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetActivitiesQuery(page, pageSize, assignedToId, contactId, dealId, status, onlyOverdue), ct));

    /// <summary>Get all overdue pending activities.</summary>
    [HttpGet("overdue")]
    [ProducesResponseType(typeof(List<ActivityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverdue(CancellationToken ct) =>
        Ok(await mediator.Send(new GetOverdueActivitiesQuery(), ct));

    /// <summary>Get an activity by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetActivityByIdQuery(id), ct));

    /// <summary>Create a new activity.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateActivityCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update an existing activity.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateActivityRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateActivityCommand(id, request.Title, request.Description,
            request.DueDate, request.DurationMinutes, request.ReminderAt), ct));

    /// <summary>Mark an activity as complete.</summary>
    [HttpPatch("{id:guid}/complete")]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteRequest? request, CancellationToken ct) =>
        Ok(await mediator.Send(new CompleteActivityCommand(id, request?.Outcome), ct));

    /// <summary>Cancel an activity.</summary>
    [HttpPatch("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new CancelActivityCommand(id), ct));

    /// <summary>Delete an activity.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteActivityCommand(id), ct);
        return NoContent();
    }
}

public record UpdateActivityRequest(string Title, string? Description, DateTime? DueDate, int? DurationMinutes, DateTime? ReminderAt);
public record CompleteRequest(string? Outcome);