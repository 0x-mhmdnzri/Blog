/**
 * Public Spotlight search — macOS-style modal.
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

  function t(key, fallback) {
    return overlay.getAttribute('data-i18n-' + key) || fallback;
  }

  function open() {
    overlay.hidden = false;
    document.body.style.overflow = 'hidden';
    if (input) {
      input.focus();
      input.select();
      input.setAttribute('aria-expanded', 'true');
    }
    if (!input || !input.value.trim()) showIdle();
    else runSearch(input.value.trim());
  }

  function close() {
    overlay.hidden = true;
    document.body.style.overflow = '';
    if (input) input.setAttribute('aria-expanded', 'false');
    active = -1;
  }

  function escapeHtml(s) {
    return String(s || '')
      .replace(new RegExp('&','g'), String.fromCharCode(38) + 'amp;')
      .replace(new RegExp('<','g'), String.fromCharCode(38) + 'lt;')
      .replace(new RegExp('>','g'), String.fromCharCode(38) + 'gt;')
      .replace(new RegExp('"','g'), String.fromCharCode(38) + 'quot;')
      .replace(new RegExp("'",'g'), String.fromCharCode(38) + '#39;');
  }
  function escapeAttr(s) {
    return escapeHtml(s).replace(/`/g, '');
  }
  function highlight(text, q) {
    var str = escapeHtml(text);
    if (!q) return str;
    try {
      var parts = q.replace(/[.*+?^${}()|[\]\\]/g, '\\$&').split(/\s+/).filter(Boolean);
      if (!parts.length) return str;
      var re = new RegExp('(' + parts.join('|') + ')', 'ig');
      return str.replace(re, '<mark>$1<' + '/mark>');
    } catch (_) {
      return str;
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
      recentHtml =
        '<p class="hint-sub" style="margin-top:1rem;margin-bottom:.4rem">' + escapeHtml(t('recent', 'Recent')) + '<' + '/p>' +
        '<div class="site-search-scopes" style="justify-content:center;border:0;flex-wrap:wrap;padding-top:0">' +
        recent.map(function (r) {
          return '<button type="button" class="scope recent-q" data-q="' + escapeAttr(r) + '">' + escapeHtml(r) + '<' + '/button>';
        }).join('') +
        '<' + '/div>';
    }
    empty.innerHTML =
      '<p class="hint">' + escapeHtml(t('idle', 'Search posts')) + '<' + '/p>' +
      '<p class="hint-sub">' + escapeHtml(t('keys', '\u2191\u2193 navigate \u00b7 Enter open \u00b7 Esc close')) + '<' + '/p>' +
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
    var urls = [
      '/' + langPrefix + '/Home/SearchSuggest?q=' + encodeURIComponent(q),
      '/Home/SearchSuggest?q=' + encodeURIComponent(q)
    ];
    var attempt = 0;
    function tryFetch() {
      var url = urls[attempt];
      return fetch(url, {
        signal: aborter ? aborter.signal : undefined,
        headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
        credentials: 'same-origin'
      }).then(function (r) {
        if (!r.ok) {
          if (attempt < urls.length - 1) {
            attempt++;
            return tryFetch();
          }
          throw new Error('fail ' + r.status);
        }
        return r.json();
      });
    }
    tryFetch()
      .then(function (items) {
        var took = Math.round(performance.now() - t0);
        render(Array.isArray(items) ? items : (items && items.hits) || [], q, took);
        pushRecent(q);
      })
      .catch(function (err) {
        if (err && err.name === 'AbortError') return;
        if (skeleton) skeleton.hidden = true;
        if (empty) {
          empty.hidden = false;
          empty.innerHTML = '<p class="hint">' + escapeHtml(t('unavailable', 'Search temporarily unavailable')) + '<' + '/p>';
        }
      });
  }

  function iconFor(h) {
    var cat = (h.category || h.type || 'post').toString();
    var letter = cat.charAt(0).toUpperCase();
    return letter || '\u00b7';
  }

  function render(items, q, tookMs) {
    if (skeleton) skeleton.hidden = true;
    hits = items || [];
    active = hits.length ? 0 : -1;
    if (meta) meta.hidden = false;
    if (countEl) {
      var resultsLabel = t('results', '{n} results').replace('{n}', String(hits.length));
      countEl.textContent = hits.length
        ? resultsLabel
        : t('no-results', 'No results');
    }
    if (latencyEl) latencyEl.textContent = (tookMs || 0) + ' ms';
    if (fullLink) {
      fullLink.hidden = !q;
      fullLink.href = '/' + langPrefix + '/?q=' + encodeURIComponent(q || '');
      fullLink.textContent = t('all', 'All results') + ' \u2192';
    }
    if (!hits.length) {
      if (list) { list.hidden = true; list.innerHTML = ''; }
      if (empty) {
        empty.hidden = false;
        empty.innerHTML =
          '<p class="hint">' +
          escapeHtml(t('no-results', 'No results')) +
          ' \u00ab' + escapeHtml(q) + '\u00bb<' + '/p>';
      }
      return;
    }
    if (empty) empty.hidden = true;
    if (list) list.hidden = false;
    list.innerHTML = hits.map(function (h, i) {
      var href = h.url || ('/' + (h.languageCode || langPrefix) + '/post/' + encodeURIComponent(h.slug || ''));
      var sub = [h.category, h.author].filter(Boolean).join(' \u00b7 ');
      var metaRight = h.publishedAt || h.date || h.relativeTime || '';
      return (
        '<li role="option">' +
        '<a class="site-search-item' + (i === active ? ' is-active' : '') + '" href="' + escapeAttr(href) + '" data-idx="' + i + '">' +
        '<span class="ss-icon" aria-hidden="true">' + escapeHtml(iconFor(h)) + '<' + '/span>' +
        '<span class="ss-main">' +
        '<span class="ss-title">' + highlight(h.title, q) + '<' + '/span>' +
        (sub ? '<span class="ss-sub">' + escapeHtml(sub) + '<' + '/span>' : '') +
        (h.summary ? '<span class="site-search-item-sum">' + highlight(h.summary, q) + '<' + '/span>' : '') +
        '<' + '/span>' +
        (metaRight ? '<span class="ss-meta">' + escapeHtml(metaRight) + '<' + '/span>' : '<span class="ss-meta"><' + '/span>') +
        '<' + '/a><' + '/li>'
      );
    }).join('');
    list.querySelectorAll('.site-search-item').forEach(function (a) {
      a.addEventListener('mouseenter', function () {
        active = Number(a.getAttribute('data-idx'));
        paintActive();
      });
      a.addEventListener('click', function () {
        pushRecent(input ? input.value.trim() : '');
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

  window.BlogSiteSearch = { open: open, close: close };
})();
