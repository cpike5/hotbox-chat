using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly HotBoxDbContext _context;

    public NotificationRepository(HotBoxDbContext context)
    {
        _context = context;
    }

    public async Task<Notification> CreateAsync(Notification notification, CancellationToken ct = default)
    {
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(ct);
        return notification;
    }

    public async Task<List<Notification>> GetByRecipientAsync(
        Guid recipientId, DateTime? before = null, int limit = 50, CancellationToken ct = default)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Include(n => n.Sender)
            .Where(n => n.RecipientId == recipientId);

        if (before.HasValue)
            query = query.Where(n => n.CreatedAtUtc < before.Value);

        return await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientId == recipientId && n.ReadAtUtc == null, ct);
    }

    public async Task MarkAllAsReadAsync(Guid recipientId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await _context.Notifications
            .Where(n => n.RecipientId == recipientId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, now), ct);
    }

    public async Task<bool> IsSourceMutedAsync(
        Guid userId, NotificationSourceType sourceType, Guid sourceId, CancellationToken ct = default)
    {
        return await _context.UserNotificationPreferences
            .AsNoTracking()
            .AnyAsync(p => p.UserId == userId && p.SourceType == sourceType && p.SourceId == sourceId && p.IsMuted, ct);
    }

    public async Task<List<UserNotificationPreference>> GetPreferencesAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.UserNotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);
    }

    public async Task SetMutePreferenceAsync(
        Guid userId, NotificationSourceType sourceType, Guid sourceId, bool isMuted, CancellationToken ct = default)
    {
        var existing = await _context.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.SourceType == sourceType && p.SourceId == sourceId, ct);

        var now = DateTime.UtcNow;
        if (existing is not null)
        {
            existing.IsMuted = isMuted;
            existing.UpdatedAtUtc = now;
        }
        else
        {
            _context.UserNotificationPreferences.Add(new UserNotificationPreference
            {
                UserId = userId,
                SourceType = sourceType,
                SourceId = sourceId,
                IsMuted = isMuted,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await _context.SaveChangesAsync(ct);
    }
}
