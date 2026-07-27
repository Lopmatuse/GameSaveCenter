# GameSaveCenter Apple-Inspired WPF Redesign Prompt

You are working inside an existing Playnite extension project named **GameSaveCenter**.

Your task is to redesign and implement the plugin UI as a polished Apple-inspired desktop application using the project’s existing C# UI technology.

This is not a mockup task.

Do not generate HTML, CSS, JavaScript, screenshots, design images, or a WebView wrapper. Implement the real UI directly in the existing C# project so that it can compile, run, and continue using the plugin’s actual backup, restore, validation, task, cloud, media, and Ludusavi logic.

The desired direction is inspired by modern macOS and iOS interface principles, especially macOS sidebar materials, vibrancy, layered surfaces, restrained translucency, native-feeling motion, clear content hierarchy, large but consistent corner radii, low-contrast borders, soft shadows, and excellent light and dark themes.

The target is **Apple-inspired native desktop material design**, not generic glassmorphism and not a Windows Fluent imitation.

Do not attempt to clone Apple applications exactly. Do not use Apple-owned icons, SF Symbols, SF Pro font files, trademarks, or copyrighted assets.

Use these only as conceptual references where useful:

- GitHub Copilot WPF and C# coding instructions
- CommunityToolkit.Mvvm conventions
- Cupertino-inspired WPF controls and themes
- MaterialWPF-style soft component design
- WPF UI navigation, dialogs, snackbar, and theming architecture

Do not blindly install third-party UI libraries. First verify target framework compatibility, Playnite compatibility, licensing, package size, build impact, and whether the project already provides equivalent infrastructure.

---

## 1. Inspect the existing project first

Before changing code, inspect the entire repository and determine:

1. Target .NET or .NET Framework version.
2. Playnite SDK version.
3. Current UI technology:
   - WPF Window
   - UserControl
   - Playnite custom window
   - WinForms
   - another UI framework
4. Main plugin UI entry point.
5. Existing XAML views and resource dictionaries.
6. Existing ViewModels, commands, converters, and models.
7. Existing services for:
   - save backup
   - save restore
   - save validation
   - candidate save-path detection
   - Ludusavi
   - game runtime monitoring
   - backup scheduling
   - cloud upload
   - media synchronization
   - task history
   - logs and errors
   - settings persistence
8. Current build, packaging, development installation, and versioning process.
9. Whether the UI is hosted inside a Playnite window or uses its own independent window.
10. Existing theme integration with Playnite.
11. Existing window chrome or title bar customization.
12. Whether native Windows backdrop effects can be used safely.

Before implementation, briefly report:

- detected UI technology
- target framework
- main UI entry point
- business modules that will remain unchanged
- files planned for modification
- files planned for creation
- compatibility risks
- whether third-party dependencies are necessary
- whether native backdrop effects are feasible
- fallback strategy if backdrop effects are unavailable

After this short report, continue directly with implementation. Do not stop after analysis and do not only provide recommendations.

---

## 2. Core implementation rules

Preserve the current business logic.

Prefer to retain:

- models
- services
- repositories
- task orchestration
- configuration storage
- backup implementation
- restore implementation
- Ludusavi integration
- cloud synchronization
- media synchronization
- logging
- extension identifiers

Refactor the presentation layer rather than rewriting the entire plugin.

If the project already uses WPF:

- redesign the XAML
- create shared resource dictionaries
- improve control templates
- improve MVVM bindings
- add visual states and animations
- keep existing commands connected
- preserve the existing data flow
- preserve Playnite integration

If the project currently uses WinForms:

- do not immediately rewrite the entire plugin
- identify whether the business layer can be reused
- implement the smallest safe WPF migration
- avoid changing unrelated modules
- preserve existing plugin entry points where possible

Do not replace the project with Electron, Avalonia, WinUI 3, MAUI, React, a browser UI, a local HTTP UI, or a WebView-based shell.

Do not change the plugin ID.

Do not create fake UI states disconnected from real operations. Loading, success, failure, progress, and cancellation states must come from actual commands and task results. Do not use `Task.Delay` to simulate successful business operations.

