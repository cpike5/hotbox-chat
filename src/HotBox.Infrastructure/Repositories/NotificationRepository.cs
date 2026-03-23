using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly HotBoxDbContext _dbContext;

    public NotificationRepository(HotBoxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Notification> CreateAsync(Notification notification, CancellationToken ct = default)
    {
        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(ct);
        return notification;
    }

    public async Task<List<Notification>> GetByRecipientAsync(
        Guid recipientId,
        DateTime? before = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var query = _dbContext.Notifications
            .Where(n => n.RecipientId == recipientId);

        if (before.HasValue)
        {
            query = query.Where(n => n.CreatedAt < before.Value);
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .Include(n => n.Sender)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken ct = default)
    {
        return await _dbContext.Notifications
            .CountAsync(n => n.RecipientId == recipientId && n.ReadAt == null, ct);
    }

    public async Task MarkAllAsReadAsync(Guid recipientId, CancellationToken ct = default)
    {
        await _dbContext.Notifications
            .Where(n => n.RecipientId == recipientId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
    }

    public async Task<bool> IsSourceMutedAsync(
        Guid userId,
        NotificationSourceType sourceType,
        Guid sourceId,
        CancellationToken ct = default)
    {
        return await _dbContext.UserNotificationPreferences
            .AnyAsync(p =>
                p.UserId == userId &&
                p.SourceType == sourceType &&
                p.SourceId == sourceId &&
                p.IsMuted,
                ct);
    }

    public async Task<List<UserNotificationPreference>> GetPreferencesAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _dbContext.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task SetMutePreferenceAsync(
        Guid userId,
        NotificationSourceType sourceType,
        Guid sourceId,
        bool isMuted,
        CancellationToken ct = default)
    {
        var preference = await _dbContext.UserNotificationPreferences
            .FirstOrDefaultAsync(p =>
                p.UserId == userId &&
                p.SourceType == sourceType &&
                p.SourceId == sourceId,
                ct);

        if (preference is not null)
        {
            preference.IsMuted = isMuted;
            preference.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _dbContext.UserNotificationPreferences.Add(new UserNotificationPreference
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SourceType = sourceType,
                SourceId = sourceId,
                IsMuted = isMuted,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
