using CRM.Application.Common.Interfaces;
using CRM.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application.Features.Dashboard.Queries;

public record GetDashboardQuery : IRequest<DashboardDto>;

public record DashboardDto(
    SummaryDto Summary,
    List<StageCountDto> DealsByStage,
    List<UserPerformanceDto> TopSalesReps,
    List<RecentActivityDto> RecentActivities,
    List<UpcomingActivityDto> UpcomingActivities,
    decimal TotalPipelineValue,
    decimal WonRevenueThisMonth,
    int OverdueActivitiesCount);

public record SummaryDto(
    int TotalContacts,
    int TotalDeals,
    int OpenDeals,
    int TotalActivities,
    int PendingActivities);

public record StageCountDto(DealStage Stage, string Label, int Count, decimal TotalValue);

public record UserPerformanceDto(
    Guid UserId,
    string FullName,
    int TotalDeals,
    int WonDeals,
    decimal WonValue,
    decimal WinRate);

public record RecentActivityDto(
    Guid Id,
    string Type,
    string Title,
    string ActorName,
    DateTime Timestamp);

public record UpcomingActivityDto(
    Guid Id,
    string Title,
    ActivityType Type,
    DateTime DueDate,
    string AssignedTo,
    string? ContactName);

public class GetDashboardHandler : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDashboardHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if user is authenticated and has a valid ID
            if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            {
                return GetEmptyDashboard("User not authenticated");
            }

            var userId = _currentUser.UserId;
            var isSales = _currentUser.Role == UserRole.Sales;

            // Summary counts
            var contactsQuery = _db.Contacts.AsQueryable();
            var dealsQuery = _db.Deals.AsQueryable();
            var activitiesQuery = _db.Activities.AsQueryable();

            if (isSales)
            {
                contactsQuery = contactsQuery.Where(c => c.AssignedToId == userId || c.CreatedBy == userId);
                dealsQuery = dealsQuery.Where(d => d.OwnerId == userId);
                activitiesQuery = activitiesQuery.Where(a => a.AssignedToId == userId);
            }

            // Get counts safely
            var totalContacts = await contactsQuery.CountAsync(cancellationToken);
            var totalDeals = await dealsQuery.CountAsync(cancellationToken);
            var openDeals = await dealsQuery.CountAsync(d => d.Stage != DealStage.Won && d.Stage != DealStage.Lost, cancellationToken);
            var totalActivities = await activitiesQuery.CountAsync(cancellationToken);
            var pendingActivities = await activitiesQuery.CountAsync(a => a.Status == ActivityStatus.Pending, cancellationToken);

            var summary = new SummaryDto(
                totalContacts,
                totalDeals,
                openDeals,
                totalActivities,
                pendingActivities);

            // Deals by stage
            var dealsByStage = await dealsQuery
                .GroupBy(d => d.Stage)
                .Select(g => new StageCountDto(g.Key, g.Key.ToString(), g.Count(), g.Sum(d => d.Value)))
                .ToListAsync(cancellationToken);

            // Pipeline value (handle null)
            var pipelineValue = await dealsQuery
                .Where(d => d.Stage != DealStage.Won && d.Stage != DealStage.Lost)
                .SumAsync(d => d.Value, cancellationToken);

            // Won revenue this month
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var wonRevenue = await dealsQuery
                .Where(d => d.Stage == DealStage.Won && d.ClosedAt >= startOfMonth)
                .SumAsync(d => d.Value, cancellationToken);

            // Overdue activities count
            var overdueCount = await activitiesQuery
                .CountAsync(a => a.Status == ActivityStatus.Pending && a.DueDate < DateTime.UtcNow, cancellationToken);

            // Top sales reps (Admin/Manager only)
            var topReps = new List<UserPerformanceDto>();
            if (!isSales)
            {
                try
                {
                    topReps = await _db.Users
                        .Where(u => u.Role == UserRole.Sales && u.IsActive)
                        .Select(u => new UserPerformanceDto(
                            u.Id,
                            u.FirstName + " " + u.LastName,
                            u.OwnedDeals.Count,
                            u.OwnedDeals.Count(d => d.Stage == DealStage.Won),
                            u.OwnedDeals.Where(d => d.Stage == DealStage.Won).Sum(d => d.Value),
                            u.OwnedDeals.Any() ? (decimal)u.OwnedDeals.Count(d => d.Stage == DealStage.Won) / u.OwnedDeals.Count * 100 : 0m))
                        .OrderByDescending(u => u.WonValue)
                        .Take(5)
                        .ToListAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    // If this fails, continue with empty list
                    Console.WriteLine($"Error getting top sales reps: {ex.Message}");
                    topReps = new List<UserPerformanceDto>();
                }
            }

            // Recent activities - handle null assigned to
            var recentActivities = await activitiesQuery
                .Include(a => a.AssignedTo)
                .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
                .Take(10)
                .Select(a => new RecentActivityDto(
                    a.Id,
                    a.Type.ToString(),
                    a.Title,
                    a.AssignedTo != null ? a.AssignedTo.FirstName + " " + a.AssignedTo.LastName : "Unknown",
                    a.UpdatedAt ?? a.CreatedAt))
                .ToListAsync(cancellationToken);

            // Upcoming activities (next 7 days)
            var next7Days = DateTime.UtcNow.AddDays(7);
            var upcoming = await activitiesQuery
                .Include(a => a.AssignedTo)
                .Include(a => a.Contact)
                .Where(a => a.Status == ActivityStatus.Pending && a.DueDate >= DateTime.UtcNow && a.DueDate <= next7Days)
                .OrderBy(a => a.DueDate)
                .Take(10)
                .Select(a => new UpcomingActivityDto(
                    a.Id,
                    a.Title,
                    a.Type,
                    a.DueDate!.Value,
                    a.AssignedTo != null ? a.AssignedTo.FirstName + " " + a.AssignedTo.LastName : "Unknown",
                    a.Contact != null ? a.Contact.FirstName + " " + a.Contact.LastName : null))
                .ToListAsync(cancellationToken);

            return new DashboardDto(
                summary,
                dealsByStage,
                topReps,
                recentActivities,
                upcoming,
                pipelineValue,
                wonRevenue,
                overdueCount);
        }
        catch (Exception ex)
        {
            // Log the error
            Console.WriteLine($"Dashboard error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");

            // Return empty dashboard instead of failing
            return GetEmptyDashboard(ex.Message);
        }
    }

    private DashboardDto GetEmptyDashboard(string errorMessage = "")
    {
        var emptySummary = new SummaryDto(0, 0, 0, 0, 0);
        return new DashboardDto(
            emptySummary,
            new List<StageCountDto>(),
            new List<UserPerformanceDto>(),
            new List<RecentActivityDto>(),
            new List<UpcomingActivityDto>(),
            0,
            0,
            0);
    }
}