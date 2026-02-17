❯ I have the debug version of this app running without visual studio running. Checking the memory usage on the app it says its
  using almost 2GB of RAM. That's way too high for this kind of app. What's going on? Propose a thoughtful solution to the
  problem.



● Explore(Explore codebase for memory issues)
  ⎿  Done (37 tool uses · 64.5k tokens · 2m 21s)
  (ctrl+o to expand)

● Let me verify the most critical findings quickly.

● Searched for 1 pattern, read 4 files (ctrl+o to expand)

● Good, I've confirmed all the key findings. Here's the analysis:

  ---
  Memory Analysis: Why Rawr is Using ~2GB in Debug Mode

  I found several compounding issues, with one debug-specific culprit and several general memory leaks.

  1. Debug Menu Event Population (the biggest suspect)

  App.axaml.cs:319-374 — The #if DEBUG tray menu calls PopulateEventMenu() on startup, which loads every calendar event from the
  repository and creates a NativeMenuItem + click handler closure for each one. Since you're running a debug build without Visual
  Studio, this debug menu is fully active and holding all those objects in memory.

  2. DashboardViewModel Event Handler Leak

  DashboardViewModel.cs:44-53 — The VM subscribes to AlertAdded, AlertRemoved, and IsSyncingChanged events but never unsubscribes.
  It's registered as Transient (line 145), so every time the dashboard window opens, a new VM is created with new subscriptions — but
   old ones are never cleaned up. The event handlers prevent GC from collecting old ViewModels.

  3. Unbounded Audio Queue

  NotificationWindowManager.cs:30 — Channel.CreateUnbounded<CalendarEvent>() has no capacity limit. If audio playback is slow or
  fails, events pile up in memory indefinitely.

  4. CalendarRepository Holds Everything In-Memory

  CalendarRepository.cs:22 — The entire _eventsBySource dictionary lives in memory with no eviction policy. For calendars with many
  recurring events (each occurrence expanded into its own CalendarEvent object), this grows large.

  ---
  Proposed Solution

  The highest-impact, lowest-risk fix targets these in priority order:

  1. Gate the debug menu population behind an explicit flag — Don't auto-populate on startup. Only populate when the user clicks
  "Refresh Events List." This alone likely reclaims a huge chunk of the 2GB.
  2. Make DashboardViewModel implement IDisposable — Unsubscribe from all events in Dispose(), and dispose the VM when the dashboard
  window closes. Change registration to scoped or ensure manual disposal.
  3. Bound the audio channel — Switch to Channel.CreateBounded<CalendarEvent>(new BoundedChannelOptions(10) { FullMode =
  BoundedChannelFullMode.DropOldest }) to cap queued audio events.
  4. Add eviction to CalendarRepository — Only keep events within the look-ahead window in memory, not the entire history.