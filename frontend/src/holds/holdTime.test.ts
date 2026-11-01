import { describe, expect, it } from 'vitest';
import { effectiveStatus, formatRemaining, remainingMs } from './holdTime';

const expiresAt = '2026-11-01T04:10:00Z';
const expiry = Date.parse(expiresAt);

describe('holdTime', () => {
  it('counts down to zero and never below', () => {
    expect(remainingMs({ expiresAt }, expiry - 65_000)).toBe(65_000);
    expect(remainingMs({ expiresAt }, expiry + 5_000)).toBe(0);
  });

  it('shows a HELD hold as EXPIRED as soon as its time is up', () => {
    expect(effectiveStatus({ status: 'HELD', expiresAt }, expiry - 1)).toBe('HELD');
    expect(effectiveStatus({ status: 'HELD', expiresAt }, expiry)).toBe('EXPIRED');
  });

  it('never changes a CONFIRMED or CANCELLED hold', () => {
    expect(effectiveStatus({ status: 'CONFIRMED', expiresAt }, expiry + 60_000)).toBe('CONFIRMED');
    expect(effectiveStatus({ status: 'CANCELLED', expiresAt }, expiry + 60_000)).toBe('CANCELLED');
  });

  it.each([
    [600_000, '10:00'],
    [65_000, '1:05'],
    [500, '0:01'],
    [0, '0:00'],
    [3_723_000, '1:02:03'],
  ])('formats %i ms as %s', (ms, text) => {
    expect(formatRemaining(ms)).toBe(text);
  });
});
