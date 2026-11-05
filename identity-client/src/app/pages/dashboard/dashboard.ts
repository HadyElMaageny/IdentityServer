import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe, JsonPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Auth, AuthError, decodeJwt, JwtClaims } from '../../services/auth';

const TIME_CLAIMS = new Set(['exp', 'iat', 'nbf', 'auth_time']);

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, JsonPipe, DatePipe],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit, OnDestroy {
  protected readonly auth = inject(Auth);

  protected readonly now = signal(Date.now());
  protected readonly userInfo = signal<JwtClaims | null>(null);
  protected readonly userInfoError = signal<string | null>(null);
  protected readonly refreshing = signal(false);
  protected readonly refreshMessage = signal<string | null>(null);
  protected readonly rotatedFrom = signal<string | null>(null);

  protected readonly tokens = this.auth.tokens;
  protected readonly idClaims = this.auth.idTokenClaims;

  protected readonly accessClaims = computed(() => {
    const t = this.tokens();
    return t ? decodeJwt(t.access_token) : null;
  });

  protected readonly accessParts = computed(() => (this.tokens()?.access_token ?? '').split('.'));

  protected readonly scopes = computed(() => (this.tokens()?.scope ?? '').split(' ').filter(Boolean));

  protected readonly displayName = computed(
    () => (this.idClaims()?.['preferred_username'] as string) ?? (this.idClaims()?.['sub'] as string) ?? 'user',
  );

  protected readonly secondsLeft = computed(() => {
    const t = this.tokens();
    return t ? Math.max(0, Math.round((t.expires_at - this.now()) / 1000)) : 0;
  });

  protected readonly lifetimePercent = computed(() => {
    const t = this.tokens();
    if (!t) return 0;
    return Math.max(0, Math.min(100, ((t.expires_at - this.now()) / (t.expires_in * 1000)) * 100));
  });

  protected readonly idClaimRows = computed(() =>
    Object.entries(this.idClaims() ?? {}).map(([key, value]) => ({
      key,
      value: TIME_CLAIMS.has(key) && typeof value === 'number' ? new Date(value * 1000) : value,
      isTime: TIME_CLAIMS.has(key) && typeof value === 'number',
    })),
  );

  private timer?: ReturnType<typeof setInterval>;

  async ngOnInit(): Promise<void> {
    this.timer = setInterval(() => this.now.set(Date.now()), 1000);
    await this.loadUserInfo();
  }

  ngOnDestroy(): void {
    clearInterval(this.timer);
  }

  protected formatCountdown(totalSeconds: number): string {
    const m = Math.floor(totalSeconds / 60);
    const s = totalSeconds % 60;
    return `${m}:${s.toString().padStart(2, '0')}`;
  }

  protected short(value: string | undefined, size = 10): string {
    return value ? `${value.slice(0, size)}…${value.slice(-4)}` : '—';
  }

  async refresh(): Promise<void> {
    this.refreshing.set(true);
    this.refreshMessage.set(null);
    const previous = this.tokens()?.refresh_token ?? null;
    try {
      await this.auth.refresh();
      this.rotatedFrom.set(previous);
      this.refreshMessage.set('New access token issued. The previous refresh token was revoked.');
      await this.loadUserInfo();
    } catch (e) {
      this.refreshMessage.set(e instanceof AuthError ? `${e.code}: ${e.message}` : 'Refresh failed.');
    } finally {
      this.refreshing.set(false);
    }
  }

  logout(): void {
    this.auth.logout();
  }

  private async loadUserInfo(): Promise<void> {
    try {
      this.userInfo.set(await this.auth.userInfo());
      this.userInfoError.set(null);
    } catch {
      this.userInfoError.set('UserInfo request failed. The access token may have expired.');
    }
  }
}
