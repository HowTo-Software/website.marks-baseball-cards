# Administrator bootstrap and photo onboarding security review

Initial source review: November 4, 2026. CodeQL artifact follow-up: November 5, 2026.
This is not an external penetration test, a guarantee of zero risk, or evidence that
production encryption is configured.

## Create the administrator without a readable credentials file

The repository ships `deploy/sql/bootstrap-admin.sql` as a static, parameterized SQL
script. It contains no email, password or usable default account. The first implementation
generated an owner-restricted SQL file containing the email and password hash; CodeQL
reported cleartext-sensitive storage and private-information exposure at that file write.
Those reports are addressed by removing that plaintext export path, without suppressing
or dismissing the queries.

Create an encrypted administrator package offline:

```powershell
# From the repository root, in an interactive terminal:
dotnet run --project tools/MarksBaseballCards.AdminBootstrap -c Release -- create
```

Enter the owner's email, a unique 16-128 character administrator password and a **different**
16-128 character package passphrase. Passwords/passphrases are entered twice, hidden, never
passed as command-line arguments or redirected standard input. The email must fit the
existing 64-character username column.

The password is hashed with IdentityV3 PBKDF2-HMAC-SHA512 (210,000 iterations, random
128-bit salt, 256-bit derived key). The email and hash are then encrypted together into
`artifacts/admin-bootstrap.enc` using AES-256-GCM with a fresh random 128-bit salt,
96-bit nonce and 128-bit authentication tag. A separate encryption key is derived from
the package passphrase using PBKDF2-HMAC-SHA512 with 210,000 iterations. The versioned
envelope authenticates its format; invalid versions, oversized/truncated input, tampering
and incorrect passphrases are rejected.

No plaintext SQL, email or hash is written to a file or console. The package passphrase
and encryption key are not stored beside the package. Keep both passwords in a password
manager, separate from the artifact. If the package passphrase is lost, regenerate a new
package rather than attempting key recovery. Only ciphertext is persisted; generated
packages remain ignored by Git and excluded from the application container.

Files are created without overwriting an existing output, restricted to the current user's
SID on Windows or mode 0600 on Unix. File permissions remain defense in depth. Protect the
encrypted artifact and its passphrase through separate approved transfer channels.

## Apply the package explicitly

1. Back up the intended database and review its migration history.
2. Apply `deploy/sql/card-photo-onboarding.sql` using a deployment/DBA account.
3. Set `MARKS_ADMIN_BOOTSTRAP_CONNECTION` securely in the operator's process environment.
   It must explicitly target `Website_Application_MarksBaseballCardsDb`, use certificate-
   validated SQL TLS and have `Persist Security Info=False`. Prefer an appropriate
   integrated identity where supported; never commit or print a connection password.
4. Place the encrypted package at `artifacts/admin-bootstrap.enc` on the operator's
   trusted machine and run:
   `dotnet run --project tools/MarksBaseballCards.AdminBootstrap -c Release -- apply`.
5. Enter the package passphrase and explicitly confirm `APPLY`. Creation mode never
   contacts a database; only this deliberately selected apply mode opens the connection.
6. Apply mode decrypts the package **only in memory**, loads the reviewed SQL embedded in
   the tool and binds `@AdminEmail` / `@AdminPasswordHash` as typed SQL parameters.
   No user value is interpolated into SQL text or logged. Keys and temporary plaintext
   byte buffers are cleared after use. Managed strings still exist transiently in memory;
   do not use an untrusted machine or collect sensitive process dumps.
7. Provision the separate least-privilege `marks_app` SQL login and review table grants
   from `deploy/sql/application-permissions.sql`. The web app must never use a DBA login.
8. Sign in through HTTPS using the owner's email and original administrator password;
   clear the temporary deployment connection environment and handle package retention
   according to the operator's protected-secret policy.

Bootstrap creation is transactional and serialized by the unique username index. A repeat
run with an existing Admin email leaves its password/role unchanged; a different existing
role is rejected. The script does not delete users or reset passwords silently. A cancelled
apply, wrong passphrase, tampered package or invalid TLS/database configuration fails before
opening a database connection. No plaintext password or SQL `HASHBYTES` shortcut is used.

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

Release solution build: zero warnings/errors. The initial suite passed 29 tests locally;
two actual-SQL-Server tests require the isolated CI service. Package encryption/authentication,
wrong-passphrase/tamper rejection and static SQL parameterization have dedicated regression tests.
Tests cover generated hash compatibility and random salts, typed SQL parameters and invalid input,
real API login, non-cacheable credentials, generic lockout errors, production SQL guards,
forged JWTs, draft privacy, publication/sale separation, image processing, quotas, and
30 MiB acceptance/rejection boundaries. SQL rowversion and idempotent admin SQL are
configured to run against a disposable SQL Server service in pull-request CI.

Browser checks use mock API data and cover compact desktop/mobile forms, pre-save previews,
upload/publication, English/PT-BR and dark theme. They are UI evidence, not production SQL
or payment validation. The temporary demo server is loopback-only and is not shipped. The initial PR CI passed
all 31 original tests; the CodeQL follow-up adds package-specific checks.

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
