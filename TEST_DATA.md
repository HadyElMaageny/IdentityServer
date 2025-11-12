# Test Data Reference

This file contains all the test data that has been seeded into the database for testing purposes.

## Test Users

All users have the same password: **`Password123!`**

| Username | Email | Password | Status |
|----------|-------|----------|--------|
| testuser | test@example.com | Password123! | Active |
| admin | admin@example.com | Password123! | Active |
| alice | alice@example.com | Password123! | Active |
| bob | bob@example.com | Password123! | Active |

## OAuth2/OIDC Clients

| Client ID | Client Secret | Client Name | Redirect URIs | Allowed Grant Types |
|-----------|---------------|-------------|---------------|---------------------|
| webapp | webapp_secret | Test Web Application | https://localhost:5001/callback<br>https://localhost:5001/signin-oidc<br>http://localhost:3000/callback | authorization_code<br>refresh_token |
| spa | spa_secret | Single Page Application | http://localhost:3000/callback<br>http://localhost:4200/callback | authorization_code |
| mobile-app | mobile_secret | Mobile Application | myapp://callback | authorization_code<br>refresh_token |
| postman | postman_secret | Postman Testing Client | https://oauth.pstmn.io/v1/callback<br>https://www.getpostman.com/oauth2/callback | authorization_code<br>refresh_token |

## Scopes

| Scope Name | Display Name | Description | Required |
|------------|--------------|-------------|----------|
| openid | OpenID | OpenID Connect scope for authentication | Yes |
| profile | User Profile | Access to user profile information | No |
| email | Email Address | Access to user email address | No |
| address | Physical Address | Access to user physical address | No |
| phone | Phone Number | Access to user phone number | No |
| offline_access | Offline Access | Access to refresh tokens for offline access | No |

## Client-Scope Mappings

**webapp** (Test Web Application):
- openid, profile, email, address, phone, offline_access

**spa** (Single Page Application):
- openid, profile, email

**mobile-app** (Mobile Application):
- openid, profile, email, offline_access

**postman** (Postman Testing Client):
- openid, profile, email, address, phone, offline_access

## Quick Test Examples

### 1. Login and Get Access Token

```bash
curl -X POST https://localhost:7208/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Password123!"
  }'
```

### 2. OAuth2 Authorization Request

```bash
# First, login to get an access token
# Then use it in the Authorization header:

curl -X GET "https://localhost:7208/connect/authorize?client_id=webapp&redirect_uri=https://localhost:5001/callback&response_type=code&scope=openid%20profile%20email&state=xyz123" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

### 3. Exchange Authorization Code for Tokens

```bash
curl -X POST https://localhost:7208/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=authorization_code&code=YOUR_AUTH_CODE&client_id=webapp&client_secret=webapp_secret&redirect_uri=https://localhost:5001/callback"
```

### 4. Refresh Access Token

```bash
curl -X POST https://localhost:7208/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=refresh_token&refresh_token=YOUR_REFRESH_TOKEN&client_id=webapp&client_secret=webapp_secret"
```

## Testing with Postman

1. **Client Configuration:**
   - Client ID: `postman`
   - Client Secret: `postman_secret`
   - Callback URL: `https://oauth.pstmn.io/v1/callback` or `https://www.getpostman.com/oauth2/callback`

2. **Authorization URL:** `https://localhost:7208/connect/authorize`

3. **Token URL:** `https://localhost:7208/connect/token`

4. **Scopes:** `openid profile email address phone offline_access`

## Notes

- The seeder runs automatically on application startup in **Development** environment only
- Data is only seeded if tables are empty (it won't duplicate data on subsequent runs)
- Client secrets are hashed using the same password hasher as user passwords
- All test data uses UTC timestamps
- Soft delete is enabled for all entities (IsDeleted flag)

## Clearing Test Data

To reset the database and re-seed:

```bash
# Drop the database
dotnet ef database drop --project src/IdentityServer.Infrastructure --startup-project src/IdentityServer.API --force

# Update to latest migration (recreates database)
dotnet ef database update --project src/IdentityServer.Infrastructure --startup-project src/IdentityServer.API

# Run the application (seeder runs automatically)
dotnet run --project src/IdentityServer.API
```
