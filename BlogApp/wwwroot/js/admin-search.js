(function () {
  const root = document.getElementById('adminSearchRoot');
  if (!root) return;

  const api = root.dataset.api || '/AdminSearch/api';
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

  function t(key, fallback) {
    return root.getAttribute('data-i18n-' + key) || fallback;
  }

  function open() {
    if (!overlay) return;
    overlay.hidden = false;
    document.body.style.overflow = 'hidden';
    input?.focus();
    input?.setAttribute('aria-expanded', 'true');
    if (!input?.value) showIdle();
  }
  function close() {
    if (!overlay) return;
    overlay.hidden = true;
    document.body.style.overflow = '';
    input?.setAttribute('aria-expanded', 'false');
    active = -1;
  }

  document.querySelectorAll('#adminSearchOpen, [data-admin-search-open], .admin-search-field').forEach(el => {
    el.addEventListener('click', (e) => {
      e.preventDefault();
      open();
    });
  });

  overlay?.addEventListener('click', (e) => { if (e.target === overlay) close(); });
  clearBtn?.addEventListener('click', () => {
    if (input) input.value = '';
    if (clearBtn) clearBtn.hidden = true;
    showIdle();
    input?.focus();
  });

  scopes.forEach(btn => {
    btn.addEventListener('click', () => {
      scopes.forEach(b => { b.classList.remove('is-active'); b.setAttribute('aria-selected', 'false'); });
      btn.classList.add('is-active');
      btn.setAttribute('aria-selected', 'true');
      scope = btn.dataset.scope || 'all';
      if (input?.value.trim()) runSearch(input.value.trim());
    });
  });

  document.addEventListener('keydown', (e) => {
    const mod = e.metaKey || e.ctrlKey;
    if (mod && e.key.toLowerCase() === 'k') {
      e.preventDefault();
      if (overlay?.hidden) open(); else close();
      return;
    }
    if (overlay?.hidden) return;
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
    if (clearBtn) clearBtn.hidden = !q;
    if (timer) clearTimeout(timer);
    if (!q) { showIdle(); return; }
    showSkeleton();
    timer = setTimeout(() => runSearch(q), 160);
  });

  function showIdle() {
    if (skeleton) skeleton.hidden = true;
    if (list) { list.hidden = true; list.innerHTML = ''; }
    if (empty) {
      empty.hidden = false;
      empty.innerHTML =
        '<p class="hint">' + escapeHtml(t('idle', 'Search the admin panel')) + '</p>' +
        '<p class="hint-sub">' + escapeHtml(t('keys', '↑↓ navigate · Enter open · Esc close')) + '</p>';
    }
    if (meta) meta.hidden = true;
    hits = [];
    active = -1;
  }
  function showSkeleton() {
    if (empty) empty.hidden = true;
    if (list) list.hidden = true;
    if (skeleton) skeleton.hidden = false;
    if (meta) meta.hidden = true;
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
      if (skeleton) skeleton.hidden = true;
      if (empty) {
        empty.hidden = false;
        empty.innerHTML = '<p class="hint">' + escapeHtml(t('no-results', 'Search temporarily unavailable')) + '</p>';
      }
    }
  }

  function render(data, q) {
    if (skeleton) skeleton.hidden = true;
    hits = data.hits || [];
    active = hits.length ? 0 : -1;

    if (meta) meta.hidden = false;
    if (countEl) countEl.textContent = data.totalHitsLabel || (hits.length + ' ' + t('results', 'results'));
    const cacheTag = data.fromCache ? ' · cache' : '';
    if (latencyEl) latencyEl.textContent = `${data.tookMs || 0} ms${cacheTag}`;

    if (!hits.length) {
      if (list) list.hidden = true;
      if (empty) {
        empty.hidden = false;
        const sug = (data.suggestions || []).map(s =>
          `<button type="button" class="scope" data-suggest="${escapeAttr(s)}">${escapeHtml(s)}</button>`
        ).join(' ');
        empty.innerHTML = `<p class="hint">${escapeHtml(t('no-results', 'No results'))} “${escapeHtml(q)}”</p>` +
          (sug ? `<div class="admin-search-scopes" style="justify-content:center;border:0">${sug}</div>` : '');
        empty.querySelectorAll('[data-suggest]').forEach(b => b.addEventListener('click', () => {
          if (input) input.value = b.dataset.suggest;
          if (clearBtn) clearBtn.hidden = false;
          runSearch(input.value);
        }));
      }
      return;
    }

    if (empty) empty.hidden = true;
    if (list) list.hidden = false;

    const groups = {};
    hits.forEach(h => { (groups[h.entityType] ||= []).push(h); });

    let html = '';
    const order = ['page', 'post', 'user', 'comment', 'media', 'theme', 'taxonomy'];
    const labels = {
      page: t('group-page', 'Pages'),
      post: t('group-post', 'Posts'),
      user: t('group-user', 'People'),
      comment: t('group-comment', 'Comments'),
      media: t('group-media', 'Media'),
      theme: t('group-theme', 'Themes'),
      taxonomy: t('group-taxonomy', 'Taxonomy')
    };
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
    if (list) list.innerHTML = html;

    list?.querySelectorAll('.admin-search-item').forEach(a => {
      a.addEventListener('mouseenter', () => {
        active = Number(a.dataset.idx);
        paintActive();
      });
    });
  }

  function paintActive() {
    list?.querySelectorAll('.admin-search-item').forEach(a => {
      a.classList.toggle('is-active', Number(a.dataset.idx) === active);
    });
  }
  function move(delta) {
    if (!hits.length) return;
    active = (active + delta + hits.length) % hits.length;
    paintActive();
    list?.querySelector(`.admin-search-item[data-idx="${active}"]`)?.scrollIntoView({ block: 'nearest' });
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
    return String(s || '').replace(/[&<>"']/g, c => ({ '&': '&', '<': '<', '>': '>', '"': '"', "'": '&#39;' }[c]));
  }
  function escapeAttr(s) { return escapeHtml(s).replace(/`/g, ''); }
  function highlight(text, q) {
    const str = escapeHtml(text);
    if (!q) return str;
    try {
      const re = new RegExp('(' + q.replace(/[.*+?^${}()|[\]\\]/g, '\\$&').split(/\s+/).filter(Boolean).join('|') + ')', 'ig');
      return str.replace(re, '<mark>$1</mark>');
    } catch { return str; }
  }
})();
