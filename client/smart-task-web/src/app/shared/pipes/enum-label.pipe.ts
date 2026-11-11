import { Pipe, PipeTransform } from '@angular/core';

/** Turns an enum name into a label: 'InProgress' → 'In progress', 'ToDo' → 'To do'. */
export function enumLabel(value: string | null | undefined): string {
  if (!value) {
    return '';
  }
  const words = value.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

@Pipe({ name: 'enumLabel' })
export class EnumLabelPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return enumLabel(value);
  }
}
