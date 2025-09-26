using System;
using System.Collections.Generic;

namespace MovieWeb.Models.Entities;

public partial class AdminLog
{
    public int LogId { get; set; }

    public int AdminId { get; set; }

    public string Action { get; set; } = null!;

    public string? Description { get; set; }

    public string? TableName { get; set; }

    public int? RecordId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User Admin { get; set; } = null!;
}
