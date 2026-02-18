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
using System.Text;
using CultureInfo = System.Globalization.CultureInfo;
using DateTimeStyles = System.Globalization.DateTimeStyles;

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

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(icsContent));
        return Parse(stream, source, lookAheadHours);
    }

    public IEnumerable<Rawr.Core.Models.CalendarEvent> Parse(Stream icsStream, CalendarSource source, int lookAheadHours)
    {
        if (icsStream == null || !icsStream.CanRead)
        {
            return Enumerable.Empty<Rawr.Core.Models.CalendarEvent>();
        }

        var now = _timeProvider.GetUtcNow();
        var searchEnd = now.AddHours(lookAheadHours);

        // Pre-filter: strip VEVENTs outside our window at the text level
        // before Ical.Net parses them into a massive object graph.
        using var filteredStream = PreFilterIcs(icsStream, now, searchEnd);

        Ical.Net.Calendar calendar;
        try
        {
            using var reader = new StreamReader(filteredStream, leaveOpen: false);
            calendar = Calendar.Load(reader);
        }
        catch
        {
            return Enumerable.Empty<Rawr.Core.Models.CalendarEvent>();
        }

        var results = ExtractEvents(calendar, source, now, searchEnd);

        // Release the Calendar object graph immediately
        calendar = null!;

        return results;
    }

    /// <summary>
    /// Text-level pre-filter that rewrites the ICS stream keeping only VEVENTs
    /// that could possibly produce occurrences in [windowStart, windowEnd].
    /// All non-VEVENT components (VTIMEZONE, VALARM, etc.) are preserved.
    /// </summary>
    private static MemoryStream PreFilterIcs(Stream input, DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        // Give 24h buffer before window start for events that may be in-progress
        var filterStart = windowStart.AddHours(-24);
        // Give generous buffer after window end for timezone edge cases
        var filterEnd = windowEnd.AddHours(24);

        var output = new MemoryStream();
        var writer = new StreamWriter(output, Encoding.UTF8, bufferSize: 8192, leaveOpen: true);
        var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true);

        var veventLines = new List<string>();
        bool inVevent = false;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!inVevent)
            {
                if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    inVevent = true;
                    veventLines.Clear();
                    veventLines.Add(line);
                }
                else
                {
                    // Pass through everything outside VEVENT blocks (VCALENDAR, VTIMEZONE, etc.)
                    writer.WriteLine(line);
                }
            }
            else
            {
                veventLines.Add(line);

                if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    inVevent = false;

                    if (ShouldKeepVevent(veventLines, filterStart, filterEnd))
                    {
                        foreach (var vl in veventLines)
                        {
                            writer.WriteLine(vl);
                        }
                    }
                }
            }
        }

        writer.Flush();
        output.Position = 0;
        return output;
    }

    /// <summary>
    /// Decides whether a VEVENT block should be kept based on text-level inspection.
    /// Keeps events that: have recurrence rules, or have dates overlapping the window.
    /// </summary>
    private static bool ShouldKeepVevent(List<string> lines, DateTimeOffset filterStart, DateTimeOffset filterEnd)
    {
        bool hasRrule = false;
        DateTime? dtStart = null;
        DateTime? dtEnd = null;

        foreach (var raw in lines)
        {
            // Handle folded lines — ICS uses continuation with leading whitespace
            // For our purposes, we only care about property lines, so skip continuations
            if (raw.Length == 0 || raw[0] == ' ' || raw[0] == '\t')
                continue;

            if (raw.StartsWith("RRULE", StringComparison.OrdinalIgnoreCase))
            {
                // Any event with a recurrence rule must be kept — it could produce
                // occurrences in our window regardless of DTSTART
                hasRrule = true;
            }
            else if (raw.StartsWith("DTSTART", StringComparison.OrdinalIgnoreCase))
            {
                dtStart = TryParseIcsDate(raw);
            }
            else if (raw.StartsWith("DTEND", StringComparison.OrdinalIgnoreCase))
            {
                dtEnd = TryParseIcsDate(raw);
            }
        }

        // Always keep recurring events
        if (hasRrule) return true;

        // If we couldn't parse the date, keep it to be safe
        if (dtStart == null) return true;

        var start = dtStart.Value;
        var end = dtEnd ?? start.AddHours(1); // Default 1hr duration if no end

        // Keep if the event's time range overlaps with our filter window
        // Event ends after our filter start AND event starts before our filter end
        return end >= filterStart.UtcDateTime && start <= filterEnd.UtcDateTime;
    }

    /// <summary>
    /// Quick and dirty ICS date parser — handles the common formats:
    /// DTSTART:20250217T120000Z
    /// DTSTART;VALUE=DATE:20250217
    /// DTSTART;TZID=America/New_York:20250217T120000
    /// </summary>
    private static DateTime? TryParseIcsDate(string line)
    {
        // Find the value part after the colon
        var colonIdx = line.IndexOf(':');
        if (colonIdx < 0 || colonIdx >= line.Length - 1) return null;

        var value = line.Substring(colonIdx + 1).Trim();
        if (value.Length < 8) return null;

        // Try UTC format: 20250217T120000Z
        if (DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utcResult))
        {
            return utcResult;
        }

        // Try local format: 20250217T120000 (treat as UTC for filtering — close enough)
        if (DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var localResult))
        {
            return localResult;
        }

        // Try date-only: 20250217
        if (DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dateResult))
        {
            return dateResult;
        }

        return null;
    }

    private List<Rawr.Core.Models.CalendarEvent> ExtractEvents(Ical.Net.Calendar calendar, CalendarSource source,
        DateTimeOffset now, DateTimeOffset searchEnd)
    {
        if (calendar == null) return new List<Rawr.Core.Models.CalendarEvent>();

        var startCal = new CalDateTime(now.UtcDateTime);
        var endCal = new CalDateTime(searchEnd.UtcDateTime);

        // Limit recurrence evaluation to prevent runaway memory usage on events
        // with infinite or very long-running recurrence rules.
        var evalOptions = new Ical.Net.Evaluation.EvaluationOptions
        {
            MaxUnmatchedIncrementsLimit = 1000
        };

        var results = new List<Rawr.Core.Models.CalendarEvent>();

        if (calendar.Events == null) return results;

        foreach (var evt in calendar.Events)
        {
             if (evt == null) continue;

             IEnumerable<Occurrence> occurrences;
             try
             {
                 occurrences = evt.GetOccurrences(startCal, evalOptions).TakeWhileBefore(endCal);
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
