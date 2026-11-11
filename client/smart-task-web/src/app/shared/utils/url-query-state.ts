import { inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';

export type QueryPatch = Record<string, string | number | boolean | null | undefined>;

/**
 * Keeps a list page's search/filter/sort/page state in the URL query string, so links, refresh and
 * back/forward restore the same view. Call from an injection context (a field initialiser).
 */
export function injectUrlQueryState() {
  const route = inject(ActivatedRoute);
  const router = inject(Router);
  const params = toSignal(route.queryParamMap, { initialValue: route.snapshot.queryParamMap });

  return {
    params,
    /** Merges the patch into the URL. Empty values remove the parameter. */
    patch(patch: QueryPatch): void {
      const queryParams: QueryPatch = {};
      for (const [key, value] of Object.entries(patch)) {
        queryParams[key] = value === '' || value === undefined || value === false ? null : value;
      }
      void router.navigate([], { relativeTo: route, queryParams, queryParamsHandling: 'merge', replaceUrl: true });
    },
  };
}

export function readString(params: ParamMap, key: string): string | undefined {
  return params.get(key) ?? undefined;
}

export function readInt(params: ParamMap, key: string, fallback: number): number {
  const value = Number(params.get(key));
  return Number.isInteger(value) && value > 0 ? value : fallback;
}

/** Reads a value only if it is in the allowed list (sort fields and enums are whitelisted). */
export function readOneOf<T extends string>(params: ParamMap, key: string, allowed: readonly T[]): T | undefined {
  const value = params.get(key);
  return allowed.find((a) => a === value);
}
