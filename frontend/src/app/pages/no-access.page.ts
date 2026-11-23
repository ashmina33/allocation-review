import { Component, inject } from '@angular/core';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-no-access-page',
  template: `
    <section class="card narrow">
      <h1>No access</h1>
      <p role="alert">
        You are signed in as <strong>{{ auth.user()?.email }}</strong> (Viewer).
        Allocation run reviews are available to Admin users only.
      </p>
      <p class="muted">Ask an administrator if you need access.</p>
    </section>
  `,
})
export class NoAccessPage {
  protected readonly auth = inject(AuthService);
}
