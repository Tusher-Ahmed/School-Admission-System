import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { api, toUserMessage } from '../api/client';
import type { CreatedClass } from '../api/types';
import { Alert } from '../components/common';

const MAX_SEATS = 100000;

export function CreateClassPage() {
  const [name, setName] = useState('');
  const [seats, setSeats] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [created, setCreated] = useState<CreatedClass | null>(null);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (busy) return;

    const trimmed = name.trim();
    const seatCount = Number(seats);
    if (!trimmed) return setError('শ্রেণির নাম দিন, যেমন "Class 6 - Morning Shift"।');
    if (trimmed.length > 200) return setError('শ্রেণির নাম সর্বোচ্চ ২০০ অক্ষরের হতে পারে।');
    if (!Number.isInteger(seatCount) || seatCount < 1 || seatCount > MAX_SEATS)
      return setError(`আসন সংখ্যা ১ থেকে ${MAX_SEATS} এর মধ্যে একটি পূর্ণ সংখ্যা হতে হবে।`);

    setBusy(true);
    setError(null);
    setCreated(null);
    try {
      const result = await api.createClass(trimmed, seatCount);
      setCreated(result);
      setName('');
      setSeats('');
    } catch (err) {
      setError(toUserMessage(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="page">
      <h1>নতুন শ্রেণি তৈরি (স্কুল এডমিন)</h1>
      <p className="muted">প্রতিটি shift আলাদা শ্রেণি, যেমন "Class 6 - Morning Shift" আর "Class 6 - Day Shift"।</p>

      {created && (
        <Alert kind="success">
          "{created.name}" তৈরি হয়েছে ({created.totalSeats} আসন)। <Link to="/">শ্রেণি তালিকায় দেখুন</Link>
        </Alert>
      )}
      {error && <Alert kind="error">{error}</Alert>}

      <form className="form" onSubmit={submit} noValidate>
        <label htmlFor="class-name">শ্রেণির নাম</label>
        <input
          id="class-name"
          value={name}
          maxLength={200}
          placeholder="Class 6 - Morning Shift"
          onChange={(e) => setName(e.target.value)}
        />

        <label htmlFor="class-seats">আসন সংখ্যা</label>
        <input
          id="class-seats"
          type="number"
          min={1}
          max={MAX_SEATS}
          step={1}
          inputMode="numeric"
          placeholder="120"
          value={seats}
          onChange={(e) => setSeats(e.target.value)}
        />

        <button type="submit" disabled={busy}>
          {busy ? 'তৈরি হচ্ছে…' : 'শ্রেণি তৈরি করুন'}
        </button>
      </form>
    </section>
  );
}
