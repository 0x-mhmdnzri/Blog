/**
 * Admin + AdminAnalytics live data: listens to SSE from _AdminLayout
 * and polls REST snapshots to keep KPIs accurate.
 */
(function () {
  'use strict';

  function setText(id, val) {
    var el = document.getElementById(id);
    if (el) el.textContent = val;
  }

  function rangeFromQuery(def) {
    var m = location.search.match(/[?&]range=(\d+)/);
    return m ? parseInt(m[1], 10) : (def || 30);
  }

  function applyAdminSnapshot(s) {
    if (!s || !s.ok) return;
    setText('kpiViewsToday', s.viewsToday);
    setText('kpiViewsRange', s.viewsRange);
    setText('kpiViewsTotal', s.viewsTotal);
    setText('kpiPending', s.pending);
    setText('kpiApproved', s.approved);
    setText('kpiRejected', s.rejected);
    var pendingCard = document.getElementById('kpiPendingCard');
    if (pendingCard) {
      pendingCard.classList.toggle('kpi-danger', (s.pending || 0) > 0);
      pendingCard.classList.toggle('kpi-good', (s.pending || 0) <= 0);
    }
    if (window.__adminViewsChart && Array.isArray(s.series) && s.series.length) {
      window.__adminViewsChart.data.labels = s.series.map(function (p) { return p.label; });
      window.__adminViewsChart.data.datasets[0].data = s.series.map(function (p) { return p.value; });
      window.__adminViewsChart.update('none');
    }
    if (window.__adminCommentChart) {
      window.__adminCommentChart.data.datasets[0].data = [s.approved, s.pending, s.rejected];
      window.__adminCommentChart.update('none');
    }
    if (Array.isArray(s.topPosts)) {
      s.topPosts.forEach(function (p) {
        var row = document.querySelector('#topPostsBody tr[data-slug="' + CSS.escape(p.slug) + '"]');
        if (!row) return;
        var totalEl = row.querySelector('[data-total-views]');
        var rangeEl = row.querySelector('[data-range-views]');
        if (totalEl) totalEl.textContent = p.views;
        if (rangeEl) rangeEl.textContent = p.rangeViews;
      });
    }
  }

  function fetchAdminSnapshot() {
    if (!document.getElementById('kpiViewsToday')) return;
    var range = rangeFromQuery(30);
    fetch('/Admin/LiveSnapshot?range=' + range, {
      headers: { Accept: 'application/json' },
      credentials: 'same-origin',
      cache: 'no-store'
    })
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(applyAdminSnapshot)
      .catch(function () {});
  }

  function applyAnaSnapshot(s) {
    if (!s || !s.ok) return;
    setText('anaKpiViews', s.totalViews);
    setText('anaKpiUnique', s.uniqueVisitors);
    setText('anaKpiBounce', (s.bounceRatePercent != null ? s.bounceRatePercent : '\u2014') + '%');
    setText('anaKpiSessions', s.sessionCount);
    setText('anaKpiSearches', s.searchQueryCount);
    setText('anaKpiHeatmap', s.heatmapClickCount);
    if (window.viewsChartAna && Array.isArray(s.viewsByDay) && s.viewsByDay.length) {
      window.viewsChartAna.data.labels = s.viewsByDay.map(function (p) { return p.label; });
      window.viewsChartAna.data.datasets[0].data = s.viewsByDay.map(function (p) { return p.value; });
      window.viewsChartAna.update('none');
    }
  }

  function fetchAnaSnapshot() {
    if (!document.getElementById('anaKpiViews')) return;
    var range = rangeFromQuery(30);
    var root = document.querySelector('[data-ana-range]');
    if (root) range = parseInt(root.getAttribute('data-ana-range'), 10) || range;
    fetch('/AdminAnalytics/LiveSnapshot?range=' + range, {
      headers: { Accept: 'application/json' },
      credentials: 'same-origin',
      cache: 'no-store'
    })
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(applyAnaSnapshot)
      .catch(function () {});
  }

  function bump(id, delta) {
    var el = document.getElementById(id);
    if (!el) return;
    var n = parseInt(String(el.textContent).replace(/[^\d-]/g, ''), 10) || 0;
    el.textContent = n + delta;
  }

  window.addEventListener('admin-sse-message', function (e) {
    var d = e.detail;
    if (!d || !d.type) return;

    if (d.type === 'view') {
      bump('kpiViewsToday', 1);
      bump('kpiViewsRange', 1);
      bump('kpiViewsTotal', 1);
      bump('anaKpiViews', 1);
      if (window.__adminViewsChart) {
        var data = window.__adminViewsChart.data.datasets[0].data;
        if (data && data.length) {
          data[data.length - 1] = (Number(data[data.length - 1]) || 0) + 1;
          window.__adminViewsChart.update('none');
        }
      }
      if (window.viewsChartAna) {
        var d2 = window.viewsChartAna.data.datasets[0].data;
        if (d2 && d2.length) {
          d2[d2.length - 1] = (Number(d2[d2.length - 1]) || 0) + 1;
          window.viewsChartAna.update('none');
        }
      }
      clearTimeout(window.__liveSnapT);
      window.__liveSnapT = setTimeout(function () {
        fetchAdminSnapshot();
        fetchAnaSnapshot();
      }, 1500);
    }

    if (d.type === 'search') {
      bump('anaKpiSearches', 1);
      clearTimeout(window.__liveSnapT);
      window.__liveSnapT = setTimeout(fetchAnaSnapshot, 1200);
    }

    if (d.type === 'heatmap') {
      bump('anaKpiHeatmap', 1);
      clearTimeout(window.__liveSnapT);
      window.__liveSnapT = setTimeout(fetchAnaSnapshot, 1200);
    }
  });

  window.addEventListener('admin-sse-open', function () {
    fetchAdminSnapshot();
    fetchAnaSnapshot();
  });

  function boot() {
    fetchAdminSnapshot();
    fetchAnaSnapshot();
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
  setInterval(boot, 25000);
})();

/* Load self from layout if missing — and SSE reconnect fallback */
(function () {
  if (!document.querySelector('script[src*="admin-live"]') && !window.__adminLiveBooted) {
    /* already executing as admin-live */
  }
  window.__adminLiveBooted = true;
  var indicator = document.getElementById('liveIndicator');
  function setLive(on) {
    if (!indicator) return;
    indicator.classList.toggle('live-on', !!on);
    indicator.classList.toggle('live-off', !on);
  }
  var tried = false;
  setTimeout(function () {
    if (tried) return;
    if (indicator && indicator.classList.contains('live-on')) return;
    tried = true;
    var retry = 1500;
    function connect() {
      var es = new EventSource('/Admin/Stream');
      es.onopen = function () {
        setLive(true);
        retry = 1500;
        window.dispatchEvent(new CustomEvent('admin-sse-open'));
      };
      es.onerror = function () {
        setLive(false);
        try { es.close(); } catch (_) {}
        setTimeout(connect, retry);
        retry = Math.min(retry * 1.6, 20000);
      };
      es.onmessage = function (e) {
        setLive(true);
        try {
          var data = JSON.parse(e.data);
          window.dispatchEvent(new CustomEvent('admin-sse-message', { detail: data }));
        } catch (_) {}
      };
    }
    connect();
  }, 8000);
})();
