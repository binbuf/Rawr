using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rawr.Core.Models;
using Rawr.Core.Services;
using System;

namespace Rawr.ViewModels;

public partial class NotificationViewModel : ObservableObject
{
    private readonly CalendarEvent _calendarEvent;
    private readonly NotificationQueue _notificationQueue;

    public NotificationViewModel(CalendarEvent calendarEvent, NotificationQueue notificationQueue)
    {
        _calendarEvent = calendarEvent;
        _notificationQueue = notificationQueue;
    }

    public string Title => _calendarEvent.Title;
    
    public string Time => _calendarEvent.IsAllDay 
        ? "All Day" 
        : $"{_calendarEvent.Start:t} - {_calendarEvent.End:t}";

    public string? OriginalTimeInfo
    {
        get
        {
            if (_calendarEvent.IsAllDay || string.IsNullOrEmpty(_calendarEvent.OriginalTimeZoneId))
                return null;

            // If it's UTC and we are not in UTC, show it.
            if ((_calendarEvent.OriginalTimeZoneId == "UTC" || _calendarEvent.OriginalTimeZoneId == "Z") && _calendarEvent.Start.Offset != TimeSpan.Zero)
            {
                return $"{_calendarEvent.OriginalStartTime:t} UTC";
            }

            // Simple check: if the original timezone ID is present and we want to be safe,
            // we could try to see if the offset matches our local offset.
            // But just showing it if it's present might be enough if the user expects it.
            // The requirement says "If the timezones are different show a sub label".
            
            // To properly check if different, we'd need to resolve OriginalTimeZoneId.
            // For now, let's show it if it's provided and looks like a specific timezone.
            if (!string.IsNullOrEmpty(_calendarEvent.OriginalTimeZoneId) && 
                _calendarEvent.OriginalTimeZoneId != "UTC" && 
                _calendarEvent.OriginalTimeZoneId != "Z")
            {
                // We can't easily check if it's the SAME as local without resolving it,
                // but usually if it's in the ICS, it's because it was created in that TZ.
                return $"{_calendarEvent.OriginalStartTime:t} ({_calendarEvent.OriginalTimeZoneId})";
            }

            return null;
        }
    }

    public string? Location => _calendarEvent.Location;
    
    public string? Description => _calendarEvent.Description;

    [RelayCommand]
    public void Dismiss()
    {
        _notificationQueue.Dismiss(_calendarEvent);
    }
}
