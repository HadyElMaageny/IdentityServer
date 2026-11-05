import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { Home } from './pages/home/home';
import { Login } from './pages/login/login';
import { Callback } from './pages/callback/callback';
import { Dashboard } from './pages/dashboard/dashboard';
import { Auth } from './services/auth';

/** Only signed-in users can open the dashboard. */
const authGuard: CanActivateFn = () =>
  inject(Auth).isAuthenticated() ? true : inject(Router).createUrlTree(['/']);

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'login', component: Login },
  { path: 'callback', component: Callback },
  { path: 'dashboard', component: Dashboard, canActivate: [authGuard] },

  { path: '**', redirectTo: '' },
];
