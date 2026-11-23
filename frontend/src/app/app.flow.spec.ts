import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';
import { PagedResult, RunReview, RunSummary } from './core/models';

/**
 * User-facing flow tests: the real App, routes, guards, interceptor and pages,
 * with only the HTTP backend replaced by HttpTestingController.
 */

const HISTORY: PagedResult<RunSummary> = {
  items: [
    { runId: 'R-102', runDate: '2026-09-14', totalAllocatedUnits: 5 },
    { runId: 'R-101', runDate: '2026-09-13', totalAllocatedUnits: 1 },
  ],
  page: 1, pageSize: 10, totalCount: 2, totalPages: 1,
};

const ALL_GAPS: RunReview['gaps'] = [
  { gapId: 'G-01', store: 'Store A', brand: 'Brand X', productType: 'Frames', originalShortfall: 5, unitsAllocated: 3, unitsRemaining: 2, status: 'PartiallyFilled' },
  { gapId: 'G-02', store: 'Store B', brand: 'Brand X', productType: 'Frames', originalShortfall: 2, unitsAllocated: 2, unitsRemaining: 0, status: 'Filled' },
  { gapId: 'G-03', store: 'Store C', brand: 'Brand Y', productType: 'Sunglasses', originalShortfall: 4, unitsAllocated: 0, unitsRemaining: 4, status: 'Unfilled' },
];

function review(exceptionsOnly: boolean): RunReview {
  return {
    runId: 'R-102', runDate: '2026-09-14', totalAllocatedUnits: 5, gapCount: 3,
    statusCounts: { filled: 1, partiallyFilled: 1, unfilled: 1 },
    exceptionsOnly,
    gaps: exceptionsOnly ? ALL_GAPS.filter((g) => g.status !== 'Filled') : ALL_GAPS,
  };
}

describe('Allocation review user flows', () => {
  let fixture: ComponentFixture<App>;
  let http: HttpTestingController;
  let router: Router;
  let el: HTMLElement;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(routes, withComponentInputBinding()),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(App);
    el = fixture.nativeElement;
  });

  afterEach(() => http.verify());

  /** Lets pending promises, router navigation and change detection settle. */
  async function settle(): Promise<void> {
    // whenStable() would wait for open HTTP requests, so tick the event loop instead.
    for (let i = 0; i < 5; i++) {
      fixture.detectChanges();
      await new Promise((r) => setTimeout(r));
    }
  }

  function text(): string {
    return el.textContent ?? '';
  }

  function gapIds(): string[] {
    return Array.from(el.querySelectorAll('[data-testid="gap-row"] td:first-child')).map((td) => td.textContent!.trim());
  }

  function type(selector: string, value: string): void {
    const input = el.querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  async function signInAs(roles: string[]): Promise<void> {
    void router.navigateByUrl('/runs');
    await settle();
    http.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await settle();
    expect(router.url).toContain('/login');

    type('#email', 'user@demo.local');
    type('#password', 'Secret#123');
    await settle();
    el.querySelector<HTMLButtonElement>('button[type=submit]')!.click();
    await settle();

    const login = http.expectOne('/api/auth/login');
    expect(login.request.method).toBe('POST');
    login.flush({ email: 'user@demo.local', roles });
    await settle();
  }

  it('Admin signs in, opens the latest run and filters to exceptions', async () => {
    await signInAs(['Admin']);

    // History: newest first with totals
    http.expectOne((r) => r.url === '/api/runs' && r.params.get('page') === '1').flush(HISTORY);
    await settle();
    const historyRows = Array.from(el.querySelectorAll('tbody tr')).map((tr) => tr.textContent);
    expect(historyRows[0]).toContain('R-102');
    expect(historyRows[0]).toContain('5');
    expect(historyRows[1]).toContain('R-101');

    // Open R-102
    el.querySelector<HTMLAnchorElement>('a[href="/runs/R-102"]')!.click();
    await settle();
    http.expectOne((r) => r.url === '/api/runs/R-102' && r.params.get('exceptionsOnly') === 'false').flush(review(false));
    await settle();

    expect(el.querySelector('[data-testid="total-units"]')!.textContent).toBe('5');
    expect(el.querySelector('[data-testid="gap-count"]')!.textContent).toBe('3');
    expect(gapIds()).toEqual(['G-01', 'G-02', 'G-03']);
    expect(text()).toContain('Partially filled');

    // Exceptions filter: rows narrow, run totals stay
    const toggle = el.querySelector<HTMLInputElement>('input[type=checkbox]')!;
    toggle.checked = true;
    toggle.dispatchEvent(new Event('change'));
    await settle();
    http.expectOne((r) => r.url === '/api/runs/R-102' && r.params.get('exceptionsOnly') === 'true').flush(review(true));
    await settle();

    expect(gapIds()).toEqual(['G-01', 'G-03']);
    expect(el.querySelector('[data-testid="total-units"]')!.textContent).toBe('5');
    expect(el.querySelector('[data-testid="count-filled"]')!.textContent).toBe('1');
    expect(router.url).toContain('exceptions=true');
  });

  it('Viewer is shown the no-access page and no review data is requested', async () => {
    await signInAs(['Viewer']);

    expect(router.url).toBe('/no-access');
    expect(text()).toContain('Admin users only');
    expect(text()).not.toContain('Run history'); // nav link hidden for viewers
    http.expectNone('/api/runs');
  });

  it('Admin sees an empty state when there are no saved runs', async () => {
    await signInAs(['Admin']);
    http.expectOne((r) => r.url === '/api/runs').flush({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 });
    await settle();

    expect(text()).toContain('No saved runs yet.');
  });

  it('expired session returns the user to sign-in with a message', async () => {
    await signInAs(['Admin']);
    http.expectOne((r) => r.url === '/api/runs').flush(null, { status: 401, statusText: 'Unauthorized' });
    await settle();

    expect(router.url).toContain('/login');
    expect(text()).toContain('Your session has expired');
  });

  it('server error shows a retryable message with the correlation reference', async () => {
    await signInAs(['Admin']);
    http.expectOne((r) => r.url === '/api/runs').flush(
      { title: 'An unexpected error occurred.', correlationId: 'abc123def456' },
      { status: 500, statusText: 'Server Error' },
    );
    await settle();

    expect(text()).toContain('Something went wrong on the server');
    expect(text()).toContain('abc123def456');

    el.querySelector<HTMLButtonElement>('.alert button')!.click();
    await settle();
    http.expectOne((r) => r.url === '/api/runs').flush(HISTORY);
    await settle();
    expect(text()).toContain('R-102');
  });
});
