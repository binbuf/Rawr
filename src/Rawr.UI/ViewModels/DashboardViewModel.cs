using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rawr.Core.Interfaces;
using Rawr.Core.Models;
using Rawr.Core.Services;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace Rawr.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ICalendarRepository _calendarRepository;
    private readonly ICalendarSyncService _syncService;
    private readonly ISettingsManager _settingsManager;
    private readonly NotificationQueue _notificationQueue;

    [ObservableProperty]
    private string _syncStatus = "Idle";

    [ObservableProperty]
    private ObservableCollection<CalendarEvent> _upcomingEvents = new();

    [ObservableProperty]
    private ObservableCollection<CalendarEvent> _activeAlerts = new();

    public DashboardViewModel(
        ICalendarRepository calendarRepository,
        ICalendarSyncService syncService,
        ISettingsManager settingsManager,
        NotificationQueue notificationQueue)
    {
        _calendarRepository = calendarRepository;
        _syncService = syncService;
        _settingsManager = settingsManager;
        _notificationQueue = notificationQueue;

        // Initialize active alerts
        ActiveAlerts = new ObservableCollection<CalendarEvent>(_notificationQueue.GetActiveAlerts());

        _notificationQueue.AlertAdded += (s, e) => 
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ActiveAlerts.Add(e));
            
        _notificationQueue.AlertRemoved += (s, e) => 
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                                  var toRemove = ActiveAlerts.FirstOrDefault(x => x.Uid == e.Uid && x.Start == e.Start);
                                  if (toRemove != null) ActiveAlerts.Remove(toRemove);
                             });
                             
                         _syncService.IsSyncingChanged += OnSyncingChanged;
                 
                         RefreshEvents();
                     }
                 
                     private void OnSyncingChanged(bool isSyncing)
                     {
                         Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                         {
                             SyncStatus = isSyncing ? "Syncing..." : $"Synced at {DateTime.Now:t}";
                             if (!isSyncing)
                             {
                                 await RefreshEventsAsync();
                             }
                         });
                     }
                 
                     // Default constructor for design-time

            public DashboardViewModel() 

            {

                _calendarRepository = null!;

                _syncService = null!;

                _settingsManager = null!;

                _notificationQueue = null!;

            }

        

            [RelayCommand]

            private void DismissAlert(CalendarEvent evt)

            {

                _notificationQueue.Dismiss(evt);

            }

        

            [RelayCommand]

            private async Task SyncNow()

            {

                SyncStatus = "Syncing...";

                try

                {

                    await _syncService.SyncAsync(CancellationToken.None);

                    SyncStatus = $"Synced at {DateTime.Now:t}";

                    await RefreshEventsAsync();

                }

                catch (Exception ex)

                {

                    SyncStatus = $"Failed: {ex.Message}";

                }

            }

        

            public async void RefreshEvents()

            {

                await RefreshEventsAsync();

            }

        

                public async Task RefreshEventsAsync()

        

                {

        

                    if (_calendarRepository == null) return;

        

            

        

                    var allEvents = await _calendarRepository.GetAllEventsAsync();

        

                    var now = DateTimeOffset.Now;

        

                    var threshold = now.AddMinutes(-_settingsManager.Settings.General.MissedEventThresholdMinutes);

        

                    var end = now.AddHours(_settingsManager.Settings.Calendar.LookAheadHours);

        

            

        

                    var events = allEvents

        

                        .Where(e => e.End >= now && e.Start <= end || e.Start >= threshold && e.Start <= end)

        

                        .OrderBy(e => e.Start);

        

                        

        

                    UpcomingEvents = new ObservableCollection<CalendarEvent>(events);

        

                }

        

            

        }
