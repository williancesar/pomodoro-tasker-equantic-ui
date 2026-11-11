# pomodoro-tasker-equantic-ui

A personal todo list, planner and pomodoro timer for one user, built **only** with
[eQuantic UI](https://github.com/eQuantic/equantic-ui): C# components compiled to JavaScript at
build time, served by ASP.NET Core. No hand-written JavaScript, HTML or CSS. Interface in PT-BR.

It is the web sibling of pomodoro-tasker, a desktop app
(SvelteKit + Tauri, not public). Same domain rules, same SQL, same SQLite schema: a database file from the
desktop app opens here as it is.

> eQuantic UI is a development preview (`0.2.0-preview.68`, pinned in `global.json`). This
> project doubles as a field report on it: see [What the framework could not do](#what-the-framework-could-not-do).

## Run

Needs the .NET 10 SDK and nothing else.

```bash
dotnet run --project src/PomodoroTasker.Web      # http://localhost:5000
dotnet watch --project src/PomodoroTasker.Web    # with hot reload
dotnet test                                      # domain and data layer, 35 tests
```

The data lives in `tasker.db` where the server runs; set `TASKER_DB` to use another file.
There is no login: run it on your own machine, bound to localhost.

## Layout

- `src/PomodoroTasker.Core/` — plain .NET, no eQuantic. The day arithmetic, the pomodoro state
  machine, recurrence rules, report shaping, and `Repo.cs` with all SQL. `Migrations/*.sql` are
  the desktop app's migration files, unchanged.
- `src/PomodoroTasker.Web/` — the eQuantic UI app.
  - `Pages/` — one component per screen (`/`, `/fechar-dia`, `/dias-fechados`, `/listas`,
    `/projetos`, `/rotinas`, `/relatorios`, `/configuracoes`).
  - `Components/` — `TimerBlock`, `TaskRows`, `TaskEditor`, and the small shared `Parts`.
  - `Server/Api.cs` — everything a Server Action does; `Views.cs` — what it answers.
  - `Theme/TaskerTheme.cs` — paper, ink and one magenta accent, as an `IAppTheme`.
  - `Strings.cs` — every interface string.
- `tests/PomodoroTasker.Core.Tests/` — the desktop app's vitest suite, ported to xUnit, against
  in-memory SQLite with the real migrations.

## How it is put together

- **Domain logic runs on the server.** Components only draw and call `[ServerAction]` methods;
  the server answers view models with their labels already worded. Nothing of the domain is
  transpiled, so nothing depends on C# and JavaScript agreeing on a date or a rounding.
- **The timer is an end timestamp**, stored in the database. The browser redraws the clock twice
  a second from it and asks the server when it reaches zero; the server applies every phase end
  that has passed, including the ones that passed while nothing was open.
- **Every server call first closes the days that ended**, in order, exactly as the desktop app
  does at start-up.

## Differences from the desktop app

| Desktop | Here |
|---|---|
| Tray icon and menu, close-to-tray, autostart | none: a browser tab |
| Sound and system notification at a phase end | a toast on the page, when the page is open |
| Drag and drop to reorder | "Mover para cima / para baixo" in the row menu |
| Theme: system, light, dark | light or dark; the system's until one is chosen |
| Hand-drawn CSS bar charts, dither textures | the framework's `BarChart`; flat colours |
| Per-statement writes ordered to survive a crash | the same order, inside a transaction |

## What the framework could not do

Found while building this against `0.2.0-preview.68`:

- **No static export.** A web app is an ASP.NET Core server; there is no client-only build.
- **No browser API for sound, system notifications, or the document title at run time**, and no
  documented way to call JavaScript. The phase-end alarm became an in-page toast.
- **No drag-to-reorder.** `Draggable` moves along one axis and `DropTarget` takes files.
- **Theme mode is light or dark.** `IThemeController` cannot go back to "follow the system".
- **`Dialog` holds a title, a text and actions.** A dialog with a field (the stop-focus reason,
  rename) needed its own layer built from `Overlay`, `Stack` and `Shortcut` (`Parts.Modal`).
- **`Checkbox` always draws its label**, so a row whose title is a separate pressable needed its
  own checkbox from `Pressable` with `PressableRole.Checkbox`.
- **`Stepper` keeps rounded corners** whatever `IAppTheme.Shape` answers.

And things that are easy to get wrong:

- **A component with Server Actions is built by the server's container** to run them. A
  constructor parameter that is not a service fails at run time with a 500, not at build. Such
  components take their inputs through properties.
- **A stateful child keeps its first inputs** unless it overrides `AdoptConfig`.
- **Type names are global to the client build.** An app type named `ListView` or `Ui` collides
  with the framework's `ListView` and `UI` (the build says so: EQ1005).
- **Fonts under `Assets/` must be `.ttf` or `.otf`**; `[assembly: GoogleFont(...)]` fetches and
  pins them instead.
- **`BarChart` with `plotHeight: 120`** drew its bars past the axis on the reports screen; 160
  did not. Whole-number data also needs an explicit axis maximum or the ticks repeat ("0, 0, 1, 1").

What worked well: the build caught most mistakes in C#, Server Actions removed the whole API
layer, and the theme and fonts took one file.

## License

MIT, see `LICENSE`. Silkscreen and Schibsted Grotesk are under the SIL Open Font License,
fetched from Google Fonts at build time and pinned in `src/PomodoroTasker.Web/fonts.lock.json`.
