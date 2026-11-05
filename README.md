# Mark's Baseball Cards

An application for publishing Mark's photographed collection, exploring the separate
1991 Topps historical checklist, viewing live sale listings,
buying through Stripe Checkout, and managing inventory through staff workspaces.
The HTS frontend uses Blazor WebAssembly with an editorial visual identity, light and
dark themes, and English, Brazilian Portuguese and Spanish interfaces.

**Current development:** [Photo onboarding, SQL Server and release procedures](docs/phase-3-development/photo-onboarding.md).
The admin workspace now supports drafts, real photographs, descriptions and publication,
with an independent option to sell. Database changes require explicit operator deployment.

**Historical documentation baseline:** the frontend contribution delivered in commit
[`39a8ecc`](https://github.com/HowTo-Software/Marks-BaseBallCards/commit/39a8ecc90bcca4a0eb0d109360f7bae42d4f1e76).
Backend code is described where it affects the client. Hank's backend restructuring,
production infrastructure and operational procedures require his documentation and
validation. See the [assessment and handoff](docs/phase-1-inception/repository-assessment.md).

## Preview the frontend

Install the .NET 10 SDK, open a terminal at the repository root, then run:

```powershell
dotnet restore src/Client/MarksBaseballCards.Client.csproj
dotnet run --project src/Client --no-launch-profile --urls http://localhost:5248
```

Open [localhost:5248](http://localhost:5248). The historical archive at `/archive` and
collection story work with local assets. Home's featured cards, the real collection,
marketplace, staff login, inventory, dashboard and payments require the configured backend.

The client sends API requests to its own origin. The standalone preview does not
proxy requests to a separately started API. For the integrated experience, use the
frontend served by the API, as explained in [onboarding](docs/phase-3-development/onboarding.md)
and the [backend reference](docs/phase-5-deployment/backend-reference.md).

## What is in the application?

| Experience | Routes | Data and access |
| --- | --- | --- |
| Introduction and collection story | `/`, `/about` | Local content; Home also loads published cards from the API |
| Real collection | `/collection` | Published SQL-backed records and photographs; purchase is optional |
| Original archive | `/archive` | Separate 64-record historical checklist with original illustrations |
| Live marketplace | `/marketplace` | Published sale listings, photographs, USD prices and Stripe Checkout |
| Staff access | `/login` | Owner-provided accounts; no public registration flow |
| Inventory workspace | `/admin` | `Admin` role; drafts, descriptions, photo upload/cover/removal, publish/unpublish, optional sale and deletion |
| Statistics and activity | `/system` | `SystemAdmin` role; current inventory, six filing sets and up to 200 history entries |
| Payment status | `/buy/success` | Confirms payment only when the API reports `paid` |

`Admin` and `SystemAdmin` are distinct roles. The browser's route checks help guide
navigation; the API enforces authorization.

### How to interpret the collection

The archive is a snapshot of the supplied checklist, separate from current inventory.
It is not proof that a card is in stock or for sale. Covers are original record
illustrations, rather than physical card scans or grading certificates. Rookie and
Royals markers retain the collector's annotations. Filing percentages describe the
records currently in the database. Language selection changes formatting and copy;
prices remain in USD.

The real collection is separate: its photos and descriptions are entered by the owner.
Drafts remain private. A published card can be displayed without being offered for sale.

## Repository map

| Location | Responsibility |
| --- | --- |
| [src/Client](src/Client) | Blazor pages, components, browser authentication, localization, CSS and JavaScript |
| [src/Shared](src/Shared) | DTOs and role names shared with the client |
| [src/Api](src/Api) | ASP.NET Core API, SQL Server/EF Core, JWT, Stripe and hosting of the client |
| [docker-compose.yml](docker-compose.yml) | Existing web container configuration; no database service |
| [deploy/sql](deploy/sql) | Reviewed idempotent schema script and least-privilege SQL grants |
| [scripts](scripts) | Offline SQL script generation |
| [tests](tests) | Isolated API, authorization, image processing and optional SQL Server tests |
| [docs](docs/README.md) | Documentation organized by lifecycle phase |

There is no npm build pipeline in this repository. The frontend uses Razor, C#, CSS
and native browser JavaScript. Dependencies and hosting boundaries are detailed in
[architecture](docs/phase-2-design/architecture.md).

## Documentation

Start with the [documentation index](docs/README.md), or go directly to:

- [Environment and development setup](docs/phase-3-development/onboarding.md)
- [Photo onboarding and SQL Server rollout](docs/phase-3-development/photo-onboarding.md)
- [Create the admin securely and configure encryption](docs/security/photo-onboarding-review.md)
- [Frontend design and source map](docs/phase-2-design/technical-design.md)
- [Client/API contracts](docs/phase-2-design/api-specification.md)
- [Themes and translation maintenance](docs/phase-3-development/themes-and-languages.md)
- [Review evidence and manual checks](docs/phase-4-testing/test-plan.md)
- [Publishing the frontend with the existing host](docs/phase-5-deployment/deployment-guide.md)
- [Security findings and recommendations](docs/security/security-review.md)
- [User guide](docs/user/user-guide.md)
- [Preview screenshots](docs/screenshots/README.md)

## Local API development example

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
# Supply ConnectionStrings__Default and Jwt__Key securely for an isolated local database.
# See .env.example and the photo onboarding guide; dotnet run does not load .env automatically.
dotnet run --project src/Api --no-launch-profile --urls http://localhost:8090
```

## Current validation and release limits

The photo onboarding work includes an automated suite and a SQL Server CI service.
Local tests exercise real API authorization and image processing using isolated SQLite;
the SQL Server rowversion test requires a separate isolated instance and is skipped locally
when none is configured. The workflow runs tests against a disposable SQL Server service.
Browser workflow checks used mocked API responses, not production data.

No production database, real payments or deployed container was exercised for this change.
Existing inventory starts unpublished after the migration and requires owner review before
public release. Normal web startup no longer migrates or seeds. See the
[rollout and validation details](docs/phase-3-development/photo-onboarding.md).
Pushes to `main` and manual workflow dispatch can still deploy automatically; this work
must be staged with the operator before merging. Older documentation records its own baseline.

## Contributions, security and licensing

See [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md) and
[CHANGELOG.md](CHANGELOG.md). The repository had no owner-selected project license
at the reviewed revision; [LICENSE](LICENSE) records that pending decision and does
not grant reuse rights. Third-party font notices and component attribution are
preserved in the [licensing notes](docs/security/compliance.md).
