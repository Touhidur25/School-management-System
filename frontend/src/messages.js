// Every error the user can see is shown in plain Bengali, chosen by the backend's error code.
const MESSAGES = {
  network_error: "সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। ইন্টারনেট সংযোগ দেখে আবার চেষ্টা করুন।",
  no_seats: "দুঃখিত, এই ক্লাসে আর কোনো আসন খালি নেই।",
  already_applied: "এই ক্লাসে আপনার আবেদন আগে থেকেই আছে (চলমান বা নিশ্চিত)।",
  hold_expired: "আবেদনের মেয়াদ শেষ হয়ে গেছে এবং আসনটি ছেড়ে দেওয়া হয়েছে। চাইলে আবার আবেদন করুন।",
  hold_cancelled: "এই আবেদন আগেই বাতিল করা হয়েছে।",
  already_confirmed: "ভর্তি আগেই নিশ্চিত হয়ে গেছে, তাই এটি বাতিল করা যাবে না।",
  not_your_hold: "এই আবেদনটি অন্য শিক্ষার্থীর। আপনার জন্ম নিবন্ধন নম্বর ঠিক আছে কিনা দেখুন।",
  missing_student_id: "আগে উপরের ঘরে শিক্ষার্থীর 17 অঙ্কের জন্ম নিবন্ধন নম্বর দিন।",
  invalid_student_id: "জন্ম নিবন্ধন নম্বর ঠিক 17 অঙ্কের হতে হবে।",
  invalid_name: "ক্লাসের নাম লিখুন (সর্বোচ্চ 200 অক্ষর)।",
  invalid_seats: "আসন সংখ্যা 1 থেকে 100000 এর মধ্যে একটি পূর্ণসংখ্যা হতে হবে।",
  invalid_request: "পাঠানো তথ্য সঠিক নয়। ঘরগুলো আবার দেখে নিন।",
  class_not_found: "ক্লাসটি খুঁজে পাওয়া যায়নি।",
  hold_not_found: "আবেদনটি খুঁজে পাওয়া যায়নি।",
  notification_not_found: "বিজ্ঞপ্তিটি খুঁজে পাওয়া যায়নি।",
  not_your_notification: "এই বিজ্ঞপ্তিটি অন্য শিক্ষার্থীর।",
  server_error: "সার্ভারে একটি সমস্যা হয়েছে। একটু পরে আবার চেষ্টা করুন।",
};

export function errorMessage(error) {
  return MESSAGES[error?.code] ?? "কিছু একটা সমস্যা হয়েছে। একটু পরে আবার চেষ্টা করুন।";
}

/** Text of a notification, in Bengali. */
export function notificationText(notification) {
  const where = notification.className;
  if (notification.previousStatus === "CONFIRMED") {
    return `স্কুল কর্তৃপক্ষ আপনার ভর্তি বাতিল করেছে (${where})।`;
  }
  return `স্কুল কর্তৃপক্ষ আপনার আবেদন বাতিল করেছে (${where})। আসনটি ছেড়ে দেওয়া হয়েছে।`;
}
