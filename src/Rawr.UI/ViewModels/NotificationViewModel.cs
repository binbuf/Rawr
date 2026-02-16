using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;
using Rawr.Core.Services;
using System;

namespace Rawr.ViewModels;

public partial class NotificationViewModel : ObservableObject
{
    private readonly CalendarEvent _calendarEvent;
    private readonly NotificationQueue _notificationQueue;
    private readonly ISettingsManager _settingsManager;

    public NotificationViewModel(CalendarEvent calendarEvent, NotificationQueue notificationQueue, ISettingsManager settingsManager)
    {
        _calendarEvent = calendarEvent;
        _notificationQueue = notificationQueue;
        _settingsManager = settingsManager;
    }

    public string Title => _calendarEvent.Title;

    public string Time
    {
        get
        {
            if (_calendarEvent.IsAllDay)
                return "All Day";

            var localStart = _calendarEvent.Start.LocalDateTime;
            var localEnd = _calendarEvent.End.LocalDateTime;

            return $"{localStart:t} - {localEnd:t}";
        }
    }

    public string? OriginalTimeInfo
    {
        get
        {
            var showTz = _settingsManager.Settings.Notifications.ShowTimezone;
            if (!showTz) return null;

            if (_calendarEvent.IsAllDay)
                return null;

            var originalTzId = _calendarEvent.OriginalTimeZoneId ?? TimeZoneInfo.Local.Id;
            var localTzId = TimeZoneInfo.Local.Id;

            bool isSameTimezone;
            try
            {
                var originalTz = TimeZoneInfo.FindSystemTimeZoneById(originalTzId);
                isSameTimezone = originalTz.Id == TimeZoneInfo.Local.Id ||
                                 (originalTz.BaseUtcOffset == TimeZoneInfo.Local.BaseUtcOffset &&
                                  originalTz.GetUtcOffset(_calendarEvent.Start) == TimeZoneInfo.Local.GetUtcOffset(_calendarEvent.Start));
            }
            catch
            {
                isSameTimezone = string.Equals(originalTzId, localTzId, StringComparison.OrdinalIgnoreCase);
            }

            if (isSameTimezone)
            {
                return $"({originalTzId})";
            }

            // If different, show original time/timezone and local time/timezone
            var originalTimeStr = _calendarEvent.OriginalStartTime.HasValue 
                ? $"{_calendarEvent.OriginalStartTime.Value:t} ({originalTzId})" 
                : $"({originalTzId})";
            
            var localTimeStr = $"{_calendarEvent.Start.LocalDateTime:t} ({localTzId})";

            return $"{originalTimeStr} / {localTimeStr}";
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
