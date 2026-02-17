using System.Collections.Generic;
using System.IO;
using Rawr.Core.Configuration;
using Rawr.Core.Models;

namespace Rawr.Core.Interfaces;

public interface ICalendarParser
{
    IEnumerable<CalendarEvent> Parse(string icsContent, CalendarSource source, int lookAheadHours);
    IEnumerable<CalendarEvent> Parse(Stream icsStream, CalendarSource source, int lookAheadHours);
}
