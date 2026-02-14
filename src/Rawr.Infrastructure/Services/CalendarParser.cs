using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Rawr.Core.Configuration;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Rawr.Infrastructure.Services;

public class CalendarParser : ICalendarParser
{
    private readonly TimeProvider _timeProvider;

    public CalendarParser(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public IEnumerable<Rawr.Core.Models.CalendarEvent> Parse(string icsContent, CalendarSource source, int lookAheadHours)
    {
        if (string.IsNullOrWhiteSpace(icsContent))
        {
            return Enumerable.Empty<Rawr.Core.Models.CalendarEvent>();
        }

        Ical.Net.Calendar calendar;
        try
        {
            calendar = Calendar.Load(icsContent);
        }
        catch
        {
            return Enumerable.Empty<Rawr.Core.Models.CalendarEvent>();
        }
        
        if (calendar == null) return Enumerable.Empty<Rawr.Core.Models.CalendarEvent>();

        var now = _timeProvider.GetUtcNow();
        var searchEnd = now.AddHours(lookAheadHours);

        var startCal = new CalDateTime(now.UtcDateTime);
        
        var results = new List<Rawr.Core.Models.CalendarEvent>();

        if (calendar.Events == null) return results;

        foreach (var evt in calendar.Events)
        {
             if (evt == null) continue;

             IEnumerable<Occurrence> occurrences;
             try 
             {
                 occurrences = evt.GetOccurrences(startCal);
             }
             catch
             {
                 continue;
             }

             if (occurrences == null) continue;

             foreach (var occurrence in occurrences)
             {
                if (occurrence == null || occurrence.Period == null) continue;

                var s = occurrence.Period.StartTime;
                if (s == null) continue;
                
                // Check if we passed the lookAhead window
                if (s.Value > searchEnd.UtcDateTime)
                {
                    break;
                }

                DateTimeOffset startDt;
                try
                {
                    // Convert to UTC first using Ical.Net's internal resolution, then wrap in DateTimeOffset
                    var utcDt = s.AsUtc;
                    startDt = new DateTimeOffset(utcDt, TimeSpan.Zero);
                }
                catch
                {
                    if (s.IsUtc)
                    {
                        startDt = new DateTimeOffset(s.Value, TimeSpan.Zero);
                    }
                    else
                    {
                        startDt = new DateTimeOffset(s.Value, TimeZoneInfo.Local.GetUtcOffset(s.Value));
                    }
                }

                DateTimeOffset endDt;
                var e = occurrence.Period.EndTime;
                
                if (e == null)
                {
                    endDt = startDt;
                }
                else
                {
                    try
                    {
                        var utcDt = e.AsUtc;
                        endDt = new DateTimeOffset(utcDt, TimeSpan.Zero);
                    }
                    catch
                    {
                        if (e.IsUtc)
                        {
                            endDt = new DateTimeOffset(e.Value, TimeSpan.Zero);
                        }
                        else
                        {
                            endDt = new DateTimeOffset(e.Value, TimeZoneInfo.Local.GetUtcOffset(e.Value));
                        }
                    }
                }

                results.Add(new Rawr.Core.Models.CalendarEvent
                {
                    Uid = evt.Uid,
                    Title = evt.Summary ?? "No Title",
                    Description = evt.Description,
                    Location = evt.Location,
                    Start = startDt,
                    End = endDt,
                    IsAllDay = evt.IsAllDay,
                    SourceId = source.Id,
                    OriginalStartTime = s.Value,
                    OriginalTimeZoneId = s.TzId
                });
             }
        }

        return results;
    }
}
