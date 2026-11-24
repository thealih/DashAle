'use client';

import { Children, isValidElement, useEffect, useId, useRef, useState, type ReactNode, type SelectHTMLAttributes } from 'react';
import { createPortal } from 'react-dom';
import { Check, ChevronDown, Search } from 'lucide-react';

type Option = { value: string; label: string; disabled: boolean };
type Props = SelectHTMLAttributes<HTMLSelectElement>;

function labelText(node: ReactNode): string {
  return Children.toArray(node).map(child => isValidElement<{ children?: ReactNode }>(child) ? labelText(child.props.children) : String(child ?? '')).join('');
}

function readOptions(children: ReactNode, parentDisabled = false): Option[] {
  const options: Option[] = [];
  Children.forEach(children, child => {
    if (!isValidElement<{ children?: ReactNode; value?: string | number; disabled?: boolean }>(child)) return;
    if (child.type === 'option') options.push({ value: String(child.props.value ?? labelText(child.props.children)), label: labelText(child.props.children), disabled: parentDisabled || !!child.props.disabled });
    else options.push(...readOptions(child.props.children, parentDisabled || !!child.props.disabled));
  });
  return options;
}

function normalize(text: string) {
  return text.toLocaleLowerCase('fa').replace(/ي/g, 'ی').replace(/ك/g, 'ک').replace(/آ/g, 'ا').replace(/[\u064B-\u065F]/g, '').replace(/[\u200c\u200f]/g, ' ').replace(/[۰-۹]/g, n => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(n))).replace(/\s+/g, ' ').trim();
}

export function SearchableSelect({ children, className = '', disabled, value, defaultValue, onChange, onInvalid, id, ...props }: Props) {
  const uid = useId();
  const button = useRef<HTMLButtonElement>(null);
  const native = useRef<HTMLSelectElement>(null);
  const popup = useRef<HTMLDivElement>(null);
  const search = useRef<HTMLInputElement>(null);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);
  const [invalid, setInvalid] = useState(false);
  const [uncontrolledValue, setUncontrolledValue] = useState(String(defaultValue ?? ''));
  const [position, setPosition] = useState<{ top?: number; bottom?: number; left: number; width: number; maxHeight: number }>({ top: 0, left: 0, width: 200, maxHeight: 300 });
  const options = readOptions(children);
  const selectedValue = String(value ?? uncontrolledValue);
  const selected = options.find(option => option.value === selectedValue);
  const tokens = normalize(query).split(' ').filter(Boolean);
  const matches = options.filter(option => tokens.every(token => normalize(option.label + ' ' + option.value).includes(token)));
  const label = props['aria-label'] || props.name || 'انتخاب گزینه';

  function close(focus = false) { setOpen(false); setQuery(''); if (focus) button.current?.focus(); }
  function choose(option: Option) {
    if (option.disabled || disabled || !native.current) return;
    setInvalid(false);
    native.current.value = option.value;
    setUncontrolledValue(option.value);
    // A real select change preserves all existing React handlers and FormData.
    native.current.dispatchEvent(new Event('change', { bubbles: true }));
    close(true);
  }
  useEffect(() => {
    if (!open) return;
    function place() {
      const rect = button.current?.getBoundingClientRect(); if (!rect) return;
      const below = window.innerHeight - rect.bottom - 12;
      const above = rect.top - 12;
      const upwards = below < 240 && above > below;
      const height = Math.min(320, Math.max(120, upwards ? above : below));
      setPosition({ top: upwards ? undefined : rect.bottom + 6, bottom: upwards ? window.innerHeight - rect.top + 6 : undefined, left: Math.max(8, Math.min(rect.left, window.innerWidth - rect.width - 8)), width: Math.min(rect.width, window.innerWidth - 16), maxHeight: height });
    }
    place(); search.current?.focus();
    function outside(e: PointerEvent) { const target = e.target as Node; if (!button.current?.contains(target) && !popup.current?.contains(target)) close(); }
    window.addEventListener('pointerdown', outside);
    window.addEventListener('resize', place);
    window.addEventListener('scroll', place, true);
    return () => { window.removeEventListener('pointerdown', outside); window.removeEventListener('resize', place); window.removeEventListener('scroll', place, true); };
  }, [open]);
  useEffect(() => { if (disabled) close(); }, [disabled]);
  useEffect(() => { if (open) document.getElementById(uid + '-option-' + active)?.scrollIntoView({ block: 'nearest' }); }, [active, open, uid]);

  return <div className={'searchable-select ' + className}>
    <select {...props} ref={native} id={uid + '-native'} value={value} defaultValue={value == null ? defaultValue : undefined} disabled={disabled} tabIndex={-1} aria-hidden="true" className="searchable-select-native" onChange={onChange} onInvalid={e => { e.preventDefault(); setInvalid(true); setOpen(true); onInvalid?.(e); }}>{children}</select>
    <button ref={button} id={id || uid + '-trigger'} type="button" role="combobox" aria-label={label} aria-expanded={open} aria-controls={open ? uid + '-list' : undefined} aria-haspopup="listbox" aria-required={props.required} aria-invalid={invalid || undefined} disabled={disabled} className={'searchable-select-trigger ' + (open ? 'is-open' : '')} title={selected?.label} onClick={e => { e.preventDefault(); setQuery(''); setActive(0); setOpen(!open); }} onKeyDown={e => { if (['ArrowDown', 'ArrowUp'].includes(e.key)) { e.preventDefault(); setOpen(true); setActive(0); } if (e.key === 'Escape') { e.stopPropagation(); close(); } }}><span>{selected?.label || 'انتخاب گزینه'}</span><ChevronDown size={15}/></button>
    {invalid && <small className="select-validation">یک گزینه انتخاب کنید.</small>}
    {open && createPortal(<div ref={popup} className="searchable-select-popup" style={position} dir="rtl"><div className="searchable-select-search"><Search size={16}/><input ref={search} type="search" value={query} placeholder="جست‌وجو…" aria-label={'جست‌وجو در ' + label} aria-controls={uid + '-list'} aria-activedescendant={matches[active] ? uid + '-option-' + active : undefined} autoComplete="off" onChange={e => { setQuery(e.target.value); setActive(0); }} onKeyDown={e => {
      if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); close(true); }
      if (e.key === 'Tab') close(true);
      if (e.key === 'Enter') { e.preventDefault(); if (matches[active]) choose(matches[active]); }
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); const step = e.key === 'ArrowDown' ? 1 : -1; let next = active; for (let i = 0; i < matches.length; i++) { next = (next + step + matches.length) % matches.length; if (!matches[next].disabled) break; } setActive(next); }
    }}/></div><div id={uid + '-list'} role="listbox" aria-label={label} className="searchable-select-options">{matches.length ? matches.map((option, index) => <button type="button" role="option" id={uid + '-option-' + index} key={option.value + '-' + index} aria-selected={option.value === selectedValue} disabled={option.disabled} tabIndex={-1} className={index === active ? 'is-active' : ''} onPointerMove={() => setActive(index)} onMouseDown={e => e.preventDefault()} onClick={() => choose(option)}><span>{option.label}</span>{option.value === selectedValue && <Check size={15}/>}</button>) : <div className="searchable-select-empty" role="status">گزینه‌ای یافت نشد.</div>}</div></div>, document.body)}
  </div>;
}
