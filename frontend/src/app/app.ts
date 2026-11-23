import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink],
  template: `
    <header class="topbar">
      <a routerLink="/runs" class="brand">Allocation Review</a>
      @if (auth.user(); as u) {
        <div class="session">
          @if (auth.isAdmin()) { <a routerLink="/runs">Run history</a> }
          <span class="who">{{ u.email }} <span class="role">{{ auth.isAdmin() ? 'Admin' : 'Viewer' }}</span></span>
          <button type="button" class="link" (click)="signOut()">Sign out</button>
        </div>
      }
    </header>
    <main>
      <router-outlet />
    </main>
  `,
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  async signOut(): Promise<void> {
    await this.auth.logout();
    await this.router.navigateByUrl('/login');
  }
}
