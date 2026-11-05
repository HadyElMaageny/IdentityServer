# Identity Server: OAuth 2.0 / OpenID Connect on .NET 8

An OAuth 2.0 / OpenID Connect authorization server written from scratch in ASP.NET Core 8, structured with Clean Architecture. It has a companion Angular 20 SPA ([`identity-client/`](identity-client)) that signs users in with the **Authorization Code flow + PKCE**.

No third-party identity framework is used: authorize, login, consent, token, userinfo and discovery are all implemented in this repository.

## Features

| Area | What is implemented |
|------|---------------------|
| **Authorization endpoint** | `GET /connect/authorize`: exact-match `redirect_uri` validation, `state`, `nonce`, scope validation per client. An invalid client or redirect URI shows an error page and never redirects, so the server is not an open redirector. |
| **PKCE (RFC 7636)** | `S256` and `plain`. Required for public clients (no secret); `code_verifier` checked in constant time. |
| **Login** | Server-rendered sign-in page with its own session cookie (HttpOnly, SameSite=Lax) and antiforgery tokens. Passwords hashed with PBKDF2. |
| **Consent** | Consent screen that lists each requested scope. Grants are remembered per user + client and merged when new scopes are approved. Deny returns `access_denied`. |
| **Token endpoint** | `POST /connect/token`, form-encoded with RFC 6749 parameter names. Grants: `authorization_code`, `refresh_token`, `client_credentials`. Client auth via `client_secret_basic` or `client_secret_post` (secrets are stored hashed). |
| **Tokens** | JWT access tokens with scope claims; OIDC ID tokens (`aud` = client, `nonce`, profile/email claims by scope); one-time authorization codes (10 min); **refresh token rotation** (the old token is revoked on every use). |
| **OIDC** | `GET /connect/userinfo` (bearer protected, claims filtered by scope), `GET /.well-known/openid-configuration`, RP-initiated logout via `/connect/endsession` with validated `post_logout_redirect_uri`. |
| **Errors** | RFC 6749 error codes (`invalid_grant`, `invalid_client` with 401 + `WWW-Authenticate`, …) and `Cache-Control: no-store` on token responses. |

## Architecture

```
src/
├── IdentityServer.API             Controllers, Razor login/consent views, middleware, DI setup
├── IdentityServer.Application     OAuth/OIDC services (authorize, token, consent), DTOs, validators
├── IdentityServer.Domain          Entities (User, Client, Scope, AuthorizationCode, UserConsent, Token), repository interfaces
├── IdentityServer.Infrastructure  EF Core DbContext, configurations, migrations, repositories, seeder
└── IdentityServer.Shared          Result pattern, settings, pagination
identity-client/                   Angular 20 SPA (public client "spa"): PKCE sign-in, callback, dashboard
```

Dependencies point inward: **API → Application → Domain ← Infrastructure**. Other building blocks: Repository + Unit of Work, the Result pattern, FluentValidation, AutoMapper, Serilog, health checks and Swagger.

## Flow

```
 Angular SPA (:4200)                  Identity Server (:7208)                    SQL Server
      │                                        │                                     │
 1    │── GET /connect/authorize ─────────────>│ client + redirect_uri valid?        │
      │   client_id, redirect_uri, scope,      │                                     │
      │   state, nonce, code_challenge         │                                     │
 2    │<── 302 /account/login?ReturnUrl=… ─────│ no session cookie yet               │
 3    │── POST /account/login ────────────────>│── check password hash ─────────────>│
      │<── 302 back to /authorize ─────────────│   + Set-Cookie (session)            │
 4    │── GET /connect/authorize (2nd) ───────>│ PKCE + scopes OK, no consent yet    │
      │<── 302 /connect/consent ───────────────│                                     │
 5    │── POST /connect/consent (Allow) ──────>│── INSERT UserConsents ─────────────>│
      │<── 302 back to /authorize ─────────────│                                     │
 6    │── GET /connect/authorize (3rd) ───────>│── INSERT AuthorizationCodes ───────>│
      │<── 302 /callback?code=…&state=… ───────│                                     │
 7    │ check state, then                      │                                     │
      │── POST /connect/token ────────────────>│── verify PKCE, mark code used ─────>│
      │   code + code_verifier                 │── INSERT refresh token ────────────>│
 8    │<── access_token · id_token · refresh ──│                                     │
      │ check nonce + aud, store tokens        │                                     │
 9    │── GET /connect/userinfo (Bearer) ─────>│                                     │
```

### Step by step

