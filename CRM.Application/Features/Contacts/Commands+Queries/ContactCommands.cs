using CRM.Application.Common.Exceptions;
using CRM.Application.Common.Interfaces;
using CRM.Domain.Entities;
using CRM.Application.Features.Contacts.Commands;
using CRM.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application.Features.Contacts.Commands;
// ── DTOs ──────────────────────────────────────────────────────────────────────

public record ContactDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? Phone,
    string? Company,
    string? Position,
    string? Notes,
    string? AvatarUrl,
    IReadOnlyList<string> Tags,
    Guid? AssignedToId,
    string? AssignedToName,
    int DealCount,
    int ActivityCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize, int TotalPages);

// ── Commands ───────────────────────────────────────────────────────────────────

public record CreateContactCommand(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Company,
    string? Position,
    string? Notes,
    List<string>? Tags) : IRequest<ContactDto>;

public class CreateContactValidator : AbstractValidator<CreateContactCommand>
{
    public CreateContactValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Company).MaximumLength(200);
    }
}

public record UpdateContactCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Company,
    string? Position,
    string? Notes,
    List<string>? Tags) : IRequest<ContactDto>;

public class UpdateContactValidator : AbstractValidator<UpdateContactCommand>
{
    public UpdateContactValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
    }
}

public record AssignContactCommand(Guid ContactId, Guid? AssignedToId) : IRequest<ContactDto>;

public record DeleteContactCommand(Guid Id) : IRequest;

// ── Handlers ───────────────────────────────────────────────────────────────────

public class CreateContactHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateContactCommand, ContactDto>
{
    public async Task<ContactDto> Handle(CreateContactCommand request, CancellationToken cancellationToken)
    {
        if (await db.Contacts.AnyAsync(c => c.Email == request.Email.ToLowerInvariant(), cancellationToken))
            throw new ConflictException($"A contact with email '{request.Email}' already exists.");

        var contact = Contact.Create(request.FirstName, request.LastName, request.Email,
            request.Phone, request.Company, request.Position, currentUser.UserId);

        if (request.Notes is not null) contact.UpdateNotes(request.Notes);
        if (request.Tags?.Any() == true) contact.SetTags(request.Tags);

        db.Contacts.Add(contact);
        await db.SaveChangesAsync(cancellationToken);

        return await ContactHelper.GetContactDto(db, contact.Id, cancellationToken);
    }
}

public class UpdateContactHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateContactCommand, ContactDto>
{
    public async Task<ContactDto> Handle(UpdateContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await db.Contacts.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException("Contact", request.Id);

        ContactHelper.EnsureAccess(currentUser, contact);

        contact.Update(request.FirstName, request.LastName, request.Email, request.Phone, request.Company, request.Position);
        contact.UpdateNotes(request.Notes);
        if (request.Tags is not null) contact.SetTags(request.Tags);

        await db.SaveChangesAsync(cancellationToken);
        return await ContactHelper.GetContactDto(db, contact.Id, cancellationToken);
    }
}

public class AssignContactHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<AssignContactCommand, ContactDto>
{
    public async Task<ContactDto> Handle(AssignContactCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Role == UserRole.Sales) throw new ForbiddenException();

        var contact = await db.Contacts.FindAsync([request.ContactId], cancellationToken)
            ?? throw new NotFoundException("Contact", request.ContactId);

        if (request.AssignedToId.HasValue)
        {
            var userExists = await db.Users.AnyAsync(u => u.Id == request.AssignedToId.Value && u.IsActive, cancellationToken);
            if (!userExists) throw new NotFoundException("User", request.AssignedToId.Value);
        }

        contact.AssignTo(request.AssignedToId);
        await db.SaveChangesAsync(cancellationToken);
        return await ContactHelper.GetContactDto(db, contact.Id, cancellationToken);
    }
}

public class DeleteContactHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<DeleteContactCommand>
{
    public async Task Handle(DeleteContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await db.Contacts.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException("Contact", request.Id);

        if (currentUser.Role == UserRole.Sales) throw new ForbiddenException("Sales users cannot delete contacts.");

        db.Contacts.Remove(contact);
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ── Shared helper ──────────────────────────────────────────────────────────────

internal static class ContactHelper
{
    public static async Task<ContactDto> GetContactDto(IApplicationDbContext db, Guid id, CancellationToken ct)
    {
        return await db.Contacts
            .Where(c => c.Id == id)
            .Select(c => new ContactDto(
                c.Id, c.FirstName, c.LastName, c.FirstName + " " + c.LastName,
                c.Email, c.Phone, c.Company, c.Position, c.Notes, c.AvatarUrl,
                c.Tags,
                c.AssignedToId,
                c.AssignedTo != null ? c.AssignedTo.FirstName + " " + c.AssignedTo.LastName : null,
                c.Deals.Count, c.Activities.Count,
                c.CreatedAt, c.UpdatedAt))
            .FirstAsync(ct);
    }

    public static void EnsureAccess(ICurrentUserService currentUser, Domain.Entities.Contact contact)
    {
        if (currentUser.Role == UserRole.Sales &&
            contact.AssignedToId != currentUser.UserId &&
            contact.CreatedBy != currentUser.UserId)
            throw new ForbiddenException("You can only modify contacts assigned to you.");
    }
}