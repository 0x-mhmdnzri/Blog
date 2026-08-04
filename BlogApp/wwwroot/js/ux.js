/**
 * User Experience: reading progress, font scale, history, infinite scroll, toasts
 * Public Spotlight search lives in site-search.js
 */
(function () {
  'use strict';

  function toast(message, type) {
    var host = document.getElementById('toast-host');
    if (window.ToastifyStack) {
      var kind = type === 'success' ? 'success'
        : (type === 'error' || type === 'danger') ? 'error' : 'info';
      return ToastifyStack.push({ title: message, kind: kind, duration: 3200, linkUrl: null });
    }
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

  function escapeHtml(s) {
    return String(s || '')
      .replace(new RegExp('&','g'), String.fromCharCode(38) + 'amp;')
      .replace(new RegExp('<','g'), String.fromCharCode(38) + 'lt;')
      .replace(new RegExp('>','g'), String.fromCharCode(38) + 'gt;')
      .replace(new RegExp('"','g'), String.fromCharCode(38) + 'quot;');
  }

  function updateProgress() {
    var bar = document.getElementById('reading-progress');
    if (!bar) return;
    var article = document.querySelector('.post-article, article.post-article');
    var bodyIsland = document.querySelector('.post-body-island, .readme-content.post-body-island');
    if (!article && !bodyIsland) {
      bar.style.width = '0%';
      bar.setAttribute('aria-valuenow', '0');
      bar.classList.remove('is-complete', 'is-active');
      return;
    }
    var target = bodyIsland || article;
    var rect = target.getBoundingClientRect();
    var pageTop = window.pageYOffset || document.documentElement.scrollTop || 0;
    var start = rect.top + pageTop;
    var end = start + target.offsetHeight;
    var viewport = window.innerHeight || document.documentElement.clientHeight;
    var scrollable = end - start - viewport;
    var pct;
    if (scrollable <= 24) {
      var bottomVisible = rect.bottom <= viewport * 0.92;
      pct = bottomVisible || pageTop + viewport >= end - 8 ? 100 : Math.min(100, Math.max(0, (pageTop / Math.max(end, 1)) * 100));
      if (rect.top > viewport) pct = 0;
      if (rect.bottom < 0) pct = 100;
    } else {
      pct = Math.min(100, Math.max(0, ((pageTop + viewport * 0.15 - start) / scrollable) * 100));
    }
    bar.style.width = pct + '%';
    bar.setAttribute('aria-valuenow', String(Math.round(pct)));
    bar.classList.toggle('is-active', pct > 0.5);
    bar.classList.toggle('is-complete', pct >= 99.5);
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
        host.innerHTML = '<p class="text-muted-dark">هنوز سابقه‌ای نیست.<' + '/p>';
        return;
      }
      host.innerHTML = list.map(function (it) {
        return '<div class="history-item"><a href="' + escapeHtml(it.url) + '" dir="auto">' +
          escapeHtml(it.title) + '<' + '/a><span class="ltr-field small" style="opacity:.6">' +
          new Date(it.at).toLocaleString() + '<' + '/span><' + '/div>';
      }).join('');
    } catch (_) {
      host.innerHTML = '<p class="text-muted-dark">خطا در خواندن سابقه<' + '/p>';
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
    var retries = 0;
    var MAX_RETRIES = 3;
    var status = document.getElementById('infinite-status');
    var abortCtrl = null;
    function setStatus(msg, isError) {
      if (!status) return;
      status.textContent = msg || '';
      status.classList.toggle('is-error', !!isError);
      status.classList.toggle('is-loading', msg && !isError && msg.indexOf('بارگذاری') !== -1);
    }
    function appendCards(html) {
      var tmp = document.createElement('div');
      tmp.innerHTML = html.trim();
      var nodes = tmp.querySelectorAll('#posts-grid > *');
      if (!nodes.length) nodes = tmp.querySelectorAll(':scope > .col-md-6, :scope > .col-lg-4, :scope > [class*="col-"]');
      if (!nodes.length && tmp.children.length) nodes = tmp.children;
      var count = 0;
      Array.prototype.forEach.call(nodes, function (c) {
        grid.appendChild(c);
        count++;
      });
      return count;
    }
    function loadNext() {
      if (loading || page >= total) return;
      loading = true;
      setStatus('در حال بارگذاری…');
      if (abortCtrl) try { abortCtrl.abort(); } catch (_) {}
      abortCtrl = typeof AbortController !== 'undefined' ? new AbortController() : null;
      var next = page + 1;
      var params = new URLSearchParams(window.location.search);
      params.set('page', String(next));
      params.set('partial', '1');
      var url = window.location.pathname + '?' + params.toString();
      var timeoutId = setTimeout(function () {
        if (abortCtrl) try { abortCtrl.abort(); } catch (_) {}
      }, 20000);
      fetch(url, {
        headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'text/html' },
        credentials: 'same-origin',
        signal: abortCtrl ? abortCtrl.signal : undefined,
        cache: 'no-store'
      })
        .then(function (r) {
          if (!r.ok) throw new Error('HTTP ' + r.status);
          return r.text();
        })
        .then(function (html) {
          clearTimeout(timeoutId);
          var added = appendCards(html);
          if (added > 0) {
            page = next;
            grid.setAttribute('data-page', String(page));
            retries = 0;
            setStatus(page >= total ? 'پایان فهرست' : '');
          } else {
            page = total;
            grid.setAttribute('data-page', String(page));
            setStatus('پایان فهرست');
          }
          loading = false;
        })
        .catch(function (err) {
          clearTimeout(timeoutId);
          loading = false;
          if (err && err.name === 'AbortError') {
            setStatus('زمان بارگذاری تمام شد — دوباره تلاش کنید', true);
          } else {
            setStatus('خطا در بارگذاری شبکه', true);
          }
          retries++;
          if (retries <= MAX_RETRIES) {
            var delay = Math.min(8000, 1000 * Math.pow(2, retries - 1));
            setTimeout(function () {
              if (page < total) loadNext();
            }, delay);
          } else if (status) {
            status.innerHTML = 'بارگذاری ناموفق. <button type="button" class="btn btn-ghost btn-sm" data-infinite-retry>تلاش مجدد<' + '/button>';
            var btn = status.querySelector('[data-infinite-retry]');
            if (btn) btn.addEventListener('click', function () {
              retries = 0;
              loadNext();
            });
          }
        });
    }
    if (page >= total) {
      setStatus(total > 1 ? 'پایان فهرست' : '');
      return;
    }
    var obs = new IntersectionObserver(function (entries) {
      if (!entries[0] || !entries[0].isIntersecting) return;
      loadNext();
    }, { rootMargin: '320px 0px', threshold: 0 });
    obs.observe(sentinel);
  }

  function setupBackTop() {
    var btn = document.getElementById('back-to-top');
    if (!btn) return;
    function sync() {
      var y = window.pageYOffset || document.documentElement.scrollTop || 0;
      var show = y > 480;
      if ('hidden' in btn) btn.hidden = !show;
      if (show) btn.classList.add('show');
      else btn.classList.remove('show');
    }
    window.addEventListener('scroll', sync, { passive: true });
    sync();
    btn.addEventListener('click', function () {
      try {
        window.scrollTo({ top: 0, behavior: 'smooth' });
      } catch (_) {
        window.scrollTo(0, 0);
      }
    });
  }

  function bind() {
    document.querySelectorAll('[data-font-delta]').forEach(function (btn) {
      btn.addEventListener('click', function () {
        changeFont(parseFloat(btn.getAttribute('data-font-delta') || '0'));
      });
    });
    var progressRaf = null;
    window.addEventListener('scroll', function () {
      if (progressRaf) return;
      progressRaf = requestAnimationFrame(function () {
        progressRaf = null;
        updateProgress();
      });
    }, { passive: true });
    window.addEventListener('resize', updateProgress, { passive: true });
    updateProgress();
    trackHistory();
    renderHistoryPage();
    setupInfinite();
    setupBackTop();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bind);
  else bind();
})();
