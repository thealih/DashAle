'use client';

import { useEffect, useState } from 'react';
import { ArrowDownToLine, ArrowLeft, Check, CircleHelp, ExternalLink, LoaderCircle, RefreshCw, Search, Settings2, Store, TrendingDown } from 'lucide-react';

type StoreConfig = { seller_id: string; seller_name: string; undercut_toman: number; matched?: boolean };
type Seller = { id: string; name: string };
type Position = { product_id: number; product_title: string; brand_name: string; variant: string; own_price: number | null; competitor_price: number | null; competitor_name: string | null; difference: number | null; target_price: number | null; reduction_needed: number | null; status: string; status_label: string; own_observed_at: string | null; competitor_observed_at: string | null; url: string };
type Result = { configured: boolean; store: StoreConfig; items: Position[]; total: number; products: number; not_best_products: number; summary: Record<string, number>; generated_at: string };
type Notice = (message: string, error?: boolean) => void;
const num = (n: number | null | undefined) => n == null ? '—' : new Intl.NumberFormat('fa-IR').format(n);
const time = (s: string | null) => s ? new Intl.DateTimeFormat('fa-IR', { timeZone: 'Asia/Tehran', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(s)) : '—';

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch('/api' + path, { ...options, headers: { 'Content-Type': 'application/json' } });
  if (!response.ok) {
    if (response.status === 401) window.dispatchEvent(new Event('session-expired'));
    const body = await response.json().catch(() => ({}));
    throw new Error(typeof body.detail === 'string' ? body.detail : 'دریافت اطلاعات ناموفق بود.');
  }
  return response.json();
}

