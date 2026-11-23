import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CurrentUser } from './models';

/**
 * Holds the signed-in user. The session itself is an HttpOnly cookie set by the API,
 * so the browser code never sees or stores a token.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  readonly user = signal<CurrentUser | null>(null);
  readonly sessionExpired = signal(false);
  readonly isAdmin = computed(() => this.user()?.roles.includes('Admin') ?? false);

  private loaded: Promise<void> | null = null;

  /** Restores the session once per page load (cookie may still be valid after a refresh). */
  ensureLoaded(): Promise<void> {
    this.loaded ??= firstValueFrom(this.http.get<CurrentUser>('/api/auth/me')).then(
      (u) => this.user.set(u),
      () => this.user.set(null),
    );
    return this.loaded;
  }

  async login(email: string, password: string): Promise<CurrentUser> {
    const user = await firstValueFrom(
      this.http.post<CurrentUser>('/api/auth/login', { email, password }),
    );
    this.user.set(user);
    this.sessionExpired.set(false);
    this.loaded = Promise.resolve();
    return user;
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', {}));
    } finally {
      this.user.set(null);
      this.loaded = Promise.resolve();
    }
  }

  /** Called by the interceptor when an API call returns 401 for a user we thought was signed in. */
  markExpired(): void {
    if (this.user()) this.sessionExpired.set(true);
    this.user.set(null);
    this.loaded = Promise.resolve();
  }
}