---

## 3. Recommended UI architecture

Adapt to the current repository, but prefer a structure similar to:

```text
Themes/
    DesignTokens.xaml
    Colors.Light.xaml
    Colors.Dark.xaml
    Typography.xaml
    Controls.xaml
    Animations.xaml
    Materials.xaml

Views/
    MainWindow.xaml
    OverviewView.xaml
    GamesView.xaml
    TasksView.xaml
    CloudTasksView.xaml
    MediaSyncView.xaml
    SettingsView.xaml

ViewModels/
    MainWindowViewModel.cs
    OverviewViewModel.cs
    GamesViewModel.cs
    TasksViewModel.cs
    SettingsViewModel.cs

Controls/
    SidebarNavigationItem.xaml
    StatCard.xaml
    StatusBadge.xaml
    ToastNotification.xaml
    LoadingButton.xaml
    EmptyState.xaml
    ErrorPanel.xaml
    MaterialPanel.xaml

Converters/
    TaskStatusToBrushConverter.cs
    TaskStatusToTextConverter.cs
    BooleanToVisibilityConverter.cs
```

Do not create duplicate abstractions when the project already has equivalent components.

Prefer MVVM:

- Views handle layout and presentation.
- ViewModels expose state and commands.
- Services execute backup, restore, synchronization, and file operations.
- Avoid calling backup services directly from click handlers.
- Avoid excessive code-behind.
- Reuse the project’s current MVVM infrastructure.
- Do not introduce a large MVVM dependency unless necessary.

Useful ViewModel properties may include, only when not already present:

- `SelectedNavigationItem`
- `SelectedGame`
- `SelectedDetailTab`
- `IsBusy`
- `ProgressPercent`
- `StatusMessage`
- `LastError`
- `IsToastVisible`
- `ToastMessage`
- `ThemeMode`
- `IsWorkerAvailable`
- `IsLudusaviAvailable`
- `IsTransparencyEnabled`
- `IsReducedMotionEnabled`

All properties must correctly notify the UI. Async commands must prevent duplicate execution. UI-bound state updates must occur safely on the UI thread.

---

## 4. Window and main layout

Create a modern desktop layout inspired by macOS applications.

Recommended dimensions:

- default width: 1180 to 1320
- default height: 760 to 860
- minimum width: approximately 960
- minimum height: approximately 680
- sidebar width: 220 to 238
- top toolbar height: approximately 56
- page padding: 18 to 24
- section spacing: 12 or 16

Main structure:

1. Left sidebar navigation
2. Top page title and action toolbar
3. Overview statistic cards
4. Main two-column game and detail region
5. Toast notification layer
6. Modal dialog layer where necessary

Navigation items:

- Overview
- Games and Status
- Backup Tasks
- Cloud Tasks
- Media Sync
- Settings

Sidebar top:

- GameSaveCenter icon
- GameSaveCenter name
- plugin version

Sidebar bottom:

- Worker status
- Ludusavi status
- offline or error indication when necessary

Resize requirements:

- no overlapping controls
- no clipped buttons
- lists scroll internally
- long content does not unexpectedly expand the window
- avoid unnecessary page-level horizontal scrollbars
- support Windows DPI scaling at 100%, 125%, 150%, 175%, and 200%

If safe custom `WindowChrome` is supported:

- use a rounded custom title bar
- support dragging
- support double-click maximize and restore
- support minimize, maximize, restore, and close
- preserve Windows snap behavior where possible
- handle maximized margins correctly

Optional macOS-style red, yellow, and green window controls may be used only with real functionality:

- red: close
- yellow: minimize
- green: maximize or restore

They must include real commands, tooltips, keyboard accessibility, and screen-reader-friendly labels.

If Playnite’s host prevents safe custom chrome, keep the host chrome and redesign only the inner content. Do not use unsafe native window modification.

---

## 5. Design system

Create centralized design tokens. Do not repeatedly hardcode colors, typography, shadows, opacity, blur levels, spacing, and corner radii inside views.

Use `DynamicResource` where theme switching is required.

