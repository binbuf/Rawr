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

        return ExtractEvents(calendar, source, lookAheadHours);
    }

    public IEnumerable<Rawr.Core.Models.CalendarEvent> Parse(Stream icsStream, CalendarSource source, int lookAheadHours)
    {
        if (icsStream == null || !icsStream.CanRead)
        {
            return Enumerable.Empty<Rawr.Core.Models.CalendarEvent>();
        }

        Ical.Net.Calendar calendar;
        try
        {
            using var reader = new StreamReader(icsStream, leaveOpen: false);
            calendar = Calendar.Load(reader);
        }
        catch
        {
            return Enumerable.Empty<Rawr.Core.Models.CalendarEvent>();
        }

        return ExtractEvents(calendar, source, lookAheadHours);
    }

    private IEnumerable<Rawr.Core.Models.CalendarEvent> ExtractEvents(Ical.Net.Calendar calendar, CalendarSource source, int lookAheadHours)
    {
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
                 var endCal = new CalDateTime(searchEnd.UtcDateTime);
                 occurrences = evt.GetOccurrences(startCal).TakeWhileBefore(endCal);
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
                    startDt = new DateTimeOffset(s.AsUtc, TimeSpan.Zero);
                }
                catch
                {
                    startDt = s.IsUtc
                        ? new DateTimeOffset(s.Value, TimeSpan.Zero)
                        : new DateTimeOffset(s.Value, TimeZoneInfo.Local.GetUtcOffset(s.Value));
                }

                TimeSpan duration = TimeSpan.Zero;
                if (evt.DtEnd != null && evt.DtStart != null)
                {
                    duration = evt.DtEnd.AsUtc - evt.DtStart.AsUtc;
                }
                else if (evt.Duration != null && evt.DtStart != null)
                {
                    duration = evt.DtStart.Add((Ical.Net.DataTypes.Duration)evt.Duration).AsUtc - evt.DtStart.AsUtc;
                }
                else if (occurrence.Period.EndTime != null && occurrence.Period.StartTime != null)
                {
                    duration = occurrence.Period.EndTime.AsUtc - occurrence.Period.StartTime.AsUtc;
                }

                DateTimeOffset endDt = startDt.Add(duration);


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
                    SourceName = source.Name,
                    OriginalStartTime = s.Value,
                    OriginalEndTime = s.Value.Add(duration),
                    OriginalTimeZoneId = s.TzId
                });
             }
        }

        return results;
    }
}
