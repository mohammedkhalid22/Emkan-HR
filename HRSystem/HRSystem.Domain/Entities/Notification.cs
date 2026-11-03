using HRSystem.Domain.Common;
using HRSystem.Domain.Enums;

namespace HRSystem.Domain.Entities;

// A message for one user: a request waiting for them, a decision on theirs, or a reminder
public class Notification : BaseEntity
{
    public string UserId { get; set; } = null!;
    public NotificationType Type { get; set; }
    public string Title { get; set; } = null!;
    public string? Body { get; set; }

    // Where clicking it goes (a path inside the app)
    public string? Url { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    // Daily reminders carry a key so the same one is never sent twice
    public string? Key { get; set; }
}