Preferred fonts:

- Segoe UI Variable
- Segoe UI
- Microsoft YaHei UI

Do not package or distribute Apple fonts.

Suggested light theme:

```text
WindowBackground       #F3F4F8
SidebarBackground      #F9F9FC
CardBackground         #FFFFFF
SecondaryBackground    #F6F6F9
PrimaryText            #171821
SecondaryText          #797E8D
Divider                #E7E8ED
Accent                 #7357FF
AccentHover            #6548EB
AccentPressed          #593ED7
AccentSoft             #EEEAFE
Success                #2BC46D
Warning                #F2A23A
Error                  #FF5F62
Information            #4F8CFF
```

Suggested dark theme:

```text
WindowBackground       #17181F
SidebarBackground      #1D1F28
CardBackground         #23252F
SecondaryBackground    #292B36
PrimaryText            #F4F5FA
SecondaryText          #A1A6B5
Divider                #343743
Accent                 #8B72FF
AccentHover            #9A84FF
AccentPressed          #765EEA
AccentSoft             #302A4D
Success                #36CE78
Warning                #F4AE4F
Error                  #FF6A6D
Information            #639BFF
```

Corner radius scale:

- small inputs: 8
- normal buttons: 10 to 12
- selected navigation items: 12 to 14
- normal cards: 16 to 18
- large panels: 20 to 22
- main window: 22 to 28

Spacing scale:

- 4
- 8
- 12
- 16
- 20
- 24
- 32

Borders:

- normally 1 device-independent pixel
- low contrast
- no strong blue outlines
- selection primarily uses a soft accent background
- keyboard focus must remain clearly visible

Shadows:

- soft and restrained
- only on meaningful elevation levels
- no shadow on every small control
- avoid harsh black borders
- avoid excessive blur
- avoid strong glossy highlights

Do not create neon, gaming RGB, cyberpunk, glossy glass, exaggerated glassmorphism, or a traditional blue-bordered enterprise dashboard style.

---

## 6. Apple-inspired materials, glass, translucency, and vibrancy

Use restrained Apple-inspired translucent materials similar in principle to macOS vibrancy and iOS system materials.

Do not apply heavy glassmorphism to every surface.

The visual goal is:

- softly tinted translucent surfaces
- contextual background blur
- clear separation between navigation, content, and floating layers
- readable text under all background conditions
- subtle material hierarchy
- restrained highlights and reflections
- native productivity utility appearance

Use translucent material primarily for:

- left sidebar
- custom title bar when supported
- floating menus
- context menus
- toast notifications
- modal dialog surfaces
- compact overlays
- selected floating controls where appropriate

Normal content cards, game lists, task lists, and settings sections should remain mostly opaque or only slightly translucent.

Do not apply backdrop blur independently to every card or list row.

### Material hierarchy

**Base Surface**

- mostly opaque
- no noticeable blur
- used for the main content background
- stable and clear for reading

**Sidebar Material**

- approximately 72% to 88% tinted opacity
- subtle backdrop blur
- very low-contrast border
- light inner highlight where appropriate
- visually distinct from the content area

**Floating Material**

- approximately 68% to 84% tinted opacity
- stronger backdrop blur than the sidebar
- soft shadow
- used for menus, toasts, and popovers

**Modal Material**

- approximately 82% to 92% tinted opacity
- moderate backdrop blur
- strong text readability
- clear elevation above the dimmed background

Suggested starting points:

Light sidebar:

- tint based on white or very light gray
- opacity approximately 0.76 to 0.86
- low-contrast white highlight
- subtle gray border

Dark sidebar:

- tint based on `#1D1F28` or a similar dark neutral
- opacity approximately 0.74 to 0.86
- subtle light border
- avoid fully black transparent surfaces

Light floating surface:

- white tint with approximately 0.72 to 0.84 opacity

Dark floating surface:

- dark neutral tint with approximately 0.76 to 0.88 opacity

Backdrop blur should visually resemble a radius of approximately 18 to 32 pixels, depending on the layer.

