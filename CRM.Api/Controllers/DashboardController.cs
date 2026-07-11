// CRM.Api/Controllers/DashboardController.cs
using CRM.Application.Features.Dashboard.Queries;
using CRM.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CRM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IMediator mediator, ILogger<DashboardController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>Get the CRM dashboard with summary stats, pipeline data, and upcoming activities.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Fetching dashboard data...");
            var result = await _mediator.Send(new GetDashboardQuery(), ct);
            _logger.LogInformation("Dashboard data fetched successfully");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching dashboard data");
            return StatusCode(500, new
            {
                error = "Error fetching dashboard data",
                details = ex.Message
            });
        }
    }

    /// <summary>Debug endpoint to check authentication claims</summary>
    [HttpGet("debug")]
    [AllowAnonymous]
    public IActionResult Debug()
    {
        var user = HttpContext.User;
        var isAuthenticated = user?.Identity?.IsAuthenticated ?? false;

        var claims = user?.Claims.Select(c => new { c.Type, c.Value }).ToList();

        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user?.FindFirstValue("sub")
            ?? user?.FindFirstValue("userId");

        var role = user?.FindFirstValue(ClaimTypes.Role)
            ?? user?.FindFirstValue("role");

        return Ok(new
        {
            IsAuthenticated = isAuthenticated,
            UserId = userId,
            Role = role,
            Claims = claims,
            HasValidUserId = Guid.TryParse(userId, out _),
            HasValidRole = Enum.TryParse<UserRole>(role, true, out _)
        });
    }
}