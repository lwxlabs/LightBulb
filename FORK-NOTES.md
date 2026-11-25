# LightBulb 2.7.1-forked-0.21 — fork notes

## forked-0.21 — Localite settings persistence

- Replace Cogwheel JSON saving with LwXLabs.Localite 1.3.11 and SQLite.
- Import existing Settings.json once, retain the original file for rollback, and persist future changes in Settings.db3.
- Preserve profiles, hotkeys, tone settings, and reset/cancel behavior.
- Include native SQLite and Localite license notices in the self-contained Windows installer.
- Version is 2.7.1-forked-0.21 / 2.7.1.21.

## forked-0.20 — tone workflow, profile explanations, installer upgrade reliability

- Main tray `Color ↕` / `Brightness ↕` rows no longer repeat the selected tone-band name; `Tone: <band> <intensity>%` is the single target indicator.
- Scrolling `Tone: Dark tones/Midtones/Highlights/All tone bands` changes that selected target's intensity directly. Scrolling the individual tone rows still changes only their independent intensity.
- Clicking the main `Tone: ...` row now disables/enables the **entire tone layer**, not merely the editing target. Disabled tones are completely removed from the gamma output while the working profile, band, color/brightness deltas, and exact intensity values are retained for restoration.
- The first tone-submenu/profile-submenu action is now dynamic **Disable tones / Enable tones** instead of the old ambiguous Off editing mode. Selecting any saved profile from the tray enables tones and opens on `Highlights`; re-enabling restores the last selected band and intensity exactly.
- Tone submenu target/reset actions remain open after clicking so repeated tuning does not require reopening the submenu.
- Added `White glare dim` and `White glare dim strong`, which affect only the highlight band in the existing luminance-based gamma-curve model. They target bright values rather than semantic white pixels, so very bright colors can also be affected.
- Added short human-readable tooltips for tone controls and editable Tooltip fields for both normal profiles and tone profiles; built-in profiles receive one-time default descriptions without overwriting later user edits.
- Tone-profile Settings no longer offers the obsolete Off editing target. Saved target defaults remain available, while selecting a profile from the tray intentionally starts on Highlights.
- Installer keeps Inno Setup's standard **Preparing to Install / applications using files** page. The earlier pre-`PrepareToInstall` shutdown was removed because it closed LightBulb before Restart Manager could display that page. `CloseApplications=force` now handles the user-selected automatic close while the existing `CloseApplicationsFilter=LightBulb.exe` keeps the normal Restart Manager detection/page.
- Setup hides the Start Menu folder page and always shows the real destination directory page (`DisableProgramGroupPage=yes`, `DisableDirPage=no`, `UsePreviousAppDir=yes`).
- Version is `2.7.1-forked-0.20` / `2.7.1.20`.

## forked-0.19 — tone target + independent tone intensity

- Kept `Tone profile` as the simple Off/profile selector and kept the version at `2.7.1-forked-0.19` because this build has not been installed yet.
- `Tones: Off / Dark tones / Midtones / Highlights / All tone bands` is now an explicit editing target for the main tray Color/Brightness wheel rows.
- With a tone target selected, scrolling the main `Color ↕` and `Brightness ↕` rows edits that target's stored color/brightness deltas; `Tones: Off` sends those rows back to the normal/base profile.
- The Dark tones / Midtones / Highlights / All tone bands rows now scroll **intensity only** (0–100%); Shift+scroll uses 1% steps.
- Tone profiles now store independent color delta, brightness delta, and intensity for every band plus the profile's default tray target. Selecting a saved tone profile restores all of them together.
- The Tone profiles Settings tab now exposes each band's intensity and the saved default tray target in addition to the existing color/brightness controls.
- Tone intensity is applied as a multiplier inside the existing smooth gamma-curve builder; `Tone profile -> Off` retains the original linear gamma path.
- Reset and Save-to-existing/Save-as-new now include the target and intensity state instead of only the color/brightness deltas.
- Starter tone profiles now include `White-page soften`, `Soft highlights`, `Warm highlights`, `Shadow warmth`, `Soft contrast`, and `Dark-room`; none is enabled automatically. Each ships with its own saved default tray target and independent band intensities.
- Version remains `2.7.1-forked-0.19` / `2.7.1.19`.

## forked-0.18 — persistent unsaved manual profile look

