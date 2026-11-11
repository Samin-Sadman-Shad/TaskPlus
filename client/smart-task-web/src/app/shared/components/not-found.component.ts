import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { EmptyStateComponent } from './empty-state.component';

@Component({
  selector: 'app-not-found',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EmptyStateComponent, MatButtonModule, RouterLink],
  template: `
    <app-empty-state icon="search_off" title="Page not found" message="The page you asked for does not exist.">
      <a mat-stroked-button routerLink="/dashboard">Go to dashboard</a>
    </app-empty-state>
  `,
  styles: `:host { display: block; padding-top: 10vh; }`,
})
export default class NotFoundComponent {}
