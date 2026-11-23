import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Signed-in users only. UI convenience only - the API enforces access itself. */
export const authGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.ensureLoaded();
  return auth.user() ? true : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Admin-only screens. A Viewer is shown the no-access page instead of a broken screen. */
export const adminGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.ensureLoaded();
  return auth.isAdmin() ? true : router.createUrlTree(['/no-access']);
};
