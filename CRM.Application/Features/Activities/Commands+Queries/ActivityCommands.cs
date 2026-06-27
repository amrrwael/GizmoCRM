using CRM.Application.Common.Exceptions;
using CRM.Application.Common.Interfaces;
using CRM.Domain.Entities;
using CRM.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application.Features.Activities.Commands;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record ActivityDto(
    Guid Id,
    ActivityType Type,
    string TypeName,
    ActivityStatus Status,
    string StatusName,
    string Title,
    string? Description,
    DateTime? DueDate,
    DateTime? CompletedAt,
    string? Outcome,
    int? DurationMinutes,
    Guid AssignedToId,
    string AssignedToName,
    Guid? ContactId,
    string? ContactName,
    Guid? DealId,
    string? DealTitle,
    bool HasReminder,
    DateTime? ReminderAt,
    bool IsOverdue,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// ── Commands ───────────────────────────────────────────────────────────────────

public record CreateActivityCommand(
    ActivityType Type,
    string Title,
    string? Description,
    DateTime? DueDate,
    Guid? AssignedToId,
    Guid? ContactId,
    Guid? DealId,
    DateTime? ReminderAt) : IRequest<ActivityDto>;

public class CreateActivityValidator : AbstractValidator<CreateActivityCommand>
{
    public CreateActivityValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactId.HasValue || x.DealId.HasValue)
            .Equal(true).WithMessage("Activity must be linked to a Contact or a Deal.");
    }
}

public record UpdateActivityCommand(
    Guid Id,
    string Title,
    string? Description,
    DateTime? DueDate,
    int? DurationMinutes,
    DateTime? ReminderAt) : IRequest<ActivityDto>;

public record CompleteActivityCommand(Guid Id, string? Outcome) : IRequest<ActivityDto>;

public record CancelActivityCommand(Guid Id) : IRequest<ActivityDto>;

public record DeleteActivityCommand(Guid Id) : IRequest;

// ── Handlers ───────────────────────────────────────────────────────────────────

public class CreateActivityHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateActivityCommand, ActivityDto>
{
    public async Task<ActivityDto> Handle(CreateActivityCommand request, CancellationToken cancellationToken)
    {
        var assignedToId = request.AssignedToId ?? currentUser.UserId;

        if (currentUser.Role == UserRole.Sales && assignedToId != currentUser.UserId)
            throw new ForbiddenException("Sales users can only create activities for themselves.");

        if (request.ContactId.HasValue && !await db.Contacts.AnyAsync(c => c.Id == request.ContactId.Value, cancellationToken))
            throw new NotFoundException("Contact", request.ContactId.Value);

        if (request.DealId.HasValue && !await db.Deals.AnyAsync(d => d.Id == request.DealId.Value, cancellationToken))
            throw new NotFoundException("Deal", request.DealId.Value);

        var activity = Activity.Create(request.Type, request.Title, request.Description,
            request.DueDate, assignedToId, request.ContactId, request.DealId, currentUser.UserId);

        if (request.ReminderAt.HasValue)
            activity.SetReminder(request.ReminderAt.Value);

        db.Activities.Add(activity);
        await db.SaveChangesAsync(cancellationToken);

        return await LoadDto(db, activity.Id, cancellationToken);
    }
}

public class UpdateActivityHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateActivityCommand, ActivityDto>
{
    public async Task<ActivityDto> Handle(UpdateActivityCommand request, CancellationToken cancellationToken)
    {
        var activity = await db.Activities.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException("Activity", request.Id);

        EnsureAccess(currentUser, activity);

        activity.Update(request.Title, request.Description, request.DueDate, request.DurationMinutes);

        if (request.ReminderAt.HasValue)
            activity.SetReminder(request.ReminderAt.Value);
        else
            activity.ClearReminder();

        await db.SaveChangesAsync(cancellationToken);
        return await LoadDto(db, activity.Id, cancellationToken);
    }
}

public class CompleteActivityHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CompleteActivityCommand, ActivityDto>
{
    public async Task<ActivityDto> Handle(CompleteActivityCommand request, CancellationToken cancellationToken)
    {
        var activity = await db.Activities.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException("Activity", request.Id);

        EnsureAccess(currentUser, activity);

        if (activity.Status == ActivityStatus.Completed)
            throw new ConflictException("Activity is already completed.");

        activity.Complete(request.Outcome);
        await db.SaveChangesAsync(cancellationToken);
        return await LoadDto(db, activity.Id, cancellationToken);
    }
}

public class CancelActivityHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CancelActivityCommand, ActivityDto>
{
    public async Task<ActivityDto> Handle(CancelActivityCommand request, CancellationToken cancellationToken)
    {
        var activity = await db.Activities.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException("Activity", request.Id);

        EnsureAccess(currentUser, activity);
        activity.Cancel();
        await db.SaveChangesAsync(cancellationToken);
        return await LoadDto(db, activity.Id, cancellationToken);
    }
}

public class DeleteActivityHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<DeleteActivityCommand>
{
    public async Task Handle(DeleteActivityCommand request, CancellationToken cancellationToken)
    {
        var activity = await db.Activities.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException("Activity", request.Id);

        EnsureAccess(currentUser, activity);
        db.Activities.Remove(activity);
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ── Shared helpers ──────────────────────────────────────────────────────────────

internal static class ActivityHelper
{
    public static void EnsureAccess(ICurrentUserService currentUser, Activity activity)
    {
        if (currentUser.Role == UserRole.Sales && activity.AssignedToId != currentUser.UserId)
            throw new ForbiddenException("You can only modify activities assigned to you.");
    }

    public static async Task<ActivityDto> LoadDto(IApplicationDbContext db, Guid id, CancellationToken ct)
    {
        return await db.Activities
            .Include(a => a.AssignedTo)
            .Include(a => a.Contact)
            .Include(a => a.Deal)
            .Where(a => a.Id == id)
            .Select(a => new ActivityDto(
                a.Id, a.Type, a.Type.ToString(), a.Status, a.Status.ToString(),
                a.Title, a.Description, a.DueDate, a.CompletedAt, a.Outcome, a.DurationMinutes,
                a.AssignedToId, a.AssignedTo.FirstName + " " + a.AssignedTo.LastName,
                a.ContactId, a.Contact != null ? a.Contact.FirstName + " " + a.Contact.LastName : null,
                a.DealId, a.Deal != null ? a.Deal.Title : null,
                a.HasReminder, a.ReminderAt, a.IsOverdue,
                a.CreatedAt, a.UpdatedAt))
            .FirstAsync(ct);
    }
}