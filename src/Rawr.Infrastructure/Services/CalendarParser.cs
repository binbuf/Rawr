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
                if (s.IsUtc)
                {
                    startDt = new DateTimeOffset(s.Value, TimeSpan.Zero);
                }
                else if (s.TzId != null)
                {
                    try
                    {
                        // Resolve the timezone and get the offset for the specific time
                        var tzid = s.TzId;
                        var dt = s.Value;
                        
                        // Ical.Net's CalDateTime has a 'Value' which is the local time.
                        // We need to find the offset for this time in the given TzId.
                        // However, Ical.Net usually handles this during parsing if the VTIMEZONE is present.
                        
                        // Let's try to get it via the calendar's timezone resolution if possible
                        // But since we are iterating occurrences, they should already be resolved or have TzId.
                        
                        // A more robust way in Ical.Net to get UTC from a CalDateTime:
                        var utcDt = s.AsUtc;
                        startDt = new DateTimeOffset(utcDt, TimeSpan.Zero);
                    }
                    catch
                    {
                         startDt = new DateTimeOffset(s.Value, TimeZoneInfo.Local.GetUtcOffset(s.Value));
                    }
                }
                else
                {
                    startDt = new DateTimeOffset(s.Value, TimeZoneInfo.Local.GetUtcOffset(s.Value));
                }

                DateTimeOffset endDt;
                var e = occurrence.Period.EndTime;
                
                if (e == null)
                {
                    endDt = startDt;
                }
                else
                {
                    if (e.IsUtc)
                    {
                        endDt = new DateTimeOffset(e.Value, TimeSpan.Zero);
                    }
                    else if (e.TzId != null)
                    {
                        try
                        {
                            var utcDt = e.AsUtc;
                            endDt = new DateTimeOffset(utcDt, TimeSpan.Zero);
                        }
                        catch
                        {
                            endDt = new DateTimeOffset(e.Value, TimeZoneInfo.Local.GetUtcOffset(e.Value));
                        }
                    }
                    else
                    {
                        endDt = new DateTimeOffset(e.Value, TimeZoneInfo.Local.GetUtcOffset(e.Value));
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