Keep the implementation subtle. Do not create highly distorted, heavily frosted, glossy, or reflective glass.

Do not add:

- strong white outer strokes
- glossy diagonal reflections
- neon edge lighting
- colorful chromatic aberration
- excessive saturation
- glass effects on every control
- large blurred decorative blobs behind every panel
- heavy reflections
- fake lens effects

The application should feel like a native productivity utility, not a glassmorphism website.

A reasonable material ratio is approximately:

- 70% clear or mostly opaque content surfaces
- 20% softly translucent structural surfaces
- 10% clearly blurred floating surfaces

This is a guideline, not a strict calculation.

---

## 7. Windows and WPF material implementation

Apple materials are the visual reference, but this is a Windows WPF application.

Use the safest compatible implementation available.

For an independent WPF window, inspect whether the project can safely use:

- Windows DWM backdrop APIs
- Windows 11 system backdrop APIs
- Acrylic-like window composition
- compatible custom `WindowChrome`
- compatible native interop already present in the project

Do not assume Windows 11.

Provide a graceful fallback for:

- Windows 10
- unsupported Windows builds
- remote desktop sessions
- disabled transparency effects
- software rendering
- Playnite-hosted windows that do not expose native backdrop control
- systems with reduced transparency enabled

Fallback behavior should use:

- carefully tinted opaque or semi-opaque backgrounds
- low-contrast borders
- soft shadows
- the same spacing and hierarchy
- no broken transparency
- no unreadable content

Do not make application functionality depend on backdrop support.

If hosted inside Playnite and unable to control the native backdrop, implement the material hierarchy inside the plugin content without unsafe native window modification.

Do not apply WPF `BlurEffect` to the entire live window or large scrolling lists.

Avoid expensive per-control blur. Prefer one backdrop layer per major region rather than blur effects on every child element.

Detect rendering performance where practical and disable expensive effects when necessary.

Do not add a large native interop dependency only for blur unless the benefit is clearly justified.

---

## 8. Material accessibility

All text and icons on translucent surfaces must remain readable over bright and dark backgrounds.

Use a tinted overlay in addition to blur. Do not rely on blur alone.

Ensure that:

- primary text remains high contrast
- secondary text remains readable
- selected navigation items remain identifiable
- keyboard focus remains visible
- status colors remain distinguishable
- transparency is reduced when accessibility or OS settings request reduced transparency

When transparency is disabled, replace material surfaces with theme-matched opaque surfaces while preserving spacing, hierarchy, borders, corner radii, navigation state, and usability.

Do not use transparency as the only visual separator.

---

## 9. Sidebar navigation

The selected navigation item should use:

- a soft lavender rounded background
- accent-colored icon and label
- slightly increased font weight
- no thick border

Hover behavior:

- subtle background change
- horizontal movement of approximately 2 pixels
- duration approximately 120 to 160 milliseconds

Selection transition:

- smooth color and background transition
- approximately 180 to 220 milliseconds
- avoid strong sliding or bouncing

Use proper vector icons:

- existing project icons
- WPF `PathGeometry`
- compatible SVG conversion
- Segoe Fluent Icons
- another lightweight vector source already present

Do not use emoji as production icons.

The sidebar should be one of the primary translucent material surfaces.

---

## 10. Overview statistic cards

Show six statistic cards:

- Managed Games
- Matched Saves
- Running
- Requires Attention
- Cloud Tasks
- Media Waiting to Sync

Each card contains:

- small rounded icon container
- title
- numeric value
- optional secondary description

Hover animation:

- move upward 2 to 3 pixels
- slightly increase elevation
- do not scale dramatically
- duration around 160 to 220 milliseconds

All cards must have consistent height and spacing. Data must come from real ViewModel state.

Cards should remain mostly opaque. Do not add heavy blur to every card.

---

## 11. Game list

Show:

- cover or icon
- game name
- platform
- save detection status
- save version count
- media count
- runtime status where available

Selected appearance:

- soft lavender background
- low-contrast accent border only if necessary
- no heavy blue outline
- clear selected state

When selecting a game:

