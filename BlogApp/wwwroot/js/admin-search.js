(function () {
  const root = document.getElementById('adminSearchRoot');
  if (!root) return;

  const api = root.dataset.api || '/AdminSearch/api';
  const openBtn = document.getElementById('adminSearchOpen');
  const overlay = document.getElementById('adminSearchOverlay');
  const input = document.getElementById('adminSearchInput');
  const clearBtn = document.getElementById('adminSearchClear');
  const list = document.getElementById('adminSearchList');
  const skeleton = document.getElementById('adminSearchSkeleton');
  const empty = document.getElementById('adminSearchEmpty');
  const meta = document.getElementById('adminSearchMeta');
  const countEl = document.getElementById('adminSearchCount');
  const latencyEl = document.getElementById('adminSearchLatency');
  const scopes = root.querySelectorAll('.admin-search-scopes .scope');

  let scope = 'all';
  let active = -1;
  let hits = [];
  let timer = null;
  let aborter = null;

  function open() {
    overlay.hidden = false;
    document.body.style.overflow = 'hidden';
    input.focus();
    input.setAttribute('aria-expanded', 'true');
    if (!input.value) showIdle();
  }
  function close() {
    overlay.hidden = true;
    document.body.style.overflow = '';
    input.setAttribute('aria-expanded', 'false');
    active = -1;
  }

  openBtn?.addEventListener('click', open);
  overlay?.addEventListener('click', (e) => { if (e.target === overlay) close(); });
  clearBtn?.addEventListener('click', () => { input.value = ''; clearBtn.hidden = true; showIdle(); input.focus(); });

  scopes.forEach(btn => {
    btn.addEventListener('click', () => {
      scopes.forEach(b => { b.classList.remove('is-active'); b.setAttribute('aria-selected', 'false'); });
      btn.classList.add('is-active');
      btn.setAttribute('aria-selected', 'true');
      scope = btn.dataset.scope || 'all';
      if (input.value.trim()) runSearch(input.value.trim());
    });
  });

  document.addEventListener('keydown', (e) => {
    const mod = e.metaKey || e.ctrlKey;
    if (mod && e.key.toLowerCase() === 'k') {
      e.preventDefault();
      if (overlay.hidden) open(); else close();
      return;
    }
    if (overlay.hidden) return;
    if (e.key === 'Escape') { e.preventDefault(); close(); return; }
    if (e.key === 'ArrowDown') { e.preventDefault(); move(1); return; }
    if (e.key === 'ArrowUp') { e.preventDefault(); move(-1); return; }
    if (e.key === 'Enter' && active >= 0 && hits[active]) {
      e.preventDefault();
      navigate(hits[active].url);
    }
  });

  input?.addEventListener('input', () => {
    const q = input.value.trim();
    clearBtn.hidden = !q;
    if (timer) clearTimeout(timer);
    if (!q) { showIdle(); return; }
    showSkeleton();
    timer = setTimeout(() => runSearch(q), 160); // debounce — low perceived latency
  });

  function showIdle() {
    skeleton.hidden = true;
    list.hidden = true;
    list.innerHTML = '';
    empty.hidden = false;
    meta.hidden = true;
    hits = [];
    active = -1;
  }
  function showSkeleton() {
    empty.hidden = true;
    list.hidden = true;
    skeleton.hidden = false;
    meta.hidden = true;
  }

  async function runSearch(q) {
    if (aborter) aborter.abort();
    aborter = new AbortController();
    try {
      const url = `${api}?q=${encodeURIComponent(q)}&scope=${encodeURIComponent(scope)}&take=28`;
      const res = await fetch(url, { signal: aborter.signal, headers: { 'Accept': 'application/json' } });
      if (!res.ok) throw new Error('search failed');
      const data = await res.json();
      render(data, q);
    } catch (err) {
      if (err.name === 'AbortError') return;
      skeleton.hidden = true;
      empty.hidden = false;
      empty.innerHTML = '<p class="hint">Search temporarily unavailable</p>';
    }
  }

  function render(data, q) {
    skeleton.hidden = true;
    hits = data.hits || [];
    active = hits.length ? 0 : -1;

    meta.hidden = false;
    countEl.textContent = data.totalHitsLabel || (hits.length + ' results');
    const cacheTag = data.fromCache ? ' · cache' : '';
    latencyEl.textContent = `${data.tookMs || 0} ms${cacheTag}`;

    if (!hits.length) {
      list.hidden = true;
      empty.hidden = false;
      const sug = (data.suggestions || []).map(s => `<button type="button" class="scope" data-suggest="${escapeAttr(s)}">${escapeHtml(s)}</button>`).join(' ');
      empty.innerHTML = `<p class="hint">No results for “${escapeHtml(q)}”</p>` +
        (sug ? `<div class="admin-search-scopes" style="justify-content:center;border:0">${sug}</div>` : '');
      empty.querySelectorAll('[data-suggest]').forEach(b => b.addEventListener('click', () => {
        input.value = b.dataset.suggest; clearBtn.hidden = false; runSearch(input.value);
      }));
      return;
    }

    empty.hidden = true;
    list.hidden = false;

    // Group by type (X-style tabs feel)
    const groups = {};
    hits.forEach(h => {
      (groups[h.entityType] ||= []).push(h);
    });

    let html = '';
    const order = ['page', 'post', 'user', 'comment', 'media', 'theme', 'taxonomy'];
    const labels = { page: 'Pages', post: 'Posts', user: 'People', comment: 'Comments', media: 'Media', theme: 'Themes', taxonomy: 'Taxonomy' };
    let flatIdx = 0;
    order.forEach(type => {
      const items = groups[type];
      if (!items) return;
      html += `<li class="admin-search-group" aria-hidden="true">${labels[type] || type}` +
        (data.countsByType && data.countsByType[type] != null ? ` · ${data.countsByType[type]}` : '') +
        `</li>`;
      items.forEach(h => {
        const idx = flatIdx++;
        html += `<li role="option" data-idx="${idx}">` +
          `<a class="admin-search-item${idx === active ? ' is-active' : ''}" href="${escapeAttr(h.url || '#')}" data-idx="${idx}">` +
          `<span class="as-icon">${iconLabel(h.entityType)}</span>` +
          `<span class="as-main"><div class="as-title">${highlight(h.title, q)}</div>` +
          `<div class="as-sub">${escapeHtml(h.subtitle || h.snippet || '')}</div></span>` +
          `<span class="as-meta">` +
          (h.status ? `<span class="as-badge">${escapeHtml(h.status)}</span>` : '') +
          (h.relativeTime ? `<span>${escapeHtml(h.relativeTime)}</span>` : '') +
          `</span></a></li>`;
      });
    });
    list.innerHTML = html;

    list.querySelectorAll('.admin-search-item').forEach(a => {
      a.addEventListener('mouseenter', () => {
        active = Number(a.dataset.idx);
        paintActive();
      });
    });
  }

  function paintActive() {
    list.querySelectorAll('.admin-search-item').forEach(a => {
      a.classList.toggle('is-active', Number(a.dataset.idx) === active);
    });
  }
  function move(delta) {
    if (!hits.length) return;
    active = (active + delta + hits.length) % hits.length;
    paintActive();
    list.querySelector(`.admin-search-item[data-idx="${active}"]`)?.scrollIntoView({ block: 'nearest' });
  }
  function navigate(url) {
    if (!url) return;
    close();
    window.location.href = url;
  }

  function iconLabel(t) {
    return ({ post: 'Po', comment: 'Co', user: 'Pe', media: 'Me', theme: 'Th', page: 'Pg', taxonomy: 'Tx' })[t] || '·';
  }
  function escapeHtml(s) {
    return String(s || '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  }
  function escapeAttr(s) { return escapeHtml(s).replace(/`/g, ''); }
  function highlight(text, q) {
    const t = escapeHtml(text);
    if (!q) return t;
    try {
      const re = new RegExp('(' + q.replace(/[.*+?^${}()|[\]\\]/g, '\\$&').split(/\s+/).filter(Boolean).join('|') + ')', 'ig');
      return t.replace(re, '<mark>$1</mark>');
    } catch { return t; }
  }
})();