- Tray tooltip now adds **`(custom)`** when a saved profile is active and its current color/brightness has unsaved manual offsets, e.g. `LightBulb — Reading (custom)`.
- Manual temperature and brightness offsets are persisted separately from profile definitions, so an unsaved tray/touch/hotkey adjustment survives application restart without silently overwriting the saved profile.
- Runtime offset persistence is debounced by 350 ms to avoid writing the settings file for every wheel/slider event and is flushed during normal shutdown.
- Reset/profile activation clears the persisted manual offsets through the existing offset reset path; saving the current look into the active profile also returns the profile to its non-custom state.
- Version advanced to `2.7.1-forked-0.18` / `2.7.1.18`.

## forked-0.17 — tray window toggle + dynamic next profile

- Tray left-click action now reads **Toggle app window** and truly toggles the dashboard: visible -> hidden, hidden -> shown.
- `ToggleMainWindow()` no longer forces the dashboard visible merely because the separate Settings window is open.
- Profiles submenu now shows the actual next cycle target, e.g. `Next profile: Neutral` or `Next profile: Automatic`, instead of the static `Next profile / Automatic` label.
- Profile cycling order itself is unchanged: `Automatic -> first saved profile -> ... -> last saved profile -> Automatic`.
- Version advanced to `2.7.1-forked-0.17` / `2.7.1.17`.


## forked-0.16 — startup visibility fix

- Fixed **Start minimized** so the persisted setting is checked during every application startup, using the persisted startup preference instead of relying only on the `--start-hidden` command-line flag.
- `--start-hidden` remains supported and is still added to the Windows Run command when **Start with Windows** and **Start minimized** are both enabled.
- Re-audited **Start with Windows**: LightBulb uses the per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pattern, quoted real executable path, and settings-save registration flow.
- Version advanced to `2.7.1-forked-0.16` / `2.7.1.16`.

## forked-0.15 — separate tray-wheel color/brightness modifiers

- Added a separate **Tray wheel color modifier** beside the existing brightness modifier.
- Direct tray-icon scrolling now changes color only while the configured color modifier is held and brightness only while the configured brightness modifier is held.
- The two modifiers cannot remain the same: choosing a modifier already used by the other action automatically moves the other action to another available modifier (Control prefers Shift as the fallback).
- Shift remains the fine-adjust modifier only when Shift is not assigned to either tray action.
- Version advanced to `2.7.1-forked-0.15` / `2.7.1.15`.

## forked-0.14 — tray left-click actions, profile cycle, focused logs, settings navigation

- Added a persistent **Tray icon left click** action: Show app window (default), Show tray menu, Enable/disable LightBulb, or Cycle profiles.
- **Show tray menu** reuses Avalonia's existing tray NativeMenu by posting the same Win32 tray callback (`WM_USER + 1024`, `WM_RBUTTONUP`) that Avalonia's Windows TrayIcon implementation handles; there is no duplicate popup menu.
- Profile cycling explicitly remains **Automatic → saved profiles in order → Automatic**; the tray label now makes the Automatic step visible.
- Persistent/user-visible logs are now **warnings and errors only**. Legacy INFO records are filtered from the Log page and newly generated INFO diagnostics are no longer written to disk.
- Settings navigation is reordered to **General → Profiles → Location → Hotkeys → Application whitelist → Advanced → Log**, uses distinct icons, and now shows icon + label instead of icon-only navigation.
- Version advanced to `2.7.1-forked-0.14` / `2.7.1.14`.


