# Administrator bootstrap and photo onboarding security review

Source review and local tests: November 4, 2026. This is not an external penetration
test, a guarantee of zero risk, or evidence that production encryption is configured.

## Create the administrator without exposing a password

The repository ships `deploy/sql/bootstrap-admin.sql` as a placeholder-only template.
It does not contain an actual email/password or a usable default account. To personalize it:

```powershell
# From the repository root, in an interactive terminal:
dotnet run --project tools/MarksBaseballCards.AdminBootstrap -c Release
```

Enter the owner's email and a unique 16-128 character passphrase twice. Password entry is
hidden; passwords are not accepted as command-line arguments or redirected standard input.
Use a password manager to retain the original passphrase; the generated hash is not a
recoverable copy. The email must fit the existing 64-character username column.

The tool generates `artifacts/admin-bootstrap.sql`, ignored by Git. It contains the email
and a salted ASP.NET IdentityV3 PBKDF2-HMAC-SHA512 password hash with 210,000 iterations,
a 128-bit random salt and a 256-bit derived key. The original password is never written
to the SQL file, console output or database. Passwords exist transiently in managed memory
during hashing; do not run the tool on an untrusted machine.

The output file is created without overwriting an existing file. On Windows it is restricted
to the current user's SID; on Unix it is created with owner-only read/write permissions.
Protect even a hash-bearing SQL file: hashes can be attacked offline if stolen. Keep the
generated file outside tickets, PR descriptions and CI logs. Review it, transfer it through
an approved protected channel, run it with a deployment/DBA identity in the intended database,
then remove the temporary copy according to the operator's secure handling procedure.

Run in this order:

1. Back up the existing database and review its migration history.
2. Apply `deploy/sql/card-photo-onboarding.sql` with a deployment account.
3. Run the locally generated admin SQL in `Website_Application_MarksBaseballCardsDb`.
4. Provision the separate `marks_app` SQL login and apply reviewed table grants from
   `deploy/sql/application-permissions.sql`; never grant the web app `sa`, `db_owner`
   or migration privileges.
5. Configure the API securely, sign in through HTTPS using the owner's email and original
   password, then enter actual inventory through `/admin`.

Bootstrap creation is transactional and protected by the unique username index. A repeat
run with an existing Admin email leaves its password and role unchanged; an existing
different role is rejected. The script never deletes users or rotates an existing password
silently. It uses `Admin`, which manages cards, separately from `SystemAdmin` statistics.
SQL literals are escaped and SQLCMD substitution characters are rejected in generated emails.
No SQL `HASHBYTES` shortcut or reversibly encrypted password is used.

## Encryption boundaries and production setup

Passwords are **hashed**, rather than encrypted reversibly. Authentication hashes are
automatically upgraded on successful login when older Identity parameters require it.
The same 210,000-iteration policy is used for explicitly initialized staff accounts.

Browser/API traffic requires a valid HTTPS reverse proxy/certificate in production.
The application processes forwarded headers only from known proxies, sends HSTS, and
redirects direct Production HTTP requests to HTTPS port 443. Development and isolated
tests retain direct HTTP support. Restrict container port 8090 to the proxy/operations
network; redirection alone does not make an initial HTTP credential submission confidential.
The minimal `/health/live` process check remains available to deployment monitoring over
the internal HTTP port; it exposes no identity, configuration or database information and
does not certify database readiness or TLS configuration.
Configure the actual proxy IP before rollout, then test that HTTPS does not redirect in a loop.

Production SQL connections require `Encrypt=True` (or Strict),
`TrustServerCertificate=False`, `Persist Security Info=False` and a dedicated application
identity. Certificates must chain to a trust root available inside the container; do not
work around certificate errors by disabling validation. Parameterized EF queries remain
separate from connection secrets. No database is accessed from browser code.

**Encryption at rest is an infrastructure release requirement, not something this PR
silently enables.** The DBA must enable SQL Server AES-256 TDE where the edition supports
it and back up the certificate/private key to a separately protected location. The
read-only `deploy/sql/security-preflight.sql` verifies TDE state and the current SQL
connection's encryption; it requires the appropriate DBA DMV permissions. It intentionally
fails until these prerequisites are met and does not create or destroy keys.