- fade the previous detail content
- fade in the new content
- optionally translate new content by 6 to 10 pixels
- total duration approximately 180 to 240 milliseconds

Requirements:

- data binding
- selected item binding
- scrolling
- virtualization for large lists
- text trimming for long game names
- tooltip with the full name
- animations must not disable virtualization

Do not apply blur effects to list rows.

---

## 12. Game details

The header should show:

- game title
- save detection source
- current status
- Backup Now
- Validate
- Detect Paths
- More Actions

Button priority:

1. Backup Now: primary accent button
2. Validate and Detect Paths: secondary buttons
3. More Actions: compact icon button

Backup states:

**Idle**

- accent background
- standard label

**Running**

- loading indicator
- label changes to “Backing up”
- duplicate clicks disabled
- optional thin progress indicator
- progress linked to real task state

**Succeeded**

- briefly display success green
- label changes to “Backup completed”
- then return to idle

**Failed**

- show error state
- provide clear failure explanation
- provide access to logs or details
- do not automatically hide critical errors

**Cancelled**

- show a neutral cancelled state

All transitions must reflect actual backup command results.

---

## 13. Backup policy panel

Include:

- backup when game exits
- periodic backup while playing
- interval in minutes
- upload to cloud
- save policy button

Design:

- subtle rounded secondary background
- compact controls
- accent-colored checked state
- clean numeric input
- Save Policy must not visually compete with Backup Now

Bind all controls to existing configuration logic.

Show an unsaved-change indication where appropriate.

Validate interval input and prevent invalid or negative values.

---

## 14. Detail tabs

Tabs:

- Save History
- Screenshots and Videos
- Candidate Save Paths
- Tasks
- Errors and Logs

Selected appearance:

- accent-colored text
- 2-pixel indicator line
- no large filled background

Transition:

- indicator smoothly expands or moves
- content cross-fades
- optional 6 to 8 pixel translation
- duration around 180 to 220 milliseconds

Do not create excessive page motion.

---

## 15. Tasks and logs

Replace the heavy bordered data-grid appearance with a lightweight modern list or subtle table.

Fields:

- time
- task type
- game
- status
- summary
- expandable details

Task states:

- Succeeded: green dot and success text
- Running: accent or blue dot, subtle pulse only when useful
- Failed: red dot and failure text
- Pending: neutral gray dot
- Cancelled: muted gray or gray-orange state

Rows:

- no heavy boxed borders
- subtle dividers
- soft hover background
- failed rows must not be filled solid red
- clicking a failed row may reveal error summary, error code, log entry, retry action, and copy error action

Load logs incrementally or on demand. Do not load extremely large log files on the UI thread.

Do not apply glass effects to every row.

---

## 16. Toasts, dialogs, popovers, and errors

Use top-right toast notifications for short non-critical feedback.

Toast behavior:

- floating material surface
- small vertical offset
- opacity transition
- subtle dark translucent background
- approximately 2 to 3 seconds
- safe queueing or replacement
- do not cover essential controls

Use modal dialogs or clear error panels for serious errors.

Dialogs may use a stronger material than the sidebar, but readability is the priority.

Error presentation provides:

- understandable summary
- optional technical detail
- View Log action
- Copy Error action
- Retry action when safe

Dangerous actions such as deleting or restoring saves must use confirmation dialogs showing:

- game name
- save version
- operation consequences

Dangerous primary actions use red. Default focus must not be on the destructive action. Escape closes non-critical dialogs.

Context menus and popovers may use compact floating material surfaces when technically safe.

---

## 17. Animation system

The animation style should feel refined and Apple-inspired while remaining appropriate for WPF and Windows.

Prefer animating:

- `Opacity`
- `TranslateTransform`
- `ScaleTransform`
- `Color`
- `Brush`
- indicator position
- shadow opacity where inexpensive

Avoid frequently animating:

- `Width`
- `Height`
- `Margin`
- `GridLength`
- large `BlurEffect`
- properties causing expensive layout recalculation

Recommended durations:

- hover: 120 ms
- pressed: 80 to 100 ms
- navigation selection: 180 to 220 ms
- tab transition: 180 to 220 ms
- page entry: 220 to 280 ms
- toast: 220 to 260 ms
- dialog entry: 200 to 240 ms
- success feedback: 250 to 400 ms
- one-time failure shake: 200 to 280 ms

Recommended easing:

- `CubicEase`
- `QuarticEase`
- `SineEase`
- EaseOut
- very limited `BackEase` for subtle success feedback only

Pressed button:

- scale to approximately 0.97 or 0.98
- smoothly return on release
- no strong bounce

Page transition:

- previous content fades out
- new content fades in
- optional 6 to 10 pixel movement
- no large-distance page motion

Failure shake:

- maximum horizontal movement of 3 to 4 pixels
- only once
- no continuous flashing

Centralize reusable animations in a resource dictionary.

Prefer:

- `VisualStateManager`
- style triggers
- storyboards
- event triggers where appropriate
- `RenderTransform`

Avoid a timer for every control.

Respect reduced-motion preferences where practical. Disable unnecessary animation when the window is not visible. Do not animate blur radius continuously.

---

## 18. Light, dark, and Playnite theme support

Prefer three theme modes if compatible:

- Follow Playnite
- Light
- Dark

Requirements:

- colors come from theme resources
- material tints come from theme resources
- opacity values come from shared tokens
- theme changes update immediately
- no color hardcoding inside individual views
- proper contrast in both themes
- dark mode redesigns backgrounds, dividers, shadows, selection states, and material tints
- do not merely invert colors

Persist the selected theme mode using the plugin’s existing settings system.

When following Playnite, adapt to the host theme without losing the GameSaveCenter visual hierarchy.

---

## 19. Accessibility and usability

Support:

- Tab keyboard navigation
- Enter and Space activation
- Escape to close dialogs
- visible keyboard focus
- hover state
- pressed state
- disabled state
- loading state
- empty state
- error state
- tooltip for truncated values
- high DPI
- minimum window dimensions
- screen-reader-friendly labels where practical
- reduced transparency
- reduced motion where practical

Do not sacrifice readability for visual imitation.

Maintain sufficient text contrast.

Do not use color as the only status indicator. Use color plus icon, dot, or text.

Do not place low-contrast gray text directly over unpredictable blurred backgrounds.

---

## 20. Performance

1. Do not apply `BlurEffect` to large numbers of list items.
2. Do not apply `BlurEffect` to the entire window content tree.
3. Reuse brushes and transforms.
4. Freeze `Freezable` resources where safe.
5. Enable UI virtualization for large lists.
6. Prefer `RenderTransform` for movement.
7. Do not scan files, compress saves, upload files, or read large logs on the UI thread.
8. Load task and log data incrementally.
9. Unsubscribe event handlers correctly.
10. Avoid retaining windows or ViewModels after closure.
11. Stop animations and timers when no longer required.
12. Avoid repeatedly recreating `ResourceDictionary` instances.
13. Do not break Playnite startup performance.
14. Use one major backdrop layer per region rather than many small blur effects.
15. Disable expensive material effects on unsupported or slow systems.
16. Ensure fallback surfaces remain visually complete.

---

## 21. Business functionality that must remain working

Preserve and reconnect:

- game detection
- Ludusavi availability detection
- save backup
- save restore
- save validation
- candidate path detection
- automatic backup policies
- game runtime detection
- task history
- cloud upload
- media synchronization
- error reporting
- log viewing
- plugin settings
- existing Playnite integration

Do not remove existing error handling.

Do not swallow exceptions merely to keep the interface visually clean.

Do not report success before the actual task succeeds.

Do not make business functionality depend on visual effects.

---

## 22. Build, version, package, and installation validation

This project previously had a problem where source code was updated but Playnite continued running an older installed plugin version.

Prevent this from happening again.

After implementation:

