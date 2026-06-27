using CRM.Application.Common.Exceptions;
using CRM.Application.Common.Interfaces;
using CRM.Application.Features.Contacts.Commands;
using CRM.Application.Features.Deals.Commands;
using CRM.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application.Features.Deals.Queries;

public record GetDealsQuery(
	int Page = 1,
	int PageSize = 20,
	DealStage? Stage = null,
	Guid? OwnerId = null,
	string? Search = null) : IRequest<PagedResult<DealDto>>;

public record GetDealByIdQuery(Guid Id) : IRequest<DealDto>;

public record GetKanbanBoardQuery : IRequest<KanbanBoardDto>;

public record KanbanBoardDto(
	KanbanColumnDto Lead,
	KanbanColumnDto Qualified,
	KanbanColumnDto Proposal,
	KanbanColumnDto Negotiation,
	KanbanColumnDto Won,
	KanbanColumnDto Lost);

public record KanbanColumnDto(DealStage Stage, string Label, List<DealDto> Deals, decimal TotalValue, int Count);

public class GetDealsHandler(IApplicationDbContext db, ICurrentUserService currentUser)
	: IRequestHandler<GetDealsQuery, PagedResult<DealDto>>
{
	public async Task<PagedResult<DealDto>> Handle(GetDealsQuery request, CancellationToken cancellationToken)
	{
		var query = db.Deals.Include(d => d.Owner).Include(d => d.Contact).AsQueryable();

		if (currentUser.Role == UserRole.Sales)
			query = query.Where(d => d.OwnerId == currentUser.UserId);
		else if (request.OwnerId.HasValue)
			query = query.Where(d => d.OwnerId == request.OwnerId.Value);

		if (request.Stage.HasValue)
			query = query.Where(d => d.Stage == request.Stage.Value);

		if (!string.IsNullOrWhiteSpace(request.Search))
		{
			var s = request.Search.ToLowerInvariant();
			query = query.Where(d => d.Title.ToLower().Contains(s));
		}

		var total = await query.CountAsync(cancellationToken);
		var items = await query
			.OrderByDescending(d => d.CreatedAt)
			.Skip((request.Page - 1) * request.PageSize)
			.Take(request.PageSize)
			.Select(d => new DealDto(
				d.Id, d.Title, d.Value, d.Stage, d.Stage.ToString(), d.Probability,
				d.OwnerId, d.Owner.FirstName + " " + d.Owner.LastName,
				d.ContactId, d.Contact.FirstName + " " + d.Contact.LastName,
				d.ExpectedCloseDate, d.Description, d.LostReason, d.ClosedAt, d.IsOpen,
				d.Activities.Count, d.CreatedAt, d.UpdatedAt))
			.ToListAsync(cancellationToken);

		return new PagedResult<DealDto>(items, total, request.Page, request.PageSize,
			(int)Math.Ceiling(total / (double)request.PageSize));
	}
}

public class GetDealByIdHandler(IApplicationDbContext db, ICurrentUserService currentUser)
	: IRequestHandler<GetDealByIdQuery, DealDto>
{
	public async Task<DealDto> Handle(GetDealByIdQuery request, CancellationToken cancellationToken)
	{
		var deal = await db.Deals
			.Include(d => d.Owner).Include(d => d.Contact).Include(d => d.Activities)
			.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
			?? throw new NotFoundException("Deal", request.Id);

		if (currentUser.Role == UserRole.Sales && deal.OwnerId != currentUser.UserId)
			throw new ForbiddenException();

		return new DealDto(
			deal.Id, deal.Title, deal.Value, deal.Stage, deal.Stage.ToString(), deal.Probability,
			deal.OwnerId, deal.Owner.FullName, deal.ContactId, deal.Contact.FullName,
			deal.ExpectedCloseDate, deal.Description, deal.LostReason, deal.ClosedAt, deal.IsOpen,
			deal.Activities.Count, deal.CreatedAt, deal.UpdatedAt);
	}
}

public class GetKanbanBoardHandler(IApplicationDbContext db, ICurrentUserService currentUser)
	: IRequestHandler<GetKanbanBoardQuery, KanbanBoardDto>
{
	public async Task<KanbanBoardDto> Handle(GetKanbanBoardQuery request, CancellationToken cancellationToken)
	{
		var query = db.Deals.Include(d => d.Owner).Include(d => d.Contact).AsQueryable();

		if (currentUser.Role == UserRole.Sales)
			query = query.Where(d => d.OwnerId == currentUser.UserId);

		var allDeals = await query
			.OrderByDescending(d => d.Value)
			.Select(d => new DealDto(
				d.Id, d.Title, d.Value, d.Stage, d.Stage.ToString(), d.Probability,
				d.OwnerId, d.Owner.FirstName + " " + d.Owner.LastName,
				d.ContactId, d.Contact.FirstName + " " + d.Contact.LastName,
				d.ExpectedCloseDate, d.Description, d.LostReason, d.ClosedAt, d.IsOpen,
				d.Activities.Count, d.CreatedAt, d.UpdatedAt))
			.ToListAsync(cancellationToken);

		KanbanColumnDto BuildColumn(DealStage stage, string label)
		{
			var deals = allDeals.Where(d => d.Stage == stage).ToList();
			return new KanbanColumnDto(stage, label, deals, deals.Sum(d => d.Value), deals.Count);
		}

		return new KanbanBoardDto(
			BuildColumn(DealStage.Lead, "Lead"),
			BuildColumn(DealStage.Qualified, "Qualified"),
			BuildColumn(DealStage.Proposal, "Proposal"),
			BuildColumn(DealStage.Negotiation, "Negotiation"),
			BuildColumn(DealStage.Won, "Won"),
			BuildColumn(DealStage.Lost, "Lost"));
	}
}