export function OwnStoreSettings({ isAdmin, onNotice, onNavigate }: { isAdmin: boolean; onNotice: Notice; onNavigate: (view: string) => void }) {
  const [config, setConfig] = useState<StoreConfig>({ seller_id: '', seller_name: '', undercut_toman: 1 });
  const [sellers, setSellers] = useState<Seller[]>([]);
  const [search, setSearch] = useState('');
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState('');
  const [reload, setReload] = useState(0);
  useEffect(() => { let active = true; setReady(false); setError(''); Promise.all([request<StoreConfig>('/settings/own-store'), request<Seller[]>('/sellers')]).then(([c, s]) => { if (active) { setConfig(c); setSellers(s); setReady(true); } }).catch(e => { if (active) setError(e.message); }); return () => { active = false; }; }, [reload]);
  async function save(value: StoreConfig) {
    setBusy(true);
    try { const result = await request<StoreConfig>('/settings/own-store', { method: 'PUT', body: JSON.stringify(value) }); setConfig(result); onNotice(result.seller_id ? 'فروشگاه پیش‌فرض ذخیره شد؛ مقایسهٔ قیمت‌ها آماده است.' : 'فروشگاه پیش‌فرض حذف شد.'); }
    catch (e) { onNotice((e as Error).message, true); }
    finally { setBusy(false); }
  }
  return <form className="panel own-store-form" onSubmit={e => { e.preventDefault(); save(config); }}>
    <div className="section-heading"><div><h2><Store size={19}/>فروشگاه خودمان</h2><p>فروشگاه پیش‌فرض را مشخص کنید تا قیمت شما با فروشندگان دیگر مقایسه شود.</p></div><span className="pill green">مقایسهٔ قیمت</span></div>
    {error ? <div className="error-box">{error}<button type="button" className="text-button" onClick={() => setReload(n => n + 1)}>تلاش مجدد</button></div> : !ready ? <div className="position-loading"><LoaderCircle className="spin" size={21}/></div> : <>
    <div className="form-grid"><label>جست‌وجوی نام فروشگاه<input value={search} onChange={e => setSearch(e.target.value)} placeholder="نام فروشگاه خودتان را بنویسید…" disabled={!isAdmin}/></label>
    <label>فروشگاه پیش‌فرض<select aria-label="فروشگاه پیش‌فرض" value={config.seller_id} disabled={!isAdmin} onChange={e => { const seller = sellers.find(s => s.id === e.target.value); setConfig({ ...config, seller_id: e.target.value, seller_name: seller?.name || '' }); }}><option value="">انتخاب فروشگاه</option>{config.seller_id && !sellers.some(s => s.id === config.seller_id) && <option value={config.seller_id}>{config.seller_name} — {config.seller_id}</option>}{sellers.filter(s => s.id === config.seller_id || s.name.includes(search) || s.id.includes(search)).map(s => <option key={s.id} value={s.id}>{s.name} — {s.id}</option>)}</select><small>شناسهٔ ثابت فروشگاه مبنای مقایسه است، حتی اگر نام آن تغییر کند.</small></label>
    <label>فاصلهٔ قیمت پیشنهادی از ارزان‌ترین رقیب · تومان<input aria-label="فاصلهٔ قیمت پیشنهادی" type="number" min={1} max={1000000000} required value={config.undercut_toman} disabled={!isAdmin} onChange={e => setConfig({ ...config, undercut_toman: Number(e.target.value) })}/><small>مثلاً ۱٬۰۰۰ یعنی پیشنهاد قیمت، هزار تومان کمتر از ارزان‌ترین رقیب باشد.</small></label></div>
    <details className="manual-store-details"><summary>فروشگاه من هنوز در فهرست نیست</summary><p>شناسهٔ فروشگاه در اسنپ‌شاپ و نام آن را وارد کنید. پس از دریافت محصولات دارای این فروشنده، قیمت‌هایش شناسایی می‌شوند.</p><div className="form-grid"><label>شناسهٔ فروشگاه در اسنپ‌شاپ<input aria-label="شناسهٔ دستی فروشگاه" dir="ltr" value={config.seller_id} disabled={!isAdmin} onChange={e => setConfig({ ...config, seller_id: e.target.value.trim() })}/></label><label>نام فروشگاه<input aria-label="نام دستی فروشگاه" value={config.seller_name} disabled={!isAdmin} onChange={e => setConfig({ ...config, seller_name: e.target.value })}/></label></div></details>
    <div className="info-note"><CircleHelp size={16}/><span>قیمت برابر با ارزان‌ترین رقیب، «بهترین قیمت مشترک» است. مقایسه فقط روی پیشنهادهای موجود، تازه و گونه/گارانتی یکسان انجام می‌شود. قیمت پیشنهادی در اسنپ‌شاپ به‌صورت خودکار اعمال نمی‌شود.</span></div>
    <div className="own-store-actions"><button className="button primary" disabled={busy || !isAdmin}>{busy ? <LoaderCircle className="spin" size={16}/> : <Check size={16}/>}ذخیرهٔ فروشگاه پیش‌فرض</button><button className="button secondary" type="button" onClick={() => onNavigate('price-position')}>مقایسهٔ قیمت فروشگاه ما<ArrowLeft size={15}/></button>{config.seller_id && isAdmin && <button type="button" className="text-button" disabled={busy} onClick={() => save({ ...config, seller_id: '', seller_name: '' })}>حذف انتخاب فروشگاه</button>}</div></>}
  </form>;
}

