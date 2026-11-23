import { HttpErrorResponse } from '@angular/common/http';

/** A user-friendly view of a failed request, built from the API's ProblemDetails body. */
export interface ApiError {
  status: number;
  message: string;
  correlationId?: string;
}

export function toApiError(err: unknown): ApiError {
  if (err instanceof HttpErrorResponse) {
    const body = (err.error ?? {}) as { title?: string; correlationId?: string };
    const correlationId =
      body.correlationId ?? err.headers?.get('X-Correlation-ID') ?? undefined;

    if (err.status === 0) {
      return { status: 0, message: 'Cannot reach the server. Check your connection and try again.' };
    }
    if (err.status === 403) {
      return { status: 403, message: 'Your account does not have access to allocation reviews.', correlationId };
    }
    if (err.status >= 500) {
      return { status: err.status, message: 'Something went wrong on the server. Please try again.', correlationId };
    }
    return { status: err.status, message: body.title ?? 'The request failed.', correlationId };
  }
  return { status: -1, message: 'An unexpected error occurred.' };
}
