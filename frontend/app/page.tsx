'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Activity, ArrowDownLeft, ArrowDownToLine, ArrowLeft, ArrowUpLeft, Bell, Check, CheckCheck, ChevronLeft, ChevronRight, Clock3, ExternalLink, FileSpreadsheet, LayoutDashboard, Link2, LoaderCircle, LogOut, Package, Pause, Play, Plus, RefreshCw, Search, Settings2, ShieldCheck, SlidersHorizontal, Store, Trash2, Undo2, TrendingDown, TrendingUp, X, CircleHelp, ChartNoAxesCombined, WifiOff } from 'lucide-react';
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { OwnStoreSettings, PricePositionPage } from './components/price-position';

type Brand = { id: string; name: string; english: string; total: number; tracked: number; changes: number; discovery_error: string | null; last_discovered: string | null; job: Job | null };
type Job = { id: string; status: string; message: string | null; created_at: string; brand_id: string };
type Product = { id: number; brand_id: string; source_id: string; title: string; url: string; image: string | null; tracked: boolean; deleted_at?: string | null; min_price: number | null; seller_count: number | null; last_success: string | null; next_check: string | null; error: string | null; offers?: Offer[] };
type Offer = { id: number; seller_id: string; seller_name: string; variant: string; price: number | null; list_price: number | null; available: boolean | null; observed_at: string; missing?: boolean };
type Notice = { id: number; kind: string; brand_id: string | null; product_id: number | null; title: string; old_price: number | null; new_price: number | null; percent: number | null; created_at: string; previous_observed_at: string | null; read: boolean; alerts: boolean };
type Config = { interval_minutes: number; threshold_percent: number; retention_days: number; discovery_hours: number };
type Dashboard = { products: number; tracked: number; sellers: number; changes_today: number; unread: number; errors: number; last_success: string | null; heartbeat: string | null; settings: Config; mode: string };
type User = { username: string; role: string; mode: string };
type Timeline = { time: string; price: number }[];
type Preview = { product_id: number; offers: Offer[]; observed_at: string; seller_count: number; availability: string };
type ManualResult = Product & { already_exists: boolean; brand_changed: boolean; message: string; brand_warning?: string | null };

const number = (value: number | null | undefined) => value == null ? '—' : new Intl.NumberFormat('fa-IR', { maximumFractionDigits: 1 }).format(value);
const date = (value: string | null | undefined, timeOnly = false) => value ? new Intl.DateTimeFormat('fa-IR', { timeZone: 'Asia/Tehran', ...(timeOnly ? {} : { month: 'short', day: 'numeric' }), hour: '2-digit', minute: '2-digit' }).format(new Date(value)) : 'هنوز بررسی نشده';
const stale = (value: string | null, minutes: number) => !value || Date.now() - new Date(value).getTime() > minutes * 60000 * 1.5;
const brandColor: Record<string, string> = { vitalayer: '#678b7c', eudoramax: '#b69c75', elvina: '#9c85b2', isol: '#719cad', vitaplex: '#ba8d77', belfamed: '#85a59a', bergamia: '#b58ca1' };

async function api<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch('/api' + path, { ...options, headers: { 'Content-Type': 'application/json', ...options?.headers } });
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    if (response.status === 401) window.dispatchEvent(new Event('session-expired'));
    throw new Error(typeof body.detail === 'string' ? body.detail : 'دریافت اطلاعات ناموفق بود. دوباره تلاش کنید.');
  }
  return response.json();
}
const post = <T,>(path: string, body = {}) => api<T>(path, { method: 'POST', body: JSON.stringify(body) });

function ProductArt({ brand, index = 0, small = false }: { brand: string; index?: number; small?: boolean }) {
  const color = brandColor[brand] || '#719c85';
  return <div className={'product-art ' + (small ? 'small' : '')} style={{ '--product-color': color } as React.CSSProperties} aria-hidden="true">
    <svg viewBox="0 0 220 160" fill="none"><ellipse cx="110" cy="143" rx="42" ry="7" fill="black" opacity=".055" />
      {index % 3 === 0 ? <><rect x="91" y="15" width="38" height="20" rx="6" fill="#e3e7e0"/><rect x="98" y="5" width="24" height="19" rx="8" fill="#f6f8f3"/><rect x="86" y="32" width="48" height="109" rx="11" fill="#fcfdf8" stroke="#d9e0d7"/><path d="M86 69H134V126C134 134 128 141 121 141H99C92 141 86 134 86 126V69Z" fill={color} opacity=".9"/><rect x="92" y="74" width="36" height="50" rx="2" fill="#fff" opacity=".92"/></> : index % 3 === 1 ? <><path d="M79 24H140L132 124H87L79 24Z" fill="#fbfcf5" stroke="#d6ddd4"/><path d="M82 58H137L134 108H85L82 58Z" fill={color} opacity=".85"/><rect x="87" y="124" width="45" height="18" rx="4" fill={color}/><path d="M81 28H138" stroke="#d7ded5"/></> : <><rect x="78" y="23" width="65" height="20" rx="6" fill="#e9ebe6"/><rect x="76" y="42" width="69" height="99" rx="14" fill="#fafcf6" stroke="#d4ded3"/><rect x="77" y="70" width="67" height="49" fill={color} opacity=".86"/><rect x="85" y="75" width="51" height="38" rx="2" fill="white" opacity=".9"/></>}
      <text x="110" y={index % 3 === 1 ? 77 : 89} textAnchor="middle" fill={index % 3 === 1 ? 'white' : color} fontSize="7" fontFamily="Arial" letterSpacing="1">{brand.toUpperCase().slice(0, 10)}</text>
      <path d="M101 100H119M104 105H116M106 110H114" stroke={index % 3 === 1 ? '#fff' : color} strokeWidth="1.5" opacity=".6"/>
    </svg><span className="illustration-label">تصویر شماتیک</span>
  </div>;
}

function Empty({ title = 'هنوز اطلاعاتی ثبت نشده', text = 'برای شروع، محصولات یک برند را جست‌وجو و برای پایش انتخاب کنید.', children }: { title?: string; text?: string; children?: React.ReactNode }) {
  return <div className="empty"><div className="empty-icon"><Package size={28}/></div><h3>{title}</h3><p>{text}</p>{children}</div>;
}

function PriceChart({ data, loading = false }: { data: Timeline; loading?: boolean }) {
  if (loading) return <div className="chart-placeholder"><LoaderCircle className="spin"/></div>;
  if (data.length < 2) return <Empty title="نمودار در حال شکل‌گیری است" text="پس از حداقل دو مشاهده، روند قیمت این محصول نمایش داده می‌شود."/>;
  return <div className="chart" dir="ltr"><ResponsiveContainer width="100%" height="100%"><AreaChart data={data} margin={{ left: 6, right: 15, top: 20, bottom: 0 }}>
    <defs><linearGradient id="priceFill" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="#2f7d65" stopOpacity={.19}/><stop offset="100%" stopColor="#2f7d65" stopOpacity={0}/></linearGradient></defs>
    <CartesianGrid strokeDasharray="3 6" vertical={false} stroke="#e9ece7"/>
    <XAxis dataKey="time" tickFormatter={v => date(v, true)} tickLine={false} axisLine={false} minTickGap={30} tick={{ fill: '#87918b', fontSize: 11 }}/>
    <YAxis domain={['auto', 'auto']} tickFormatter={v => number(v / 1000) + 'k'} tickLine={false} axisLine={false} width={58} tick={{ fill: '#87918b', fontSize: 11 }}/>
    <Tooltip labelFormatter={v => date(String(v))} formatter={v => [number(Number(v)) + ' تومان', 'کمترین قیمت مشاهده‌شده']} contentStyle={{ direction: 'rtl', borderRadius: 12, border: '1px solid #e6eae3', fontFamily: 'Vazirmatn, Tahoma', fontSize: 12 }}/>
    <Area type="monotone" dataKey="price" stroke="#357b63" fill="url(#priceFill)" strokeWidth={2.5} dot={false} activeDot={{ r: 5, stroke: '#fff', strokeWidth: 3 }}/>
  </AreaChart></ResponsiveContainer></div>;
}

