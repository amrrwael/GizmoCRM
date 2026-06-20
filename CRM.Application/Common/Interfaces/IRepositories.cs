using CRM.Domain.Entities;
using CRM.Domain.Enums;

namespace CRM.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<List<User>> GetAllAsync();
    Task<User> AddAsync(User user);
    Task UpdateAsync(User user);
    Task<bool> EmailExistsAsync(string email);
}

public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(Guid id);
    Task<List<Contact>> GetAllAsync(int page = 1, int pageSize = 10);
    Task<List<Contact>> GetByUserIdAsync(Guid userId, int page = 1, int pageSize = 10);
    Task<int> GetTotalCountAsync();
    Task<Contact> AddAsync(Contact contact);
    Task UpdateAsync(Contact contact);
    Task DeleteAsync(Guid id);
}

public interface IDealRepository
{
    Task<Deal?> GetByIdAsync(Guid id);
    Task<List<Deal>> GetByStageAsync(DealStage stage, int page = 1, int pageSize = 20);
    Task<List<Deal>> GetByUserIdAsync(Guid userId, int page = 1, int pageSize = 20);
    Task<List<Deal>> GetAllAsync(int page = 1, int pageSize = 20);
    Task<int> GetTotalCountByStageAsync(DealStage stage);
    Task<Deal> AddAsync(Deal deal);
    Task UpdateAsync(Deal deal);
    Task UpdateStageAsync(Guid dealId, DealStage stage);
}

public interface IActivityRepository
{
    Task<Activity?> GetByIdAsync(Guid id);
    Task<List<Activity>> GetByContactIdAsync(Guid contactId, int page = 1, int pageSize = 20);
    Task<List<Activity>> GetByUserIdAsync(Guid userId, int page = 1, int pageSize = 20);
    Task<List<Activity>> GetOverdueAsync();
    Task<Activity> AddAsync(Activity activity);
    Task UpdateAsync(Activity activity);
    Task DeleteAsync(Guid id);
}