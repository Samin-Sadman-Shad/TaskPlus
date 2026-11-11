import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TaskItemStatus } from '../../core/models/enums';
import { EnumLabelPipe } from '../pipes/enum-label.pipe';

@Component({
  selector: 'app-status-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EnumLabelPipe],
  template: `<span class="chip" [attr.data-status]="status()">{{ status() | enumLabel }}</span>`,
  styles: `
    .chip { display: inline-block; padding: 2px 10px; border-radius: 12px; font-size: 12px; font-weight: 500; white-space: nowrap; border: 1px solid; }
    [data-status='ToDo'] { color: #475569; border-color: #cbd5e1; background: #f8fafc; }
    [data-status='InProgress'] { color: #1d4ed8; border-color: #bfdbfe; background: #eff6ff; }
    [data-status='Completed'] { color: #15803d; border-color: #bbf7d0; background: #f0fdf4; }
    [data-status='Cancelled'] { color: #6b7280; border-color: #e5e7eb; background: #fff; text-decoration: line-through; }
  `,
})
export class StatusChipComponent {
  readonly status = input.required<TaskItemStatus>();
}
