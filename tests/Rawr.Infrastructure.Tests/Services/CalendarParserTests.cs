using Microsoft.Extensions.Time.Testing;
using Rawr.Core.Configuration;
using Rawr.Infrastructure.Services;
using System;
using System.Linq;
using Xunit;

namespace Rawr.Infrastructure.Tests.Services;

public class CalendarParserTests
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly CalendarParser _parser;

    public CalendarParserTests()
    {
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero)); // Oct 1st 2023 12:00 UTC
        _parser = new CalendarParser(_timeProvider);
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsEmpty()
    {
        var result = _parser.Parse("", new CalendarSource(), 48);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_InvalidContent_ReturnsEmpty()
    {
        var result = _parser.Parse("INVALID CONTENT", new CalendarSource(), 48);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_RecurringEvent_ExpandsCorrectly()
    {
        var ics = string.Join("\r\n", 
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//Rawr//Test//EN",
            "BEGIN:VEVENT",
            "UID:recurring-event-1",
            "DTSTART:20231001T100000Z",
            "DTEND:20231001T110000Z",
            "RRULE:FREQ=DAILY",
            "SUMMARY:Daily Meeting",
            "END:VEVENT",
            "END:VCALENDAR"
        );

        var result = _parser.Parse(ics, new CalendarSource(), 48).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("Daily Meeting", result[0].Title);
        Assert.Equal(new DateTimeOffset(2023, 10, 2, 10, 0, 0, TimeSpan.Zero), result[0].Start);
        Assert.Equal(new DateTimeOffset(2023, 10, 3, 10, 0, 0, TimeSpan.Zero), result[1].Start);
    }

    [Fact]
    public void Parse_SimpleEvent_Future_ReturnsOne()
    {
        var ics = string.Join("\r\n",
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "BEGIN:VEVENT",
            "UID:single-event-1",
            "DTSTART:20231001T150000Z",
            "DTEND:20231001T160000Z",
            "SUMMARY:One Off",
            "END:VEVENT",
            "END:VCALENDAR"
        );

        var result = _parser.Parse(ics, new CalendarSource(), 48).ToList();

        Assert.Single(result);
        Assert.Equal("One Off", result[0].Title);
        Assert.Equal(new DateTimeOffset(2023, 10, 1, 15, 0, 0, TimeSpan.Zero), result[0].Start);
    }
    
    [Fact]
    public void Parse_SimpleEvent_Past_ReturnsNone()
    {
        var ics = string.Join("\r\n",
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "BEGIN:VEVENT",
            "UID:single-event-past",
            "DTSTART:20231001T100000Z",
            "DTEND:20231001T110000Z",
            "SUMMARY:Past Event",
            "END:VEVENT",
            "END:VCALENDAR"
        );

        var result = _parser.Parse(ics, new CalendarSource(), 48).ToList();

        Assert.Empty(result);
    }
}
