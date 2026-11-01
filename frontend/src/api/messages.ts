// User-facing messages for every error code the backend sends, plus network failure.
const MESSAGES: Record<string, string> = {
  NETWORK:
    'সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। সার্ভার চালু আছে কিনা দেখে একটু পরে আবার চেষ্টা করুন।',
  DATABASE_UNAVAILABLE: 'সার্ভারে সাময়িক সমস্যা হচ্ছে (ডাটাবেস)। একটু পরে আবার চেষ্টা করুন।',
  SERVER_ERROR: 'সার্ভারে একটি সমস্যা হয়েছে। একটু পরে আবার চেষ্টা করুন।',
  SERVER_BUSY: 'এই মুহূর্তে অনেক আবেদন আসছে। কয়েক সেকেন্ড পরে আবার চেষ্টা করুন।',
  CONFLICT: 'তথ্য ইতিমধ্যে বদলে গেছে। পেজটি হালনাগাদ হলে আবার চেষ্টা করুন।',
  INVALID_INPUT: 'দেওয়া তথ্যে এমন অক্ষর বা মান আছে যা সংরক্ষণ করা যায় না।',
  PAYLOAD_TOO_LARGE: 'পাঠানো তথ্য অনেক বড়। তথ্য ছোট করে আবার চেষ্টা করুন।',

  CLASS_FULL: 'দুঃখিত, এই শ্রেণিতে আর কোনো আসন খালি নেই।',
  ALREADY_APPLIED: 'এই শিক্ষার্থীর এই শ্রেণিতে ইতিমধ্যে একটি চলমান আবেদন বা নিশ্চিত ভর্তি আছে।',
  ALREADY_APPLIED_THIS_YEAR:
    'এই শিক্ষার্থীর এই বছরে অন্য একটি শ্রেণিতে চলমান আবেদন বা নিশ্চিত ভর্তি আছে। নতুন আবেদনের আগে সেটি বাতিল করুন বা মেয়াদ শেষ হতে দিন।',
  HOLD_EXPIRED: 'আবেদনের মেয়াদ শেষ হয়ে গেছে। আসন পেতে আবার আবেদন করুন।',
  HOLD_CANCELLED: 'এই আবেদনটি বাতিল করা হয়েছে।',
  ALREADY_CONFIRMED: 'ভর্তি ইতিমধ্যে নিশ্চিত হয়েছে, তাই এটি বাতিল করা যাবে না।',
  NOT_YOUR_APPLICATION:
    'এই আবেদনটি অন্য শিক্ষার্থীর। উপরের জন্ম নিবন্ধন নম্বরটি ঠিক আছে কিনা দেখুন।',

  CLASS_NOT_FOUND: 'এই শ্রেণিটি পাওয়া যায়নি।',
  HOLD_NOT_FOUND: 'এই আবেদনটি পাওয়া যায়নি।',
  NOT_FOUND: 'যা খুঁজছেন তা পাওয়া যায়নি।',

  MISSING_STUDENT_ID: 'আগে উপরে শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।',
  INVALID_STUDENT_ID: 'জন্ম নিবন্ধন নম্বর ঠিক ১৭ অঙ্কের হতে হবে।',
  INVALID_NAME: 'শ্রেণির নাম সঠিক নয়: নাম দিতে হবে, সর্বোচ্চ ২০০ অক্ষর, কোনো লাইন ব্রেক বা বিশেষ নিয়ন্ত্রণ-অক্ষর চলবে না।',
  INVALID_TOTAL_SEATS: 'আসন সংখ্যা ১ থেকে ১০০০০০ এর মধ্যে একটি পূর্ণ সংখ্যা হতে হবে।',
  INVALID_BODY: 'তথ্যগুলো সঠিকভাবে পূরণ করুন।',
  BAD_REQUEST: 'অনুরোধটি সঠিক নয়। তথ্যগুলো আবার দেখুন।',
};

export function friendlyMessage(code: string, status: number): string {
  if (MESSAGES[code]) return MESSAGES[code];
  if (status >= 500) return MESSAGES.SERVER_ERROR;
  if (status === 404) return MESSAGES.NOT_FOUND;
  return MESSAGES.BAD_REQUEST;
}
