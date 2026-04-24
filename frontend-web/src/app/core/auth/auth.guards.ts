import { inject } from '@angular/core';
import { Router, type CanMatchFn, type UrlSegment } from '@angular/router';
import { AuthService } from './auth.service';

function segmentsToPath(segments: UrlSegment[]): string {
  return '/' + segments.map((s) => s.path).join('/');
}

/**
 * CanMatchFn — lazy chunks for the target route are only fetched if the
 * user is authenticated. Anonymous users never download guarded code.
 */
export const authGuard: CanMatchFn = (_route, segments) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isAuthenticated()) return true;
  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: segmentsToPath(segments) },
  });
};

/**
 * Mirror guard — authenticated users are redirected away from guest-only
 * routes (login / register).
 */
export const guestGuard: CanMatchFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) return true;
  return router.createUrlTree(['/home']);
};
