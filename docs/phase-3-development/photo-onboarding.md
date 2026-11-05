# Real collection and photo onboarding

Implemented locally on November 4, 2026. This guide supplements the October frontend
handoff; it does not certify the running production installation.

## Owner workflow

1. Sign in with an `Admin` account and open `/admin`.
2. Choose **Add a card** and enter its number, player, description and condition.
   Select front, back or detail photographs directly in that form; local thumbnails
   appear before saving. Optional collector fields are collapsed under **Collection details**.
   Card numbers remain unique in the existing data model.
3. Choose **Save card**. The draft is saved first, then selected photos upload one at a
   time while the file input remains mounted. Partial upload failures keep the draft
   and any completed photos; review those before retrying. In the saved photo panel,
   choose **Use as cover** or upload additional photographs with captions. Up to six
   images are allowed per card. Browser file selection replaces the previous selection.
4. Choose **Publish card**. Publication requires a description and at least one photo.
   The card appears in `/collection`, independently of whether it is for sale.
5. To sell, edit the saved card, enable **List in marketplace**, enter a positive USD
   price and save. It then appears in `/marketplace` and can use existing Stripe Checkout.
6. Unpublish to remove the card from public API results and new photo requests.
   Unpublishing also clears its sale flag. A published card's last photo cannot be removed.

The original 64-record illustrated checklist remains at `/archive`. It is not imported
automatically, and its illustrations are not substituted for the owner's real photographs.
The existing `SystemAdmin` statistics role does not receive inventory mutation rights.

## Source map and data flow

| Location | Responsibility |
| --- | --- |
| `src/Client/Pages/Admin/AdminCards.razor` | Paged inventory, draft/detail editor and optional sale settings |
| `src/Client/Components/PhotoOnboarding.razor` | Sequential uploads, authenticated draft previews, cover/removal and publication |
| `src/Client/Components/LiveCatalog.razor` | Real public collection and marketplace, paged search, on-demand detail gallery |
| `src/Client/Components/CardImage.razor` | Lazy thumbnails/full-size details using same-origin URLs |
| `src/Client/Services/ApiClient.cs` | API contracts, bounded client timeouts and upload client |
| `src/Api/Controllers/CardsController.cs` | Authorized inventory CRUD, validation and audit writes |
| `src/Api/Controllers/CardPhotosController.cs` | Authorized uploads/changes and publication-aware image delivery |
| `src/Api/Controllers/CollectionController.cs` | Public published-card pages and details |
| `src/Api/Services/CardQueries.cs` | Bounded SQL queries, filters, sorting and scalar public projections |
| `src/Api/Services/PhotoStorage.cs` | Image validation, metadata removal, orientation, resizing and private files |
| `src/Api/Data/Migrations/*CardPhotoOnboarding*` | SQL Server schema additions |
| `deploy/sql/` | Idempotent schema SQL and example scoped grants |
| `tests/MarksBaseballCards.Tests/` | HTTP authorization, workflow, image and SQL concurrency tests |

The browser talks only to the website/API. EF Core talks to SQL Server using the server's
connection string. SQL contains card descriptions, publication state, photo IDs, captions,
order, dimensions and sizes. JPEG files and thumbnails live outside `wwwroot` in persistent
storage; image bytes and original filenames are not stored in SQL.

Relevant routes:

| Route | Access |
| --- | --- |
| `GET /api/cards/page`, `GET /api/cards/{id}` | `Admin`; draft and published metadata |
| `POST /api/cards`, `PUT/DELETE /api/cards/{id}` | `Admin`; validated mutations |
| `POST /api/cards/{id}/photos` | `Admin`; multipart `file` and optional `caption` |
| `DELETE /api/cards/{id}/photos/{photoId}` | `Admin`; photo must belong to the card |
| `PUT /api/cards/{id}/photos/{photoId}/cover` | `Admin`; photo must belong to the card |
| `GET /api/collection`, `GET /api/collection/{id}` | Published cards only |
| `GET /api/marketplace/page` | Published listings; optionally sold records |
| `GET /api/card-photos/{photoId}/{image\|thumbnail}` | Published card, or authenticated `Admin` for a draft |

