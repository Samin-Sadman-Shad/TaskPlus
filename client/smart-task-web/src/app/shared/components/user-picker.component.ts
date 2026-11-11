import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Observable, catchError, combineLatest, debounceTime, distinctUntilChanged, map, of, startWith, switchMap } from 'rxjs';

export interface UserOption {
  id: string;
  fullName: string;
  email: string;
  /** Optional extra line under the name, e.g. what picking this user will do. */
  note?: string;
}

export type UserSearchFn = (term: string) => Observable<UserOption[]>;

/**
 * Autocomplete that finds a user through a search function: the users/lookup endpoint for project
 * members, or a local filter over project members when assigning a task.
 */
@Component({
  selector: 'app-user-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule],
  template: `
    <mat-form-field class="field" subscriptSizing="dynamic">
      <mat-label>{{ label() }}</mat-label>
      <input
        matInput
        type="text"
        [formControl]="term"
        [matAutocomplete]="auto"
        [placeholder]="placeholder()"
        autocomplete="off"
      />
      <mat-autocomplete #auto="matAutocomplete" [displayWith]="clearDisplay" (optionSelected)="select($event)">
        @for (user of options(); track user.id) {
          <mat-option [value]="user">
            <span>{{ user.fullName }}</span>
            <small class="email"> · {{ user.email }}</small>
            @if (user.note) {
              <small class="note">{{ user.note }}</small>
            }
          </mat-option>
        } @empty {
          @if (term.value.length > 0) {
            <mat-option disabled>No matching users</mat-option>
          }
        }
      </mat-autocomplete>
    </mat-form-field>
  `,
  styles: `
    .field { width: 100%; }
    .email { color: var(--app-muted); }
    .note { display: block; color: var(--app-muted); font-size: 12px; line-height: 1.2; }
  `,
})
export class UserPickerComponent {
  readonly search = input.required<UserSearchFn>();
  readonly excludeIds = input<readonly string[]>([]);
  readonly label = input('Find a user');
  readonly placeholder = input('Type a name or email');
  readonly picked = output<UserOption>();

  protected readonly term = new FormControl('', { nonNullable: true });
  private readonly reset = signal(0);

  protected readonly options = toSignal(
    combineLatest([
      this.term.valueChanges.pipe(
        startWith(''),
        map((value) => (typeof value === 'string' ? value.trim() : '')),
        debounceTime(300),
        distinctUntilChanged(),
      ),
      toObservable(this.search),
      toObservable(this.excludeIds),
      toObservable(this.reset),
    ]).pipe(
      switchMap(([term, search, exclude]) =>
        search(term).pipe(
          map((users) => users.filter((u) => !exclude.includes(u.id))),
          catchError(() => of([] as UserOption[])),
        ),
      ),
    ),
    { initialValue: [] as UserOption[] },
  );

  /** The input is cleared after a pick, so it never shows the selected object. */
  protected readonly clearDisplay = (): string => '';

  protected select(event: MatAutocompleteSelectedEvent): void {
    this.picked.emit(event.option.value as UserOption);
    this.term.setValue('');
    this.reset.update((n) => n + 1);
  }
}
