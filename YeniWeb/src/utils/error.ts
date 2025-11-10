export function toErrorMessage(error: unknown, fallback = 'Unknown error'): string {
  if (error instanceof Error) {
    return error.message;
  }
  if (typeof error === 'string') {
    return error;
  }
  if (error && typeof error === 'object' && 'message' in error) {
    const maybeMessage = (error as Record<string, unknown>).message;
    if (typeof maybeMessage === 'string') {
      return maybeMessage;
    }
  }
  return fallback;
}
