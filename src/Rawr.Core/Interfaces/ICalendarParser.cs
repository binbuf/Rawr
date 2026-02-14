using System.Collections.Generic;
using Rawr.Core.Configuration;
using Rawr.Core.Models;

namespace Rawr.Core.Interfaces;

public interface ICalendarParser
{
    IEnumerable<CalendarEvent> Parse(string icsContent, CalendarSource source, int lookAheadHours);
}
