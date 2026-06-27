using CRM.Application.Features.Contacts.Commands;
using CRM.Application.Features.Deals.Commands;
using CRM.Application.Features.Deals.Queries;
using CRM.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class DealsController(IMediator mediator) : ControllerBase
{
    /// <summary>Get paginated list of deals with optional filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DealDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DealStage? stage = null,
        [FromQuery] Guid? ownerId = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetDealsQuery(page, pageSize, stage, ownerId, search), ct));

    /// <summary>Get the Kanban board view — all deals organized by stage.</summary>
    [HttpGet("kanban")]
    [ProducesResponseType(typeof(KanbanBoardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKanban(CancellationToken ct) =>
        Ok(await mediator.Send(new GetKanbanBoardQuery(), ct));

    /// <summary>Get a deal by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DealDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetDealByIdQuery(id), ct));

    /// <summary>Create a new deal.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(DealDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateDealCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update deal details.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DealDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDealRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateDealCommand(id, request.Title, request.Value, request.ExpectedCloseDate, request.Description), ct));

    /// <summary>Move a deal to a new pipeline stage (drag & drop).</summary>
    [HttpPatch("{id:guid}/stage")]
    [ProducesResponseType(typeof(DealDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> MoveStage(Guid id, [FromBody] MoveStageRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new MoveDealStageCommand(id, request.Stage, request.LostReason), ct));

    /// <summary>Reassign a deal to a different owner (Admin/Manager only).</summary>
    [HttpPatch("{id:guid}/owner")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(DealDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reassign(Guid id, [FromBody] ReassignRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new ReassignDealCommand(id, request.NewOwnerId), ct));

    /// <summary>Delete a deal (Admin/Manager only).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteDealCommand(id), ct);
        return NoContent();
    }
}

public record UpdateDealRequest(string Title, decimal Value, DateTime? ExpectedCloseDate, string? Description);
public record MoveStageRequest(DealStage Stage, string? LostReason);
public record ReassignRequest(Guid NewOwnerId);