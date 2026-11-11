import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Page title on the left, actions (projected content) on the right. */
@Component({
  selector: 'app-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="titles">
      <h1>{{ title() }}</h1>
      @if (subtitle()) {
        <p class="subtitle">{{ subtitle() }}</p>
      }
    </div>
    <div class="actions"><ng-content /></div>
  `,
  styles: `
    :host { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 20px; }
    h1 { margin: 0; font: var(--mat-sys-headline-small); }
    .subtitle { margin: 4px 0 0; color: var(--app-muted); }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
  `,
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
}
