import { Component, inject, OnInit } from '@angular/core';
import { Auth } from '../../services/auth';

/** /login: immediately starts the Authorization Code + PKCE redirect to the identity server. */
@Component({
  selector: 'app-login',
  imports: [],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login implements OnInit {
  private readonly auth = inject(Auth);

  ngOnInit(): void {
    void this.auth.startLogin();
  }
}
