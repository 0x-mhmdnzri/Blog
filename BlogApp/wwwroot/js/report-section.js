/**
 * Post report panel — open/close, login-return auto-open, char counter
 */
(function () {
  'use strict';

  function qs(sel, root) {
    return (root || document).querySelector(sel);
  }

  function openPanel(section) {
    var box = qs('#reportBox', section) || qs('#reportBox');
    var trigger = qs('[data-report-open]', section);
    if (!box) return;
    box.hidden = false;
    if (trigger) trigger.setAttribute('aria-expanded', 'true');
    var focusable = box.querySelector('input:checked, input[type="radio"], textarea, a.report-btn, button');
    if (focusable) {
      try { focusable.focus({ preventScroll: true }); } catch (_) {}
    }
    box.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  function closePanel(section) {
    var box = qs('#reportBox', section) || qs('#reportBox');
    var trigger = qs('[data-report-open]', section);
    if (!box) return;
    box.hidden = true;
    if (trigger) trigger.setAttribute('aria-expanded', 'false');
  }

  function shouldAutoOpen() {
    try {
      var params = new URLSearchParams(window.location.search);
      if (params.get('report') === '1') return true;
      if (window.location.hash === '#report') return true;
    } catch (_) {}
    return false;
  }

  function stripReportQuery() {
    try {
      var url = new URL(window.location.href);
      if (url.searchParams.has('report')) {
        url.searchParams.delete('report');
        var next = url.pathname + (url.searchParams.toString() ? '?' + url.searchParams.toString() : '') + (url.hash || '');
        window.history.replaceState({}, '', next || url.pathname);
      }
    } catch (_) {}
  }

  function bind() {
    var section = qs('[data-report-section]');
    if (!section) return;

    section.addEventListener('click', function (e) {
      var t = e.target;
      if (t.closest && t.closest('[data-report-open]')) {
        e.preventDefault();
        openPanel(section);
        return;
      }
      if (t.closest && t.closest('[data-report-close]')) {
        e.preventDefault();
        closePanel(section);
      }
    });

    document.querySelectorAll('[data-report-open-legacy], [onclick*="reportBox"]').forEach(function (el) {
      el.addEventListener('click', function (e) {
        e.preventDefault();
        openPanel(section);
      });
    });

    var ta = qs('#report-details', section);
    var counter = qs('[data-report-count]', section);
    if (ta && counter) {
      var sync = function () {
        counter.textContent = (ta.value || '').length + ' / 1000';
      };
      ta.addEventListener('input', sync);
      sync();
    }

    var form = qs('[data-report-form]', section);
    if (form) {
      form.addEventListener('submit', function () {
        var btn = form.querySelector('button[type="submit"]');
        if (btn) {
          btn.disabled = true;
          btn.setAttribute('aria-busy', 'true');
        }
      });
    }

    if (shouldAutoOpen()) {
      openPanel(section);
      stripReportQuery();
    }
  }

  window.BlogReport = {
    open: function () {
      var section = qs('[data-report-section]');
      if (section) openPanel(section);
    }
  };

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bind);
  else bind();
})();
