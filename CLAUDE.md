# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Output Rules

- Write clean code with ZERO inline comments unless strictly required.
- Do not add descriptive notes, visual explanations, or business logic summaries inside code.
- Avoid conversational intro/outro text; return only the code.

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
`OracleDataService` (`PmcWh.Api/Services`) wraps `Oracle.ManagedDataAccess` directly — no ORM/EF, no repository layer. Two methods:

- `QueryAsync(sql, params)` — SELECT, returns `IEnumerable<Dictionary<string, object?>>` rows (see `HealthController`).
- `ExecuteAsync(sql, params)` — INSERT/UPDATE/DELETE, returns affected row count.
- `QueryPagedAsync(innerSql, page, pageSize, params)` — pass a plain `SELECT ... ORDER BY ...` (no paging in it); wraps it in Oracle 10g's classic double-`ROWNUM` idiom and returns `PagedResult<Dictionary<string, object?>>` (`Items`, `Page`, `PageSize`, `TotalCount`, computed `TotalPages`). Use this instead of hand-writing `ROWNUM` paging per endpoint.

Connection config comes from the `Oracle` section (`Username`, `Password`, `DataSource`), bound to `OracleConnectionOptions`. **Real credentials live in .NET User Secrets, not `appsettings.json`/`appsettings.Development.json`** — those files are committed to the (GitHub) repo, User Secrets are stored outside the repo (`~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`) and load automatically in Development, no code change needed. To set/inspect: `cd PmcWh.Api && dotnet user-secrets set "Oracle:Password" "..."` / `dotnet user-secrets list`. Live connectivity to the real Oracle 10g box (`192.168.1.32:1521`, SID `vmes`) has been verified working via `GET /api/Health`.

New endpoints follow this same raw-SQL-via-service pattern; always use `OracleParameter` bind variables, never string-concatenate values into SQL. All three methods set `command.BindByName = true` — **ODP.NET binds by ordinal position by default**, not by the `:name` in the SQL text, which silently breaks the moment parameters aren't supplied in left-to-right textual order (exactly what `QueryPagedAsync` does, appending its own `:pmcEndRow`/`:pmcStartRow` after the caller's params). Keep `BindByName = true` on any new raw `OracleCommand` you create outside this service.

**Target database is Oracle 10g — write SQL as if nothing past 10g exists.** Concretely, avoid/don't use:
- `OFFSET ... FETCH NEXT` (12c+) — paginate with a `ROWNUM`-wrapped subquery instead.
- Identity columns (`GENERATED ... AS IDENTITY`, 12c+) — use a `SEQUENCE` and reference `seq.NEXTVAL` in the insert.
- `LISTAGG` (11g R2+), recursive `WITH` CTEs (11g R2+), `JSON_TABLE`/`JSON_VALUE`/JSON column type (12c+), `MERGE` enhancements newer than 9i.
- Anything found in Oracle docs tagged 11g or later — if unsure whether a function/syntax existed in 10g, assume it didn't and ask, or use the older equivalent (e.g. `CONNECT BY` instead of recursive CTE).

### Web layout & sidebar menu
The Material Dashboard (Creative Tim) asset bundle lives in `PmcWh.Web/wwwroot/assets`. The sidebar/menu system is a deliberate, reusable pattern — **follow it when adding new pages/menu items**, don't hand-roll new sidebar markup:

- `Models/SideMenuItem.cs` — menu node (`Id`, `Title`, `Icon`, `Controller`, `Action`, `Children`, optional `VisibleWhen` predicate for future role-based visibility).
- `Helpers/SideMenuBuilder.cs` — the single place that declares the menu tree. Add new pages here as children of an existing group, or add a new top-level group.
- `Views/Shared/_SideNav.cshtml` — renders the tree as a Bootstrap collapse accordion, builds each link with `Url.Action(item.Action, item.Controller)`, and highlights the active item by comparing that generated URL against `Context.Request.Path`.
- `Views/Shared/_Layout.cshtml` — wires the sidebar (`#wh-sidebar`) + main content together. Desktop collapse/expand state persists via `localStorage` (`wh_menu_hidden`); mobile uses an off-canvas slide-in with a `.wh-backdrop` overlay. This mirrors the pattern used in the separate HR_web ("My SAMHO") project, renamed to a `wh-` prefix to avoid collisions.

