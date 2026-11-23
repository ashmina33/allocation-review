import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { toApiError } from '../core/api-error';

@Component({
  selector: 'app-login-page',
  imports: [FormsModule],
  template: `
    <section class="card narrow">
      <h1>Sign in</h1>
      <p class="muted">Allocation run history and exception review</p>

      @if (auth.sessionExpired()) {
        <p class="alert warn" role="alert">Your session has expired. Please sign in again.</p>
      }
      @if (error()) {
        <p class="alert error" role="alert">{{ error() }}</p>
      }

      <form (ngSubmit)="submit()" #f="ngForm">
        <label for="email">Email</label>
        <input id="email" name="email" type="email" autocomplete="username" required [(ngModel)]="email" />

        <label for="password">Password</label>
        <input id="password" name="password" type="password" autocomplete="current-password" required [(ngModel)]="password" />

        <button type="submit" [disabled]="busy() || f.invalid">
          {{ busy() ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>
    </section>
  `,
})
export class LoginPage {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected email = '';
  protected password = '';
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      const user = await this.auth.login(this.email, this.password);
      this.password = '';
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
      const target = user.roles.includes('Admin') ? (safeReturnUrl(returnUrl) ?? '/runs') : '/no-access';
      await this.router.navigateByUrl(target);
    } catch (err) {
      this.error.set(toApiError(err).message);
    } finally {
      this.busy.set(false);
    }
  }
}

/** Only follow in-app paths, never an external URL from the query string. */
function safeReturnUrl(url: string | null): string | null {
  return url && url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/login') ? url : null;
}
