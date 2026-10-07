# Agent rules for DesktopCalendar

## Scope

- This repository holds one WPF app (`net10.0-windows`) at the repository root. It builds and runs on Windows only.
- The app shows Google Calendar events read-only from private iCal feeds. Editing happens in Google Calendar.
- The app runs on one PC. Do not add multi-PC sync, Google Drive storage, memo editing, or OAuth. OAuth was dropped because publishing a Google Cloud OAuth app requires a homepage and privacy policy; iCal feeds cover the read-only need.

## Layout

- `App.xaml`, `MainWindow.xaml(.cs)`: the window, month grid, drag, edge resize, and refresh timers.
- `Models`: plain view and settings types.
- `Services/DesktopHostService.cs`: keeps the window on the desktop layer (`Progman` as owner, tool window style, `HWND_BOTTOM` every 3 s, and every z-order change in `WM_WINDOWPOSCHANGING` redirected to the bottom so activation by the context menu does not raise it). It is stable; change it only with a manual check after `Win + D`, Explorer restart, drag, and opening the context menu.
- `Services/WindowBackdropService.cs`: acrylic blur through `SetWindowCompositionAttribute`.
- `Services/CalendarFeedService.cs`: reads `calendars.json`, downloads and caches feeds, expands occurrences with Ical.Net.
- `Services/KoreanHolidayService.cs`: holiday rules plus the public Google holiday feed.
- `scripts/add-calendar.ps1`: interactive tool that appends feed addresses to `calendars.json`.

## Build and verify

- Build with `dotnet build` from the repository root; keep it at zero warnings.
- A running dev instance locks `bin\Debug\...\DesktopCalendar.exe`. To check a change without closing it, build with `-p:OutDir=<scratch dir>\`.
- XAML errors appear only at run time. After UI changes, start the built exe for a few seconds and confirm it stays alive.
- There is no test project. Check holiday rules and occurrence expansion from a scratch console project that links the source files.

## Install

- The installed copy is `%LOCALAPPDATA%\Programs\DesktopCalendar`, started by a shortcut in the Startup folder.
- Update it with `dotnet publish -c Release -o "$env:LOCALAPPDATA\Programs\DesktopCalendar"`. Ask the user to close the app through its context menu first; `Stop-Process` skips `Window_Closing`, which is where window bounds are saved.

## User data and secrets

- Data lives in `%APPDATA%\DesktopCalendar`. `settings.json` is owned by the app and rewritten on close. `calendars.json` is owned by the user; the app only reads it and creates a commented template when missing. `cache\` holds the last downloaded feed per URL.
- `calendars.json` contains private iCal URLs that grant read access to the user's calendars, and `cache\` holds full calendar contents. Never open, print, copy, or commit them. Verify changes through hashes, counts, or ACL listings instead.
- Both are restricted to the current user, SYSTEM, and Administrators so sandboxed agents cannot read them. `scripts/add-calendar.ps1` applies this before writing. The app does not set ACLs: a `calendars.json` or `cache\` it creates inherits the profile permissions until the script runs. Apply folder inheritance flags (`(OI)(CI)`) to the folder only; on files they leave an empty ACL.

## Holidays

- The local rules are authoritative. The public Google holiday feed only adds dates the rules do not produce, such as election days and one-off holidays.
- Take only feed events whose `DESCRIPTION` is `공휴일` (or `Public holiday`); the rest are observances.
- The Google feed is incomplete for future years and sometimes follows the Chinese lunar calendar (Seollal 2028). Do not let it override rule results.
- Overlapping holidays on one date earn a single substitute day.

## Conventions

- The layout constants at the top of `MainWindow.xaml.cs` mirror sizes in `MainWindow.xaml` (cell padding, font sizes, line heights). Update both together.
- PowerShell scripts with Korean text must be saved as UTF-8 with BOM so Windows PowerShell 5.1 reads them correctly.
- UI text and `README.md` are Korean; code, comments, commit messages, and this file are English.
