import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult, RunReview, RunSummary } from './models';

export const PAGE_SIZE = 10;

@Injectable({ providedIn: 'root' })
export class RunsService {
  private readonly http = inject(HttpClient);

  getHistory(page: number, pageSize = PAGE_SIZE): Observable<PagedResult<RunSummary>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<RunSummary>>('/api/runs', { params });
  }

  getReview(runId: string, exceptionsOnly: boolean): Observable<RunReview> {
    const params = new HttpParams().set('exceptionsOnly', exceptionsOnly);
    return this.http.get<RunReview>(`/api/runs/${encodeURIComponent(runId)}`, { params });
  }
}
