/**
 * Public Spotlight search — macOS-style modal (parity with admin search).
 * Opens via [data-search-open] / Ctrl+K · queries /Home/SearchSuggest
 */
(function () {
  'use strict';

  var overlay = document.getElementById('search-overlay');
  if (!overlay) return;

  var input = document.getElementById('search-input');
  var clearBtn = document.getElementById('search-clear');
  var list = document.getElementById('search-list');
  var skeleton = document.getElementById('search-skeleton');
  var empty = document.getElementById('search-empty');
  var meta = document.getElementById('search-meta');
  var countEl = document.getElementById('search-count');
  var latencyEl = document.getElementById('search-latency');
  var fullLink = document.getElementById('search-full-link');

  var langPrefix = document.documentElement.getAttribute('data-culture') || 'fa';
  var RECENT_KEY = 'blog-search-recent';
  var hits = [];
  var active = -1;
  var timer = null;
  var aborter = null;

  function open() {
    overlay.hidden = false;
    document.body.style.overflow = 'hidden';
    if (input) {
      input.focus();
      input.select();
      input.setAttribute('aria-expanded', 'true');
    }
    if (!input || !input.value.trim()) showIdle();
  }

  function close() {
    overlay.hidden = true;
    document.body.style.overflow = '';
    if (input) input.setAttribute('aria-expanded', 'false');
    active = -1;
  }

  function escapeHtml(s) {
    return String(s || '').replace(/[&<>"']/g, function (c) {
      return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c];
    });
  }
  function escapeAttr(s) {
    return escapeHtml(s).replace(/`/g, '');
  }
  function highlight(text, q) {
    var t = escapeHtml(text);
    if (!q) return t;
    try {
      var parts = q.replace(/[.*+?^${}()|[\]\\]/g, '\\$&').split(/\s+/).filter(Boolean);
      if (!parts.length) return t;
      var re = new RegExp('(' + parts.join('|') + ')', 'ig');
      return t.replace(re, '<mark>$1</mark>');
    } catch (_) {
      return t;
    }
  }

  function loadRecent() {
    try {
      return JSON.parse(localStorage.getItem(RECENT_KEY) || '[]');
    } catch (_) {
      return [];
    }
  }
  function pushRecent(q) {
    if (!q || q.length < 2) return;
    try {
      var listR = loadRecent().filter(function (x) { return x !== q; });
      listR.unshift(q);
      if (listR.length > 8) listR = listR.slice(0, 8);
      localStorage.setItem(RECENT_KEY, JSON.stringify(listR));
    } catch (_) {}
  }

  function showIdle() {
    if (skeleton) skeleton.hidden = true;
    if (list) { list.hidden = true; list.innerHTML = ''; }
    if (meta) meta.hidden = true;
    hits = [];
    active = -1;
    if (!empty) return;
    empty.hidden = false;
    var recent = loadRecent();
    var recentHtml = '';
    if (recent.length) {
      recentHtml = '<div class="site-search-scopes" style="justify-content:center;border:0;flex-wrap:wrap;padding-top:0.75rem">' +
        recent.map(function (r) {
          return '<button type="button" class="scope recent-q" data-q="' + escapeAttr(r) + '">' + escapeHtml(r) + '</button>';
        }).join('') +
        '</div>';
    }
    empty.innerHTML =
      '<p class="hint">جست‌وجو در نوشته‌ها</p>' +
      '<p class="hint-sub">↑↓ پیمایش · Enter باز کردن · Esc بستن · Ctrl+K</p>' +
      recentHtml;
    empty.querySelectorAll('[data-q]').forEach(function (b) {
      b.addEventListener('click', function () {
        if (input) input.value = b.getAttribute('data-q') || '';
        if (clearBtn) clearBtn.hidden = !input.value;
        runSearch(input.value.trim());
      });
    });
  }

  function showSkeleton() {
    if (empty) empty.hidden = true;
    if (list) list.hidden = true;
    if (skeleton) skeleton.hidden = false;
    if (meta) meta.hidden = true;
  }

  function runSearch(q) {
    if (!q) { showIdle(); return; }
    if (aborter) try { aborter.abort(); } catch (_) {}
    aborter = typeof AbortController !== 'undefined' ? new AbortController() : null;
    var t0 = performance.now();

    fetch('/Home/SearchSuggest?q=' + encodeURIComponent(q), {
      signal: aborter ? aborter.signal : undefined,
      headers: { Accept: 'application/json' },
      credentials: 'same-origin'
    })
      .then(function (r) {
        if (!r.ok) throw new Error('fail');
        return r.json();
      })
      .then(function (items) {
        var took = Math.round(performance.now() - t0);
        render(items || [], q, took);
        pushRecent(q);
      })
      .catch(function (err) {
        if (err && err.name === 'AbortError') return;
        if (skeleton) skeleton.hidden = true;
        if (empty) {
          empty.hidden = false;
          empty.innerHTML = '<p class="hint">جست‌وجو موقتاً در دسترس نیست</p>';
        }
      });
  }

  function render(items, q, tookMs) {
    if (skeleton) skeleton.hidden = true;
    hits = items;
    active = hits.length ? 0 : -1;

    if (meta) meta.hidden = false;
    if (countEl) {
      countEl.textContent = hits.length
        ? (hits.length + ' نتیجه')
        : 'بدون نتیجه';
    }
    if (latencyEl) latencyEl.textContent = (tookMs || 0) + ' ms';
    if (fullLink) {
      fullLink.href = '/' + langPrefix + '/?q=' + encodeURIComponent(q);
      fullLink.hidden = false;
    }

    if (!hits.length) {
      if (list) list.hidden = true;
      if (empty) {
        empty.hidden = false;
        empty.innerHTML =
          '<p class="hint">نتیجه‌ای برای «' + escapeHtml(q) + '» نیست</p>' +
          '<p class="hint-sub">عبارت دیگری امتحان کنید</p>';
      }
      return;
    }

    if (empty) empty.hidden = true;
    if (list) list.hidden = false;

    var html = '<li class="site-search-group" aria-hidden="true">نوشته‌ها · ' + hits.length + '</li>';
    hits.forEach(function (h, idx) {
      var href = h.url || ('/' + (h.languageCode || langPrefix) + '/post/' + encodeURIComponent(h.slug || ''));
      var sub = [h.category, h.author, h.summary].filter(Boolean).join(' · ');
      if (sub.length > 110) sub = sub.slice(0, 108) + '…';
      html +=
        '<li role="option" data-idx="' + idx + '">' +
        '<a class="site-search-item' + (idx === active ? ' is-active' : '') + '" href="' + escapeAttr(href) + '" data-idx="' + idx + '">' +
        '<span class="ss-icon" aria-hidden="true">Po</span>' +
        '<span class="ss-main">' +
        '<div class="ss-title" dir="auto">' + highlight(h.title || '', q) + '</div>' +
        (sub ? '<div class="ss-sub" dir="auto">' + escapeHtml(sub) + '</div>' : '') +
        '</span>' +
        '<span class="ss-meta ltr-field">' + escapeHtml((h.languageCode || '').toUpperCase()) + '</span>' +
        '</a></li>';
    });
    list.innerHTML = html;

    list.querySelectorAll('.site-search-item').forEach(function (a) {
      a.addEventListener('mouseenter', function () {
        active = Number(a.getAttribute('data-idx'));
        paintActive();
      });
      a.addEventListener('click', function () {
        pushRecent(q);
      });
    });
  }

  function paintActive() {
    if (!list) return;
    list.querySelectorAll('.site-search-item').forEach(function (a) {
      a.classList.toggle('is-active', Number(a.getAttribute('data-idx')) === active);
    });
  }

  function move(delta) {
    if (!hits.length) return;
    active = (active + delta + hits.length) % hits.length;
    paintActive();
    var el = list && list.querySelector('.site-search-item[data-idx="' + active + '"]');
    if (el) el.scrollIntoView({ block: 'nearest' });
  }

  function navigateActive() {
    if (active < 0 || !hits[active]) return;
    var h = hits[active];
    var href = h.url || ('/' + (h.languageCode || langPrefix) + '/post/' + encodeURIComponent(h.slug || ''));
    pushRecent(input ? input.value.trim() : '');
    close();
    window.location.href = href;
  }

  document.querySelectorAll('[data-search-open]').forEach(function (btn) {
    btn.addEventListener('click', function (e) {
      e.preventDefault();
      open();
    });
  });
  document.querySelectorAll('[data-search-close]').forEach(function (btn) {
    btn.addEventListener('click', function (e) {
      e.preventDefault();
      close();
    });
  });

  overlay.addEventListener('click', function (e) {
    if (e.target === overlay) close();
  });

  if (clearBtn) {
    clearBtn.addEventListener('click', function () {
      if (input) input.value = '';
      clearBtn.hidden = true;
      showIdle();
      input && input.focus();
    });
  }

  if (input) {
    input.addEventListener('input', function () {
      var q = input.value.trim();
      if (clearBtn) clearBtn.hidden = !q;
      if (timer) clearTimeout(timer);
      if (!q) { showIdle(); return; }
      showSkeleton();
      timer = setTimeout(function () { runSearch(q); }, 160);
    });
  }

  document.addEventListener('keydown', function (e) {
    var mod = e.metaKey || e.ctrlKey;
    if (mod && e.key.toLowerCase() === 'k') {
      e.preventDefault();
      if (overlay.hidden) open();
      else close();
      return;
    }
    if (overlay.hidden) return;
    if (e.key === 'Escape') { e.preventDefault(); close(); return; }
    if (e.key === 'ArrowDown') { e.preventDefault(); move(1); return; }
    if (e.key === 'ArrowUp') { e.preventDefault(); move(-1); return; }
    if (e.key === 'Enter') {
      if (active >= 0 && hits[active]) {
        e.preventDefault();
        navigateActive();
      } else if (input && input.value.trim()) {
        e.preventDefault();
        var q = input.value.trim();
        pushRecent(q);
        close();
        window.location.href = '/' + langPrefix + '/?q=' + encodeURIComponent(q);
      }
    }
  });

  // Expose for ux.js bridge
  window.BlogSiteSearch = { open: open, close: close };
})();
