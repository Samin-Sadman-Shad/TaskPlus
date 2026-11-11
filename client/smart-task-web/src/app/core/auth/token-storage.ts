import { Injectable, signal } from '@angular/core';

/**
 * Holds the access token in memory only. It is never written to browser web storage,
 * so an XSS payload can't read it from storage; a page reload restores it through the refresh cookie.
 */
@Injectable({ providedIn: 'root' })
export class TokenStorage {
  private readonly token = signal<string | null>(null);

  get(): string | null {
    return this.token();
  }

  set(token: string): void {
    this.token.set(token);
  }

  clear(): void {
    this.token.set(null);
  }
}
