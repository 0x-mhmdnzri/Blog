/**
 * Post report panel — open/close, login-return auto-open,
 * reason validation, char counter. Report never deletes the post;
 * it queues as pending for owner / SuperAdmin.
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

    if (section.getAttribute('data-report-ok') === '1') {
      var toast = qs('[data-report-toast]', section);
      if (toast) {
        try { toast.scrollIntoView({ behavior: 'smooth', block: 'nearest' }); } catch (_) {}
      }
    }

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
      form.addEventListener('submit', function (e) {
        var reasonErr = qs('[data-report-reason-error]', section);
        var checked = form.querySelector('input[name="Reason"]:checked');
        if (!checked) {
          e.preventDefault();
          if (reasonErr) reasonErr.hidden = false;
          var firstRadio = form.querySelector('input[name="Reason"]');
          if (firstRadio) {
            try { firstRadio.focus({ preventScroll: true }); } catch (_) {}
          }
          return;
        }
        if (reasonErr) reasonErr.hidden = true;

        var btn = form.querySelector('[data-report-submit], button[type="submit"]');
        if (btn) {
          btn.disabled = true;
          btn.setAttribute('aria-busy', 'true');
        }
      });

      form.querySelectorAll('input[name="Reason"]').forEach(function (r) {
        r.addEventListener('change', function () {
          var reasonErr = qs('[data-report-reason-error]', section);
          if (reasonErr) reasonErr.hidden = true;
        });
      });
    }

    if (shouldAutoOpen() && section.getAttribute('data-report-ok') !== '1') {
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
