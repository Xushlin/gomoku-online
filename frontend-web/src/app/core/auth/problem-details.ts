/**
 * RFC 7807 ProblemDetails. Backend's global exception middleware emits this
 * for every non-2xx response. `errors` is present on 400 ValidationException
 * and maps field name → messages.
 */
export interface ProblemDetails {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly errors?: Readonly<Record<string, readonly string[]>>;
}

export function isProblemDetails(value: unknown): value is ProblemDetails {
  if (value === null || typeof value !== 'object') return false;
  const candidate = value as Record<string, unknown>;
  // At least one of the RFC 7807 fields is present.
  return (
    typeof candidate['type'] === 'string' ||
    typeof candidate['title'] === 'string' ||
    typeof candidate['status'] === 'number' ||
    typeof candidate['detail'] === 'string' ||
    typeof candidate['errors'] === 'object'
  );
}
