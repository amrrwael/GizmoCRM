using CRM.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Contact> Contacts { get; }
    DbSet<Deal> Deals { get; }
    DbSet<Activity> Activities { get; }

    // Added: these were referenced everywhere in TelegramService but never
    // declared on this interface, which meant the whole Telegram feature
    // failed to compile (CS1061 "IApplicationDbContext does not contain a
    // definition for TelegramChats/TelegramMessages").
    DbSet<TelegramChat> TelegramChats { get; }
    DbSet<TelegramMessage> TelegramMessages { get; }

    DbSet<IntegrationSetting> IntegrationSettings { get; }
    DbSet<GmailAccount> GmailAccounts { get; }
    DbSet<EmailMessage> EmailMessages { get; }
    DbSet<CallLog> CallLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

}