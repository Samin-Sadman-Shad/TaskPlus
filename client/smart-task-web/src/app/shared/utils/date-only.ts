import { DateOnlyString } from '../../core/http/api.models';

/** Formats a Date as the API's DateOnly ("yyyy-MM-dd") using the local calendar day. */
export function toDateOnly(date: Date | null | undefined): DateOnlyString | null {
  if (!date || Number.isNaN(date.getTime())) {
    return null;
  }
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

/** Parses "yyyy-MM-dd" as a local date (new Date("yyyy-MM-dd") would be UTC midnight). */
export function fromDateOnly(value: DateOnlyString | null | undefined): Date | null {
  if (!value) {
    return null;
  }
  const [year, month, day] = value.split('-').map(Number);
  if (!year || !month || !day) {
    return null;
  }
  return new Date(year, month - 1, day);
}

/** Today at local midnight, optionally shifted by a number of days. */
export function today(offsetDays = 0): Date {
  const now = new Date();
  return new Date(now.getFullYear(), now.getMonth(), now.getDate() + offsetDays);
}
