import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { EmptyStateComponent } from './empty-state.component';

@Component({
  selector: 'app-forbidden',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EmptyStateComponent, MatButtonModule, RouterLink],
  template: `
    <app-empty-state icon="block" title="Not allowed" message="Your role does not have access to this page.">
      <a mat-stroked-button routerLink="/dashboard">Go to dashboard</a>
    </app-empty-state>
  `,
})
export default class ForbiddenComponent {}
