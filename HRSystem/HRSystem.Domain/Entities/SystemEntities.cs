namespace HRSystem.Domain.Entities;

// Not BaseEntity: settings and audit rows are never soft-deleted or audited themselves
public class SystemSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
    public string? Description { get; set; }
}

// One run of the basic-data import (Excel), kept so it can be undone
public class ImportBatch : Common.BaseEntity
{
    public string FileName { get; set; } = null!;
    public string Summary { get; set; } = null!;
    public DateTime? UndoneAt { get; set; }
    public ICollection<ImportBatchItem> Items { get; set; } = new List<ImportBatchItem>();
}

// A record the import created or changed (only created ones are removed by an undo)
public class ImportBatchItem
{
    public long Id { get; set; }
    public int ImportBatchId { get; set; }
    public ImportBatch ImportBatch { get; set; } = null!;
    public string EntityName { get; set; } = null!;
    public int EntityId { get; set; }
    public bool Created { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string Action { get; set; } = null!;
    public string EntityName { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime Timestamp { get; set; }
}
