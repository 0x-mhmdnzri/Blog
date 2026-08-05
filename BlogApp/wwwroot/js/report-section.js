/**
 * Report modal popup — open/close, focus trap, login-return auto-open.
 * Report queues as pending for owner / SuperAdmin; never deletes the post.
 */
(function () {
  'use strict';

  function qs(sel, root) {
    return (root || document).querySelector(sel);
  }

  var lastFocus = null;

  function openModal() {
    var modal = qs('[data-report-modal]');
    var trigger = qs('[data-report-open]');
    if (!modal) return;
    lastFocus = document.activeElement;
    modal.hidden = false;
    modal.setAttribute('aria-hidden', 'false');
    document.body.classList.add('report-modal-open');
    if (trigger) trigger.setAttribute('aria-expanded', 'true');

    var focusable = modal.querySelector(
      'input:checked, input[type="radio"], textarea, a.report-btn, button.report-close, button'
    );
    if (focusable) {
      try { focusable.focus({ preventScroll: true }); } catch (_) {}
    }
  }

  function closeModal() {
    var modal = qs('[data-report-modal]');
    var trigger = qs('[data-report-open]');
    if (!modal || modal.hidden) return;
    modal.hidden = true;
    modal.setAttribute('aria-hidden', 'true');
    document.body.classList.remove('report-modal-open');
    if (trigger) trigger.setAttribute('aria-expanded', 'false');
    if (lastFocus && typeof lastFocus.focus === 'function') {
      try { lastFocus.focus({ preventScroll: true }); } catch (_) {}
    }
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
    var modal = qs('[data-report-modal]');
    if (!section && !modal) return;

    if (section && section.getAttribute('data-report-ok') === '1') {
      var toast = qs('[data-report-toast]', section);
      if (toast) {
        try { toast.scrollIntoView({ behavior: 'smooth', block: 'nearest' }); } catch (_) {}
      }
    }

    document.addEventListener('click', function (e) {
      var t = e.target;
      if (t.closest && t.closest('[data-report-open], [data-report-open-legacy]')) {
        e.preventDefault();
        openModal();
        return;
      }
      if (t.closest && t.closest('[data-report-close]')) {
        e.preventDefault();
        closeModal();
      }
    });

    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') {
        var m = qs('[data-report-modal]');
        if (m && !m.hidden) {
          e.preventDefault();
          closeModal();
        }
      }
    });

    var ta = qs('#report-details');
    var counter = qs('[data-report-count]');
    if (ta && counter) {
      var sync = function () {
        counter.textContent = (ta.value || '').length + ' / 1000';
      };
      ta.addEventListener('input', sync);
      sync();
    }

    var form = qs('[data-report-form]');
    if (form) {
      form.addEventListener('submit', function (e) {
        var reasonErr = qs('[data-report-reason-error]');
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
          var reasonErr = qs('[data-report-reason-error]');
          if (reasonErr) reasonErr.hidden = true;
        });
      });
    }

    if (shouldAutoOpen() && (!section || section.getAttribute('data-report-ok') !== '1')) {
      openModal();
      stripReportQuery();
    }
  }

  window.BlogReport = {
    open: openModal,
    close: closeModal
  };

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bind);
  else bind();
})();
