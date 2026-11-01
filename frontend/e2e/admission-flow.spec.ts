import { expect, test, type Page } from '@playwright/test';
import { HOLD_SECONDS } from '../playwright.config';

// Unique per run, so every run leaves its own clearly named rows in the database.
const stamp = new Date().toISOString().slice(0, 19).replace('T', ' ');
const MORNING = `Class 6 - Morning Shift [E2E ${stamp}]`;
const DAY = `Class 6 - Day Shift [E2E ${stamp}]`;

// Realistic 17-digit birth registration numbers, unique per run: prefix + 13-digit timestamp.
const now = String(Date.now()).padStart(13, '0').slice(-13);
const STUDENT_A = `2015${now}`; // pays the fee
const STUDENT_B = `2016${now}`; // cancels
const STUDENT_C = `2017${now}`; // lets the hold expire
const STUDENT_D = `2018${now}`; // finds the class full

const toBangla = (digits: string) => digits.replace(/\d/g, (d) => '০১২৩৪৫৬৭৮৯'[Number(d)]);

async function useStudent(page: Page, id: string) {
  const change = page.getByRole('button', { name: 'পরিবর্তন করুন' });
  if (await change.isVisible()) await change.click();
  await page.getByLabel('শিক্ষার্থীর জন্ম নিবন্ধন নম্বর (১৭ অঙ্ক)').fill(id);
  await page.getByRole('button', { name: 'ব্যবহার করুন' }).click();
}

// Top navigation bar (pages also have links with the same text further down).
const nav = (page: Page, name: string) => page.getByRole('navigation').getByRole('link', { name, exact: true }).click();

const classRow = (page: Page, name: string) => page.getByRole('row').filter({ hasText: name });

async function expectSeats(page: Page, name: string, total: number, held: number, confirmed: number, available: number) {
  await expect(classRow(page, name).getByRole('cell')).toHaveText([name, `${total}`, `${held}`, `${confirmed}`, `${available}`, /.*/]);
}

async function applyTo(page: Page, name: string): Promise<string> {
  await nav(page, 'শ্রেণি তালিকা');
  await classRow(page, name).getByRole('button', { name: 'আবেদন করুন' }).click();
  await expect(page).toHaveURL(/\/holds\/[0-9a-f-]{36}$/);
  await expect(page.getByTestId('hold-status')).toHaveText('অপেক্ষমাণ (HELD)');
  return page.url();
}

