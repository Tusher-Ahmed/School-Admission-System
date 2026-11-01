const BANGLA_ZERO = 0x09e6; // '০'

/**
 * Turns what a parent typed into the canonical ID: Bangla digits become 0-9, and spaces and dashes are dropped,
 * so "২০১৫ ২৬৯১-২৩৪৫৬৭৮৯০" and "20152691234567890" are the same student.
 */
export function normalizeStudentId(raw: string): string {
  return raw
    .replace(/[০-৯]/g, (d) => String(d.charCodeAt(0) - BANGLA_ZERO))
    .replace(/[\s-]/g, '');
}

export function isValidStudentId(id: string): boolean {
  return /^[0-9]{17}$/.test(id);
}