1. Stop Playnite and related GameSaveCenter worker processes before installation.
2. Build from the current working tree.
3. Ensure `extension.yaml` version matches the intended release version.
4. Ensure assembly version and file version are correct.
5. Ensure package file names are not hardcoded to an older version.
6. Do not silently ignore failure when deleting or replacing the old extension.
7. Detect the actual Playnite extension installation directory.
8. Validate the package in a temporary directory before replacing the installed extension.
9. Replace the extension atomically where practical.
10. Verify installed:
    - `extension.yaml`
    - main DLL
    - theme XAML resources
    - material resource dictionaries
    - file modification timestamps
    - assembly version
11. Verify Playnite’s extension manager displays the new version.
12. Do not report successful installation when build or copy failed.
13. Output the final installed extension path.
14. Preserve the existing reliable build and install workflow where one exists.

Do not change the plugin ID.

Do not break existing packaging scripts.

---

## 23. Implementation order

### Phase 1

- inspect repository
- detect framework and UI technology
- map existing business bindings
- inspect current theme integration
- determine safe material implementation
- report the implementation plan

### Phase 2

- create centralized design tokens
- create light and dark theme resources
- create shared control styles
- create material surface resources
- redesign the main window and sidebar

### Phase 3

- redesign overview statistic cards
- redesign game list
- redesign game detail area
- redesign backup policy panel
- redesign tabs
- redesign task and log views

### Phase 4

- implement safe sidebar translucency
- implement safe floating material surfaces
- implement fallback opaque surfaces
- implement reusable navigation animations
- implement button visual states
- implement loading states
- implement page transitions
- implement toast notifications
- implement dialog transitions
- implement success and error feedback

### Phase 5

- reconnect all existing commands
- verify async behavior
- verify error propagation
- verify UI thread safety
- verify settings persistence
- verify task progress binding
- verify theme persistence
- verify transparency fallback

### Phase 6

- build
- fix XAML parser errors
- fix binding errors
- test Playnite loading
- test plugin window opening
- test window resizing
- test DPI scaling
- test light and dark themes
- test transparency enabled and disabled
- test unsupported backdrop fallback
- test backup and restore workflows
- test installation and deployed version

---

## 24. Acceptance criteria

The work is complete only when:

1. The project builds successfully.
2. Playnite loads the extension successfully.
3. The main UI opens and closes correctly.
4. Backup, restore, validation, path detection, cloud sync, and media sync remain operational.
5. The interface no longer resembles a traditional blue-bordered enterprise dashboard.
6. The light theme has a polished macOS-inspired hierarchy.
7. The dark theme remains readable and consistent.
8. Navigation, buttons, tabs, loading, and toast transitions are smooth.
9. Animations do not noticeably stutter.
10. Resizing does not cause overlap or clipping.
11. The UI remains usable at 125%, 150%, and 200% DPI.
12. Buttons have hover, pressed, disabled, and loading states.
13. Status colors are centralized.
14. There are no obvious WPF binding errors.
15. Business success states are not simulated.
16. The plugin ID remains unchanged.
17. Build and development installation workflows still work.
18. The installed version is verified instead of assumed.
19. No HTML or WebView UI has been introduced.
20. No emoji remain as production icons.
21. The sidebar uses a restrained Apple-inspired material when technically supported.
22. Toasts, menus, and dialogs use consistent floating material surfaces.
23. Normal content cards remain mostly opaque and readable.
24. The UI does not look like a generic glassmorphism website.
25. The UI works correctly when transparency effects are disabled.
26. The UI works correctly when native backdrop APIs are unavailable.
27. No large scrolling region uses expensive per-item blur.
28. Visual effects do not block or alter business functionality.

---

## 25. Final report

After completing the implementation, provide:

- detected original UI architecture
- modified files
- newly created files
- removed or replaced files
- final UI structure
- theme implementation
- material and blur implementation
- Windows fallback implementation
- animation implementation
- ViewModel and command changes
- business logic integration points
- external packages added, with reasons
- build result
- automated test result
- Playnite loading result
- installed extension path
- installed extension version
- known limitations
- manual verification checklist

Do not only provide sample snippets.

Do not stop after creating a static visual shell.

Implement the redesign directly in the existing C# Playnite extension project and ensure that the real plugin remains functional.
