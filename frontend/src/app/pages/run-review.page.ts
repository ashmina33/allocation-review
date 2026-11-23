import { DatePipe } from '@angular/common';
import { Component, booleanAttribute, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../core/api-error';
import { RunReview, STATUS_LABELS } from '../core/models';
import { RunsService } from '../core/runs.service';

@Component({
  selector: 'app-run-review-page',
  imports: [RouterLink, DatePipe],
  template: `
    <section class="card">
      <a routerLink="/runs" class="back">← Run history</a>

      @if (loading() && !review()) {
        <p class="status" role="status">Loading run…</p>
      } @else if (error(); as e) {
        <div class="alert error" role="alert">
          <p>{{ e.message }}</p>
          @if (e.correlationId) { <p class="small">Reference: {{ e.correlationId }}</p> }
          @if (e.status !== 404) { <button type="button" (click)="load()">Try again</button> }
        </div>
      } @else if (review(); as r) {
        <h1>Run {{ r.runId }}</h1>
        <p class="muted">{{ r.runDate | date: 'd MMM yyyy' }}</p>

        <!-- Totals and counts always describe the whole run, even when the filter is on. -->
        <div class="stats">
          <div class="stat"><span class="label">Total allocated units</span><span class="value" data-testid="total-units">{{ r.totalAllocatedUnits }}</span></div>
          <div class="stat"><span class="label">Gaps</span><span class="value" data-testid="gap-count">{{ r.gapCount }}</span></div>
          <div class="stat ok"><span class="label">Filled</span><span class="value" data-testid="count-filled">{{ r.statusCounts.filled }}</span></div>
          <div class="stat warn"><span class="label">Partially filled</span><span class="value" data-testid="count-partial">{{ r.statusCounts.partiallyFilled }}</span></div>
          <div class="stat bad"><span class="label">Unfilled</span><span class="value" data-testid="count-unfilled">{{ r.statusCounts.unfilled }}</span></div>
        </div>

        <label class="toggle">
          <input type="checkbox" [checked]="exceptions()" (change)="setExceptions($any($event.target).checked)" />
          Show exceptions only (partially filled and unfilled)
        </label>

        @if (r.gaps.length === 0) {
          <p class="empty">{{ exceptions() ? 'No exceptions in this run - every gap is filled.' : 'This run has no gaps.' }}</p>
        } @else {
          <table [attr.aria-busy]="loading()">
            <thead>
              <tr>
                <th>Gap</th><th>Store</th><th>Brand</th><th>Product type</th>
                <th class="num">Original shortfall</th><th class="num">Allocated</th><th class="num">Remaining</th><th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (g of r.gaps; track g.gapId) {
                <tr data-testid="gap-row">
                  <td>{{ g.gapId }}</td><td>{{ g.store }}</td><td>{{ g.brand }}</td><td>{{ g.productType }}</td>
                  <td class="num">{{ g.originalShortfall }}</td>
                  <td class="num">{{ g.unitsAllocated }}</td>
                  <td class="num">{{ g.unitsRemaining }}</td>
                  <td><span class="badge" [class]="'badge ' + g.status">{{ labels[g.status] }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        }
      }
    </section>
  `,
})
export class RunReviewPage {
  private readonly runs = inject(RunsService);
  private readonly router = inject(Router);

  readonly runId = input.required<string>();
  /** ?exceptions=true keeps the filter in the URL so it can be shared or refreshed. */
  readonly exceptionsParam = input(false, { alias: 'exceptions', transform: booleanAttribute });

  protected readonly labels = STATUS_LABELS;
  protected readonly exceptions = signal(false);
  protected readonly loading = signal(true);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly review = signal<RunReview | null>(null);

  constructor() {
    effect(() => {
      this.exceptions.set(this.exceptionsParam());
      this.load(this.runId(), this.exceptionsParam());
    });
  }

  load(runId = this.runId(), exceptionsOnly = this.exceptions()): void {
    this.loading.set(true);
    this.error.set(null);
    this.runs.getReview(runId, exceptionsOnly).subscribe({
      next: (r) => {
        this.review.set(r);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(toApiError(err));
        this.review.set(null);
        this.loading.set(false);
      },
    });
  }

  setExceptions(on: boolean): void {
    this.router.navigate([], { queryParams: { exceptions: on ? true : null }, queryParamsHandling: 'merge' });
  }
}
