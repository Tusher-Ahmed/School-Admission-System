export type HoldStatus = 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED';

export interface SchoolClass {
  id: string;
  name: string;
  totalSeats: number;
  heldSeats: number;
  confirmedSeats: number;
  availableSeats: number;
}

export interface CreatedClass {
  id: string;
  name: string;
  totalSeats: number;
}

export interface Hold {
  holdId: string;
  classId: string;
  className?: string;
  studentId?: string;
  status: HoldStatus;
  expiresAt: string;
}

export interface ConfirmResult {
  holdId: string;
  status: HoldStatus;
}
