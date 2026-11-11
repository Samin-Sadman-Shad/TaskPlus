import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { LoadingService } from '../http/loading.service';
import { EnumLabelPipe } from '../../shared/pipes/enum-label.pipe';
import { BreakpointService } from './breakpoint.service';
import { navItemsFor } from './nav-items';

@Component({
  selector: 'app-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatProgressBarModule,
    EnumLabelPipe,
  ],
  template: `
    <mat-toolbar class="toolbar">
      @if (isHandset()) {
        <button mat-icon-button type="button" aria-label="Toggle navigation" (click)="sidenav.toggle()">
          <mat-icon>menu</mat-icon>
        </button>
      }
      <a class="brand" routerLink="/dashboard">
        <mat-icon aria-hidden="true">check_circle</mat-icon>
        <span>Smart Task</span>
      </a>
      <span class="spacer"></span>
      @if (user(); as u) {
        <button mat-button type="button" [matMenuTriggerFor]="userMenu" aria-label="Account menu">
          <mat-icon>account_circle</mat-icon>
          <span class="user-name">{{ u.fullName }}</span>
        </button>
        <mat-menu #userMenu="matMenu" xPosition="before">
          <div class="menu-header" mat-menu-item disabled>
            <span>{{ u.email }}</span>
            <small>{{ u.role | enumLabel }}</small>
          </div>
          <a mat-menu-item routerLink="/account/change-password">
            <mat-icon>lock_reset</mat-icon><span>Change password</span>
          </a>
          <button mat-menu-item type="button" (click)="logout()">
            <mat-icon>logout</mat-icon><span>Sign out</span>
          </button>
        </mat-menu>
      }
    </mat-toolbar>
    <div class="progress" aria-hidden="true">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
    </div>

    <mat-sidenav-container class="container">
      <mat-sidenav
        #sidenav
        class="sidenav"
        [mode]="isHandset() ? 'over' : 'side'"
        [opened]="!isHandset()"
        [fixedInViewport]="isHandset()"
        fixedTopGap="64"
      >
        <mat-nav-list aria-label="Main navigation">
          @for (item of navItems(); track item.path) {
            <a
              mat-list-item
              [routerLink]="item.path"
              routerLinkActive="active"
              #rla="routerLinkActive"
              [activated]="rla.isActive"
              (click)="isHandset() && sidenav.close()"
            >
              <mat-icon matListItemIcon>{{ item.icon }}</mat-icon>
              <span matListItemTitle>{{ item.label }}</span>
            </a>
          }
        </mat-nav-list>
      </mat-sidenav>
      <mat-sidenav-content class="content">
        <main>
          <router-outlet />
        </main>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    :host { display: flex; flex-direction: column; height: 100%; }
    .toolbar { position: sticky; top: 0; z-index: 3; gap: 8px; border-bottom: 1px solid var(--app-border); }
    .brand { display: flex; align-items: center; gap: 8px; color: inherit; text-decoration: none; font-weight: 500; }
    .brand mat-icon { color: var(--mat-sys-primary); }
    .spacer { flex: 1; }
    .user-name { margin-left: 4px; }
    .menu-header { display: flex; flex-direction: column; line-height: 1.3; height: auto; padding-block: 8px; }
    .progress { height: 4px; }
    .container { flex: 1; }
    .sidenav { width: 220px; border-right: 1px solid var(--app-border); }
    .active { font-weight: 500; }
    main { padding: 24px; max-width: 1280px; margin: 0 auto; }
    @media (max-width: 599.98px) {
      main { padding: 16px 12px; }
      .user-name { display: none; }
    }
  `,
})
export class ShellComponent {
  private readonly auth = inject(AuthService);
  protected readonly isHandset = inject(BreakpointService).isHandset;
  protected readonly loading = inject(LoadingService).isLoading;
  protected readonly user = this.auth.currentUser;
  protected readonly navItems = computed(() => navItemsFor(this.auth.role()));

  protected logout(): void {
    this.auth.logout();
  }
}
