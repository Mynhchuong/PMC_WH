# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

PMC WH — a warehouse management system for PMC. .NET 9 solution (`PmcWh.sln`) with two web projects and a placeholder Android app:

- **PmcWh.Api** — ASP.NET Core Web API. Talks to Oracle directly via `Oracle.ManagedDataAccess.Core` (no ORM/EF). Swagger UI enabled in Development.
- **PmcWh.Web** — ASP.NET Core MVC app (Razor views, not Blazor — the `Components/` folder is unused leftover template scaffolding from `dotnet new`). This is where the Material Dashboard UI lives.
- **PmcWh.Android** — empty, not started. Just a README with Android Studio setup instructions.

Not a git repository yet.

## Commands

Run from the repo root (where `PmcWh.sln` is):

```bash
dotnet build                              # build everything
dotnet build PmcWh.Api/PmcWh.Api.csproj   # build a single project
dotnet build PmcWh.Web/PmcWh.Web.csproj
```

Run each project from its own folder (both default to `ASPNETCORE_ENVIRONMENT=Development` via `launchSettings.json`):

```bash
cd PmcWh.Api && dotnet run   # http://localhost:5073, swagger at /swagger
cd PmcWh.Web && dotnet run   # http://localhost:5139
```

No test projects exist yet.

## Architecture notes

### Api ↔ Oracle
`OracleDataService` (`PmcWh.Api/Services`) wraps `Oracle.ManagedDataAccess` directly — controllers call `QueryAsync(sql, params)` and get back `IEnumerable<Dictionary<string, object?>>` rows. Connection config comes from the `Oracle` section in `appsettings.json`, bound to `OracleConnectionOptions`. There's no repository/EF layer — new endpoints follow the same raw-SQL-via-service pattern used in `HealthController`.

### Web layout & sidebar menu
The Material Dashboard (Creative Tim) asset bundle lives in `PmcWh.Web/wwwroot/assets`. The sidebar/menu system is a deliberate, reusable pattern — **follow it when adding new pages/menu items**, don't hand-roll new sidebar markup:

- `Models/SideMenuItem.cs` — menu node (`Id`, `Title`, `Icon`, `Controller`, `Action`, `Children`, optional `VisibleWhen` predicate for future role-based visibility).
- `Helpers/SideMenuBuilder.cs` — the single place that declares the menu tree. Add new pages here as children of an existing group, or add a new top-level group.
- `Views/Shared/_SideNav.cshtml` — renders the tree as a Bootstrap collapse accordion, builds each link with `Url.Action(item.Action, item.Controller)`, and highlights the active item by comparing that generated URL against `Context.Request.Path`.
- `Views/Shared/_Layout.cshtml` — wires the sidebar (`#wh-sidebar`) + main content together. Desktop collapse/expand state persists via `localStorage` (`wh_menu_hidden`); mobile uses an off-canvas slide-in with a `.wh-backdrop` overlay. This mirrors the pattern used in the separate HR_web ("My SAMHO") project, renamed to a `wh-` prefix to avoid collisions.

**Icons:** use `<span class="material-icons">icon_name</span>` (Google's classic ligature font, already linked in `_Layout.cshtml`) for content icons. For UI chrome that must render reliably regardless of font load timing (e.g. carets/chevrons), prefer a CSS-only shape instead of another icon-font ligature — a missing/late-loading font glyph renders as a broken tofu box. **Font Awesome is not loaded anywhere in this project**, but `material-dashboard.css` was authored assuming it is: it auto-injects Font Awesome glyphs (`content: "\f107"`, `font-family: FontAwesome`/`"Font Awesome 5 Free"`) via `::after` on both `[data-bs-toggle="collapse"]` sidenav triggers and *every* `.dropdown-toggle`. Both are neutralized already (sidenav caret suppressed in `_SideNav.cshtml`'s `<style>`; the dropdown caret reset to Bootstrap's native CSS-triangle in `wwwroot/css/site.css`) — if a new broken-glyph tofu box shows up anywhere, this is almost certainly why; check for another undiscovered `material-dashboard.css` rule referencing FontAwesome before assuming it's something else.

**Gotcha:** `~/...` tilde paths only resolve inside HTML attributes that Razor's `UrlResolutionTagHelper` processes (`href`, `src`, etc.) — they are **not** rewritten inside a `<style>` block's raw CSS (e.g. `background-image: url('~/...')` renders the literal tilde and 404s). Compute the path first with `@{ var x = Url.Content("~/..."); }` and interpolate `@x` into the CSS instead (see `Views/Account/Login.cshtml`).

### Authentication (currently fake — real check pending Oracle schema)
Cookie authentication is wired up in `Program.cs`: every controller requires login by default (global `AuthorizeFilter`), opt out per-controller with `[AllowAnonymous]`. `Controllers/AccountController.cs` handles `Login`/`Logout`, but the credential check is a **hardcoded fake** (`admin` / `admin123`, constants at the top of the controller) — there is no Users table/schema defined yet. When one exists, replace only the body of `AccountController.Login` (POST) with a real query (same raw-SQL-via-service pattern as `OracleDataService`); nothing else needs to change. The signed-in user's display name comes from the `ClaimTypes.GivenName` claim set at sign-in, read in `_Layout.cshtml` for the header dropdown.

### Editing `.cshtml` while `dotnet run` is running
`AddRazorRuntimeCompilation()` is wired up in `Program.cs` (Development only), so Razor view edits are picked up on the next request — no restart needed. Plain `.cs` changes (controllers, helpers, models) still require a rebuild + restart.

### UI text language
Existing UI strings mix English (page titles, model property names) and Vietnamese (menu labels in `SideMenuBuilder`, user-facing copy). Match whichever convention the surrounding code already uses in a given file rather than picking one globally.

### UI feedback — toasts, confirm/delete popups, generic modal
`wwwroot/js/ui-helper.js` defines `window.PmcUI` (vanilla JS, built on Bootstrap 5.3's native `Toast`/`Modal` — no SweetAlert2, it's Pro-only and not in this asset bundle). Already loaded globally in `_Layout.cshtml`. Always use this instead of `alert()`/`confirm()` or hand-rolled toast/modal markup:

- `PmcUI.success(msg)` / `.error(msg)` / `.warning(msg)` / `.info(msg)` — top-right Bootstrap toast, auto-dismiss.
- `PmcUI.confirm(msg, { title, confirmText, cancelText, variant })` — returns `Promise<boolean>`; `await` it before proceeding.
- `PmcUI.confirmDelete(msg)` — shorthand for a danger-styled delete confirmation.
- `PmcUI.popup(title, bodyHtml)` — generic modal for arbitrary content (details view, embedded form, etc.), just a close button.

### Never hardcode links — always generate them
No raw path strings for internal navigation (`href="/Home/Index"`, `Url = "~/Home/Index"`, string-concatenated URLs, etc.), in either C# or `.cshtml`. Always resolve links through routing helpers so a controller/action rename or route change can't silently break a link:

- In `.cshtml`: `@Url.Action("Action", "Controller")`, or `asp-controller`/`asp-action` tag helpers on `<a>` tags.
- In C# menu/data structures (like `SideMenuItem`): store `Controller` + `Action` (or route values), never a pre-built path string — resolve to a URL only at render time via `Url.Action(...)`.
- Static assets (css/js/img under `wwwroot`) are the one exception — those use `~/...` with `asp-append-version="true"`, since they aren't routed controller actions.
