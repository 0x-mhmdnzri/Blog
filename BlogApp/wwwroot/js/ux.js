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
    return String(s || '').replace(/&/g,'&').replace(/</g,'<').replace(/>/g,'>').replace(/"/g,'"');
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

  function bindInfiniteScroll() {
    var feed = document.getElementById('blog-feed');
    if (!feed || feed.getAttribute('data-infinite') !== '1') return;
    var page = parseInt(feed.getAttribute('data-page') || '1', 10);
    var total = parseInt(feed.getAttribute('data-total-pages') || '1', 10);
    var loading = false;
    var sentinel = document.getElementById('infinite-sentinel');
    if (!sentinel || page >= total) return;

    var obs = new IntersectionObserver(function (entries) {
      if (!entries[0].isIntersecting || loading || page >= total) return;
      loading = true;
      page += 1;
      var params = new URLSearchParams(window.location.search);
      params.set('page', String(page));
      params.set('partial', '1');
      fetch(window.location.pathname + '?' + params.toString(), { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(function (r) { return r.text(); })
        .then(function (html) {
          if (html && html.trim()) {
            feed.insertAdjacentHTML('beforeend', html);
            feed.setAttribute('data-page', String(page));
          }
          if (page >= total && sentinel) sentinel.remove();
        })
        .catch(function () {})
        .finally(function () { loading = false; });
    }, { rootMargin: '200px' });
    obs.observe(sentinel);
  }

  function bind() {
    document.querySelectorAll('[data-open-search]').forEach(function (btn) {
      btn.addEventListener('click', function (e) { e.preventDefault(); openSearch(); });
    });
    document.querySelectorAll('[data-close-search]').forEach(function (btn) {
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
        e.preventDefault();
        openSearch();
      }
      if (e.key === 'Escape') closeSearch();
    });
    try {
      var saved = parseFloat(localStorage.getItem('blog-font-scale') || '1');
      if (saved && saved !== 1) applyFontScale(saved);
    } catch (_) {}
    window.addEventListener('scroll', updateProgress, { passive: true });
    updateProgress();
    trackHistory();
    bindInfiniteScroll();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bind);
  else bind();
})();
