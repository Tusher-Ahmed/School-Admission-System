import { friendlyMessage } from './messages';
import type { ConfirmResult, CreatedClass, Hold, SchoolClass } from './types';

export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080').replace(/\/+$/, '');

/** An error the UI can show as-is: `message` is always user-friendly. */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;

  constructor(status: number, code: string, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

export function toUserMessage(error: unknown): string {
  return error instanceof ApiError ? error.message : friendlyMessage('SERVER_ERROR', 500);
}

// Difference between the server clock and this browser's clock. Countdowns use server time, so a wrong
// computer clock cannot make a hold look alive (or expired) when it is not.
let clockOffsetMs = 0;

export function serverNow(): number {
  return Date.now() + clockOffsetMs;
}

function updateClockOffset(response: Response) {
  const header = response.headers.get('Date');
  if (!header) return;
  const serverTime = Date.parse(header);
  if (Number.isNaN(serverTime)) return;
  const diff = serverTime - Date.now();
  // The Date header has 1-second precision; only correct clocks that are clearly off.
  clockOffsetMs = Math.abs(diff) > 2000 ? diff : 0;
}

interface RequestOptions {
  studentId?: string | null;
  body?: unknown;
}

export async function request<T>(method: string, path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (options.body !== undefined) headers['Content-Type'] = 'application/json';
  if (options.studentId) headers['X-Student-Id'] = options.studentId;

  let response: Response;
  try {
    response = await fetch(API_BASE_URL + path, {
      method,
      headers,
      body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    });
  } catch {
    // fetch only throws when the server cannot be reached at all.
    throw new ApiError(0, 'NETWORK', friendlyMessage('NETWORK', 0));
  }

  updateClockOffset(response);

  let data: unknown = null;
  try {
    const text = await response.text();
    data = text ? JSON.parse(text) : null;
  } catch {
    data = null;
  }

  if (!response.ok) {
    const code =
      data && typeof data === 'object' && 'error' in data && typeof data.error === 'string'
        ? data.error
        : `HTTP_${response.status}`;
    throw new ApiError(response.status, code, friendlyMessage(code, response.status));
  }

  return data as T;
}

export const api = {
  listClasses: () => request<SchoolClass[]>('GET', '/classes'),
  getClass: (classId: string) => request<SchoolClass>('GET', `/classes/${encodeURIComponent(classId)}`),
  createClass: (name: string, totalSeats: number) =>
    request<CreatedClass>('POST', '/classes', { body: { name, totalSeats } }),

  apply: (classId: string, studentId: string) =>
    request<Hold>('POST', `/classes/${encodeURIComponent(classId)}/holds`, { studentId }),
  getHold: (holdId: string) => request<Hold>('GET', `/holds/${encodeURIComponent(holdId)}`),
  confirm: (holdId: string, studentId: string) =>
    request<ConfirmResult>('POST', `/holds/${encodeURIComponent(holdId)}/confirm`, { studentId }),
  cancel: (holdId: string, studentId: string) =>
    request<void>('DELETE', `/holds/${encodeURIComponent(holdId)}`, { studentId }),
  myHolds: (studentId: string) => request<Hold[]>('GET', '/me/holds', { studentId }),
};
