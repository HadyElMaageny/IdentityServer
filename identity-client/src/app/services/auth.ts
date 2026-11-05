import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

/** Token endpoint response (RFC 6749 §5.1) plus the absolute expiry we compute locally. */
export interface TokenSet {
  access_token: string;
  token_type: string;
  expires_in: number;
  scope?: string;
  id_token?: string;
  refresh_token?: string;
  /** Epoch milliseconds when the access token expires */
  expires_at: number;
  /** Epoch milliseconds when this token set was obtained */
  obtained_at: number;
}

export type JwtClaims = Record<string, unknown>;

/** Values that must survive the round-trip through the identity server. */
interface PendingLogin {
  state: string;
  nonce: string;
  codeVerifier: string;
}

export class AuthError extends Error {
  constructor(public readonly code: string, description: string) {
    super(description);
  }
}

const TOKENS_KEY = 'oidc.tokens';
const PENDING_KEY = 'oidc.pending';

/**
 * OAuth 2.0 Authorization Code flow with PKCE (RFC 7636) for a public SPA client.
 *
 * 1. startLogin()     → redirect to /connect/authorize with code_challenge, state and nonce
 * 2. handleCallback() → verify state, exchange code + code_verifier at /connect/token, verify id_token nonce
 * 3. refresh()        → refresh_token grant (the server rotates the refresh token)
 * 4. logout()         → clear tokens and end the server session via /connect/endsession
 */
@Injectable({ providedIn: 'root' })
export class Auth {
  private readonly http = inject(HttpClient);
  private readonly tokenSet = signal<TokenSet | null>(this.load<TokenSet>(TOKENS_KEY));

  readonly tokens = this.tokenSet.asReadonly();
  readonly isAuthenticated = computed(() => this.tokenSet() !== null);
  readonly idTokenClaims = computed(() => {
    const idToken = this.tokenSet()?.id_token;
    return idToken ? decodeJwt(idToken) : null;
  });

  async startLogin(): Promise<void> {
    const pending: PendingLogin = {
      state: randomString(32),
      nonce: randomString(32),
      codeVerifier: randomString(64),
    };
    sessionStorage.setItem(PENDING_KEY, JSON.stringify(pending));

    const params = new URLSearchParams({
      client_id: environment.clientId,
      redirect_uri: environment.redirectUri,
      response_type: 'code',
      scope: environment.scope,
      state: pending.state,
      nonce: pending.nonce,
      code_challenge: await sha256Base64Url(pending.codeVerifier),
      code_challenge_method: 'S256',
    });

    window.location.assign(`${environment.identityServerUrl}/connect/authorize?${params}`);
  }

  async handleCallback(query: URLSearchParams): Promise<void> {
    const pending = this.load<PendingLogin>(PENDING_KEY);
    sessionStorage.removeItem(PENDING_KEY);

    const error = query.get('error');
    if (error) {
      throw new AuthError(error, query.get('error_description') ?? 'The authorization request failed.');
    }

    const code = query.get('code');
    if (!code) throw new AuthError('invalid_response', 'No authorization code was returned.');
    if (!pending) throw new AuthError('invalid_state', 'No sign-in was started from this browser tab.');
    if (query.get('state') !== pending.state) {
      throw new AuthError('invalid_state', 'The state parameter does not match (possible CSRF).');
    }

    const tokens = await this.tokenRequest({
      grant_type: 'authorization_code',
      code,
      redirect_uri: environment.redirectUri,
      client_id: environment.clientId,
      code_verifier: pending.codeVerifier,
    });

    if (tokens.id_token) {
      const claims = decodeJwt(tokens.id_token);
      if (claims['nonce'] !== pending.nonce) {
        throw new AuthError('invalid_nonce', 'The id_token nonce does not match (possible replay).');
      }
      if (claims['aud'] !== environment.clientId) {
        throw new AuthError('invalid_audience', 'The id_token was not issued for this client.');
      }
    }

    this.store(tokens);
  }

  async refresh(): Promise<void> {
    const refreshToken = this.tokenSet()?.refresh_token;
    if (!refreshToken) throw new AuthError('no_refresh_token', 'No refresh token is available.');

    const tokens = await this.tokenRequest({
      grant_type: 'refresh_token',
      refresh_token: refreshToken,
      client_id: environment.clientId,
    });

    // Keep the original id_token if the refresh response did not include a new one
    this.store({ ...tokens, id_token: tokens.id_token ?? this.tokenSet()?.id_token });
  }

  userInfo(): Promise<JwtClaims> {
    const headers = new HttpHeaders({ Authorization: `Bearer ${this.tokenSet()?.access_token}` });
    return firstValueFrom(
      this.http.get<JwtClaims>(`${environment.identityServerUrl}/connect/userinfo`, { headers }),
    );
  }

  logout(): void {
    this.clear();
    const params = new URLSearchParams({
      client_id: environment.clientId,
      post_logout_redirect_uri: environment.postLogoutRedirectUri,
    });
    window.location.assign(`${environment.identityServerUrl}/connect/endsession?${params}`);
  }

  clear(): void {
    sessionStorage.removeItem(TOKENS_KEY);
    this.tokenSet.set(null);
  }

  private async tokenRequest(body: Record<string, string>): Promise<TokenSet> {
    const headers = new HttpHeaders({ 'Content-Type': 'application/x-www-form-urlencoded' });
    try {
      const response = await firstValueFrom(
        this.http.post<Omit<TokenSet, 'expires_at' | 'obtained_at'>>(
          `${environment.identityServerUrl}/connect/token`,
          new URLSearchParams(body).toString(),
          { headers },
        ),
      );
      const now = Date.now();
      return { ...response, obtained_at: now, expires_at: now + response.expires_in * 1000 };
    } catch (e) {
      if (e instanceof HttpErrorResponse && e.error?.error) {
        throw new AuthError(e.error.error, e.error.error_description ?? 'Token request failed.');
      }
      throw new AuthError('network_error', 'Could not reach the identity server.');
    }
  }

  private store(tokens: TokenSet): void {
    // sessionStorage: tokens are scoped to this tab and cleared when it closes
    sessionStorage.setItem(TOKENS_KEY, JSON.stringify(tokens));
    this.tokenSet.set(tokens);
  }

  private load<T>(key: string): T | null {
    try {
      const raw = sessionStorage.getItem(key);
      return raw ? (JSON.parse(raw) as T) : null;
    } catch {
      return null;
    }
  }
}

/** Decodes a JWT payload. The client only reads claims; signatures are verified by the server. */
export function decodeJwt(token: string): JwtClaims {
  const payload = token.split('.')[1] ?? '';
  const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
  const json = decodeURIComponent(
    atob(base64)
      .split('')
      .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
      .join(''),
  );
  return JSON.parse(json);
}

/** RFC 7636 §4.1: unreserved characters only. 64 symbols, so `byte % 64` has no modulo bias. */
function randomString(length: number): string {
  const charset = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_';
  const values = crypto.getRandomValues(new Uint8Array(length));
  return Array.from(values, (v) => charset[v % charset.length]).join('');
}

/** RFC 7636 §4.2: code_challenge = BASE64URL(SHA256(code_verifier)) */
async function sha256Base64Url(input: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(input));
  return btoa(String.fromCharCode(...new Uint8Array(digest)))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
}