test('full admission flow on real backend and database', async ({ page, browser }) => {
  console.log(`\nRun: ${stamp}\n  ${MORNING}\n  ${DAY}\n  A=${STUDENT_A} B=${STUDENT_B} C=${STUDENT_C} D=${STUDENT_D}\n`);

  await test.step('1. Admin creates two classes (one per shift)', async () => {
    await page.goto('/classes/new');
    for (const [name, seats] of [[MORNING, '2'], [DAY, '1']] as const) {
      await page.getByLabel('শ্রেণির নাম').fill(name);
      await page.getByLabel('আসন সংখ্যা').fill(seats);
      await page.getByRole('button', { name: 'শ্রেণি তৈরি করুন' }).click();
      await expect(page.getByRole('status')).toContainText(`"${name}" তৈরি হয়েছে (${seats} আসন)`);
    }
  });

  await test.step('2. Invalid input shows a readable error', async () => {
    await page.getByLabel('শ্রেণির নাম').fill('Class 9');
    await page.getByLabel('আসন সংখ্যা').fill('0');
    await page.getByRole('button', { name: 'শ্রেণি তৈরি করুন' }).click();
    await expect(page.getByRole('alert')).toContainText('আসন সংখ্যা ১ থেকে');

    await page.getByLabel('শিক্ষার্থীর জন্ম নিবন্ধন নম্বর (১৭ অঙ্ক)').fill('12345');
    await page.getByRole('button', { name: 'ব্যবহার করুন' }).click();
    await expect(page.locator('#student-id-error')).toContainText('১৭ অঙ্কের');
  });

  await test.step('3. Class list shows the new classes with all seats free', async () => {
    await nav(page, 'শ্রেণি তালিকা');
    await expectSeats(page, MORNING, 2, 0, 0, 2);
    await expectSeats(page, DAY, 1, 0, 0, 1);
  });

  let holdA = '';
  await test.step('4. Student A (ID typed in Bangla digits) applies and pays the fee', async () => {
    await useStudent(page, toBangla(STUDENT_A));
    await expect(page.getByTestId('current-student')).toHaveText(STUDENT_A);

    holdA = await applyTo(page, MORNING);
    await expect(page.getByTestId('countdown')).toHaveText(/^0:1\d$/);

    await page.getByRole('button', { name: 'ভর্তি ফি দিন' }).click();
    await expect(page.getByTestId('hold-status')).toHaveText('নিশ্চিত (CONFIRMED)');
    await expect(page.getByRole('status')).toContainText('ভর্তি নিশ্চিত হয়েছে');
    await expect(page.getByRole('button', { name: 'ভর্তি ফি দিন' })).toBeDisabled();
  });

  await test.step('5. Refresh and a new browser session still show A’s application (rule 11)', async () => {
    await page.reload();
    await expect(page.getByTestId('hold-status')).toHaveText('নিশ্চিত (CONFIRMED)');

    // "Close and reopen the browser": a new context that only keeps the saved storage.
    const reopened = await browser.newContext({ storageState: await page.context().storageState() });
    const page2 = await reopened.newPage();
    await page2.goto('/me');
    await expect(page2.getByTestId('current-student')).toHaveText(STUDENT_A);
    await expect(page2.getByRole('row').filter({ hasText: MORNING })).toContainText('CONFIRMED');
    await reopened.close();
  });

  await test.step('6. Student B applies, then cancels; the seat is released', async () => {
    await useStudent(page, STUDENT_B);
    await applyTo(page, MORNING);

    page.once('dialog', (dialog) => dialog.accept());
    await page.getByRole('button', { name: 'বাতিল' }).click();
    await expect(page.getByTestId('hold-status')).toHaveText('বাতিল (CANCELLED)');

    await nav(page, 'শ্রেণি তালিকা');
    await expectSeats(page, MORNING, 2, 0, 1, 1);
  });

  let holdC = '';
  await test.step('7. Student C takes the only Day Shift seat', async () => {
    await useStudent(page, STUDENT_C);
    holdC = await applyTo(page, DAY);
  });

  await test.step('8. Student D sees the Day Shift as full (seat counts refresh by themselves)', async () => {
    await useStudent(page, STUDENT_D);
    await nav(page, 'শ্রেণি তালিকা');
    await expectSeats(page, DAY, 1, 1, 0, 0);
    await expect(classRow(page, DAY).getByRole('button', { name: 'আসন নেই' })).toBeDisabled();
  });

  await test.step(`9. C's hold expires after ${HOLD_SECONDS}s: EXPIRED without reload, pay disabled`, async () => {
    await useStudent(page, STUDENT_C);
    await page.goto(holdC);
    await expect(page.getByRole('button', { name: 'ভর্তি ফি দিন' })).toBeEnabled();

    await expect(page.getByTestId('hold-status')).toHaveText('মেয়াদ শেষ (EXPIRED)', { timeout: (HOLD_SECONDS + 10) * 1000 });
    await expect(page.getByRole('button', { name: 'ভর্তি ফি দিন' })).toBeDisabled();
    await expect(page.getByRole('button', { name: 'বাতিল' })).toBeDisabled();
  });

  await test.step('10. The expired seat is free again for D, who applies successfully', async () => {
    await useStudent(page, STUDENT_D);
    await nav(page, 'শ্রেণি তালিকা');
    await expectSeats(page, DAY, 1, 0, 0, 1); // updates within one 3 s refresh
    await applyTo(page, DAY);
  });

  await test.step('11. My applications lists each student’s own history', async () => {
    await useStudent(page, STUDENT_C);
    await nav(page, 'আমার আবেদন');
    await expect(page.getByRole('row').filter({ hasText: DAY })).toContainText('EXPIRED');
    await expect(page.getByRole('row').filter({ hasText: MORNING })).toHaveCount(0);

    await useStudent(page, STUDENT_A);
    await expect(page.getByRole('row').filter({ hasText: MORNING })).toContainText('CONFIRMED');
    expect(holdA).toContain('/holds/');
  });

  await test.step('12. Final seat counts', async () => {
    await nav(page, 'শ্রেণি তালিকা');
    await expectSeats(page, MORNING, 2, 0, 1, 1); // A confirmed, B cancelled
    await expectSeats(page, DAY, 1, 1, 0, 0); // C expired, D holding
  });
});