Encrypt the photo volume, `.env`/secret storage and backups using the host/storage platform's
managed encryption, such as LUKS2/BitLocker or the storage provider's managed encryption.
The application stores optimized JPEGs outside `wwwroot` and serves draft photos only
after authorization; it does not itself turn that volume into an encrypted filesystem.
Back up SQL data, photographs and encryption keys through separate protected processes;
losing a TDE certificate can make database backups unrecoverable. Publicly published photos
can naturally be downloaded by visitors; this is not end-to-end encryption of public content.

## Controls reviewed and changed

- Server-side role checks protect mutations and draft images. Forging the browser's role
  does not bypass JWT signature/issuer/audience/expiry checks; allowed algorithm is HS256.
- Login responses use `no-store`; unknown and locked accounts return the same generic
  message and perform password verification work. Account lockout and IP rate limits remain.
- Default or placeholder staff passwords are rejected. Normal web startup neither migrates
  nor initializes users. Initial/managed staff setup requires explicit operator execution.
- Live demonstration seed code/data were removed. Temporary preview records, local photos,
  artifacts and real `.env` files are excluded from the commit and Docker build context.
  The user-approved historical `/archive` remains distinct from SQL inventory.
- Actual file size, dimensions, format, image decoding, publication and photo quota are
  validated by the server. Uploaded originals are re-encoded and metadata removed.
- Browser object URLs for pre-save thumbnails are revoked; source files are streamed
  sequentially rather than copied wholesale into base64/WASM memory.
- GUID storage names avoid original-file path injection. Image delivery uses fixed MIME,
  `nosniff`, image CSP and private/no-store caching as appropriate.
- Paged queries, bounded retries/timeouts, limited upload processing, SQL rowversion and
  atomic inventory/audit saves address resource exhaustion and lost writes.
- Patched .NET packages and a 10.0.14 runtime image replace known vulnerable dependencies.
  The runtime image tag was verified to exist; the Linux image was not built locally.

## Validation and remaining boundaries

Release solution build: zero warnings/errors. Local automated suite: 29 passing tests;
two actual-SQL-Server tests skipped because no isolated server is installed locally.
Tests cover generated hash compatibility and random salts, SQL escaping and invalid input,
real API login, non-cacheable credentials, generic lockout errors, production SQL guards,
forged JWTs, draft privacy, publication/sale separation, image processing, quotas, and
30 MiB acceptance/rejection boundaries. SQL rowversion and idempotent admin SQL are
configured to run against a disposable SQL Server service in pull-request CI.

Browser checks use mock API data and cover compact desktop/mobile forms, pre-save previews,
upload/publication, English/PT-BR and dark theme. They are UI evidence, not production SQL
or payment validation. The temporary demo server is loopback-only and is not shipped.

Existing JWT storage remains localStorage, so an XSS defect could expose a session;
HTTP-only session/MFA migration is outside this PR. A strict application-wide CSP needs
separate Blazor import-map/WASM validation; the image endpoint CSP is not a site-wide CSP.
Network DDoS/WAF protection, disk quotas/free-space monitoring, audited secret rotation,
Stripe reservation/concurrent-checkout behavior, payment-mode validation, and real TLS,
TDE, backup recovery and load testing require operator/staging verification. Do not treat
the dependency audit as proof that no undiscovered vulnerabilities exist.

No production database was modified or contacted for this implementation. Merging to
`main` can trigger the existing automatic deployment: coordinate schema changes, existing
inventory's default-unpublished transition, credentials, encryption and persistent storage
before release. The PR is prepared for review/configuration; it is not a production sign-off.

References: [OWASP password storage](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html),
[ASP.NET Core PasswordHasher](https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Extensions.Core/src/PasswordHasher.cs),
[Microsoft SQL Server TDE and recovery](https://learn.microsoft.com/sql/relational-databases/security/encryption/transparent-data-encryption).
