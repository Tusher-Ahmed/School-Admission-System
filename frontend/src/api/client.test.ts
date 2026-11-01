import { describe, expect, it } from 'vitest';
import { json, mockFetch, serverDown } from '../test/fakeApi';
import { ApiError, api } from './client';

describe('api client', () => {
  it('sends the student ID in the X-Student-Id header', async () => {
    const fetchMock = mockFetch(() => json({ holdId: 'h1', classId: 'c1', status: 'HELD', expiresAt: '' }, 201));

    await api.apply('c1', '20152691234567890');

    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toBe('http://localhost:8080/classes/c1/holds');
    expect(init?.method).toBe('POST');
    expect((init?.headers as Record<string, string>)['X-Student-Id']).toBe('20152691234567890');
  });

  it('turns a server error code into a readable message', async () => {
    mockFetch(() => json({ error: 'CLASS_FULL', message: 'No seats are available in this class.' }, 409));

    const error = await api.apply('c1', '20152691234567890').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(409);
    expect((error as ApiError).code).toBe('CLASS_FULL');
    expect((error as ApiError).message).toContain('আসন খালি নেই');
  });

  it('reports a stopped server as a network error, not a crash', async () => {
    serverDown();

    const error = await api.listClasses().catch((e: unknown) => e);

    expect((error as ApiError).code).toBe('NETWORK');
    expect((error as ApiError).message).toContain('সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না');
  });

  it('asks to retry when the server is busy', async () => {
    mockFetch(() => json({ error: 'SERVER_BUSY', message: 'Too many requests right now.' }, 503));

    const error = await api.apply('c1', '20152691234567890').catch((e: unknown) => e);

    expect((error as ApiError).message).toContain('কয়েক সেকেন্ড পরে আবার চেষ্টা করুন');
  });

  it('handles a non-JSON error body', async () => {
    mockFetch(() => new Response('<html>Bad gateway</html>', { status: 502 }));

    const error = await api.listClasses().catch((e: unknown) => e);

    expect((error as ApiError).status).toBe(502);
    expect((error as ApiError).message).toContain('সার্ভারে একটি সমস্যা হয়েছে');
  });

  it('returns nothing for 204 No Content', async () => {
    mockFetch(() => new Response(null, { status: 204 }));

    await expect(api.cancel('h1', '20152691234567890')).resolves.toBeNull();
  });
});
