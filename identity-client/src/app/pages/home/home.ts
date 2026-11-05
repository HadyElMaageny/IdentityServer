import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Auth } from '../../services/auth';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home {
  protected readonly auth = inject(Auth);
  protected readonly discoveryUrl = `${environment.identityServerUrl}/.well-known/openid-configuration`;

  protected readonly steps = [
    { title: 'Authorization request', detail: 'Redirect to /connect/authorize with code_challenge (S256), state and nonce' },
    { title: 'Sign in', detail: 'The identity server authenticates the user with its own session cookie' },
    { title: 'Consent', detail: 'The user approves the scopes this app asked for' },
    { title: 'Authorization code', detail: 'Returned to /callback, and the state is checked against CSRF' },
    { title: 'Token exchange', detail: 'code + code_verifier → access, ID & refresh tokens' },
  ];

  login(): void {
    void this.auth.startLogin();
  }
}
