using AutoMapper;
using CRM.Domain.Entities;

namespace CRM.Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // User mappings
        CreateMap<RegisterUserCommand, User>()
            .ConstructUsing(cmd => User.Create(
                cmd.Email,
                BCrypt.Net.BCrypt.HashPassword(cmd.Password),
                cmd.FirstName,
                cmd.LastName,
                cmd.Role));

        // Contact mappings
        CreateMap<CreateContactCommand, Contact>()
            .ConstructUsing((cmd, ctx) => Contact.Create(
                cmd.FirstName,
                cmd.LastName,
                cmd.Email,
                cmd.Phone,
                cmd.Company,
                (Guid)ctx.Items["UserId"]));

        CreateMap<Contact, ContactDto>();

        // Deal mappings
        CreateMap<CreateDealCommand, Deal>()
            .ConstructUsing(cmd => Deal.Create(
                cmd.Title,
                cmd.Value,
                cmd.OwnerId,
                cmd.ContactId,
                cmd.ExpectedCloseDate,
                cmd.Description));

        CreateMap<Deal, DealDto>()
            .ForMember(d => d.OwnerName, opt => opt.MapFrom(s => $"{s.Owner.FirstName} {s.Owner.LastName}"))
            .ForMember(d => d.ContactName, opt => opt.MapFrom(s => $"{s.Contact.FirstName} {s.Contact.LastName}"));

        // Activity mappings
        CreateMap<CreateActivityCommand, Activity>()
            .ConstructUsing(cmd => Activity.Create(
                cmd.Type,
                cmd.Title,
                cmd.Description,
                cmd.DueDate,
                cmd.AssignedToId,
                cmd.ContactId,
                cmd.DealId));

        CreateMap<Activity, ActivityDto>()
            .ForMember(d => d.AssignedToName, opt => opt.MapFrom(s => $"{s.AssignedTo.FirstName} {s.AssignedTo.LastName}"));
    }
}