import { describe, expect, it } from 'vitest';
import { isValidStudentId, normalizeStudentId } from './studentId';

describe('normalizeStudentId', () => {
  it('converts Bangla digits to ASCII digits', () => {
    expect(normalizeStudentId('২০১৫২৬৯১২৩৪৫৬৭৮৯০')).toBe('20152691234567890');
  });

  it('drops spaces and dashes so the same child always gets the same ID', () => {
    expect(normalizeStudentId(' 2015 2691-2345 67890 ')).toBe('20152691234567890');
  });

  it('keeps leading zeros', () => {
    expect(normalizeStudentId('00000000000000001')).toBe('00000000000000001');
  });
});

describe('isValidStudentId', () => {
  it.each(['20152691234567890', '00000000000000001'])('accepts %s', (id) => {
    expect(isValidStudentId(id)).toBe(true);
  });

  it.each(['', '2015269123456789', '201526912345678901', '2015269123456789A', '২০১৫২৬৯১২৩৪৫৬৭৮৯০'])(
    'rejects "%s"',
    (id) => {
      expect(isValidStudentId(id)).toBe(false);
    },
  );
});
