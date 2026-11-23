import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/guards';
import { LoginPage } from './pages/login.page';
import { NoAccessPage } from './pages/no-access.page';
import { RunHistoryPage } from './pages/run-history.page';
import { RunReviewPage } from './pages/run-review.page';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'runs' },
  { path: 'login', component: LoginPage, title: 'Sign in' },
  { path: 'runs', component: RunHistoryPage, canActivate: [authGuard, adminGuard], title: 'Run history' },
  { path: 'runs/:runId', component: RunReviewPage, canActivate: [authGuard, adminGuard], title: 'Run review' },
  { path: 'no-access', component: NoAccessPage, canActivate: [authGuard], title: 'No access' },
  { path: '**', redirectTo: 'runs' },
];
