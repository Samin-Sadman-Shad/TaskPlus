import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <mat-icon aria-hidden="true">{{ icon() }}</mat-icon>
    <p class="title">{{ title() }}</p>
    @if (message()) {
      <p class="message">{{ message() }}</p>
    }
    <ng-content />
  `,
  styles: `
    :host { display: flex; flex-direction: column; align-items: center; text-align: center; padding: 40px 16px; color: var(--app-muted); }
    mat-icon { font-size: 40px; width: 40px; height: 40px; }
    .title { margin: 12px 0 4px; font-weight: 500; color: var(--mat-sys-on-surface); }
    .message { margin: 0 0 12px; }
  `,
})
export class EmptyStateComponent {
  readonly icon = input('inbox');
  readonly title = input.required<string>();
  readonly message = input<string>();
}
