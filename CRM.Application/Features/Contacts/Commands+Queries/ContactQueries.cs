using CRM.Application.Common.Exceptions;
using CRM.Application.Common.Interfaces;
using CRM.Application.Features.Contacts.Commands;
using CRM.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application.Features.Contacts.Queries;

public record GetContactsQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Tag = null)
    : IRequest<PagedResult<ContactDto>>;

public record GetContactByIdQuery(Guid Id) : IRequest<ContactDto>;

public record GetContactTimelineQuery(Guid ContactId, int Page = 1, int PageSize = 30)
    : IRequest<PagedResult<TimelineItemDto>>;

public record TimelineItemDto(
    Guid Id,
    string ItemType,
    string Title,
    string? Description,
    string? ActorName,
    DateTime Timestamp);

public class GetContactsHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetContactsQuery, PagedResult<ContactDto>>
{
    public async Task<PagedResult<ContactDto>> Handle(GetContactsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Contacts.AsQueryable();

        // Role-based filter: Sales sees only assigned/created contacts
        if (currentUser.Role == UserRole.Sales)
            query = query.Where(c => c.AssignedToId == currentUser.UserId || c.CreatedBy == currentUser.UserId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.ToLowerInvariant();
            query = query.Where(c =>
                c.FirstName.ToLower().Contains(s) ||
                c.LastName.ToLower().Contains(s) ||
                c.Email.Contains(s) ||
                (c.Company != null && c.Company.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(request.Tag))
            query = query.Where(c => c.Tags.Contains(request.Tag));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.FirstName).ThenBy(c => c.LastName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new ContactDto(
                c.Id, c.FirstName, c.LastName, c.FirstName + " " + c.LastName,
                c.Email, c.Phone, c.Company, c.Position, c.Notes, c.AvatarUrl,
                c.Tags,
                c.AssignedToId,
                c.AssignedTo != null ? c.AssignedTo.FirstName + " " + c.AssignedTo.LastName : null,
                c.Deals.Count, c.Activities.Count,
                c.CreatedAt, c.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<ContactDto>(items, total, request.Page, request.PageSize,
            (int)Math.Ceiling(total / (double)request.PageSize));
    }
}

public class GetContactByIdHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetContactByIdQuery, ContactDto>
{
    public async Task<ContactDto> Handle(GetContactByIdQuery request, CancellationToken cancellationToken)
    {
        var contact = await db.Contacts
            .Include(c => c.AssignedTo)
            .Include(c => c.Deals)
            .Include(c => c.Activities)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Contact", request.Id);

        if (currentUser.Role == UserRole.Sales &&
            contact.AssignedToId != currentUser.UserId &&
            contact.CreatedBy != currentUser.UserId)
            throw new ForbiddenException();

        return new ContactDto(
            contact.Id, contact.FirstName, contact.LastName, contact.FullName,
            contact.Email, contact.Phone, contact.Company, contact.Position,
            contact.Notes, contact.AvatarUrl, contact.Tags,
            contact.AssignedToId,
            contact.AssignedTo?.FullName,
            contact.Deals.Count, contact.Activities.Count,
            contact.CreatedAt, contact.UpdatedAt);
    }
}

public class GetContactTimelineHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetContactTimelineQuery, PagedResult<TimelineItemDto>>
{
    public async Task<PagedResult<TimelineItemDto>> Handle(GetContactTimelineQuery request, CancellationToken cancellationToken)
    {
        var contact = await db.Contacts.FindAsync([request.ContactId], cancellationToken)
            ?? throw new NotFoundException("Contact", request.ContactId);

        if (currentUser.Role == UserRole.Sales &&
            contact.AssignedToId != currentUser.UserId &&
            contact.CreatedBy != currentUser.UserId)
            throw new ForbiddenException();

        var activities = await db.Activities
            .Include(a => a.AssignedTo)
            .Where(a => a.ContactId == request.ContactId)
            .Select(a => new TimelineItemDto(
                a.Id, "Activity", a.Title, a.Description,
                a.AssignedTo.FirstName + " " + a.AssignedTo.LastName,
                a.CompletedAt ?? a.CreatedAt))
            .ToListAsync(cancellationToken);

        var deals = await db.Deals
            .Include(d => d.Owner)
            .Where(d => d.ContactId == request.ContactId)
            .Select(d => new TimelineItemDto(
                d.Id, "Deal", d.Title, d.Description,
                d.Owner.FirstName + " " + d.Owner.LastName,
                d.CreatedAt))
            .ToListAsync(cancellationToken);

        var timeline = activities.Concat(deals)
            .OrderByDescending(x => x.Timestamp)
            .ToList();

        var total = timeline.Count;
        var paged = timeline
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PagedResult<TimelineItemDto>(paged, total, request.Page, request.PageSize,
            (int)Math.Ceiling(total / (double)request.PageSize));
    }
}