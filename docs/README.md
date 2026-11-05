# Frontend documentation

**November 4 implementation:** see [Real collection and photo onboarding](phase-3-development/photo-onboarding.md)
for the current admin workflow, SQL Server schema/scripts, private photo storage, security,
performance limits and staged deployment. The lifecycle pages below retain their October baseline.
See also [Secure administrator bootstrap and encryption review](security/photo-onboarding-review.md).

**Status:** source-backed frontend handoff. **Maintainers:** HTS frontend contributors.
**Updated:** October 3, 2026. **Source baseline:** `39a8ecc`.

This documentation covers the frontend work delivered for Mark's Baseball Cards.
It uses the lifecycle folders from the local `best-document` template, with content
adapted to the inspected application. Backend details appear only where a developer
needs them to understand or connect the client. Hank's backend restructuring and
production procedures remain his handoff items.

## Start with your task

| Task | Read |
| --- | --- |
| Understand the delivery and open questions | [Assessment and Hank handoff](phase-1-inception/repository-assessment.md) |
| Start a local frontend preview | [Onboarding](phase-3-development/onboarding.md) |
| Find a page, component or service | [Technical design](phase-2-design/technical-design.md) |
| Connect the client to the existing backend | [Architecture](phase-2-design/architecture.md), [API contracts](phase-2-design/api-specification.md) |
| Change themes or translated copy | [Themes and languages](phase-3-development/themes-and-languages.md) |
| Review a frontend change | [Test plan and evidence](phase-4-testing/test-plan.md), [manual cases](phase-4-testing/test-cases.md) |
| Publish the frontend with the API | [Deployment guide](phase-5-deployment/deployment-guide.md) |
| Diagnose a running client | [Runbook](phase-6-operations/runbook.md) |
| Understand remaining security findings | [Security review](security/security-review.md), [threat model](security/threat-model.md) |
| Learn the interface | [User guide](user/user-guide.md) |

## Lifecycle map

| Folder | Content |
| --- | --- |
| `phase-1-inception/` | [Vision and scope](phase-1-inception/vision-and-scope.md), [implemented user stories](phase-1-inception/user-stories.md), [repository assessment](phase-1-inception/repository-assessment.md) |
| `phase-2-design/` | [Architecture](phase-2-design/architecture.md), [technical design](phase-2-design/technical-design.md), [client data model](phase-2-design/data-model.md), [API contracts](phase-2-design/api-specification.md), [diagrams](phase-2-design/diagrams.md), [ADRs](phase-2-design/adr/README.md), [visual references](phase-2-design/ui-references.md) |
| `phase-3-development/` | [Onboarding](phase-3-development/onboarding.md), [coding conventions](phase-3-development/coding-standards.md), [Git workflow](phase-3-development/git-workflow.md), [themes and languages](phase-3-development/themes-and-languages.md) |
| `phase-4-testing/` | [Test plan and existing evidence](phase-4-testing/test-plan.md), [manual review cases](phase-4-testing/test-cases.md) |
| `phase-5-deployment/` | [Deployment guide](phase-5-deployment/deployment-guide.md), [backend configuration reference](phase-5-deployment/backend-reference.md), [CI/CD status](phase-5-deployment/ci-cd.md), [frontend release notes](phase-5-deployment/release-notes.md) |
| `phase-6-operations/` | [Client runbook](phase-6-operations/runbook.md), [monitoring and ownership limits](phase-6-operations/monitoring.md) |
| `security/` | [Preserved source review](security/security-review.md), [frontend threat model](security/threat-model.md), [licensing and compliance status](security/compliance.md) |
| `user/` | [User guide](user/user-guide.md), [FAQ](user/faq.md), [troubleshooting](user/troubleshooting.md) |
| `screenshots/` | [Existing October 2 preview gallery](screenshots/README.md) |

## How to read the status of a statement

- **Implemented / source-confirmed:** observable in a linked source or configuration file.
- **Recorded evidence:** an earlier guide, capture or commit records the check. This
  documentation work did not repeat the application build or browser review.
- **Recommended / manual procedure:** guidance to follow; not a claim that a check passed.
- **Pending Hank / maintainer:** backend, infrastructure or policy detail not established
  in the reviewed files. It must be completed by the person who owns that work.
- **N/A:** the template topic does not apply to this frontend contribution.

Dates identify the documentation snapshot. Package versions are those declared in
the project files, not claims about the latest available release. ADRs describe
implemented choices retrospectively; they do not invent meeting notes or approvals.

## Template sections intentionally omitted

| Generic section | Treatment and reason |
| --- | --- |
| Separate PRD, SRS and roadmap | **N/A for this contribution.** Delivered scope and stories describe the frontend; no independently approved backlog, milestones or roadmap were provided. |
| Full database ERD and migration strategy | **Pending Hank.** The client data model documents DTOs; it does not assert the restructured production schema. |
| Disaster-recovery plan | **N/A as a standalone frontend plan.** This client has no authoritative database. Asset recovery is in the runbook; database backups, RPO/RTO and payment reconciliation require Hank's infrastructure evidence. |
| Generic bug and release templates | Omitted. Concrete review cases, contribution instructions and release notes are more useful for this delivery. |
| Code of conduct and supported-version policy | **Pending maintainer policy.** No approved policy, contact or version matrix was provided. |
| Template `.github` files | Nothing copied. The repository's build/deploy workflow is documented from its checked-in configuration; Copilot sessions, transcripts, editor histories and debug logs are excluded. |

## Preserved documents

The full security review, motion/licensing references and theme/language guide have
been moved into their relevant phase folders. Their previous paths remain as short
links for existing references. Screenshot assets and font license notices remain in
place. Original README quotations in the security review link to its immutable
frontend revision, keeping the cited line numbers meaningful.
