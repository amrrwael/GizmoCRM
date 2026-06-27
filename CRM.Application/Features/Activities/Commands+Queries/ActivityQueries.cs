using CRM.Application.Common.Exceptions;
using CRM.Application.Common.Interfaces;
using CRM.Application.Features.Activities.Commands;
using CRM.Application.Features.Contacts.Commands;
using CRM.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application.Features.Activities.Queries;

public record GetActivitiesQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? AssignedToId = null,
    Guid? ContactId = null,
    Guid? DealId = null,
    ActivityStatus? Status = null,
    bool OnlyOverdue = false) : IRequest<PagedResult<ActivityDto>>;

public record GetActivityByIdQuery(Guid Id) : IRequest<ActivityDto>;

public record GetOverdueActivitiesQuery : IRequest<List<ActivityDto>>;

public class GetActivitiesHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetActivitiesQuery, PagedResult<ActivityDto>>
{
    public async Task<PagedResult<ActivityDto>> Handle(GetActivitiesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Activities.Include(a => a.AssignedTo).Include(a => a.Contact).Include(a => a.Deal).AsQueryable();

        if (currentUser.Role == UserRole.Sales)
            query = query.Where(a => a.AssignedToId == currentUser.UserId);
        else if (request.AssignedToId.HasValue)
            query = query.Where(a => a.AssignedToId == request.AssignedToId.Value);

        if (request.ContactId.HasValue) query = query.Where(a => a.ContactId == request.ContactId.Value);
        if (request.DealId.HasValue) query = query.Where(a => a.DealId == request.DealId.Value);
        if (request.Status.HasValue) query = query.Where(a => a.Status == request.Status.Value);
        if (request.OnlyOverdue)
            query = query.Where(a => a.Status == ActivityStatus.Pending && a.DueDate != null && a.DueDate < DateTime.UtcNow);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(a => a.DueDate ?? DateTime.MaxValue).ThenByDescending(a => a.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new ActivityDto(
                a.Id, a.Type, a.Type.ToString(), a.Status, a.Status.ToString(),
                a.Title, a.Description, a.DueDate, a.CompletedAt, a.Outcome, a.DurationMinutes,
                a.AssignedToId, a.AssignedTo.FirstName + " " + a.AssignedTo.LastName,
                a.ContactId, a.Contact != null ? a.Contact.FirstName + " " + a.Contact.LastName : null,
                a.DealId, a.Deal != null ? a.Deal.Title : null,
                a.HasReminder, a.ReminderAt, a.IsOverdue,
                a.CreatedAt, a.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<ActivityDto>(items, total, request.Page, request.PageSize,
            (int)Math.Ceiling(total / (double)request.PageSize));
    }
}

public class GetActivityByIdHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetActivityByIdQuery, ActivityDto>
{
    public async Task<ActivityDto> Handle(GetActivityByIdQuery request, CancellationToken cancellationToken)
    {
        var a = await db.Activities
            .Include(a => a.AssignedTo).Include(a => a.Contact).Include(a => a.Deal)
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Activity", request.Id);

        if (currentUser.Role == UserRole.Sales && a.AssignedToId != currentUser.UserId)
            throw new ForbiddenException();

        return new ActivityDto(
            a.Id, a.Type, a.Type.ToString(), a.Status, a.Status.ToString(),
            a.Title, a.Description, a.DueDate, a.CompletedAt, a.Outcome, a.DurationMinutes,
            a.AssignedToId, a.AssignedTo.FullName,
            a.ContactId, a.Contact?.FullName,
            a.DealId, a.Deal?.Title,
            a.HasReminder, a.ReminderAt, a.IsOverdue,
            a.CreatedAt, a.UpdatedAt);
    }
}

public class GetOverdueActivitiesHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetOverdueActivitiesQuery, List<ActivityDto>>
{
    public async Task<List<ActivityDto>> Handle(GetOverdueActivitiesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Activities
            .Include(a => a.AssignedTo).Include(a => a.Contact).Include(a => a.Deal)
            .Where(a => a.Status == ActivityStatus.Pending && a.DueDate != null && a.DueDate < DateTime.UtcNow);

        if (currentUser.Role == UserRole.Sales)
            query = query.Where(a => a.AssignedToId == currentUser.UserId);

        return await query
            .OrderBy(a => a.DueDate)
            .Select(a => new ActivityDto(
                a.Id, a.Type, a.Type.ToString(), a.Status, a.Status.ToString(),
                a.Title, a.Description, a.DueDate, a.CompletedAt, a.Outcome, a.DurationMinutes,
                a.AssignedToId, a.AssignedTo.FirstName + " " + a.AssignedTo.LastName,
                a.ContactId, a.Contact != null ? a.Contact.FirstName + " " + a.Contact.LastName : null,
                a.DealId, a.Deal != null ? a.Deal.Title : null,
                a.HasReminder, a.ReminderAt, a.IsOverdue,
                a.CreatedAt, a.UpdatedAt))
            .ToListAsync(cancellationToken);
    }
}