1. **The user clicks "Sign in" in the Angular app.** The app generates a random `state`, `nonce` and `code_verifier`, saves them in `sessionStorage`, computes `code_challenge = BASE64URL(SHA256(code_verifier))`, and redirects the browser to **`/connect/authorize`** with `client_id`, `redirect_uri`, `scope`, `state`, `nonce` and `code_challenge`. The entry point is always `/connect/authorize`, never the login page.
2. **The server checks the client.** It checks that the client exists and is enabled, that the `redirect_uri` is registered for it (exact match), and that it may use the `authorization_code` grant. There is no session cookie yet, so `Challenge("cookie")` redirects to **`/account/login`**, carrying the whole original request in `ReturnUrl`. If the client or `redirect_uri` is invalid, the server shows an error page and never redirects.
3. **The user signs in.** The server looks the user up by username or email and verifies the password against the PBKDF2 hash stored in `Users.Password`. It then issues an encrypted, `HttpOnly` **session cookie**. The cookie is stored in the browser, not in the database. The user is redirected back to the `ReturnUrl`, which is `/connect/authorize` again.
4. **`/connect/authorize`, second time.** The user is now known (the `sub` claim comes from the cookie). The server checks PKCE (a `code_challenge` is required for public clients) and the scopes (they must be allowed for this client in `ClientScopes`). The user has not consented yet (no row in `UserConsents`), so the server redirects to **`/connect/consent`**, again carrying the original request in `returnUrl`.
5. **The user clicks Allow.** The server stores the grant in **`UserConsents`**, which is the first database write of the flow, and redirects back to `/connect/authorize`. If the user clicks Deny, the app receives `error=access_denied` instead.
6. **`/connect/authorize`, third time.** Everything passes, including consent. The server creates an **authorization code** (random, valid for 10 minutes, single use) and stores it in `AuthorizationCodes` together with the `code_challenge` and `nonce`. The browser is redirected to the app's **`/callback?code=…&state=…`**.
7. **The app exchanges the code.** It checks that the returned `state` matches the one in `sessionStorage`, then sends a direct request to **`POST /connect/token`** with the `code` and the **`code_verifier`**. The server checks the code (it exists, is unused and unexpired, and belongs to the same client and `redirect_uri`), verifies `SHA256(code_verifier) == code_challenge`, marks the code as used, and stores a refresh token in `Tokens`.
8. **The app receives three tokens:** an `access_token`, an `id_token` and a `refresh_token`. It checks that the `id_token`'s `nonce` and `aud` match, stores the tokens in `sessionStorage`, and navigates to the dashboard.
9. **The app calls APIs with the access token** (`Authorization: Bearer …`). In this project, `/connect/userinfo` is the protected resource. Other services (orders, catalog, …) would validate the JWT themselves (signature, `iss`, `aud`, `exp`, scopes) without calling the identity server.

**Refresh:** when the access token expires, the app calls `/connect/token` with `grant_type=refresh_token`. The server looks the token up in `Tokens`, issues new tokens, and revokes the old refresh token (rotation).

### Where things are stored

| Place | What lives there |
|-------|------------------|
| App `sessionStorage` (`:4200`) | `state`, `nonce` and `code_verifier` during sign-in, then the tokens |
| URLs (`ReturnUrl` / `returnUrl`) | The original authorize request, carried through login and consent |
| Browser cookies (`:7208`) | `.IdentityServer.Session` (encrypted sign-in session) and the antiforgery cookie |
| `Users`, `Clients`, `Scopes`, `ClientScopes` | Seeded configuration (read only during the flow) |
| `UserConsents` | Scopes each user approved for each client |
| `AuthorizationCodes` | Issued codes with `CodeChallenge`, `Nonce`, `ExpiresAt`, `IsUsed` |
| `Tokens` | Refresh tokens (`IsRevoked` is set to 1 when rotated) |

### What each check protects against

| Check | Protects against |
|-------|------------------|
| Exact `redirect_uri` match, with no redirect when it is invalid | Codes being sent to an attacker's site (open redirect) |
| `state` | Login CSRF: an attacker's callback link signing you into *their* account |
| PKCE (`code_challenge` / `code_verifier`) | A stolen authorization code being exchanged by someone else |
| Single-use code that expires after 10 minutes | Replay of a leaked code |
| `nonce` in the `id_token` | Replay of an old `id_token` |
| Consent | Apps receiving user data the user never approved |
| Refresh token rotation | Long-lived use of a leaked refresh token |
| Antiforgery token on login and consent forms | Forms submitted from another site |

### Tracing it yourself

Run the API in Debug and set breakpoints in this order: `AuthorizationController.Authorize` → `AccountController.Login` (POST) → `AuthorizationEndpointService.ProcessAuthorizeRequestAsync` → `ConsentController.Submit` → `AuthorizationService.GenerateAuthorizationCodeAsync` → `TokenEndpointService.ProcessAuthorizationCodeGrantAsync` → `OidcController.UserInfo`. EF Core logs every SQL command to the console.

[`oauth-playground.http`](src/IdentityServer.API/oauth-playground.http) has ready-made requests for the token endpoint, including failure cases: a wrong `code_verifier`, a reused code, a tampered token and a wrong client secret.

## Getting started

Prerequisites: .NET 8 SDK, SQL Server LocalDB (or any SQL Server), Node 20+.

```bash
# 1. Database
dotnet ef database update --project src/IdentityServer.Infrastructure --startup-project src/IdentityServer.API

# 2. Identity server: https://localhost:7208 (Swagger at the root, test data seeded in Development)
dotnet run --project src/IdentityServer.API --launch-profile https

# 3. Angular client: http://localhost:4200
cd identity-client && npm install && npx ng serve
```

Open http://localhost:4200 and click **Sign in with Identity Server**. Demo users are `alice`, `bob` and `testuser`, all with the password `Password123!`. See [TEST_DATA.md](TEST_DATA.md) for the clients, secrets and scopes.

> The first time, trust the ASP.NET Core development certificate (`dotnet dev-certs https --trust`) so the browser accepts `https://localhost:7208`.

### Try the token endpoint directly

```bash
# Confidential client with HTTP Basic authentication
curl -X POST https://localhost:7208/connect/token \
  -u webapp:webapp_secret \
  -d "grant_type=refresh_token&refresh_token=<token>"
```

## Configuration

`src/IdentityServer.API/appsettings.json`

- `ConnectionStrings:DefaultConnection`: SQL Server connection string
- `JwtSettings`: `Issuer`, `Audience`, `Secret` (at least 32 characters; keep it in user secrets or a key vault outside development), `AccessTokenMinutes`, `RefreshTokenDays`

## Limitations

These are deliberate scope limits for this project:

- Tokens are signed with **HS256** (shared secret). A multi-tenant production deployment would use RS256/ES256 with a published JWKS endpoint.
- No token introspection or revocation endpoints yet, and no admin UI for managing clients (clients are seeded).

## Author

Hady Ahmed