- Clicking either tray `Color ↕` or `Brightness ↕` now opens one reusable **Quick adjustment** window instead of resetting that individual offset. The window contains live color-temperature and brightness sliders plus a large `Reset to current profile` button. The controls update the same Dashboard offsets used by tray-wheel input and hotkeys, so mouse, keyboard and touchscreen input stay synchronized.
- The quick-adjust panel is fixed-dark, resizable, scroll-safe, and uses 44px slider rows plus a 48px reset action for touchscreen use. It displays the active profile/Automatic name and live K/% values.
- Replaced the old startup `RegistrySwitch` command construction with a dedicated `StartupService` using standard Windows startup registration: HKCU `Software\Microsoft\Windows\CurrentVersion\Run`, `Environment.ProcessPath`, quoted executable path, and per-user registration.
- Added **Start minimized** directly below **Start with Windows**. When enabled, the Run entry adds LightBulb's existing `--start-hidden` argument; when disabled, Windows startup opens the normal dashboard. Changing either setting and pressing Save rewrites the Run command.
- Uninstall now removes the exact `HKCU\...\Run\LightBulb` startup value.
- Settings now constrains every tab to the actual viewport width instead of allowing the horizontal ScrollViewer to preserve an oversized desired width. This makes the existing responsive profile breakpoint fire while the window is resized.
- Profiles wide mode now uses proportional `2*:5*` list/editor columns instead of a fixed 280px list. Narrow mode remains stacked. Profile and hotkey editors stretch with available width.
- Log actions now wrap below the heading when space is tight, and Location mode choices use a WrapPanel so they do not collide at narrow widths.
- The existing working shortcut capture/conflict changes and tray hover-wheel behavior from 0.12 are retained.
- The 0.13 touch/startup/responsive-settings work is retained in 0.14.

## forked-0.10 — Windows interaction/reliability pass

### Installer

- Uses Inno Setup's built-in Windows Restart Manager (`CloseApplications=yes`, `CloseApplicationsFilter=LightBulb.exe`).
- No Inno `AppMutex`, no private installer `--exit-existing` command, and no unconditional `taskkill` upgrade path.
- A running installed LightBulb therefore triggers Inno's standard applications-in-use choice, where Setup can close the process before replacing files.
- `RestartApplications=no`; the standard post-install Launch action starts the newly installed executable instead.

### Shortcuts

- Replaced Win32 `RegisterHotKey` with a low-level `WH_KEYBOARD_LL` hook.
- Modifier matching uses the hook's own key-down set plus `GetAsyncKeyState`, addressing the observed failure of combinations such as Ctrl+Alt+L while preserving arrow/function/media-key fallback mapping.
- Existing shortcuts are paused while a shortcut input is recording.
- Shortcut input remains visually blank until the main key is captured.
- Every shortcut field has an inline × clear/reset button.
- Letter/digit display is normalized (`Ctrl + Alt + L`, not `Ctrl + Alt + KeyL`).

### Tray wheel

- Reworked hover-scroll detection for the native tray popup. The implementation now finds process-owned `#32768` menu windows, obtains their `HMENU`, and hit-tests each displayed row with `GetMenuItemRect` in screen coordinates.
- `GetMenuItemRect` is deliberately called with a null HWND for the popup `HMENU`, allowing Windows to resolve the popup menu window as documented.
- Color/Brightness row text is updated directly in the open native menu after each wheel notch and the popup is repainted.
- Direct tray-icon wheel behavior remains, including configurable brightness modifier (Ctrl by default) and fine Shift steps.

### Theme/runtime error

- Removed the re-entrant `ActualThemeVariantChanged` theme-update path.
- System theme is resolved to explicit Light or Dark before Material theme updates, preventing the active material updater from using an unresolved `Default` key — the failure seen in the 0.08 unobserved-task log.

### Settings and profiles

- Settings is now a dedicated non-modal, resizable 940×660 window with sensible minimum dimensions rather than a modal DialogHost overlay on the small dashboard.
- Reopening Settings or selecting Manage profiles while it is open focuses/reuses the existing window.
- Hiding the dashboard no longer blocks/orphans Settings.
- Profiles UI was rebuilt as a clear list/actions column + responsive editor column.
- One-time starter profiles are added when the installation has no profiles: Warm evening, Late night, Reading, Neutral.
- Tray Profiles submenu remains the direct profile switcher.
- Added `LightBulb enabled` immediately above the day/night schedule area. It directly binds to Dashboard `IsEnabled`, so tray Enable/Disable, Settings, and Toggle LightBulb shortcut operate the same state.

## Earlier fork features retained

- forked-0.07: global Day/night schedule switch.
- forked-0.06: local-time Log settings tab, configurable tray brightness modifier, Profiles tray submenu.
- forked-0.04/0.05: trimming-safe tray identity lookup and compile corrections.
- forked-0.03: profiles, manual tray Color/Brightness controls, fine adjustment, Save current look.
- forked-0.01/0.02: fork cleanup, removed Git/updater/promotional policy pieces, initial shortcut reliability changes.

## Version

- Display version: `2.7.1-forked-0.19`
- Numeric Windows version: `2.7.1.19`