**Icons:** use `<span class="material-icons">icon_name</span>` (Google's classic ligature font, already linked in `_Layout.cshtml`) for content icons. For UI chrome that must render reliably regardless of font load timing (e.g. carets/chevrons), prefer a CSS-only shape instead of another icon-font ligature — a missing/late-loading font glyph renders as a broken tofu box. **Font Awesome is not loaded anywhere in this project**, but `material-dashboard.css` was authored assuming it is: it auto-injects Font Awesome glyphs (`content: "\f107"`, `font-family: FontAwesome`/`"Font Awesome 5 Free"`) via `::after` on both `[data-bs-toggle="collapse"]` sidenav triggers and *every* `.dropdown-toggle`. Both are neutralized already (sidenav caret suppressed in `_SideNav.cshtml`'s `<style>`; the dropdown caret reset to Bootstrap's native CSS-triangle in `wwwroot/css/site.css`) — if a new broken-glyph tofu box shows up anywhere, this is almost certainly why; check for another undiscovered `material-dashboard.css` rule referencing FontAwesome before assuming it's something else.

**Gotcha:** `~/...` tilde paths only resolve inside HTML attributes that Razor's `UrlResolutionTagHelper` processes (`href`, `src`, etc.) — they are **not** rewritten inside a `<style>` block's raw CSS (e.g. `background-image: url('~/...')` renders the literal tilde and 404s). Compute the path first with `@{ var x = Url.Content("~/..."); }` and interpolate `@x` into the CSS instead (see `Views/Account/Login.cshtml`).

### Excel import/export
Users work in Excel heavily, so import/export is a core, recurring feature — not a one-off. `PmcWh.Web/Helpers/ExcelHelper.cs` (uses `ClosedXML`, MIT-licensed — not EPPlus, which is commercial-licensed past v4) is the shared helper, already round-trip tested:

- `ExcelHelper.ReadRows(stream)` — parses the first sheet of an uploaded `.xlsx` into `List<Dictionary<string, string?>>`, keyed by the header row. Values are always raw strings (or `null` for empty cells); parse/convert types yourself when building the SQL.
- `ExcelHelper.ImportBatchSize` (= **40**) and the `.ToBatches()` extension — **always import in batches of 40 rows, never the whole file in one go.** This is a fixed project rule (not just a suggestion), meant to keep each write short/safe against Oracle 10g. Loop `rows.ToBatches()` and run one `OracleDataService.ExecuteAsync(...)` (or a transaction per batch) per chunk — don't do a single request for the whole file.
- `ExcelHelper.WriteRows(headers, rows)` — builds an `.xlsx` in memory and returns `byte[]`; return it from a controller action via `File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "filename.xlsx")`.

No import/export controller/view exists yet — wire the helper into a real screen once the target table/columns are known (SQL pending).

### Pagination
Any list page must paginate — pair these two, don't build ad-hoc paging:

- **Api**: `OracleDataService.QueryPagedAsync(innerSql, page, pageSize, params)` (see above) does the Oracle 10g `ROWNUM` paging and returns `PagedResult<T>`.
- **Web**: `Helpers/PaginationHelper.GetPageWindow(currentPage, totalPages, window=2)` computes which page numbers to show (caps the window instead of rendering hundreds of `<li>`s; `0` in the result means render an "…" ellipsis, not a real page). `Views/Shared/_Pagination.cshtml` is a ready-to-use partial that renders it as Bootstrap pagination markup — pass it a `PaginationViewModel { Page, TotalPages, Controller, Action, RouteValues }` (`RouteValues` = any other query-string filters to preserve, e.g. a search keyword) and render with `@await Html.PartialAsync("_Pagination", model)`.

No real paged list screen exists yet (pending SQL); both pieces are unit-verified in isolation (page-window edge cases, e.g. first/last page, page count ≤ window) but not yet exercised against a live paged query.

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
- `PmcUI.showLoading()` / `.hideLoading()` — full-page overlay + spinner while a query/API call is in flight; call-count based, so concurrent calls don't hide it early. `await PmcUI.withLoading(() => fetch(...))` wraps show/hide around an async call automatically.

### Never hardcode links — always generate them
No raw path strings for internal navigation (`href="/Home/Index"`, `Url = "~/Home/Index"`, string-concatenated URLs, etc.), in either C# or `.cshtml`. Always resolve links through routing helpers so a controller/action rename or route change can't silently break a link:

- In `.cshtml`: `@Url.Action("Action", "Controller")`, or `asp-controller`/`asp-action` tag helpers on `<a>` tags.
- In C# menu/data structures (like `SideMenuItem`): store `Controller` + `Action` (or route values), never a pre-built path string — resolve to a URL only at render time via `Url.Action(...)`.
- Static assets (css/js/img under `wwwroot`) are the one exception — those use `~/...` with `asp-append-version="true"`, since they aren't routed controller actions.
