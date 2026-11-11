import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, firstValueFrom, map, of, shareReplay, tap } from 'rxjs';
import { apiUrl, unwrap } from '../http/api-url';
import { BaseResponse } from '../http/api.models';
import { UserRole } from '../models/enums';
import { AuthResponseDto, ChangePasswordDto, LoginRequestDto, RegisterRequestDto, UserDto } from './auth.models';
import { TokenStorage } from './token-storage';

/** Refresh this long before the access token expires. */
const REFRESH_LEAD_MS = 60_000;
/** Never schedule a refresh sooner than this (protects against clock skew loops). */
const MIN_REFRESH_DELAY_MS = 5_000;

/**
 * Session state for the signed-in user.
 * The access token lives in {@link TokenStorage} (memory only); the refresh token is an HttpOnly
 * cookie scoped to /api/v1/auth that the browser sends on auth calls.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly tokens = inject(TokenStorage);
  private readonly router = inject(Router);

  private readonly user = signal<UserDto | null>(null);
  private refreshTimer: ReturnType<typeof setTimeout> | undefined;
  private refreshInFlight: Observable<AuthResponseDto> | null = null;

  readonly currentUser = this.user.asReadonly();
  readonly isAuthenticated = computed(() => this.user() !== null);
  readonly role = computed<UserRole | null>(() => this.user()?.role ?? null);
  readonly mustChangePassword = computed(() => this.user()?.mustChangePassword === true);

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.refreshTimer));
  }

  login(dto: LoginRequestDto): Observable<UserDto> {
    return this.http
      .post<BaseResponse<AuthResponseDto>>(apiUrl('auth', 'login'), dto, { withCredentials: true })
      .pipe(
        unwrap(),
        tap((session) => this.startSession(session)),
        map((session) => session.user),
      );
  }

  register(dto: RegisterRequestDto): Observable<UserDto> {
    return this.http
      .post<BaseResponse<UserDto>>(apiUrl('auth', 'register'), dto, { withCredentials: true })
      .pipe(unwrap());
  }

  /**
   * Exchanges the refresh cookie for a new access token. Concurrent callers share one request,
   * so several parallel 401s cause a single refresh (the server rotates the cookie on each call).
   */
  refresh(): Observable<AuthResponseDto> {
    this.refreshInFlight ??= this.http
      .post<BaseResponse<AuthResponseDto>>(apiUrl('auth', 'refresh'), null, { withCredentials: true })
      .pipe(
        unwrap(),
        tap((session) => this.startSession(session)),
        finalize(() => (this.refreshInFlight = null)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    return this.refreshInFlight;
  }

  /** Called once at start-up: restores the session after a reload. A 401 just means "signed out". */
  tryRestoreSession(): Promise<void> {
    return firstValueFrom(
      this.refresh().pipe(
        map(() => undefined),
        catchError(() => of(undefined)),
      ),
    );
  }

  /** Signs out on the server (revokes the refresh token, clears the cookie) and locally. */
  logout(): void {
    this.http
      .post<void>(apiUrl('auth', 'logout'), null, { withCredentials: true })
      .pipe(
        catchError(() => of(undefined)),
        finalize(() => {
          this.clearSession();
          void this.router.navigateByUrl('/login');
        }),
      )
      .subscribe();
  }

  /** The session can't be renewed: clear it and send the user to sign in, then back here. */
  endExpiredSession(): void {
    const returnUrl = this.router.url;
    this.clearSession();
    void this.router.navigate(['/login'], {
      queryParams: returnUrl && returnUrl !== '/' && !returnUrl.startsWith('/login') ? { returnUrl } : {},
    });
  }

  /** Changes the password; the server signs out every other session and returns a fresh one for this device. */
  changePassword(dto: ChangePasswordDto): Observable<UserDto> {
    return this.http
      .post<BaseResponse<AuthResponseDto>>(apiUrl('users', 'me', 'password'), dto, { withCredentials: true })
      .pipe(
        unwrap(),
        tap((session) => this.startSession(session)),
        map((session) => session.user),
      );
  }

  hasRole(...roles: UserRole[]): boolean {
    const role = this.role();
    return role !== null && roles.includes(role);
  }

  clearSession(): void {
    clearTimeout(this.refreshTimer);
    this.tokens.clear();
    this.user.set(null);
  }

  private startSession(session: AuthResponseDto): void {
    this.tokens.set(session.accessToken);
    this.user.set(session.user);
    this.scheduleRefresh(session.accessTokenExpiresAt);
  }

  private scheduleRefresh(expiresAt: string): void {
    clearTimeout(this.refreshTimer);
    const delay = Math.max(new Date(expiresAt).getTime() - Date.now() - REFRESH_LEAD_MS, MIN_REFRESH_DELAY_MS);
    this.refreshTimer = setTimeout(() => {
      this.refresh().subscribe({ error: () => this.endExpiredSession() });
    }, delay);
  }
}
