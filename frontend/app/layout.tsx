import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = { title: 'پارسا آل شریف | پایش قیمت فروشندگان', description: 'پنل مدیریت و پایش قیمت محصولات و فروشندگان اسنپ‌شاپ' };

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return <html lang="fa" dir="rtl"><body>{children}</body></html>;
}