function ChangeIcon({ kind }: { kind: string }) {
  return <span className={'change-icon ' + (kind === 'price_up' ? 'rose' : kind === 'price_down' ? 'green' : kind === 'error' ? 'amber' : 'neutral')}>{kind === 'price_up' ? <TrendingUp size={17}/> : kind === 'price_down' ? <TrendingDown size={17}/> : kind === 'error' ? <WifiOff size={17}/> : kind === 'seller_added' ? <Store size={17}/> : <Package size={17}/>}</span>;
}

function Login({ onLogin }: { onLogin: () => void }) {
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  return <main className="login-page"><div className="login-story"><div className="logo-mark"><ChartNoAxesCombined size={30}/></div><h1>قیمت‌ها تغییر می‌کنند.<br/><span>شما باخبر می‌مانید.</span></h1><p>پارسا آل شریف، فضای یکپارچهٔ پایش قیمت محصولات<br/>و فروشندگان شما در اسنپ‌شاپ.</p><div className="login-decoration"><div/><div/><div/><div/><div/><div/><div/></div><small>پایش دقیق · تصمیم آگاهانه</small></div><form className="login-form" onSubmit={async e => { e.preventDefault(); setBusy(true); setError(''); const f = new FormData(e.currentTarget); try { await post('/auth/login', { username: f.get('username'), password: f.get('password') }); onLogin(); } catch (e) { setError((e as Error).message); } finally { setBusy(false); } }}>
    <span className="eyebrow">خوش آمدید به پارسا آل شریف</span><h2>ورود به پنل مدیریت</h2><p>برای مشاهدهٔ وضعیت بازار وارد حساب خود شوید.</p>
    <label>نام کاربری<input name="username" autoComplete="username" dir="ltr" required placeholder="admin"/></label><label>رمز عبور<input name="password" type="password" autoComplete="current-password" dir="ltr" required placeholder="••••••••••"/></label>
    {error && <div className="error-box" role="alert">{error}</div>}<button className="button primary" disabled={busy}>{busy ? <LoaderCircle className="spin" size={17}/> : <ArrowLeft size={17}/>}ورود به پنل</button><small><ShieldCheck size={15}/> ورود امن ویژهٔ اعضای مجموعه</small>
  </form></main>;
}

