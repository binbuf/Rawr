using System;

namespace Rawr.Core.Models;

public class CalendarEvent
{
    public string Uid { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public bool IsAllDay { get; set; }
    public string SourceId { get; set; } = string.Empty;
}
