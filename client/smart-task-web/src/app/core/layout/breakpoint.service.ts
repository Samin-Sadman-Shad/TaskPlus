import { BreakpointObserver } from '@angular/cdk/layout';
import { Injectable, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';

/** Screen-size signals shared by the shell (sidenav mode) and the list pages (table vs cards). */
@Injectable({ providedIn: 'root' })
export class BreakpointService {
  private readonly observer = inject(BreakpointObserver);

  /** Below 960 px the sidenav becomes an overlay. */
  readonly isHandset = toSignal(this.observer.observe('(max-width: 959.98px)').pipe(map((s) => s.matches)), {
    initialValue: false,
  });

  /** Below 600 px tables switch to cards. */
  readonly isSmall = toSignal(this.observer.observe('(max-width: 599.98px)').pipe(map((s) => s.matches)), {
    initialValue: false,
  });
}
