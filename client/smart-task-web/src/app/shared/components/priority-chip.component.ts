import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TaskPriority } from '../../core/models/enums';

@Component({
  selector: 'app-priority-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="chip" [attr.data-priority]="priority()"><span class="dot" aria-hidden="true"></span>{{ priority() }}</span>`,
  styles: `
    .chip { display: inline-flex; align-items: center; gap: 6px; font-size: 13px; white-space: nowrap; }
    .dot { width: 8px; height: 8px; border-radius: 50%; }
    [data-priority='Low'] .dot { background: #94a3b8; }
    [data-priority='Medium'] .dot { background: #3b82f6; }
    [data-priority='High'] .dot { background: #f59e0b; }
    [data-priority='Critical'] .dot { background: #dc2626; }
  `,
})
export class PriorityChipComponent {
  readonly priority = input.required<TaskPriority>();
}
