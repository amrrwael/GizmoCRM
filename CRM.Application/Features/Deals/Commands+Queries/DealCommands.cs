using CRM.Application.Common.Exceptions;
using CRM.Application.Common.Interfaces;
using CRM.Domain.Entities;
using CRM.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application.Features.Deals.Commands;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record DealDto(
    Guid Id,
    string Title,
    decimal Value,
    DealStage Stage,
    string StageName,
    int Probability,
    Guid OwnerId,
    string OwnerName,
    Guid ContactId,
    string ContactName,
    DateTime? ExpectedCloseDate,
    string? Description,
    string? LostReason,
    DateTime? ClosedAt,
    bool IsOpen,
    int ActivityCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// ── Commands ───────────────────────────────────────────────────────────────────

public record CreateDealCommand(
    string Title,
    decimal Value,
    Guid ContactId,
    Guid? OwnerId,
    DateTime? ExpectedCloseDate,
    string? Description) : IRequest<DealDto>;

public class CreateDealValidator : AbstractValidator<CreateDealCommand>
{
    public CreateDealValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ContactId).NotEmpty();
    }
}

public record UpdateDealCommand(
    Guid Id,
    string Title,
    decimal Value,
    DateTime? ExpectedCloseDate,
    string? Description) : IRequest<DealDto>;

public class UpdateDealValidator : AbstractValidator<UpdateDealCommand>
{
    public UpdateDealValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
    }
}

public record MoveDealStageCommand(Guid Id, DealStage NewStage, string? LostReason) : IRequest<DealDto>;

public class MoveDealStageValidator : AbstractValidator<MoveDealStageCommand>
{
    public MoveDealStageValidator()
    {
        RuleFor(x => x.NewStage).IsInEnum();
        RuleFor(x => x.LostReason)
            .NotEmpty().When(x => x.NewStage == DealStage.Lost)
            .WithMessage("Lost reason is required when marking a deal as lost.");
    }
}

public record ReassignDealCommand(Guid DealId, Guid NewOwnerId) : IRequest<DealDto>;

public record DeleteDealCommand(Guid Id) : IRequest;

// ── Handlers ───────────────────────────────────────────────────────────────────

public class CreateDealHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateDealCommand, DealDto>
{
    public async Task<DealDto> Handle(CreateDealCommand request, CancellationToken cancellationToken)
    {
        var contactExists = await db.Contacts.AnyAsync(c => c.Id == request.ContactId, cancellationToken);
        if (!contactExists) throw new NotFoundException("Contact", request.ContactId);

        var ownerId = request.OwnerId ?? currentUser.UserId;

        if (currentUser.Role == UserRole.Sales && ownerId != currentUser.UserId)
            throw new ForbiddenException("Sales users can only create deals for themselves.");

        var deal = Deal.Create(request.Title, request.Value, ownerId, request.ContactId,
            request.ExpectedCloseDate, request.Description, currentUser.UserId);

        db.Deals.Add(deal);
        await db.SaveChangesAsync(cancellationToken);

        return await LoadDealDto(db, deal.Id, cancellationToken);
    }
}

public class UpdateDealHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateDealCommand, DealDto>
{
    public async Task<DealDto> Handle(UpdateDealCommand request, CancellationToken cancellationToken)
    {
        var deal = await db.Deals.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException("Deal", request.Id);

        EnsureDealAccess(currentUser, deal);

        deal.UpdateDetails(request.Title, request.Value, request.ExpectedCloseDate, request.Description);
        await db.SaveChangesAsync(cancellationToken);

        return await LoadDealDto(db, deal.Id, cancellationToken);
    }
}

public class MoveDealStageHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<MoveDealStageCommand, DealDto>
{
    public async Task<DealDto> Handle(MoveDealStageCommand request, CancellationToken cancellationToken)
    {
        var deal = await db.Deals.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException("Deal", request.Id);

        EnsureDealAccess(currentUser, deal);

        deal.MoveToStage(request.NewStage, request.LostReason);
        await db.SaveChangesAsync(cancellationToken);

        return await LoadDealDto(db, deal.Id, cancellationToken);
    }
}

public class ReassignDealHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<ReassignDealCommand, DealDto>
{
    public async Task<DealDto> Handle(ReassignDealCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Role == UserRole.Sales) throw new ForbiddenException();

        var deal = await db.Deals.FindAsync([request.DealId], cancellationToken)
            ?? throw new NotFoundException("Deal", request.DealId);

        var userExists = await db.Users.AnyAsync(u => u.Id == request.NewOwnerId && u.IsActive, cancellationToken);
        if (!userExists) throw new NotFoundException("User", request.NewOwnerId);

        deal.Reassign(request.NewOwnerId);
        await db.SaveChangesAsync(cancellationToken);

        return await LoadDealDto(db, deal.Id, cancellationToken);
    }
}

public class DeleteDealHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<DeleteDealCommand>
{
    public async Task Handle(DeleteDealCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Role == UserRole.Sales) throw new ForbiddenException("Sales users cannot delete deals.");

        var deal = await db.Deals.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException("Deal", request.Id);

        db.Deals.Remove(deal);
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ── Shared helpers ──────────────────────────────────────────────────────────────

internal static class DealHelper
{
    public static void EnsureDealAccess(ICurrentUserService currentUser, Deal deal)
    {
        if (currentUser.Role == UserRole.Sales && deal.OwnerId != currentUser.UserId)
            throw new ForbiddenException("You can only modify deals assigned to you.");
    }

    public static async Task<DealDto> LoadDealDto(IApplicationDbContext db, Guid id, CancellationToken ct)
    {
        return await db.Deals
            .Include(d => d.Owner)
            .Include(d => d.Contact)
            .Include(d => d.Activities)
            .Where(d => d.Id == id)
            .Select(d => new DealDto(
                d.Id, d.Title, d.Value, d.Stage, d.Stage.ToString(), d.Probability,
                d.OwnerId, d.Owner.FirstName + " " + d.Owner.LastName,
                d.ContactId, d.Contact.FirstName + " " + d.Contact.LastName,
                d.ExpectedCloseDate, d.Description, d.LostReason, d.ClosedAt, d.IsOpen,
                d.Activities.Count, d.CreatedAt, d.UpdatedAt))
            .FirstAsync(ct);
    }
}