## Security boundaries

The API validates JWT issuer, audience, signature, expiry and role. Client-side route
checks are convenience only. Draft images require a valid bearer token; their IDs alone
do not grant access. Bearer headers are attached only to the configured same origin,
never to an external image host. Draft thumbnails are fetched with authorization and
displayed in memory; no JWT is placed in a URL.

Uploads are limited to 30 MiB of actual stream data, six photos per card and 16 megapixels.
Only decoded, still JPEG/PNG/WebP images are accepted. SVG, HTML, GIF, invalid/truncated
images and oversized pixel dimensions are rejected. Original names and MIME headers are
not trusted. Files are re-encoded as JPEG, flattened on white, EXIF-oriented and stripped
of original metadata, including GPS. Full images are capped at a 1600-pixel long edge;
thumbnails at 360 pixels. Storage names are server-generated GUIDs.

The 30 MiB limit is shared by browser streams, frontend checks, server file checks and
actual-stream validation (`CardPhotoLimits.MaxFileBytes = 31,457,280`). The upload endpoint
overrides Kestrel's default request limit to 31,522,816 bytes, allowing multipart overhead;
other endpoints retain their limits. A reverse proxy must also allow this size: for nginx,
set `client_max_body_size 32m` on the upload route/location; with IIS, configure its separate
request-filtering limit to at least 33,554,432 bytes. These infrastructure settings were
not applied to a running server. Uploads remain subject to the independent pixel/format
limits; SQL stores metadata and references, not 30 MB image blobs. No schema change is
needed for the larger original input. Images continue to be optimized before persistence.

Pre-save thumbnails use revocable browser object URLs, avoiding a base64 copy of each
30 MB input in WASM memory. URLs are revoked when the selection is replaced or the editor
closes. Selected files upload only on Save and are not persisted in browser storage.

Upload processing is limited to two simultaneous transforms per process; excess work
gets 429 rather than an unbounded queue. Uploads are also limited to 20 per minute per
authenticated identity; the login limiter remains enabled. These are application limits,
not network-level DDoS protection or an overall disk quota.

EF queries are parameterized. SQL rowversion detects concurrent writes; conflicting edits
return 409 instead of overwriting silently. Photo quota/order changes also update the
parent rowversion. Audit records accompany inventory mutations in the same database save.
Razor encodes descriptions/captions as text; arbitrary markup is not rendered.

Photos are served through authorization-aware endpoints with fixed JPEG MIME, `nosniff`
and a restrictive image-response CSP. Drafts use `no-store`; published images use private
browser caching for at most 300 seconds, avoiding shared proxy caching. Already downloaded
public photographs cannot be revoked from a visitor's device. No CORS wildcard or HTTP-to-HTTPS
asset upgrade was introduced. Existing Blazor script execution is preserved.

Normal production error handling uses sanitized ProblemDetails. The SQL connection uses
encryption with certificate validation in `.env.example`, and no sensitive EF logging is
enabled. JWT storage remains the existing browser localStorage design: preventing XSS is
still essential; this work does not introduce an HTTP-only session migration or E2E encryption.
TLS termination, trusted proxy addresses, firewall/WAF, backups and secret rotation remain
operator responsibilities. Do not expose SQL Server to public browser clients.

## Performance and timeouts

Public pages request 24 records; admin pages request 25. The server enforces a maximum of
50 records per page, bounded search length and bounded page offsets. Count and projected
page queries are separate, bounded requests; no image blobs or entire inventory are sent.
Public list projections select the cover ID without per-record follow-up queries. Detail
photos load on demand and thumbnails use lazy browser decoding.