export default function Panel() {
  const [user, setUser] = useState<User | null>(null);
  const [booting, setBooting] = useState(true);
  const [view, setView] = useState('dashboard');
  const [brands, setBrands] = useState<Brand[]>([]);
  const [dash, setDash] = useState<Dashboard | null>(null);
  const [products, setProducts] = useState<Product[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('all');
  const [notices, setNotices] = useState<Notice[]>([]);
  const [noticeTotal, setNoticeTotal] = useState(0);
  const [noticeKind, setNoticeKind] = useState('');
  const [noticeUnread, setNoticeUnread] = useState(false);
  const [noticeBrand, setNoticeBrand] = useState('');
  const [selected, setSelected] = useState<number[]>([]);
  const [detailId, setDetailId] = useState<number | null>(null);
  const [detail, setDetail] = useState<Product | null>(null);
  const [sellerFilter, setSellerFilter] = useState('');
  const [detailTab, setDetailTab] = useState('sellers');
  const [historyPage, setHistoryPage] = useState(1);
  const [historyRows, setHistoryRows] = useState<Offer[]>([]);
  const [historyTotal, setHistoryTotal] = useState(0);
  const [detailChart, setDetailChart] = useState<Timeline>([]);
  const [chartId, setChartId] = useState('');
  const [chartData, setChartData] = useState<Timeline>([]);
  const [chartBusy, setChartBusy] = useState(false);
  const [config, setConfig] = useState<Config>({ interval_minutes: 60, threshold_percent: 0, retention_days: 30, discovery_hours: 24 });
  const [toast, setToast] = useState<{ message: string; error: boolean } | null>(null);
  const [offline, setOffline] = useState(false);
  const [busy, setBusy] = useState(false);
  const [showAdd, setShowAdd] = useState(false);
  const [reportBrand, setReportBrand] = useState('');
  const [reportProduct, setReportProduct] = useState('');
  const [reportProducts, setReportProducts] = useState<Product[]>([]);
  const [reportSeller, setReportSeller] = useState('');
  const [sellers, setSellers] = useState<{ id: string; name: string }[]>([]);
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [includeHistory, setIncludeHistory] = useState(true);
  const [clock, setClock] = useState('');
  const [detailPreview, setDetailPreview] = useState<Preview | null>(null);
  const [previewBusy, setPreviewBusy] = useState(false);
  const [previewError, setPreviewError] = useState('');
  const previewRequested = useRef<number | null>(null);
  const [manualBrand, setManualBrand] = useState('');
  const [manualResult, setManualResult] = useState<ManualResult | null>(null);
  const productRequest = useRef(0);
  const detailRequest = useRef(0);
  const isAdmin = user?.role === 'admin';
  const currentBrand = brands.find(b => b.id === view);
  const isBrand = !!currentBrand;
  const inform = (message: string, error = false) => setToast({ message, error });

  const boot = useCallback(async () => { try { setUser(await api<User>('/me')); } catch { setUser(null); } finally { setBooting(false); } }, []);
  useEffect(() => { boot(); const expired = () => setUser(null); window.addEventListener('session-expired', expired); return () => window.removeEventListener('session-expired', expired); }, [boot]);
  useEffect(() => { const change = () => { productRequest.current++; setProducts([]); setTotal(0); setView(window.location.hash.slice(1) || 'dashboard'); setPage(1); setSelected([]); setSearch(''); setFilter('all'); }; change(); window.addEventListener('hashchange', change); return () => window.removeEventListener('hashchange', change); }, []);
  useEffect(() => { const update = () => setClock(new Intl.DateTimeFormat('fa-IR', { timeZone: 'Asia/Tehran', weekday: 'long', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' }).format(new Date())); update(); const t = setInterval(update, 60000); return () => clearInterval(t); }, []);
  useEffect(() => { if (toast) { const t = setTimeout(() => setToast(null), 6000); return () => clearTimeout(t); } }, [toast]);

  const reload = useCallback(async () => {
    if (!user) return;
    try {
      const [d, b] = await Promise.all([api<Dashboard>('/dashboard'), api<Brand[]>('/brands')]);
      setDash(d); setBrands(b); setOffline(false);
    } catch { setOffline(true); }
  }, [user]);
  useEffect(() => { reload(); const t = setInterval(reload, 15000); return () => clearInterval(t); }, [reload]);

  const loadProducts = useCallback(async () => {
    if (!user) return;
    const requestId = ++productRequest.current;
    const qs = new URLSearchParams({ page: String(page), page_size: '12' });
    if (!['dashboard', 'products'].includes(view)) {
      if (!brands.some(b => b.id === view)) return;
      qs.set('brand_id', view);
    }
    if (search) qs.set('q', search);
    if (filter === 'deleted') qs.set('deleted', 'true');
    else if (filter !== 'all') qs.set('tracked', filter === 'tracked' ? 'true' : 'false');
    try { const p = await api<{ items: Product[]; total: number }>('/products?' + qs); if (requestId === productRequest.current) { setProducts(p.items); setTotal(p.total); } } catch (e) { if (requestId === productRequest.current) inform((e as Error).message, true); }
  }, [user, page, view, search, filter, brands.map(b => b.id).join(',')]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { const t = setTimeout(loadProducts, 250); return () => { clearTimeout(t); productRequest.current++; }; }, [loadProducts]);
  useEffect(() => { if (!user) return; const t = setInterval(loadProducts, 15000); return () => clearInterval(t); }, [loadProducts, user]);

  const loadNotices = useCallback(async () => {
    if (!user) return;
    try {
      const params = new URLSearchParams({ page: String(view === 'notifications' ? page : 1), page_size: String(view === 'notifications' ? 15 : 5) });
      if (view === 'notifications') { if (noticeKind) params.set('kind', noticeKind); if (noticeUnread) params.set('unread', 'true'); if (noticeBrand) params.set('brand_id', noticeBrand); }
      else { params.set('alerts_only', 'true'); }
      const data = await api<{ items: Notice[]; total: number }>('/notifications?' + params); setNotices(data.items); setNoticeTotal(data.total);
    } catch { /* global connection banner is owned by dashboard refresh */ }
  }, [user, view, page, noticeKind, noticeUnread, noticeBrand]);
  useEffect(() => { loadNotices(); const t = setInterval(loadNotices, 15000); return () => clearInterval(t); }, [loadNotices]);

  useEffect(() => { if (!products.length || view !== 'dashboard') return; if (!products.some(p => String(p.id) === chartId)) setChartId(String(products.find(p => p.tracked)?.id || products[0].id)); }, [products, view, chartId]);
  useEffect(() => { let cancelled = false; if (!chartId || view !== 'dashboard') return; setChartBusy(true); api<Timeline>(`/products/${chartId}/timeline`).then(d => { if (!cancelled) setChartData(d); }).catch(() => { if (!cancelled) setChartData([]); }).finally(() => { if (!cancelled) setChartBusy(false); }); return () => { cancelled = true; }; }, [chartId, view, dash?.last_success]);
  useEffect(() => { if (view === 'settings' && user) api<Config>('/settings').then(setConfig).catch(e => inform(e.message, true)); }, [view, user]);
  useEffect(() => { if (view === 'reports' && user) api<{ id: string; name: string }[]>('/sellers').then(setSellers).catch(e => inform(e.message, true)); }, [view, user]);
  useEffect(() => { if (view !== 'reports' || !user) return; let active = true; setReportProduct(''); setReportProducts([]); (async () => { try { const all: Product[] = []; let page = 1; let count = 0; do { const result = await api<{ items: Product[]; total: number }>(`/products?page_size=100&page=${page}` + (reportBrand ? '&brand_id=' + reportBrand : '')); all.push(...result.items); count = result.total; page++; } while (all.length < count && active); if (active) setReportProducts(all); } catch (e) { if (active) inform((e as Error).message, true); } })(); return () => { active = false; }; }, [reportBrand, view, user]);

  const loadDetail = useCallback(async () => {
    if (!detailId) return;
    const requestId = ++detailRequest.current;
    try {
      const qs = sellerFilter ? '?seller_id=' + encodeURIComponent(sellerFilter) : '';
      const [d, chart, history] = await Promise.all([api<Product>(`/products/${detailId}`), api<Timeline>(`/products/${detailId}/timeline${qs}`), api<{ items: Offer[]; total: number }>(`/products/${detailId}/history?${new URLSearchParams({ page: String(historyPage), page_size: '20', ...(sellerFilter ? { seller_id: sellerFilter } : {}) })}`)]);
      if (requestId === detailRequest.current) { setDetail(d); setDetailChart(chart); setHistoryRows(history.items); setHistoryTotal(history.total); }
    } catch (e) { inform((e as Error).message, true); }
  }, [detailId, sellerFilter, historyPage]);
  useEffect(() => { loadDetail(); const t = setInterval(loadDetail, 15000); return () => { clearInterval(t); detailRequest.current++; }; }, [loadDetail]);
  async function loadPreview(id: number) {
    previewRequested.current = id; setPreviewBusy(true); setPreviewError('');
    try { const preview = await api<Preview>(`/products/${id}/preview`); if (previewRequested.current === id) setDetailPreview(preview); }
    catch (e) { if (previewRequested.current === id) setPreviewError((e as Error).message); }
    finally { if (previewRequested.current === id) setPreviewBusy(false); }
  }
  useEffect(() => { if (detail && detail.id === detailId && previewRequested.current !== detailId && (!detail.tracked || !detail.last_success || !detail.offers?.length)) loadPreview(detail.id); }, [detailId, detail?.id]); // Preview once on opening; periodic refresh reads only persisted history.
  useEffect(() => { if (detailPreview && detail?.id === detailPreview.product_id && detail.last_success && detail.last_success >= detailPreview.observed_at) setDetailPreview(null); }, [detail?.last_success, detailPreview]);
  useEffect(() => { if (!detailId) return; const key = (e: KeyboardEvent) => { if (e.key === 'Escape') setDetailId(null); }; document.body.style.overflow = 'hidden'; window.addEventListener('keydown', key); return () => { document.body.style.overflow = ''; window.removeEventListener('keydown', key); }; }, [detailId]);

  async function action(work: () => Promise<unknown>, message: string) { setBusy(true); try { await work(); inform(message); await Promise.all([reload(), loadProducts(), loadNotices(), loadDetail()]); } catch (e) { inform((e as Error).message, true); } finally { setBusy(false); } }
  const navigate = (target: string) => { window.location.hash = target; };
  function openProduct(id: number) { previewRequested.current = null; setDetailPreview(null); setPreviewError(''); setPreviewBusy(false); setDetail(null); setDetailId(id); setSellerFilter(''); setDetailTab('sellers'); setHistoryPage(1); }
  const track = (ids: number[], tracked: boolean) => action(async () => { await post('/products/tracking', { ids, tracked }); setSelected([]); }, tracked ? 'پایش محصولات فعال شد؛ اولین بررسی در صف قرار گرفت.' : 'پایش متوقف شد؛ تاریخچه محفوظ است.');
  const deleteProducts = (ids: number[]) => action(async () => { await post('/products/delete', { ids }); setSelected([]); if (detailId && ids.includes(detailId)) setDetailId(null); setPage(1); }, 'محصول حذف شد؛ از تب «حذف‌شده‌ها» قابل بازگردانی است و با جست‌وجوی مجدد برنمی‌گردد.');
  const restoreProducts = (ids: number[]) => action(async () => { await post('/products/restore', { ids }); setSelected([]); setPage(1); }, 'محصول بازگردانده شد؛ برای ادامه، پایش آن را فعال کنید.');

  async function downloadReport(productId?: number) {
    setBusy(true);
    try {
      const params = new URLSearchParams({ include_history: String(includeHistory) });
      if (reportBrand && !productId) params.set('brand_id', reportBrand);
      if (productId || reportProduct) params.set('product_id', String(productId || reportProduct));
      if (reportSeller && !productId) params.set('seller_id', reportSeller);
      if (startDate && !productId) params.set('start', new Date(startDate + 'T00:00:00+03:30').toISOString());
      if (endDate && !productId) { const d = new Date(endDate + 'T00:00:00+03:30'); d.setTime(d.getTime() + 86400000); params.set('end', d.toISOString()); }
      const response = await fetch('/api/reports/excel?' + params);
      if (!response.ok) { const d = await response.json(); throw new Error(d.detail || 'تهیهٔ گزارش ناموفق بود.'); }
      const url = URL.createObjectURL(await response.blob()); const a = document.createElement('a'); a.href = url; a.download = `didban-${user?.mode}-${new Date().toISOString().slice(0, 10)}.xlsx`; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000); inform('فایل اکسل آماده و دانلود شد.');
    } catch (e) { inform((e as Error).message, true); } finally { setBusy(false); }
  }

  const pagination = (count: number, size = 12, customPage?: number, setter?: (n: number) => void) => {
    const active = customPage ?? page; const change = setter || setPage;
    return count > size ? <div className="pagination"><span>{number(count)} نتیجه · صفحهٔ {number(active)} از {number(Math.ceil(count / size))}</span><div><button className="icon-button" aria-label="صفحه قبل" disabled={active <= 1} onClick={() => change(active - 1)}><ChevronRight size={17}/></button><button className="icon-button" aria-label="صفحه بعد" disabled={active * size >= count} onClick={() => change(active + 1)}><ChevronLeft size={17}/></button></div></div> : null;
  };

  function renderNotices(compact = false) {
    if (!notices.length) return <Empty title="تغییری برای نمایش نیست" text="تغییرات پس از شروع پایش محصولات در این قسمت ثبت می‌شوند."/>;
    return <div className={'notice-list ' + (compact ? 'compact' : '')}>{notices.map(n => <div className={'notice-row ' + (!n.read ? 'unread' : '')} key={n.id}>
      <ChangeIcon kind={n.kind}/><div className="notice-copy"><button className="text-title" onClick={() => n.product_id && openProduct(n.product_id)} disabled={!n.product_id}>{n.title}</button><div className="notice-meta"><span>{brands.find(b => b.id === n.brand_id)?.name || 'سامانه'}</span><i/> <span>{date(n.created_at)}</span>{!n.alerts && <span className="pill neutral">زیر آستانهٔ اعلان</span>}</div>{!compact && n.previous_observed_at && <small>تغییر بین {date(n.previous_observed_at)} و {date(n.created_at)} مشاهده شده است.</small>}</div>
      {n.new_price !== null && <div className="notice-prices"><strong>{number(n.new_price)} <small>تومان</small></strong><div><del>{number(n.old_price)}</del><span className={n.kind === 'price_up' ? 'text-rose' : 'text-green'}>{n.kind === 'price_up' ? '+' : '−'}{number(Math.abs(n.percent || 0))}٪</span></div></div>}
      {!compact && !n.read && <button className="icon-button" title="خوانده شد" onClick={() => action(() => post('/notifications/read', { ids: [n.id] }), 'اعلان خوانده شد.')}><Check size={17}/></button>}
    </div>)}</div>;
  }

  function renderProductCards() {
    if (!products.length && filter === 'deleted') return <Empty title="محصول حذف‌شده‌ای یافت نشد" text="محصولاتی که حذف می‌کنید اینجا برای بازگردانی در دسترس می‌مانند."/>;
    if (!products.length) return <Empty title={search ? 'محصولی با این نام پیدا نشد' : 'فهرست محصولات آمادهٔ کشف است'} text={isBrand ? 'جست‌وجوی اسنپ‌شاپ را بزنید؛ سپس محصولات موردنظر را برای پایش انتخاب کنید.' : 'از منوی برندها، یک برند را انتخاب کنید و محصولاتش را جست‌وجو کنید.'}><button className="button primary" onClick={() => isBrand ? action(() => post(`/brands/${view}/discover`), 'جست‌وجو در صف قرار گرفت.') : navigate('vitalayer')} disabled={busy || !isAdmin}><Search size={16}/>{isBrand ? 'جست‌وجوی محصولات در اسنپ‌شاپ' : 'رفتن به صفحهٔ ویتالیر'}</button></Empty>;
    return <><div className="products-grid">{products.map((p, i) => <article className={'product-card ' + (selected.includes(p.id) ? 'selected' : '')} key={p.id}>
      <div className="product-card-top">{isAdmin && <input type="checkbox" aria-label={'انتخاب ' + p.title} checked={selected.includes(p.id)} onChange={e => setSelected(s => e.target.checked ? [...s, p.id] : s.filter(id => id !== p.id))}/>}<span className={'pill ' + (p.deleted_at ? 'neutral' : p.error ? 'amber' : p.tracked ? 'green' : 'neutral')}>{p.deleted_at ? 'حذف‌شده' : p.error ? 'خطای دریافت' : p.tracked ? <><span className="status-dot"/>تحت پایش</> : 'انتخاب نشده'}</span></div>
      <button className="product-open" disabled={!!p.deleted_at} onClick={() => openProduct(p.id)}>{p.image ? <div className="product-art"><img src={p.image} alt={p.title} loading="lazy" referrerPolicy="no-referrer" onError={e => { e.currentTarget.style.display = 'none'; }}/></div> : <ProductArt brand={p.brand_id} index={i}/>}<div className="product-info"><span className="product-brand">{brands.find(b => b.id === p.brand_id)?.english}</span><h3>{p.title}</h3></div></button>
      <div className="product-price"><span>کمترین قیمت مشاهده‌شده</span><strong>{number(p.min_price)} <small>تومان</small></strong></div><div className="product-bottom"><span className={p.seller_count === 0 && !p.error ? "no-sellers" : ""}><Store size={14}/>{p.seller_count == null ? "فروشندگان هنوز ثبت نشده‌اند" : p.seller_count === 0 ? (p.error ? "دریافت فروشندگان ناموفق" : "فروشنده‌ای یافت نشد") : number(p.seller_count) + " فروشنده"}</span><div className="product-row-actions">{p.deleted_at ? <button className="mini-action start" disabled={busy || !isAdmin} onClick={() => restoreProducts([p.id])}><Undo2 size={13}/>بازگردانی</button> : <><button className={'mini-action ' + (p.tracked ? '' : 'start')} disabled={busy || !isAdmin} onClick={() => track([p.id], !p.tracked)}>{p.tracked ? <><Pause size={12}/>توقف پایش</> : <><Plus size={13}/>شروع پایش</>}</button>{isAdmin && <button className="mini-action delete-action" aria-label={'حذف محصول ' + p.title} title="حذف محصول" disabled={busy} onClick={() => deleteProducts([p.id])}><Trash2 size={14}/></button>}</>}</div></div>
      <div className={'product-time ' + (p.error || (p.last_success && stale(p.last_success, dash?.settings.interval_minutes || 60)) ? 'text-amber' : '')}><Clock3 size={12}/>{p.last_success ? 'آخرین مشاهده: ' + date(p.last_success) : 'منتظر اولین بررسی'}</div>
    </article>)}</div>{pagination(total)}</>;
  }

  if (booting) return <div className="boot"><div className="logo-mark"><ChartNoAxesCombined/></div><LoaderCircle className="spin"/><p>در حال آماده‌سازی پارسا آل شریف…</p></div>;
  if (!user) return <Login onLogin={boot}/>;
  const title = currentBrand ? currentBrand.name : ({ dashboard: 'نمای کلی', products: 'همهٔ محصولات', 'price-position': 'قیمت فروشگاه ما', 'add-product': 'افزودن محصول با لینک', notifications: 'مرکز اعلان‌ها', reports: 'گزارش‌ها و خروجی', settings: 'تنظیمات پایش' }[view] || 'نمای کلی');
  const workerOnline = dash?.heartbeat && Date.now() - new Date(dash.heartbeat).getTime() < 180000;
  const currentPreview = detailPreview?.product_id === detail?.id ? detailPreview : null;
  const displayedOffers = currentPreview?.offers ?? detail?.offers ?? [];

  return <div className="app-shell">
    <aside className="sidebar"><a className="brand-logo" href="#dashboard"><span className="logo-mark"><ChartNoAxesCombined size={23}/></span><div><strong>پارسا آل شریف</strong><span>پایش هوشمند قیمت</span></div><span className="logo-version">۱.۰</span></a>
      <div className="workspace"><span className="workspace-icon"><Store size={18}/></span><div><strong>میز کار مجموعه</strong><span>بازار اسنپ‌شاپ</span></div><ShieldCheck size={16}/></div>
      <nav aria-label="منوی اصلی"><a href="#dashboard" className={view === 'dashboard' ? 'active' : ''}><LayoutDashboard size={18}/>نمای کلی</a><a href="#products" className={view === 'products' ? 'active' : ''}><Package size={18}/>همهٔ محصولات<span className="nav-count">{number(dash?.products || 0)}</span></a><a href="#price-position" className={view === 'price-position' ? 'active' : ''}><TrendingDown size={18}/>قیمت فروشگاه ما</a><a href="#add-product" className={view === 'add-product' ? 'active' : ''}><Link2 size={18}/>افزودن محصول با لینک</a><a href="#notifications" className={view === 'notifications' ? 'active' : ''}><Bell size={18}/>اعلان‌ها{!!dash?.unread && <span className="nav-count alert">{number(dash.unread)}</span>}</a>
      <div className="nav-label">برندهای شما <span>۷</span></div>{brands.map(b => <a href={'#' + b.id} key={b.id} className={'brand-nav ' + (view === b.id ? 'active' : '')}><span className="brand-dot" style={{ background: brandColor[b.id] }}/>{b.name}<span className="brand-nav-count">{number(b.tracked)}</span></a>)}
      <div className="nav-divider"/><a href="#reports" className={view === 'reports' ? 'active' : ''}><FileSpreadsheet size={18}/>گزارش‌ها و خروجی</a><a href="#settings" className={view === 'settings' ? 'active' : ''}><Settings2 size={18}/>تنظیمات پایش</a></nav>
      <div className="sidebar-bottom"><div className="worker-state"><span className={'status-dot ' + (workerOnline ? '' : 'muted')}/><span>{workerOnline ? 'موتور پایش در حال اجراست' : 'در انتظار موتور پایش'}</span></div><div className="sidebar-user"><span className="avatar">{user.username.slice(0, 1).toUpperCase()}</span><div><strong>{user.username}</strong><span>{isAdmin ? 'مدیر سامانه' : 'مشاهده‌گر'}</span></div>{user.mode !== 'demo' && <button title="خروج" onClick={async () => { await post('/auth/logout'); setUser(null); }}><LogOut size={17}/></button>}</div></div>
    </aside>
    <div className="main-shell"><header className="topbar"><div className="breadcrumb">میز کار <ChevronLeft size={13}/><strong>{title}</strong></div><div className="topbar-actions"><span className="clock"><Clock3 size={14}/>{clock}</span><button className="top-bell" aria-label="اعلان‌ها" onClick={() => navigate('notifications')}><Bell size={20}/>{!!dash?.unread && <i/>}</button><span className="avatar header-avatar">{user.username.slice(0, 1).toUpperCase()}</span></div></header>
      {user.mode === 'demo' && <div className="demo-banner"><span><CircleHelp size={15}/><strong>نسخهٔ نمایشی</strong> — تمام نام‌ها و قیمت‌های این نسخه ساختگی هستند.</span><span>داده‌های واقعی در پایگاه جدا نگهداری می‌شوند.</span></div>}
      {offline && <div className="connection-banner"><WifiOff size={16}/>ارتباط با سرور قطع است؛ اطلاعات نمایش‌داده‌شده ممکن است قدیمی باشند.<button onClick={reload}>تلاش مجدد</button></div>}
      <main className="main-content">
        {view === 'dashboard' ? <>
          <div className="dashboard-toolbar"><h1 className="sr-only">نمای کلی پایش قیمت</h1><button className="button secondary" disabled={busy} onClick={() => downloadReport()}><ArrowDownToLine size={17}/>دریافت گزارش اکسل</button></div>
          <section className="overview-banner"><div><div className="banner-label"><span className="status-dot"/>مرکز پایش محصولات</div><h2>از اولین قیمت، تا آخرین تغییر.</h2><p>{number(dash?.tracked || 0)} محصول از ۷ برند، برای شما پایش می‌شوند.</p><button onClick={() => navigate('products')}>مدیریت محصولات تحت پایش<ArrowLeft size={16}/></button></div><div className="banner-visual" aria-hidden="true"><div className="radar-ring r1"/><div className="radar-ring r2"/><div className="radar-ring r3"/><div className="radar-line"/><div className="radar-center"><Activity size={31}/></div><div className="floating-price one"><TrendingDown size={16}/>رصد قیمت فروشندگان</div><div className="floating-price two"><Check size={15}/>ثبت تاریخچهٔ تغییرات</div></div><div className="banner-status"><span>فاصلهٔ بررسی</span><strong>{number(dash?.settings.interval_minutes || 60)} <small>دقیقه</small></strong><div/><span>آخرین مشاهدهٔ موفق</span><b>{date(dash?.last_success)}</b></div></section>
          <div className="stats-grid">{[
            { label: 'محصولات تحت پایش', value: dash?.tracked, icon: Package, note: `از ${number(dash?.products || 0)} محصول ثبت‌شده`, color: 'green' },
            { label: 'فروشندگان رصدشده', value: dash?.sellers, icon: Store, note: 'فروشندگان محصولات تحت پایش', color: 'sand' },
            { label: 'تغییرات قیمت امروز', value: dash?.changes_today, icon: Activity, note: 'افزایش و کاهش قیمت ثبت‌شده', color: 'lavender' },
            { label: 'اعلان‌های خوانده‌نشده', value: dash?.unread, icon: Bell, note: dash?.errors ? `${number(dash.errors)} محصول با خطای دریافت` : 'تغییرات منتظر بررسی شما', color: 'rose' },
          ].map(({ label, value, icon: Icon, note, color }) => <div className="stat-card" key={label}><div><span>{label}</span><span className={'stat-icon ' + color}><Icon size={19}/></span></div><strong>{number(value || 0)}</strong><small>{note}</small></div>)}</div>
          <div className="dashboard-middle"><section className="panel chart-panel"><div className="section-heading"><div><h2>روند قیمت محصول</h2><p>کمترین قیمت مشاهده‌شده در هر نوبت · تومان</p></div><span className="pill neutral">آخرین ۱۶۸ مشاهده</span></div><select className="chart-select" aria-label="محصول نمودار" value={chartId} onChange={e => setChartId(e.target.value)}><option value="">انتخاب محصول</option>{products.map(p => <option key={p.id} value={p.id}>{p.title}</option>)}</select><PriceChart data={chartData} loading={chartBusy}/></section>
          <section className="panel brand-summary"><div className="section-heading"><div><h2>برندهای شما</h2><p>پوشش پایش محصولات هر برند</p></div><span className="pill neutral">۷ برند</span></div>{brands.map(b => <button className="brand-summary-row" key={b.id} onClick={() => navigate(b.id)}><span className="brand-monogram" style={{ '--brand-color': brandColor[b.id] } as React.CSSProperties}>{b.english.slice(0, 1)}</span><span><strong>{b.name}</strong><small>{b.english}</small></span><div className="brand-track"><strong>{number(b.tracked)} <small>/ {number(b.total)}</small></strong><div><i style={{ width: `${b.total ? b.tracked / b.total * 100 : 0}%`, background: brandColor[b.id] }}/></div></div><ChevronLeft size={14}/></button>)}</section></div>
          <section className="panel recent-panel"><div className="section-heading"><div><h2><span className="small-status-dot"/>آخرین رویدادهای بازار</h2><p>تغییرات قیمت و رویدادهای محصولات شما</p></div><button className="text-button" onClick={() => navigate('notifications')}>مشاهدهٔ همه<ArrowLeft size={15}/></button></div>{renderNotices(true)}</section>
          <div className="bottom-note"><ShieldCheck size={14}/>قیمت‌ها بر اساس مشاهده در اسنپ‌شاپ و برای مقصد تهران هستند.<span>زمان‌ها به وقت تهران</span></div>
        </> : (isBrand || view === 'products') ? <>
          <div className="page-heading"><div><div className="eyebrow">{currentBrand ? currentBrand.english + ' / مدیریت محصولات' : 'کاتالوگ محصولات مجموعه'}</div><h1>{currentBrand ? 'محصولات ' + currentBrand.name : 'همهٔ محصولات'}<span className="heading-dot">.</span></h1><p>{currentBrand ? 'محصولات این برند را انتخاب کنید؛ پارسا آل شریف قیمت فروشندگان را دنبال می‌کند.' : 'تمام محصولات کشف‌شده و وضعیت پایش آن‌ها را مدیریت کنید.'}</p></div><div className="heading-buttons">{isBrand && <><button className="button secondary" onClick={() => setShowAdd(true)} disabled={!isAdmin}><Plus size={16}/>افزودن با لینک</button><button className="button primary" disabled={busy || !isAdmin || ['queued', 'running'].includes(currentBrand?.job?.status || '')} onClick={() => action(() => post(`/brands/${view}/discover`), 'جست‌وجو در صف قرار گرفت؛ نتیجه در همین صفحه ظاهر می‌شود.')}><Search size={16}/>{['queued', 'running'].includes(currentBrand?.job?.status || '') ? 'جست‌وجو در حال انجام…' : 'جست‌وجوی اسنپ‌شاپ'}</button></>}</div></div>
          {currentBrand?.discovery_error && <div className="error-box"><WifiOff size={17}/>{currentBrand.discovery_error}</div>}
          {currentBrand?.job && <div className={'job-status ' + (currentBrand.job.status === 'failed' ? 'failed' : '')}>{['queued', 'running'].includes(currentBrand.job.status) ? <LoaderCircle size={16} className="spin"/> : <Clock3 size={15}/>}<span>{currentBrand.job.message || 'جست‌وجو در صف قرار دارد…'}</span><small>{date(currentBrand.job.created_at)}</small></div>}
          <div className="catalog-toolbar"><div className="tabs"><button className={filter === 'all' ? 'active' : ''} onClick={() => { setFilter('all'); setSelected([]); setPage(1); }}>همهٔ محصولات</button><button className={filter === 'tracked' ? 'active' : ''} onClick={() => { setFilter('tracked'); setSelected([]); setPage(1); }}>تحت پایش</button><button className={filter === 'untracked' ? 'active' : ''} onClick={() => { setFilter('untracked'); setSelected([]); setPage(1); }}>انتخاب نشده</button>{isAdmin && <button className={filter === 'deleted' ? 'active' : ''} onClick={() => { setFilter('deleted'); setSelected([]); setPage(1); }}>حذف‌شده‌ها</button>}</div><label className="search-field"><Search size={17}/><input placeholder="جست‌وجو در محصولات…" value={search} onChange={e => { setSearch(e.target.value); setPage(1); }}/></label></div>
          {selected.length > 0 && <div className="selection-bar"><span><CheckCheck size={17}/>{number(selected.length)} محصول انتخاب شده</span><div><button className="text-button" onClick={() => setSelected([])}>لغو انتخاب</button>{filter === 'deleted' ? <button className="button primary" disabled={busy} onClick={() => restoreProducts(selected)}><Undo2 size={14}/>بازگردانی انتخاب‌شده‌ها</button> : <><button className="button secondary" disabled={busy} onClick={() => track(selected, false)}><Pause size={14}/>توقف پایش</button><button className="button primary" disabled={busy} onClick={() => track(selected, true)}><Play size={14}/>شروع پایش</button><button className="button delete-button" disabled={busy} onClick={() => deleteProducts(selected)}><Trash2 size={14}/>حذف انتخاب‌شده‌ها</button></>}</div></div>}
          {filter === 'deleted' && <div className="info-note trash-info"><Trash2 size={16}/><span>محصولات این بخش از پایش، مقایسه و گزارش‌ها کنار گذاشته شده‌اند و جست‌وجوی برند دوباره اضافه‌شان نمی‌کند. با بازگردانی، محصول و سابقه‌اش به فهرست برمی‌گردند؛ پایش را جداگانه فعال کنید.</span></div>}
          <div className="catalog-count"><span>{number(total)} محصول</span>{isAdmin && products.length > 0 && <button className="text-button" onClick={() => setSelected(selected.length === products.length ? [] : products.map(p => p.id))}>انتخاب همهٔ این صفحه</button>}<span>قیمت‌ها به تومان</span></div>{renderProductCards()}
        </> : view === 'price-position' ? <PricePositionPage brands={brands} onOpenProduct={openProduct} onNavigate={navigate} onNotice={inform}/> : view === 'add-product' ? <>
          <div className="page-heading"><div><div className="eyebrow">ثبت مستقیم محصول در برند دلخواه</div><h1>افزودن محصول با لینک<span className="heading-dot">.</span></h1><p>لینک اسنپ‌شاپ و برند را مشخص کنید؛ محصول موجود یا جدید وارد پایش می‌شود.</p></div></div>
          <form className="panel manual-product-form" onSubmit={e => { e.preventDefault(); const form = new FormData(e.currentTarget); setManualResult(null); action(async () => { const result = await post<ManualResult>('/products', { brand_id: manualBrand, url: form.get('url') }); setManualResult(result); }, 'محصول ثبت شد و پایش آن فعال است.'); }}>
            <div className="section-heading"><div><h2>ثبت و شروع پایش</h2><p>اگر قبلاً ثبت شده باشد، رکورد تکراری ساخته نمی‌شود و سابقه محفوظ می‌ماند.</p></div><Link2 size={21}/></div>
            <label>برند محصول<select name="brand" value={manualBrand} required disabled={!isAdmin} onChange={e => { setManualBrand(e.target.value); setManualResult(null); }}><option value="">انتخاب برند</option>{brands.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}</select></label>
            <label>لینک صفحهٔ محصول در اسنپ‌شاپ<input type="url" name="url" required disabled={!isAdmin} dir="ltr" placeholder="https://snappshop.ir/product/snp-983678429"/></label>
            <div className="info-note"><CircleHelp size={16}/><span>برندی که انتخاب می‌کنید مبنای دسته‌بندی است. اگر همین محصول قبلاً در برند دیگری ثبت شده باشد به برند انتخاب‌شده منتقل می‌شود؛ شناسه و تاریخچه‌اش باقی می‌ماند.</span></div>
            <button className="button primary" disabled={busy || !isAdmin || !manualBrand}>{busy ? <LoaderCircle className="spin" size={17}/> : <Plus size={17}/>}ثبت محصول و فعال‌کردن پایش</button>
          </form>
          {manualResult && <section className="panel manual-product-result" role="status"><span className="stat-icon green"><CheckCheck size={23}/></span><div><span className="pill green">{manualResult.already_exists ? 'محصول قبلاً موجود بود' : 'محصول جدید اضافه شد'}</span><h2>{manualResult.title}</h2><p>{manualResult.message}</p>{manualResult.brand_changed && <p>محصول به برند انتخاب‌شده منتقل شد؛ سابقهٔ قبلی حفظ شده است.</p>}{manualResult.brand_warning && <p className="text-amber">{manualResult.brand_warning}</p>}<div><button className="button secondary" type="button" onClick={() => openProduct(manualResult.id)}><Store size={15}/>مشاهدهٔ فروشندگان</button><button className="text-button" type="button" onClick={() => navigate(manualResult.brand_id)}>رفتن به صفحهٔ برند<ArrowLeft size={14}/></button></div></div></section>}
        </> : view === 'notifications' ? <>
          <div className="page-heading"><div><div className="eyebrow">از تغییرات باخبر بمانید</div><h1>مرکز اعلان‌ها<span className="heading-dot">.</span></h1><p>هر تغییر قیمت، هر فروشندهٔ جدید، با زمان مشاهده و جزئیات.</p></div><button className="button secondary" disabled={busy} onClick={() => action(() => post('/notifications/read'), 'همهٔ اعلان‌ها خوانده شدند.')}><CheckCheck size={17}/>خواندن همه</button></div>
          <div className="filter-bar"><select aria-label="نوع رویداد" value={noticeKind} onChange={e => { setNoticeKind(e.target.value); setPage(1); }}><option value="">همهٔ رویدادها</option><option value="price_up">افزایش قیمت</option><option value="price_down">کاهش قیمت</option><option value="seller_added">فروشندهٔ جدید</option><option value="product_found">محصول جدید</option><option value="stock">تغییر موجودی</option><option value="error">خطاهای دریافت</option></select><select aria-label="برند اعلان" value={noticeBrand} onChange={e => { setNoticeBrand(e.target.value); setPage(1); }}><option value="">همهٔ برندها</option>{brands.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}</select><label className="check-label"><input type="checkbox" checked={noticeUnread} onChange={e => { setNoticeUnread(e.target.checked); setPage(1); }}/>فقط خوانده‌نشده‌ها</label><span className="filter-count">{number(noticeTotal)} رویداد</span></div><section className="panel">{renderNotices()}{pagination(noticeTotal, 15)}</section>
        </> : view === 'reports' ? <>
          <div className="page-heading"><div><div className="eyebrow">آماده برای ارائه به مدیریت</div><h1>داده‌های دقیق، گزارش‌های مرتب<span className="heading-dot">.</span></h1><p>گزارش را با فیلتر دلخواه بسازید و فایل اکسل دریافت کنید.</p></div><span className="report-badge"><FileSpreadsheet size={25}/>XLSX</span></div>
          <div className="report-layout"><form className="panel report-form" onSubmit={e => { e.preventDefault(); downloadReport(); }}><div className="section-heading"><div><h2>ساخت گزارش جدید</h2><p>محدودهٔ اطلاعات موردنیاز را مشخص کنید.</p></div><SlidersHorizontal size={19}/></div><div className="form-grid"><label>برند<select value={reportBrand} onChange={e => setReportBrand(e.target.value)}><option value="">همهٔ برندها</option>{brands.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}</select></label><label>محصول<select value={reportProduct} onChange={e => setReportProduct(e.target.value)}><option value="">همهٔ محصولات</option>{reportProducts.map(p => <option key={p.id} value={p.id}>{p.title}</option>)}</select><small>برای دسترسی به فهرست دقیق‌تر، ابتدا برند را انتخاب کنید.</small></label><label>از تاریخ<input type="date" value={startDate} onChange={e => setStartDate(e.target.value)}/></label><label>تا تاریخ<input type="date" min={startDate} value={endDate} onChange={e => setEndDate(e.target.value)}/></label><label className="full-width">فروشنده<select value={reportSeller} onChange={e => setReportSeller(e.target.value)}><option value="">همهٔ فروشندگان</option>{sellers.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label></div><label className="check-label history-check"><input type="checkbox" checked={includeHistory} onChange={e => setIncludeHistory(e.target.checked)}/>تاریخچهٔ مشاهدات هم در فایل قرار بگیرد</label><div className="info-note"><CircleHelp size={16}/><span>انتخاب تاریخ میلادی است و مرز روز با زمان تهران محاسبه می‌شود. بازه روی تغییرات و تاریخچه اعمال می‌شود؛ شیت آخرین قیمت‌ها، آخرین وضعیت ثبت‌شده را نشان می‌دهد.</span></div><button className="button primary download-button" disabled={busy}>{busy ? <LoaderCircle className="spin" size={18}/> : <ArrowDownToLine size={18}/>}ساخت و دانلود گزارش اکسل</button></form>
          <div className="report-aside"><div className="excel-preview"><div className="excel-icon"><FileSpreadsheet size={35}/></div><span>گزارش پایش قیمت</span><h3>مرتب. خوانا. قابل ارائه.</h3><div className="preview-table"><div/><div/><div/><div/></div><div className="excel-tabs"><span>خلاصه</span><span>قیمت‌ها</span><span>تغییرات</span></div></div><div className="report-features">{['خلاصهٔ مدیریتی و جزئیات فروشندگان', 'قیمت‌های عددی و قابل محاسبه', 'راست‌چین، دارای فیلتر و سرستون ثابت', 'زمان مشاهده و لینک منبع برای بررسی'].map(t => <div key={t}><Check size={15}/>{t}</div>)}</div><p className="muted-note">در گزارش‌های بزرگ، بازه را محدود کنید. اگر سقف ردیف‌ها پر شود، پیام دریافت می‌کنید و داده‌ای به‌صورت پنهانی حذف نمی‌شود.</p></div></div>
        </> : view === 'settings' ? <>
          <div className="page-heading"><div><div className="eyebrow">پایش متناسب با نیاز شما</div><h1>تنظیمات سامانه<span className="heading-dot">.</span></h1><p>زمان‌بندی، حساسیت اعلان‌ها و مدت نگهداری تاریخچه را تنظیم کنید.</p></div></div><OwnStoreSettings isAdmin={isAdmin} onNotice={inform} onNavigate={navigate}/><form className="panel settings-form" onSubmit={e => { e.preventDefault(); action(() => api('/settings', { method: 'PUT', body: JSON.stringify(config) }), 'تنظیمات ذخیره شد.'); }}><div className="settings-section"><span className="stat-icon green"><Clock3 size={21}/></span><div><h3>فاصلهٔ بررسی قیمت‌ها</h3><p>پس از هر بررسی موفق، نوبت بعدی بر اساس این فاصله برنامه‌ریزی می‌شود.</p></div><label><input type="number" min={5} max={1440} value={config.interval_minutes} disabled={!isAdmin} onChange={e => setConfig({ ...config, interval_minutes: Number(e.target.value) })}/>دقیقه</label></div><div className="settings-section"><span className="stat-icon rose"><Bell size={21}/></span><div><h3>آستانهٔ اعلان تغییر قیمت</h3><p>تمام تغییرات ثبت می‌شوند؛ فقط تغییرات برابر یا بیشتر از این درصد در شمارندهٔ اعلان‌ها می‌آیند.</p></div><label><input type="number" step="0.1" min={0} max={1000} value={config.threshold_percent} disabled={!isAdmin} onChange={e => setConfig({ ...config, threshold_percent: Number(e.target.value) })}/>درصد</label></div><div className="settings-section"><span className="stat-icon sand"><RefreshCw size={21}/></span><div><h3>کشف دوبارهٔ محصولات برند</h3><p>برندها پس از اولین جست‌وجوی موفق، با این فاصله برای محصولات تازه بررسی می‌شوند.</p></div><label><input type="number" min={1} max={168} value={config.discovery_hours} disabled={!isAdmin} onChange={e => setConfig({ ...config, discovery_hours: Number(e.target.value) })}/>ساعت</label></div><div className="settings-section"><span className="stat-icon lavender"><FileSpreadsheet size={21}/></span><div><h3>نگهداری تاریخچهٔ مشاهدات</h3><p>مشاهدات قدیمی‌تر حذف می‌شوند؛ آخرین قیمت و رویدادهای تغییر باقی می‌مانند. پیش از کاهش این مقدار، خروجی بگیرید.</p></div><label><input type="number" min={1} max={365} value={config.retention_days} disabled={!isAdmin} onChange={e => setConfig({ ...config, retention_days: Number(e.target.value) })}/>روز</label></div><div className="settings-footer"><span><ShieldCheck size={16}/>منطقهٔ زمانی: تهران · واحد قیمت: تومان</span><button className="button primary" disabled={busy || !isAdmin}><Check size={16}/>ذخیرهٔ تنظیمات</button></div></form><div className="info-note standalone"><CircleHelp size={18}/><span>سامانه زمان مشاهدهٔ تغییر را ثبت می‌کند. تغییراتی که بین دو بررسی رخ دهند و برگردند، ممکن است دیده نشوند. بستن مرورگر پایش را متوقف نمی‌کند؛ فرآیند سرور باید روشن بماند.</span></div>
        </> : <Empty title="صفحه پیدا نشد"><button className="button primary" onClick={() => navigate('dashboard')}>بازگشت به داشبورد</button></Empty>}
      </main><footer className="app-footer"><span>پارسا آل شریف <i/> فضای پایش قیمت مجموعهٔ شما</span><span>مبدأ اطلاعات: اسنپ‌شاپ · زمان تهران</span></footer>
    </div>
    {toast && <div className={'toast ' + (toast.error ? 'error' : '')} role="status">{toast.error ? <WifiOff size={19}/> : <CheckCheck size={19}/>}<span>{toast.message}</span><button aria-label="بستن پیام" onClick={() => setToast(null)}><X size={16}/></button></div>}
    {busy && <div className="busy-bar"/>}
    {showAdd && <div className="modal-backdrop" onClick={() => setShowAdd(false)}><form className="add-modal panel" onClick={e => e.stopPropagation()} onSubmit={e => { e.preventDefault(); const form = new FormData(e.currentTarget); action(async () => { const result = await post<ManualResult>('/products', { brand_id: view, url: form.get('url') }); setManualResult(result); setShowAdd(false); }, 'محصول ثبت شد و پایش آن فعال است.'); }}><div className="section-heading"><h2>افزودن محصول با لینک</h2><button className="icon-button" type="button" aria-label="بستن" onClick={() => setShowAdd(false)}><X size={19}/></button></div><p>لینک صفحهٔ محصول {currentBrand?.name} در اسنپ‌شاپ را وارد کنید.</p><label>لینک محصول<input type="url" name="url" placeholder="https://snappshop.ir/product/…" dir="ltr" required/></label><button className="button primary" disabled={busy}><Plus size={16}/>افزودن و شروع پایش</button></form></div>}
    {detailId && <div className="drawer-backdrop" onClick={() => setDetailId(null)}><section className="detail-drawer" role="dialog" aria-modal="true" aria-label="جزئیات محصول" onClick={e => e.stopPropagation()}><header className="drawer-header"><span><Package size={18}/>جزئیات و تاریخچهٔ محصول</span><button className="icon-button" aria-label="بستن جزئیات" onClick={() => setDetailId(null)}><X size={21}/></button></header>{!detail ? <div className="boot"><LoaderCircle className="spin"/></div> : <div className="drawer-body"><div className="detail-product"><ProductArt brand={detail.brand_id} small/><div><span className="eyebrow">{brands.find(b => b.id === detail.brand_id)?.english}</span><h2>{detail.title}</h2><span className={'pill ' + (detail.tracked ? 'green' : 'neutral')}>{detail.tracked ? 'تحت پایش' : 'پایش غیرفعال'}</span>{detail.url && <a className="source-link" href={detail.url} target="_blank" rel="noreferrer">مشاهده در اسنپ‌شاپ<ExternalLink size={13}/></a>}</div></div><div className="detail-actions"><button className="button primary" disabled={busy || !isAdmin} onClick={() => track([detail.id], !detail.tracked)}>{detail.tracked ? <Pause size={15}/> : <Play size={15}/>} {detail.tracked ? 'توقف پایش' : 'شروع پایش'}</button><button className="button secondary" disabled={busy || !isAdmin || !detail.tracked} onClick={() => action(() => post(`/products/${detail.id}/check`), 'بررسی جدید در صف قرار گرفت.')}><RefreshCw size={15}/>بررسی اکنون</button><button className="button secondary" disabled={previewBusy} onClick={() => loadPreview(detail.id)}>{previewBusy ? <LoaderCircle size={15} className="spin"/> : <Store size={15}/>}دریافت فروشندگان اکنون</button><button className="button secondary" disabled={busy} onClick={() => downloadReport(detail.id)}><FileSpreadsheet size={15}/>اکسل محصول</button></div>
      {detail.error && <div className="error-box">{detail.error}</div>}<div className="detail-observed"><Clock3 size={14}/>آخرین مشاهدهٔ موفق: {date(detail.last_success)}{detail.last_success && stale(detail.last_success, dash?.settings.interval_minutes || 60) && <span className="pill amber">اطلاعات ممکن است قدیمی باشد</span>}</div>
      <div className="panel detail-chart"><div className="section-heading"><h3>روند کمترین قیمت مشاهده‌شده</h3><select aria-label="فروشنده نمودار و تاریخچه" value={sellerFilter} onChange={e => { setSellerFilter(e.target.value); setHistoryPage(1); }}><option value="">همهٔ فروشندگان</option>{Array.from(new Map(displayedOffers.map(o => [o.seller_id, o.seller_name]))).map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></div><PriceChart data={detailChart}/></div>
      {previewBusy && <div className="job-status"><LoaderCircle className="spin" size={15}/>در حال دریافت فروشندگان از اسنپ‌شاپ…</div>}{previewError && <div className="error-box">{previewError}</div>}{currentPreview && <div className="info-note preview-note"><Store size={16}/><span>نمایش لحظه‌ای فروشندگان · {date(currentPreview.observed_at)}. این دریافت وارد تاریخچه و اکسل پایش نمی‌شود؛ برای ثبت دوره‌ای، پایش را فعال کنید.</span></div>}<div className="tabs detail-tabs"><button className={detailTab === 'sellers' ? 'active' : ''} onClick={() => setDetailTab('sellers')}>قیمت فروشندگان <span>{number(new Set(displayedOffers.map(o => o.seller_id)).size)}</span></button><button className={detailTab === 'history' ? 'active' : ''} onClick={() => setDetailTab('history')}>تاریخچهٔ مشاهدات</button></div>
      <div className="table-scroll"><table className="data-table"><thead><tr><th>فروشنده / گونه</th><th>قیمت فروش · تومان</th><th>موجودی / وضعیت</th><th>زمان مشاهده</th></tr></thead><tbody>{(detailTab === 'sellers' ? displayedOffers.filter(o => !sellerFilter || o.seller_id === sellerFilter) : historyRows).map(o => <tr key={o.id}><td><strong>{o.seller_name}</strong><small>{o.variant}</small></td><td className="price-cell">{number(o.price)}{o.list_price && o.price && o.list_price > o.price ? <del>{number(o.list_price)}</del> : null}</td><td><span className={'pill ' + (o.missing ? 'amber' : o.available ? 'green' : o.available === false ? 'rose' : 'neutral')}>{o.missing ? 'در دریافت اخیر دیده نشد' : o.available ? 'موجود' : o.available === false ? 'ناموجود' : 'نامشخص'}</span></td><td>{date(o.observed_at)}</td></tr>)}</tbody></table></div>{detailTab === 'history' ? pagination(historyTotal, 20, historyPage, setHistoryPage) : null}{!displayedOffers.length && <Empty title={previewBusy ? 'در حال دریافت فروشندگان…' : previewError ? 'دریافت فروشندگان ناموفق بود' : currentPreview || detail.last_success ? 'فروشنده‌ای یافت نشد' : 'فروشندگان هنوز بررسی نشده‌اند'} text={previewBusy ? 'اطلاعات جاری این محصول از اسنپ‌شاپ خوانده می‌شود.' : previewError ? 'این خطا به معنی نداشتن فروشنده نیست؛ دوباره دریافت کنید.' : currentPreview?.availability === 'out_of_stock' ? 'اسنپ‌شاپ این صفحهٔ محصول را ناموجود اعلام کرده و پیشنهاد فروشی برنگردانده است.' : currentPreview || detail.last_success ? 'در آخرین پاسخ دریافتی برای همین صفحه، پیشنهاد فروشی وجود نداشت.' : 'برای دیدن فروشندگان، «دریافت فروشندگان اکنون» را بزنید؛ فعال‌کردن پایش برای مشاهده لازم نیست.'}/>}<div className="info-note"><CircleHelp size={15}/><span>هر گونه و گارانتی جدا ثبت می‌شود. دیده‌نشدن یک پیشنهاد در پاسخ تازه، به معنی ناموجودشدن قطعی نیست.</span></div></div>}</section></div>}
  </div>;
}
