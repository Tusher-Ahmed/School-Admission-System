import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { json, mockFetch, serverDown } from './test/fakeApi';
import { renderApp } from './test/renderApp';

const STUDENT = '20152691234567890';

describe('student ID box', () => {
  it('accepts Bangla digits, saves the ID and keeps it for the next visit', async () => {
    mockFetch(() => json([]));
    const user = userEvent.setup();
    renderApp('/');

    await user.type(screen.getByLabelText(/জন্ম নিবন্ধন নম্বর/), '২০১৫২৬৯১২৩৪৫৬৭৮৯০');
    await user.click(screen.getByRole('button', { name: 'ব্যবহার করুন' }));

    expect(screen.getByTestId('current-student')).toHaveTextContent(STUDENT);
    expect(window.localStorage.getItem('school-admission.studentId')).toBe(STUDENT);
  });

  it('is not shown on the admin page', () => {
    renderApp('/classes/new', STUDENT);

    expect(screen.getByRole('heading', { name: /নতুন শ্রেণি তৈরি/ })).toBeInTheDocument();
    expect(screen.queryByLabelText(/জন্ম নিবন্ধন নম্বর/)).not.toBeInTheDocument();
    expect(screen.queryByTestId('current-student')).not.toBeInTheDocument();
  });

  it('rejects a number that is not 17 digits', async () => {
    mockFetch(() => json([]));
    const user = userEvent.setup();
    renderApp('/');

    await user.type(screen.getByLabelText(/জন্ম নিবন্ধন নম্বর/), '12345');
    await user.click(screen.getByRole('button', { name: 'ব্যবহার করুন' }));

    expect(screen.getByRole('alert')).toHaveTextContent('১৭ অঙ্কের');
    expect(window.localStorage.getItem('school-admission.studentId')).toBeNull();
  });
});