Search is debounced by 350 ms; new searches cancel previous requests and stale responses
cannot replace current results. Read endpoints have a 12-second timeout, SQL commands a
10-second timeout and one bounded transient retry. Regular client calls time out after
20 seconds; uploads use a separate 60-second client and are not automatically retried.
Request cancellation propagates to paged SQL reads. SQL timeout/retry settings cap individual
attempts and do not promise a total end-to-end latency on every failure path.

New indexes cover publication/listing filters and photo ordering. Player-name substring
search can still scan large inventories; use measured SQL plans and load tests before
adding full-text search or more indexes. Actual latency requires staging measurements.

## Configuration, ports and local development

Copy `.env.example` to the ignored `.env` for Docker Compose and supply real secrets
securely. `dotnet run` does not load this file automatically: use environment variables
or development user secrets. Never copy real secrets into scripts, commits or browser code.

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings__Default` | External SQL Server, application-only user, encrypted connection and bounded pool |
| `Photos__RootPath` | Writable folder outside `wwwroot`; `/data/card-photos` in the container |
| `PublicBaseUrl` | Trusted canonical HTTPS origin for Stripe return URLs |
| `AllowedHosts` | Explicit website hosts; add development hosts only in development configuration |
| `ReverseProxy__KnownProxies__0` | Actual trusted reverse proxy IP; never an arbitrary client address |
| `Database__InitializeOnStartup` | Defaults false; leave false for the web application's least-privilege login |
| Live demo data | Removed from backend initialization; historical records remain only in the separate archive |
| `Jwt__*`, `Stripe__*` | Existing authentication and payment server configuration |
| `Seed__*` | Used only by explicitly requested initialization, not every normal deployment |

Existing Compose ports remain **host 8090 -> container 8080** for website and API on the
same origin. SQL Server uses its configured TCP port, normally **1433**, accessible only
from authorized servers. No new public SQL service or browser database connection is added.
Standalone client preview uses 5248 but has no API proxy; use the API-served client for
real onboarding. Use an isolated database for development, not the production inventory.

The container runs as the non-root .NET user and uses the named `card_photos` volume.
Retain the existing Compose project name `marks-baseballcards` when deploying; changing it
can select a different empty volume. Back up both the database and that volume together.
Do not run `docker compose down -v` against the production stack.

## SQL scripts and staged release

**This change has not been applied to production. Existing records become unpublished
by default after the migration. Their data is retained, but they disappear from public
collection/marketplace queries until the owner adds the required descriptions/photos
and publishes them. Coordinate this with the owner before merging or deploying.**

1. Back up the current database and any photo storage, inventory active listings and
   outstanding payment sessions, and prepare a staging clone with private credentials.
2. Review `deploy/sql/card-photo-onboarding.sql`. It includes guarded initial and photo
   migrations using `__EFMigrationsHistory`; existing initial tables are skipped when
   that migration is recorded. Verify existing schema/history compatibility first.
3. Apply reviewed SQL using a DBA/deployment account explicitly targeting the intended
   database. The web account should not have schema privileges. The script generator
   does not create a database, connect to a server or execute SQL.
4. Create/provision the `marks_app` login and database user through the operator's secure
   process. Review `deploy/sql/application-permissions.sql` and grant only needed table
   access. Do not use `sa`, `db_owner`, server roles or DDL rights for the website.
5. Supply patched .NET/ASP.NET Core **10.0.14 or later**. Compatible package references
   were updated and the final container image is pinned to `aspnet:10.0.14`; production
   startup refuses an older CLR. The local PC still has runtime 10.0.11 and was not
   globally upgraded. Updating NuGet references alone does not patch that runtime.
6. Start the staged API/container with automatic initialization disabled. Verify a
   writable persistent photo volume, SQL TLS trust, trusted proxy headers, login,
   drafts, publication, optional sale, hard refresh and storage survival on recreation.
7. Reconcile owner-approved existing inventory, then coordinate the production SQL and
   application rollout. Test Stripe separately in test mode before real purchases.

To regenerate schema SQL offline after a model change:

```powershell
./scripts/Generate-DatabaseScripts.ps1
```

For a deliberately new isolated database, the API also supports the explicit
`--initialize-database` switch, which migrates/seeds and exits. It requires deployment
privileges and explicit strong initial staff passwords when creating initial accounts.
Managed staff password/role reconciliation occurs only during that initialization.
It is not part of routine web startup; production operators should use reviewed SQL.

Roll back application code only with a reviewed compatibility plan. Do not execute the
generated migration `Down` or drop the photo table to undo a web deployment: those actions
remove persisted descriptions/publication state and metadata. Keep both backups.

File storage and SQL are not a distributed transaction. A failed upload can leave inert,
unreferenced files after an ambiguous database commit; inaccessible orphan files are safer
than deleting a potentially committed photo. Failed file deletion is logged by photo ID.
Monitor free space and reconcile orphans against metadata offline, with backups and operator
review; do not blindly delete storage contents or retry an uncertain upload indefinitely.

## Verification and limits

```powershell
dotnet build MarksBaseballCards.slnx -c Release --warnaserror
dotnet test tests/MarksBaseballCards.Tests/MarksBaseballCards.Tests.csproj -c Release
dotnet list src/Api/MarksBaseballCards.Api.csproj package --vulnerable --include-transitive
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/Api
```

Local API tests cover publication vs sale, draft image privacy, invalid images, image
orientation/resize/metadata, photo quotas, ownership checks and cleanup. They use a unique
temporary SQLite database. The SQL rowversion test is skipped when no isolated SQL Server
is available. Set `MARKS_TEST_SQLSERVER` only for a dedicated localhost database with a
`MarksCardsTests_` prefix; the harness creates unique test databases and deletes only those.
It rejects external production database addresses. In Actions, a private `sqlserver` service
is accepted only within the CI process; tests apply the actual SQL Server migrations there.

Local evidence on November 4: Release solution build succeeded with zero warnings/errors;
Release publish succeeded for the initial onboarding implementation. After the compact
editor and 30 MiB upload update, the Release build passed without warnings/errors;
17 tests passed and the one SQL Server-only test was skipped. The boundary test sends
an actual 30 MiB photo payload and verifies rejection of one byte over that limit.
The suite also rejects a JWT whose browser role claim was edited without a valid signature,
and verifies that unpublishing removes public listings and denies new anonymous image reads.
EF reported no model changes missing a migration. API and client dependency audits reported
no known vulnerable packages from the configured NuGet feed.

Chromium UI checks exercised draft creation, upload, publication, collection details,
optional marketplace sale, escaped descriptions, mobile overflow and recoverable API errors.
Those browser checks use mocked responses; they do not certify live SQL connectivity.
The production database, deployed Linux image, real Stripe payments and the modified CI
workflow have not been executed for this local change. Cross-browser/load testing and
actual SQL Server staging verification remain release gates.

The pre-PR local suite now passes 29 tests, with two actual-SQL-Server tests skipped locally
(rowversion and the admin bootstrap SQL). These run against the disposable CI database.
Production also refuses `sa`, disabled SQL encryption, unvalidated SQL certificates and
persisted connection-security information. HTTPS redirection is enabled only in Production,
after trusted forwarded-header processing; Development HTTP remains supported. Configure
the real trusted proxy address before deploying to avoid redirect loops.

For a single email-based inventory administrator, follow
[Secure administrator bootstrap and encryption](../security/photo-onboarding-review.md).
It does not require seeding demo data or configuring a second account.

The November dependency audit reported no known vulnerable API packages from the current
NuGet advisory feed after updating compatible .NET packages and OpenAPI. This is not a
guarantee against unknown vulnerabilities. SkiaSharp is MIT-licensed; keep its third-party
notices with distributed packages.

References: [Microsoft upload guidance](https://learn.microsoft.com/aspnet/core/mvc/models/file-uploads),
[EF Core efficient querying](https://learn.microsoft.com/ef/core/performance/efficient-querying),
[SkiaSharp source/license](https://github.com/mono/SkiaSharp).
