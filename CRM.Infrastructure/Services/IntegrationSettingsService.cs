using CRM.Application.Common.Interfaces;
using CRM.Domain.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CRM.Infrastructure.Services;

public class IntegrationSettingsService : IIntegrationSettingsService
{
    private const string Purpose = "CRM.IntegrationSettings.v1";

    private readonly IApplicationDbContext _context;
    private readonly IDataProtector _protector;

    public IntegrationSettingsService(IApplicationDbContext context, IDataProtectionProvider dataProtectionProvider)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var setting = await _context.IntegrationSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting?.Value is null) return null;

        return setting.IsSecret ? Unprotect(setting.Value) : setting.Value;
    }

    public async Task SetAsync(string key, string value, string category, bool isSecret = true, CancellationToken ct = default)
    {
        var setting = await _context.IntegrationSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        var storedValue = isSecret ? _protector.Protect(value) : value;

        if (setting is null)
        {
            setting = new IntegrationSetting
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = storedValue,
                Category = category,
                IsSecret = isSecret,
                CreatedAt = DateTime.UtcNow,
            };
            _context.IntegrationSettings.Add(setting);
        }
        else
        {
            setting.Value = storedValue;
            setting.Category = category;
            setting.IsSecret = isSecret;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var setting = await _context.IntegrationSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null) return;
        _context.IntegrationSettings.Remove(setting);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<string, string?>> GetMaskedForCategoryAsync(string category, CancellationToken ct = default)
    {
        var settings = await _context.IntegrationSettings
            .Where(s => s.Category == category)
            .ToListAsync(ct);

        var result = new Dictionary<string, string?>();
        foreach (var s in settings)
        {
            var shortKey = s.Key.StartsWith($"{category}:") ? s.Key[(category.Length + 1)..] : s.Key;
            if (string.IsNullOrEmpty(s.Value)) { result[shortKey] = null; continue; }

            if (!s.IsSecret) { result[shortKey] = s.Value; continue; }

            var plain = Unprotect(s.Value);
            result[shortKey] = Mask(plain);
        }
        return result;
    }

    public async Task<bool> IsCategoryConfiguredAsync(string category, IEnumerable<string> requiredKeys, CancellationToken ct = default)
    {
        var keys = requiredKeys.ToList();
        var configuredKeys = await _context.IntegrationSettings
            .Where(s => s.Category == category && keys.Contains(s.Key) && s.Value != null && s.Value != "")
            .Select(s => s.Key)
            .ToListAsync(ct);

        return keys.All(configuredKeys.Contains);
    }

    private string? Unprotect(string protectedValue)
    {
        try { return _protector.Unprotect(protectedValue); }
        catch { return null; } // key rotated / corrupted value — treat as not configured rather than crash
    }

    private static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= 8) return "••••••••";
        return $"{value[..4]}••••{value[^4..]}";
    }
}
