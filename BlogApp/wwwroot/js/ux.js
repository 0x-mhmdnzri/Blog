/**
 * User Experience: search, progress, font scale, history, infinite scroll, TOC, toasts
 * Theme switching is only via /Themes (theme-picker-btn) — no light/dark toggle.
 */
(function () {
  'use strict';

  function toast(message, type) {
    var host = document.getElementById('toast-host');
    if (!host) return;
    var el = document.createElement('div');
    el.className = 'toast' + (type ? ' ' + type : '');
    el.textContent = message;
    host.appendChild(el);
    setTimeout(function () {
      el.style.opacity = '0';
      el.style.transition = 'opacity .3s';
      setTimeout(function () { el.remove(); }, 300);
    }, 3200);
  }
  window.blogToast = toast;

  var overlay = document.getElementById('search-overlay');
  var searchInput = document.getElementById('search-input');
  var suggestBox = document.getElementById('search-suggest');
  var suggestTimer = null;
  var langPrefix = (document.documentElement.getAttribute('data-culture') || 'fa');

  function openSearch() {
    if (!overlay) return;
    overlay.hidden = false;
    document.body.style.overflow = 'hidden';
    if (searchInput) { searchInput.focus(); searchInput.select(); }
  }
  function closeSearch() {
    if (!overlay) return;
    overlay.hidden = true;
    document.body.style.overflow = '';
    if (suggestBox) suggestBox.innerHTML = '';
  }
  function escapeHtml(s) {
    return String(s || '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
  }
  function fetchSuggest(q) {
    if (!suggestBox || !q || q.length < 2) { if (suggestBox) suggestBox.innerHTML = ''; return; }
    var suggestUrl = '/' + langPrefix + '/Home/SearchSuggest?q=' + encodeURIComponent(q);
    fetch(suggestUrl)
      .then(function (r) { return r.json(); })
      .then(function (items) {
        if (!items || !items.length) {
          suggestBox.innerHTML = '<div class="small p-2" style="opacity:.7">نتیجه‌ای یافت نشد</div>';
          return;
        }
        suggestBox.innerHTML = items.map(function (it) {
          var href = it.url || ('/' + (it.languageCode || langPrefix) + '/post/' + encodeURIComponent(it.slug));
          var sum = it.summary ? '<div class="s-sum" dir="auto">' + escapeHtml(it.summary).slice(0, 100) + '</div>' : '';
          return '<a role="option" href="' + href + '"><div class="s-title" dir="auto">' + escapeHtml(it.title) + '</div>' + sum + '</a>';
        }).join('');
      }).catch(function () {});
  }

  function updateProgress() {
    var bar = document.getElementById('reading-progress');
    if (!bar) return;
    var article = document.querySelector('.post-article, article.post-article, .readme-content');
    var scrollTop = window.scrollY || document.documentElement.scrollTop;
    var pct = 0;
    if (article) {
      var start = article.offsetTop;
      var height = article.offsetHeight - window.innerHeight;
      pct = height > 0 ? Math.min(100, Math.max(0, ((scrollTop - start) / height) * 100)) : 0;
    } else {
      var docH = document.documentElement.scrollHeight - window.innerHeight;
      pct = docH > 0 ? Math.min(100, (scrollTop / docH) * 100) : 0;
    }
    bar.style.width = pct + '%';
    bar.setAttribute('aria-valuenow', String(Math.round(pct)));
  }

  function applyFontScale(scale) {
    scale = Math.min(1.4, Math.max(0.85, scale));
    document.documentElement.style.setProperty('--reader-font-scale', String(scale));
    try { localStorage.setItem('blog-font-scale', String(scale)); } catch (_) {}
  }
  function changeFont(delta) {
    var cur = parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--reader-font-scale')) || 1;
    applyFontScale(cur + delta);
  }

  var HISTORY_KEY = 'blog-reading-history';
  function trackHistory() {
    var article = document.querySelector('.post-article[data-analytics-post-id], article.post-article');
    if (!article) return;
    var id = article.getAttribute('data-analytics-post-id');
    var titleEl = document.querySelector('h1.post-title, h1');
    var title = titleEl ? titleEl.textContent.trim() : document.title;
    var url = location.pathname + location.search;
    try {
      var list = JSON.parse(localStorage.getItem(HISTORY_KEY) || '[]');
      list = list.filter(function (x) { return x.url !== url; });
      list.unshift({ id: id, title: title, url: url, at: Date.now() });
      if (list.length > 40) list = list.slice(0, 40);
      localStorage.setItem(HISTORY_KEY, JSON.stringify(list));
    } catch (_) {}
  }
  function renderHistoryPage() {
    var host = document.getElementById('reading-history-list');
    if (!host) return;
    try {
      var list = JSON.parse(localStorage.getItem(HISTORY_KEY) || '[]');
      if (!list.length) {
        host.innerHTML = '<p class="text-muted-dark">هنوز سابقه‌ای نیست.</p>';
        return;
      }
      host.innerHTML = list.map(function (it) {
        return '<div class="history-item"><a href="' + escapeHtml(it.url) + '" dir="auto">' +
          escapeHtml(it.title) + '</a><span class="ltr-field small" style="opacity:.6">' +
          new Date(it.at).toLocaleString() + '</span></div>';
      }).join('');
    } catch (_) {
      host.innerHTML = '<p class="text-muted-dark">خطا در خواندن سابقه</p>';
    }
  }
  window.blogClearHistory = function () {
    try { localStorage.removeItem(HISTORY_KEY); } catch (_) {}
    renderHistoryPage();
    toast('سابقه پاک شد', 'success');
  };

  function setupInfinite() {
    var grid = document.getElementById('posts-grid');
    var sentinel = document.getElementById('infinite-sentinel');
    if (!grid || !sentinel) return;
    var page = parseInt(grid.getAttribute('data-page') || '1', 10);
    var total = parseInt(grid.getAttribute('data-total-pages') || '1', 10);
    var loading = false;
    var status = document.getElementById('infinite-status');
    var obs = new IntersectionObserver(function (entries) {
      if (!entries[0].isIntersecting || loading || page >= total) return;
      loading = true;
      if (status) status.textContent = 'در حال بارگذاری…';
      var next = page + 1;
      var params = new URLSearchParams(window.location.search);
      params.set('page', String(next));
      params.set('partial', '1');
      fetch(window.location.pathname + '?' + params.toString(), { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(function (r) { return r.text(); })
        .then(function (html) {
          var tmp = document.createElement('div');
          tmp.innerHTML = html;
          var cards = tmp.querySelectorAll('#posts-grid > *, .col-md-6');
          if (cards.length) {
            cards.forEach(function (c) { grid.appendChild(c); });
            page = next;
            grid.setAttribute('data-page', String(page));
          } else {
            page = total;
          }
          if (status) status.textContent = page >= total ? 'پایان فهرست' : '';
          loading = false;
        })
        .catch(function () { loading = false; if (status) status.textContent = ''; });
    }, { rootMargin: '200px' });
    obs.observe(sentinel);
  }

  function setupToc() {
    var links = document.querySelectorAll('.toc-nav a[href^="#"]');
    if (!links.length) return;
    var map = [];
    links.forEach(function (a) {
      var id = a.getAttribute('href').slice(1);
      var el = document.getElementById(id);
      if (el) map.push({ a: a, el: el });
    });
    function onScroll() {
      var y = window.scrollY + 100;
      var active = null;
      map.forEach(function (m) { if (m.el.offsetTop <= y) active = m; });
      links.forEach(function (a) { a.classList.remove('active'); });
      if (active) active.a.classList.add('active');
    }
    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();
  }

  function setupBackTop() {
    var btn = document.getElementById('back-to-top');
    if (!btn) return;
    window.addEventListener('scroll', function () {
      btn.classList.toggle('show', (window.scrollY || 0) > 480);
    }, { passive: true });
    btn.addEventListener('click', function () { window.scrollTo({ top: 0, behavior: 'smooth' }); });
  }

  function bind() {
    document.querySelectorAll('[data-search-open]').forEach(function (btn) {
      btn.addEventListener('click', function (e) { e.preventDefault(); openSearch(); });
    });
    document.querySelectorAll('[data-search-close]').forEach(function (btn) {
      btn.addEventListener('click', function (e) { e.preventDefault(); closeSearch(); });
    });
    if (overlay) {
      overlay.addEventListener('click', function (e) { if (e.target === overlay) closeSearch(); });
    }
    if (searchInput) {
      searchInput.addEventListener('input', function () {
        clearTimeout(suggestTimer);
        var q = searchInput.value.trim();
        suggestTimer = setTimeout(function () { fetchSuggest(q); }, 220);
      });
      searchInput.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') closeSearch();
        if (e.key === 'Enter') {
          e.preventDefault();
          var q = searchInput.value.trim();
          if (q) location.href = '/' + langPrefix + '/?q=' + encodeURIComponent(q);
        }
      });
    }
    document.querySelectorAll('[data-font-delta]').forEach(function (btn) {
      btn.addEventListener('click', function () {
        changeFont(parseFloat(btn.getAttribute('data-font-delta') || '0'));
      });
    });
    document.addEventListener('keydown', function (e) {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault(); openSearch();
      }
      if (e.key === 'Escape') closeSearch();
    });

    window.addEventListener('scroll', updateProgress, { passive: true });
    updateProgress();
    trackHistory();
    renderHistoryPage();
    setupInfinite();
    setupToc();
    setupBackTop();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bind);
  else bind();
})();
