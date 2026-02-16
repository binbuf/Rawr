using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;
using Rawr.Core.Services;
using System;
using System.Linq;

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

            var timeRange = $"{localStart:t} - {localEnd:t}";

            if (_settingsManager.Settings.Notifications.ShowLocalTimezone)
            {
                var tzAbbr = GetTimeZoneAbbreviation(TimeZoneInfo.Local, localStart);
                return $"{timeRange} {tzAbbr}";
            }

            return timeRange;
        }
    }

    public string? OriginalTimeInfo
    {
        get
        {
            if (_calendarEvent.IsAllDay)
                return null;

            var originalTzId = _calendarEvent.OriginalTimeZoneId ?? TimeZoneInfo.Local.Id;
            var localTzId = TimeZoneInfo.Local.Id;

            bool isSameTimezone;
            TimeZoneInfo? originalTz = null;
            try
            {
                originalTz = TimeZoneInfo.FindSystemTimeZoneById(originalTzId);
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
                // If same timezone, it's already shown in the Time property (if enabled)
                return null;
            }

            // If different, show original time/timezone
            var originalTzAbbr = originalTz != null 
                ? GetTimeZoneAbbreviation(originalTz, _calendarEvent.Start.DateTime) 
                : originalTzId;

            // TODO: add end time here
            var originalTimeStr = _calendarEvent.OriginalStartTime.HasValue 
                ? $"{_calendarEvent.OriginalStartTime.Value:t} ({originalTzAbbr})" 
                : $"({originalTzAbbr})";

            return originalTimeStr;
        }
    }

    public string? Location => _calendarEvent.Location;

    public string? Description => _calendarEvent.Description;

    [RelayCommand]
    public void Dismiss()
    {
        _notificationQueue.Dismiss(_calendarEvent);
    }

    private string GetTimeZoneAbbreviation(TimeZoneInfo tz, DateTime dateTime)
    {
        // Special case for UTC
        if (tz.Id == TimeZoneInfo.Utc.Id || tz.BaseUtcOffset == TimeSpan.Zero)
            return "UTC";

        string name = tz.IsDaylightSavingTime(dateTime) ? tz.DaylightName : tz.StandardName;

        if (string.IsNullOrEmpty(name)) return tz.Id;

        // If it's already a short abbreviation (e.g. "PST", "EST")
        if (name.Length <= 4 && !name.Contains(' '))
            return name;

        // Try to create abbreviation from uppercase letters (e.g. "Pacific Standard Time" -> "PST")
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var abbreviation = new string(parts
            .Where(x => x.Length > 0 && char.IsUpper(x[0]))
            .Select(x => x[0])
            .ToArray());

        return abbreviation.Length >= 2 ? abbreviation : tz.Id;
    }
}
