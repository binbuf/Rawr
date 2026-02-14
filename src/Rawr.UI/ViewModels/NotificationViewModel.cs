using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rawr.Core.Models;
using Rawr.Core.Services;

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

    public string? Location => _calendarEvent.Location;
    
    public string? Description => _calendarEvent.Description;

    [RelayCommand]
    public void Dismiss()
    {
        _notificationQueue.Dismiss(_calendarEvent);
    }
}