describe('class list', () => {
  it('shows seat numbers and a readable message when the server is down', async () => {
    serverDown();
    renderApp('/');

    expect(await screen.findByRole('alert')).toHaveTextContent('সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না');
  });

  it('shows seat counts and disables apply when a class is full', async () => {
    mockFetch((url) =>
      url.endsWith('/me/holds')
        ? json([])
        : json([
            { id: 'c1', name: 'Class 6 - Morning Shift', totalSeats: 120, heldSeats: 15, confirmedSeats: 80, availableSeats: 25 },
            { id: 'c2', name: 'Class 6 - Day Shift', totalSeats: 2, heldSeats: 1, confirmedSeats: 1, availableSeats: 0 },
          ]),
    );
    renderApp('/', STUDENT);

    const card = (name: string) => screen.getByText(name).closest<HTMLElement>('[data-testid="class-card"]')!;
    await screen.findByText('Class 6 - Morning Shift');
    const morning = card('Class 6 - Morning Shift');
    expect(within(morning).getByTestId('seats-total')).toHaveTextContent('120');
    expect(within(morning).getByTestId('seats-held')).toHaveTextContent('15');
    expect(within(morning).getByTestId('seats-confirmed')).toHaveTextContent('80');
    expect(within(morning).getByTestId('seats-available')).toHaveTextContent('25');
    expect(within(morning).getByRole('button', { name: 'আবেদন করুন' })).toBeEnabled();
    const day = card('Class 6 - Day Shift');
    expect(within(day).getByText('পূর্ণ')).toBeInTheDocument();
    expect(within(day).getByRole('button', { name: 'আসন নেই' })).toBeDisabled();
  });

  it('blocks other classes while the student has an active application this year', async () => {
    const now = new Date();
    mockFetch((url) =>
      url.endsWith('/me/holds')
        ? json([
            {
              holdId: 'h1',
              classId: 'c1',
              className: 'Class 6 - Morning Shift',
              status: 'HELD',
              expiresAt: new Date(now.getTime() + 300_000).toISOString(),
              createdAt: now.toISOString(),
            },
          ])
        : json([
            { id: 'c1', name: 'Class 6 - Morning Shift', totalSeats: 5, heldSeats: 1, confirmedSeats: 0, availableSeats: 4 },
            { id: 'c2', name: 'Class 6 - Day Shift', totalSeats: 5, heldSeats: 0, confirmedSeats: 0, availableSeats: 5 },
          ]),
    );
    renderApp('/', STUDENT);

    const card = (name: string) => screen.getByText(name).closest<HTMLElement>('[data-testid="class-card"]')!;
    await screen.findByText('Class 6 - Day Shift');
    expect(await within(card('Class 6 - Morning Shift')).findByRole('link', { name: 'আবেদন দেখুন' })).toBeInTheDocument();
    expect(within(card('Class 6 - Day Shift')).getByRole('button', { name: 'অন্য শ্রেণিতে আবেদন আছে' })).toBeDisabled();
    expect(screen.getByText(/চলমান আবেদন আছে/)).toBeInTheDocument();
  });

  it('shows the server reason when applying fails', async () => {
    mockFetch((url, init) => {
      if (init.method === 'POST') return json({ error: 'ALREADY_APPLIED', message: '...' }, 409);
      if (url.endsWith('/me/holds')) return json([]);
      return json([{ id: 'c1', name: 'Class 7', totalSeats: 5, heldSeats: 0, confirmedSeats: 0, availableSeats: 5 }]);
    });
    const user = userEvent.setup();
    renderApp('/', STUDENT);

    await user.click(await screen.findByRole('button', { name: 'আবেদন করুন' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('ইতিমধ্যে একটি চলমান আবেদন');
  });
});

describe('application page', () => {
  const hold = (status: string, expiresInMs: number) => ({
    holdId: 'h1',
    classId: 'c1',
    className: 'Class 6 - Morning Shift',
    status,
    expiresAt: new Date(Date.now() + expiresInMs).toISOString(),
  });

  it('switches to EXPIRED and disables payment when the countdown ends, without a reload', async () => {
    const held = hold('HELD', 1200);
    mockFetch((url) => (url.endsWith('/me/holds') ? json([held]) : json(held)));
    renderApp('/holds/h1', STUDENT);

    expect(await screen.findByTestId('countdown')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ভর্তি ফি দিন' })).toBeEnabled();

    await waitFor(() => expect(screen.getByTestId('hold-status')).toHaveTextContent('EXPIRED'), { timeout: 3000 });
    expect(screen.getByRole('button', { name: 'ভর্তি ফি দিন' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'বাতিল' })).toBeDisabled();
  });

  it('pays the fee and shows CONFIRMED', async () => {
    let status = 'HELD';
    const fetchMock = mockFetch((url) => {
      if (url.endsWith('/confirm')) {
        status = 'CONFIRMED';
        return json({ holdId: 'h1', status: 'CONFIRMED' });
      }
      if (url.endsWith('/me/holds')) return json([hold(status, 600_000)]);
      return json({ ...hold(status, 600_000) });
    });
    const user = userEvent.setup();
    renderApp('/holds/h1', STUDENT);

    await user.click(await screen.findByRole('button', { name: 'ভর্তি ফি দিন' }));

    await waitFor(() => expect(screen.getByTestId('hold-status')).toHaveTextContent('CONFIRMED'));
    expect(screen.getByRole('button', { name: 'ভর্তি ফি দিন' })).toBeDisabled();
    const confirmCall = fetchMock.mock.calls.find(([u]) => String(u).endsWith('/confirm'));
    expect((confirmCall?.[1]?.headers as Record<string, string>)['X-Student-Id']).toBe(STUDENT);
  });

  it('cancels after asking for confirmation', async () => {
    let status = 'HELD';
    mockFetch((url, init) => {
      if (init.method === 'DELETE') {
        status = 'CANCELLED';
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/me/holds')) return json([hold(status, 600_000)]);
      return json(hold(status, 600_000));
    });
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const user = userEvent.setup();
    renderApp('/holds/h1', STUDENT);

    await user.click(await screen.findByRole('button', { name: 'বাতিল' }));

    await waitFor(() => expect(screen.getByTestId('hold-status')).toHaveTextContent('CANCELLED'));
  });
  it('does not show another student’s application', async () => {
    mockFetch((url) => (url.endsWith('/me/holds') ? json([]) : json(hold('HELD', 600_000))));
    renderApp('/holds/h1', STUDENT);

    expect(await screen.findByText(/বর্তমান শিক্ষার্থীর নয়/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ভর্তি ফি দিন' })).not.toBeInTheDocument();
    expect(screen.queryByText('Class 6 - Morning Shift')).not.toBeInTheDocument();
  });

  it('hides the application right after switching to another student', async () => {
    const OTHER = '20150000000099999';
    mockFetch((url, init) => {
      if (!url.endsWith('/me/holds')) return json(hold('HELD', 600_000));
      const sid = (init.headers as Record<string, string>)['X-Student-Id'];
      return json(sid === STUDENT ? [hold('HELD', 600_000)] : []);
    });
    const user = userEvent.setup();
    renderApp('/holds/h1', STUDENT);
    expect(await screen.findByRole('button', { name: 'ভর্তি ফি দিন' })).toBeEnabled();

    await user.click(screen.getByRole('button', { name: 'পরিবর্তন করুন' }));
    await user.type(screen.getByLabelText(/জন্ম নিবন্ধন নম্বর/), OTHER);
    await user.click(screen.getByRole('button', { name: 'ব্যবহার করুন' }));

    expect(await screen.findByText(/বর্তমান শিক্ষার্থীর নয়/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ভর্তি ফি দিন' })).not.toBeInTheDocument();
  });
});

describe('my applications', () => {
  it('loads the saved student’s applications on open (as after a refresh)', async () => {
    const fetchMock = mockFetch(() =>
      json([
        { holdId: 'h1', classId: 'c1', className: 'Class 6 - Morning Shift', status: 'HELD', expiresAt: new Date(Date.now() + 300_000).toISOString() },
        { holdId: 'h2', classId: 'c2', className: 'Class 7 - Day Shift', status: 'CONFIRMED', expiresAt: new Date().toISOString() },
      ]),
    );
    renderApp('/me', STUDENT);

    expect(await screen.findByText('Class 6 - Morning Shift')).toBeInTheDocument();
    expect(screen.getByText('Class 7 - Day Shift')).toBeInTheDocument();
    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/me\/holds$/);
    expect((init?.headers as Record<string, string>)['X-Student-Id']).toBe(STUDENT);
  });
});
