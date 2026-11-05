import { Component, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Auth, AuthError } from '../../services/auth';

/** Redirect target of the identity server: validates the response and exchanges the code for tokens. */
@Component({
  selector: 'app-callback',
  imports: [RouterLink],
  templateUrl: './callback.html',
  styleUrl: './callback.scss',
})
export class Callback implements OnInit {
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);

  protected readonly error = signal<{ code: string; message: string } | null>(null);

  async ngOnInit(): Promise<void> {
    const query = new URLSearchParams(window.location.search);
    try {
      await this.auth.handleCallback(query);
      await this.router.navigate(['/dashboard'], { replaceUrl: true });
    } catch (e) {
      const err = e instanceof AuthError ? e : new AuthError('unexpected_error', String(e));
      this.error.set({ code: err.code, message: err.message });
      // Remove the code/state from the address bar
      history.replaceState(null, '', '/callback');
    }
  }

  retry(): void {
    void this.auth.startLogin();
  }
}