export function PricePositionPage({ brands, onOpenProduct, onNavigate, onNotice }: { brands: { id: string; name: string }[]; onOpenProduct: (id: number) => void; onNavigate: (view: string) => void; onNotice: Notice }) {
  const [data, setData] = useState<Result | null>(null);
  const [brand, setBrand] = useState('');
  const [q, setQ] = useState('');
  const [status, setStatus] = useState('not_best');
  const [page, setPage] = useState(1);
  const [error, setError] = useState('');
  const [refresh, setRefresh] = useState(0);
  const [downloading, setDownloading] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    async function load() { try { const params = new URLSearchParams({ brand_id: brand, q, status, page: String(page) }); const result = await request<Result>('/price-position?' + params, { signal: controller.signal }); if (!controller.signal.aborted) { setData(result); setError(''); } } catch (e) { if (!controller.signal.aborted) setError((e as Error).message); } }
    const debounce = setTimeout(load, 200); const timer = setInterval(load, 15000);
    return () => { controller.abort(); clearTimeout(debounce); clearInterval(timer); };
  }, [brand, q, status, page, refresh]);
  async function download() {
    setDownloading(true);
    try { const response = await fetch('/api/price-position/excel?' + new URLSearchParams({ brand_id: brand, q, status })); if (!response.ok) { const body = await response.json(); throw new Error(body.detail || 'تهیهٔ اکسل ناموفق بود.'); } const url = URL.createObjectURL(await response.blob()); const a = document.createElement('a'); a.href = url; a.download = 'price-position.xlsx'; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000); onNotice('اکسل مقایسهٔ قیمت دانلود شد.'); } catch (e) { onNotice((e as Error).message, true); } finally { setDownloading(false); }
  }
  const count = (key: string) => data?.summary[key] || 0;
  return <>
    <div className="page-heading"><div><div className="eyebrow">قیمت فروشگاه ما در برابر رقبا</div><h1>کجا بهترین قیمت را نداریم؟</h1><p>محصولات نیازمند اصلاح قیمت، همراه با ارزان‌ترین رقیب و قیمت پیشنهادی.</p></div><button className="button secondary" onClick={() => onNavigate('settings')}><Settings2 size={16}/>انتخاب فروشگاه</button></div>
    {error && <div className="error-box">{error}؛ اطلاعات نمایش‌داده‌شده ممکن است قدیمی باشد.</div>}
    {!data ? <div className="position-loading"><LoaderCircle className="spin" size={24}/></div> : !data.configured ? <section className="panel"><div className="empty"><div className="empty-icon"><Store size={26}/></div><h3>ابتدا فروشگاه خودتان را مشخص کنید</h3><p>در تنظیمات، نام فروشگاه خود را انتخاب کنید تا محصولات شما با رقبا مقایسه شوند.</p><button className="button primary" onClick={() => onNavigate('settings')}>رفتن به تنظیمات<ArrowLeft size={15}/></button></div></section> : <>
    <div className="position-store-strip"><span className="stat-icon green"><Store size={20}/></span><div><strong>{data.store.seller_name}</strong><small>شناسهٔ فروشگاه: {data.store.seller_id}</small></div><div className="position-asof">آخرین محاسبه: {time(data.generated_at)}<small>پیشنهاد قیمت: {num(data.store.undercut_toman)} تومان پایین‌تر از رقیب</small></div></div>
    <div className="stats-grid"><div className="stat-card"><div><span>محصولات نیازمند اصلاح قیمت</span><span className="stat-icon rose"><TrendingDown size={18}/></span></div><strong>{num(data.not_best_products)}</strong><small>رقیبِ قابل مقایسه قیمت پایین‌تری دارد</small></div><div className="stat-card"><div><span>پیشنهادهای دارای بهترین قیمت</span><span className="stat-icon green"><Check size={18}/></span></div><strong>{num(count('best') + count('tied'))}</strong><small>شامل {num(count('tied'))} مورد بهترین قیمت مشترک</small></div><div className="stat-card"><div><span>فروشگاه ما یافت نشد</span><span className="stat-icon sand"><Store size={18}/></span></div><strong>{num(count('missing_own') + count('own_unavailable'))}</strong><small>نبود پیشنهاد جاری یا موجود فروشگاه ما</small></div><div className="stat-card"><div><span>نیازمند بررسی یا فعال‌کردن پایش</span><span className="stat-icon lavender"><CircleHelp size={18}/></span></div><strong>{num(count('needs_review') + count('not_monitored'))}</strong><small>اطلاعات ناکافی با بهترین قیمت اشتباه نمی‌شود</small></div></div>
    <div className="filter-bar position-filters"><label className="search-field"><Search size={16}/><input aria-label="جست‌وجوی مقایسهٔ قیمت" placeholder="نام محصول…" value={q} onChange={e => { setQ(e.target.value); setPage(1); }}/></label><select aria-label="برند مقایسهٔ قیمت" value={brand} onChange={e => { setBrand(e.target.value); setPage(1); }}><option value="">همهٔ برندها</option>{brands.map(b => <option value={b.id} key={b.id}>{b.name}</option>)}</select><select aria-label="وضعیت مقایسهٔ قیمت" value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="not_best">قیمت ما بهترین نیست</option><option value="all">همهٔ وضعیت‌ها</option><option value="best">قیمت ما پایین‌تر است</option><option value="tied">هم‌قیمت ارزان‌ترین رقیب</option><option value="no_competitor">رقیب قابل مقایسه نداریم</option><option value="missing_own">فروشگاه ما یافت نشد</option><option value="own_unavailable">پیشنهاد ما موجود یا جاری نیست</option><option value="needs_review">نیازمند بررسی اطلاعات</option><option value="not_monitored">پایش غیرفعال</option></select><button className="icon-button" aria-label="تازه‌سازی مقایسه" onClick={() => setRefresh(n => n + 1)}><RefreshCw size={16}/></button><button className="button secondary" disabled={downloading} onClick={download}>{downloading ? <LoaderCircle className="spin" size={16}/> : <ArrowDownToLine size={16}/>}اکسل مقایسه</button></div>
    <section className="panel"><div className="position-result-count">{num(data.total)} مورد · بررسی {num(data.products)} محصول ثبت‌شده</div>{!data.items.length ? <div className="empty"><div className="empty-icon"><Check size={25}/></div><h3>موردی با این فیلتر وجود ندارد</h3><p>وضعیت‌های دیگر را هم بررسی کنید؛ نبود نتیجه به‌تنهایی به معنی بهترین‌بودن تمام محصولات نیست.</p></div> : <div className="table-scroll"><table className="data-table position-table"><thead><tr><th>محصول / گونه</th><th>قیمت ما · تومان</th><th>ارزان‌ترین رقیب · تومان</th><th>اختلاف با رقیب</th><th>قیمت پیشنهادی · تومان</th><th>وضعیت</th><th>جزئیات</th></tr></thead><tbody>{data.items.map((item, index) => <tr key={item.product_id + '-' + index}><td><strong className="position-title">{item.product_title}</strong><small>{item.brand_name} · {item.variant}</small><small>قیمت ما: {time(item.own_observed_at)} · رقیب: {time(item.competitor_observed_at)}</small></td><td className="price-cell">{num(item.own_price)}</td><td className="price-cell">{num(item.competitor_price)}<small>{item.competitor_name || '—'}</small></td><td className={item.status === 'not_best' ? 'text-rose' : 'text-green'}>{num(item.difference)}<small>تومان</small></td><td className="price-cell">{num(item.target_price)}{item.reduction_needed != null && <small>کاهش {num(item.reduction_needed)} تومان</small>}{item.status === 'not_best' && item.target_price == null && <small>با فاصلهٔ انتخابی قیمت مثبت ممکن نیست</small>}</td><td><span className={'pill ' + (item.status === 'not_best' ? 'rose' : ['best', 'tied'].includes(item.status) ? 'green' : 'amber')}>{item.status_label}</span></td><td><button className="text-button" onClick={() => onOpenProduct(item.product_id)}>محصول<ArrowLeft size={13}/></button>{item.url && <a className="position-source" href={item.url} target="_blank" rel="noreferrer">اسنپ‌شاپ<ExternalLink size={12}/></a>}</td></tr>)}</tbody></table></div>}{data.total > 20 && <div className="pagination"><span>صفحهٔ {num(page)} از {num(Math.ceil(data.total / 20))}</span><div><button className="button secondary" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>قبلی</button><button className="button secondary" disabled={page * 20 >= data.total} onClick={() => setPage(p => p + 1)}>بعدی</button></div></div>}</section>
    <div className="info-note standalone"><CircleHelp size={17}/><span>این جدول با آخرین پایش موفق به‌روز می‌شود. محصولات غیرفعال، خطادار، قدیمی یا دارای موجودی/گونهٔ نامشخص، بهترین قیمت اعلام نمی‌شوند. «تازه‌سازی» جدول را دوباره محاسبه می‌کند؛ برای دریافت قیمت تازه از منبع، در جزئیات محصول «بررسی اکنون» را بزنید.</span></div></>}
  </>;
}
