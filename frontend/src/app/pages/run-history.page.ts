import { DatePipe } from '@angular/common';
import { Component, inject, input, numberAttribute, signal, effect } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../core/api-error';
import { PagedResult, RunSummary } from '../core/models';
import { RunsService } from '../core/runs.service';

@Component({
  selector: 'app-run-history-page',
  imports: [RouterLink, DatePipe],
  template: `
    <section class="card">
      <h1>Allocation run history</h1>
      <p class="muted">Saved runs, newest first.</p>

      @if (loading()) {
        <p class="status" role="status">Loading runs…</p>
      } @else if (error(); as e) {
        <div class="alert error" role="alert">
          <p>{{ e.message }}</p>
          @if (e.correlationId) { <p class="small">Reference: {{ e.correlationId }}</p> }
          <button type="button" (click)="load()">Try again</button>
        </div>
      } @else if (result(); as r) {
        @if (r.items.length === 0) {
          <p class="empty">No saved runs yet.</p>
        } @else {
          <table>
            <thead>
              <tr><th>Run</th><th>Run date</th><th class="num">Total allocated units</th></tr>
            </thead>
            <tbody>
              @for (run of r.items; track run.runId) {
                <tr>
                  <td><a [routerLink]="['/runs', run.runId]">{{ run.runId }}</a></td>
                  <td>{{ run.runDate | date: 'd MMM yyyy' }}</td>
                  <td class="num">{{ run.totalAllocatedUnits }}</td>
                </tr>
              }
            </tbody>
          </table>

          <nav class="pager" aria-label="Pagination">
            <button type="button" [disabled]="r.page <= 1" (click)="goTo(r.page - 1)">Previous</button>
            <span>Page {{ r.page }} of {{ r.totalPages }} · {{ r.totalCount }} runs</span>
            <button type="button" [disabled]="r.page >= r.totalPages" (click)="goTo(r.page + 1)">Next</button>
          </nav>
        }
      }
    </section>
  `,
})
export class RunHistoryPage {
  private readonly runs = inject(RunsService);
  private readonly router = inject(Router);

  /** Bound from the ?page= query parameter so the page survives refresh and back/forward. */
  readonly page = input(1, { transform: (v: unknown) => Math.max(1, numberAttribute(v, 1)) });

  protected readonly loading = signal(true);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly result = signal<PagedResult<RunSummary> | null>(null);

  constructor() {
    effect(() => this.load(this.page()));
  }

  load(page = this.page()): void {
    this.loading.set(true);
    this.error.set(null);
    this.runs.getHistory(page).subscribe({
      next: (r) => {
        this.result.set(r);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(toApiError(err));
        this.loading.set(false);
      },
    });
  }

  goTo(page: number): void {
    this.router.navigate([], { queryParams: { page }, queryParamsHandling: 'merge' });
  }
}
