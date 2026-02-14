Let's design a cross platform desktop application using Avalonia. The app will live in the task bar mainly. It is called `Rawr`. It allows you to import and use cloud syncing (webcal, http, or https), or locally provided ics (no syncing). When the app is setup then we can use an alert window which and voice synthesis to to share what time it is and what event is about to happen or is happening.

## Features

- Configurable notification/voice alerts n minutes before event starts or `On Time`
  - Allow as many as user desires
  - Allow to select which Days of the week, Start Time, End time for when alerts should occur
  - Allow for interval type alerts (Every hour, every 30 mins, every 15 mins, every custom n minutes)
- Cloud sync refresh inverval (minutes)
- Popup window for notification
  - Show the current local time (Default: 8:00am)
  - Configurable voice prompt ("The current time is 8 am")
  - Button for a aonfigurable snooze on notifications delay time
- Settings view
  - General
    - Start with OS
    - Log Level
    - Popup Duration (seconds), default 10
    - Default snooze (minutes), default 60
    - Skip visual notifications when app or game is in fullscreen or os level focus mode is enabled
  - Schedule
    - Days (Sun-Sat as checkoxes)
    - Start Time (time selector)
    - End Time
    - Interval-based time alerts (Every hour, every 30 mins, every 15 mins, every custom n minutes)
      - Show a text edit field if custom is selected
  - Calendar
    - Enable calendar integration
    - Event Reminder Times (list of currently selected mins to alert be for an event and a delete button next to it)
      - Add button
    - Calendar sources (Add, Edit, Remove)
      - Popup with:
        - Source Type Calendar URL (webcal, http, https) or Local .ics file
        - Calendar URL
        - Local file path (not editable textbox and browse button)
        - Refresh interval
    - Upcoming Events (if calendar is synced show upcoming events in the next 24 hours that would trigger a notification)

  - Voice
    - Native OS voices list
    - Rate
    - Volume
    - Mute voice
    - Test Voice