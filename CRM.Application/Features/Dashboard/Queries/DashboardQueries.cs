using CRM.Application.Common.Interfaces;
using CRM.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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

public class GetDashboardHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    public async Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var isSales = currentUser.Role == UserRole.Sales;

        // Summary counts
        var contactsQuery = db.Contacts.AsQueryable();
        var dealsQuery = db.Deals.AsQueryable();
        var activitiesQuery = db.Activities.AsQueryable();

        if (isSales)
        {
            contactsQuery = contactsQuery.Where(c => c.AssignedToId == currentUser.UserId || c.CreatedBy == currentUser.UserId);
            dealsQuery = dealsQuery.Where(d => d.OwnerId == currentUser.UserId);
            activitiesQuery = activitiesQuery.Where(a => a.AssignedToId == currentUser.UserId);
        }

        var summary = new SummaryDto(
            await contactsQuery.CountAsync(cancellationToken),
            await dealsQuery.CountAsync(cancellationToken),
            await dealsQuery.CountAsync(d => d.Stage != DealStage.Won && d.Stage != DealStage.Lost, cancellationToken),
            await activitiesQuery.CountAsync(cancellationToken),
            await activitiesQuery.CountAsync(a => a.Status == ActivityStatus.Pending, cancellationToken));

        // Deals by stage
        var dealsByStage = await dealsQuery
            .GroupBy(d => d.Stage)
            .Select(g => new StageCountDto(g.Key, g.Key.ToString(), g.Count(), g.Sum(d => d.Value)))
            .ToListAsync(cancellationToken);

        // Pipeline value
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
            topReps = await db.Users
                .Where(u => u.Role == UserRole.Sales && u.IsActive)
                .Select(u => new UserPerformanceDto(
                    u.Id,
                    u.FirstName + " " + u.LastName,
                    u.OwnedDeals.Count,
                    u.OwnedDeals.Count(d => d.Stage == DealStage.Won),
                    u.OwnedDeals.Where(d => d.Stage == DealStage.Won).Sum(d => d.Value),
                    u.OwnedDeals.Any() ? (double)u.OwnedDeals.Count(d => d.Stage == DealStage.Won) / u.OwnedDeals.Count * 100 : 0))
                .OrderByDescending(u => u.WonValue)
                .Take(5)
                .ToListAsync(cancellationToken);
        }

        // Recent activities
        var recentActivities = await activitiesQuery
            .Include(a => a.AssignedTo)
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .Take(10)
            .Select(a => new RecentActivityDto(
                a.Id, a.Type.ToString(), a.Title,
                a.AssignedTo.FirstName + " " + a.AssignedTo.LastName,
                a.UpdatedAt ?? a.CreatedAt))
            .ToListAsync(cancellationToken);

        // Upcoming activities (next 7 days)
        var next7Days = DateTime.UtcNow.AddDays(7);
        var upcoming = await activitiesQuery
            .Include(a => a.AssignedTo).Include(a => a.Contact)
            .Where(a => a.Status == ActivityStatus.Pending && a.DueDate >= DateTime.UtcNow && a.DueDate <= next7Days)
            .OrderBy(a => a.DueDate)
            .Take(10)
            .Select(a => new UpcomingActivityDto(
                a.Id, a.Title, a.Type, a.DueDate!.Value,
                a.AssignedTo.FirstName + " " + a.AssignedTo.LastName,
                a.Contact != null ? a.Contact.FirstName + " " + a.Contact.LastName : null))
            .ToListAsync(cancellationToken);

        return new DashboardDto(summary, dealsByStage, topReps, recentActivities, upcoming,
            pipelineValue, wonRevenue, overdueCount);
    }
}