// Shapes returned by the ASP.NET Core API (camelCase JSON, enums as strings).

export type GapStatus = 'Filled' | 'PartiallyFilled' | 'Unfilled';

export interface CurrentUser {
  email: string;
  roles: string[];
}

export interface RunSummary {
  runId: string;
  runDate: string; // yyyy-MM-dd
  totalAllocatedUnits: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface GapRow {
  gapId: string;
  store: string;
  brand: string;
  productType: string;
  originalShortfall: number;
  unitsAllocated: number;
  unitsRemaining: number;
  status: GapStatus;
}

export interface RunReview {
  runId: string;
  runDate: string;
  totalAllocatedUnits: number;
  gapCount: number;
  statusCounts: { filled: number; partiallyFilled: number; unfilled: number };
  exceptionsOnly: boolean;
  gaps: GapRow[];
}

export const STATUS_LABELS: Record<GapStatus, string> = {
  Filled: 'Filled',
  PartiallyFilled: 'Partially filled',
  Unfilled: 'Unfilled',
};
