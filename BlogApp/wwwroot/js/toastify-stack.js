/**
 * ToastifyStack — modular, single entry-point toast system.
 * iPhone-style stacked banners for realtime notifications + general toasts.
 *
 * API:
 *   ToastifyStack.push({ title, body, linkUrl, id, kind, duration, onClick, onDismiss })
 *   ToastifyStack.notify(payload)  // alias for kind:notification
 *   ToastifyStack.success / info / error(message, opts)
 *   ToastifyStack.clear()
 *
 * Events (document):
 *   toastify:click   { detail: toast }
 *   toastify:dismiss { detail: toast }  // auto or close — not clicked
 *   toastify:open    { detail: toast }
 */
(function (global) {
  'use strict';

  var HOST_ID = 'toastify-host';
  var MAX_VISIBLE = 4;
  var DEFAULT_DURATION = 5500;
  var reduceMotion = false;
  try {
    reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  } catch (_) {}

  var queue = [];
  var host = null;

  function ensureHost() {
    if (host && document.body.contains(host)) return host;
    host = document.getElementById(HOST_ID);
    if (!host) {
      host = document.createElement('div');
      host.id = HOST_ID;
      host.className = 'toastify-host';
      host.setAttribute('aria-live', 'polite');
      host.setAttribute('aria-relevant', 'additions');
      document.body.appendChild(host);
    }
    return host;
  }

  function escapeHtml(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  function iconFor(kind) {
    switch (kind) {
      case 'success': return '✓';
      case 'error':
      case 'danger': return '!';
      case 'info': return 'i';
      case 'notification':
      default: return '🔔';
    }
  }

  function emit(name, detail) {
    try {
      document.dispatchEvent(new CustomEvent(name, { detail: detail }));
    } catch (_) {}
  }

  function trimStack() {
    while (queue.length > MAX_VISIBLE) {
      var oldest = queue[0];
      if (oldest) dismiss(oldest.id, 'overflow');
      else break;
    }
  }

  function dismiss(id, reason) {
    var idx = -1;
    for (var i = 0; i < queue.length; i++) {
      if (queue[i].id === id) { idx = i; break; }
    }
    if (idx < 0) return;
    var item = queue[idx];
    if (item._leaving) return;
    item._leaving = true;
    if (item._timer) clearTimeout(item._timer);

    var el = item.el;
    if (el) {
      el.classList.add('is-leaving');
      var remove = function () {
        if (el.parentNode) el.parentNode.removeChild(el);
      };
      if (reduceMotion) remove();
      else setTimeout(remove, 320);
    }

    queue.splice(idx, 1);

    if (reason !== 'click') {
      if (typeof item.onDismiss === 'function') {
        try { item.onDismiss(item, reason); } catch (_) {}
      }
      emit('toastify:dismiss', { toast: item, reason: reason || 'auto' });
    }
  }

  function navigate(item) {
    var url = item.linkUrl;
    if (!url) url = '/Notifications';
    // same-origin relative preferred
    try {
      if (url.charAt(0) === '/' || url.indexOf(window.location.origin) === 0) {
        window.location.href = url;
        return;
      }
    } catch (_) {}
    window.location.href = '/Notifications';
  }

  function push(opts) {
    opts = opts || {};
    var id = opts.id != null ? String(opts.id) : ('t-' + Date.now() + '-' + Math.random().toString(36).slice(2, 7));
    var kind = (opts.kind || opts.type || 'notification').toLowerCase();
    var duration = typeof opts.duration === 'number' ? opts.duration : DEFAULT_DURATION;
    if (duration < 0) duration = 0;

    var title = opts.title || opts.message || '';
    var body = opts.body || opts.text || '';
    if (!title && body) {
      title = body;
      body = '';
    }

    var item = {
      id: id,
      kind: kind,
      title: title,
      body: body,
      linkUrl: opts.linkUrl || opts.href || null,
      notificationId: opts.notificationId || opts.notifId || null,
      duration: duration,
      onClick: opts.onClick,
      onDismiss: opts.onDismiss,
      el: null,
      _timer: null,
      _leaving: false,
      _clicked: false
    };

    var h = ensureHost();
    var card = document.createElement('div');
    card.className = 'toastify-card';
    card.dataset.kind = kind;
    card.dataset.id = id;
    card.setAttribute('role', 'status');
    card.tabIndex = 0;

    var appLabel = opts.appLabel || (kind === 'notification' ? 'Notification' : 'Blog');
    card.innerHTML =
      '<div class="toastify-icon" aria-hidden="true">' + iconFor(kind) + '</div>' +
      '<div class="toastify-body">' +
        '<div class="toastify-app">' + escapeHtml(appLabel) + '</div>' +
        (title ? '<p class="toastify-title" dir="auto">' + escapeHtml(title) + '</p>' : '') +
        (body ? '<p class="toastify-text" dir="auto">' + escapeHtml(body) + '</p>' : '') +
      '</div>' +
      '<button type="button" class="toastify-close" aria-label="Dismiss">×</button>' +
      (duration > 0
        ? '<div class="toastify-progress" aria-hidden="true"><span style="animation-duration:' + duration + 'ms"></span></div>'
        : '');

    item.el = card;

    card.querySelector('.toastify-close').addEventListener('click', function (e) {
      e.preventDefault();
      e.stopPropagation();
      dismiss(id, 'close');
    });

    function handleActivate(e) {
      if (e && e.target && e.target.closest && e.target.closest('.toastify-close')) return;
      item._clicked = true;
      emit('toastify:click', { toast: item });
      if (typeof item.onClick === 'function') {
        try {
          var handled = item.onClick(item);
          if (handled === false) {
            dismiss(id, 'click');
            return;
          }
        } catch (_) {}
      }
      dismiss(id, 'click');
      navigate(item);
    }

    card.addEventListener('click', handleActivate);
    card.addEventListener('keydown', function (e) {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        handleActivate(e);
      }
      if (e.key === 'Escape') dismiss(id, 'close');
    });

    // Newest on top
    if (h.firstChild) h.insertBefore(card, h.firstChild);
    else h.appendChild(card);

    queue.push(item);
    trimStack();

    if (duration > 0) {
      item._timer = setTimeout(function () {
        dismiss(id, 'auto');
      }, duration);
    }

    emit('toastify:open', { toast: item });
    return id;
  }

  function notify(opts) {
    opts = opts || {};
    opts.kind = 'notification';
    opts.appLabel = opts.appLabel || 'Notification';
    return push(opts);
  }

  function simple(kind, message, opts) {
    opts = opts || {};
    opts.kind = kind;
    opts.title = opts.title || message;
    if (opts.body == null && opts.title !== message) opts.body = message;
    return push(opts);
  }

  function clear() {
    var ids = queue.map(function (q) { return q.id; });
    ids.forEach(function (id) { dismiss(id, 'clear'); });
  }

  var api = {
    push: push,
    notify: notify,
    success: function (m, o) { return simple('success', m, o); },
    info: function (m, o) { return simple('info', m, o); },
    error: function (m, o) { return simple('error', m, o); },
    danger: function (m, o) { return simple('danger', m, o); },
    clear: clear,
    dismiss: dismiss
  };

  global.ToastifyStack = api;

  // Compatibility: existing blogToast(message, type)
  global.blogToast = function (message, type) {
    var kind = type === 'success' ? 'success'
      : type === 'error' || type === 'danger' ? 'error'
      : type === 'info' ? 'info'
      : 'info';
    return api.push({ title: message, kind: kind, duration: 3200, linkUrl: null });
  };
})(typeof window !== 'undefined' ? window : this);
