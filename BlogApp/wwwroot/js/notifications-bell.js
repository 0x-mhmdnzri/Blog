/**
 * Notification bell — SSE realtime + dropdown + ToastifyStack banners.
 * Single path: SSE → iPhone stack toast → click navigates / dismiss keeps in hub.
 */
(function () {
  var root = document.getElementById('notifBell');
  if (!root) return;

  var badge = document.getElementById('notifBadge');
  var list = document.getElementById('notifList');
  var markAllBtn = document.getElementById('notifMarkAll');
  var tokenInput = root.querySelector('input[name="__RequestVerificationToken"]');
  var csrf = tokenInput ? tokenInput.value : '';

  function t(attr, fallback) {
    return root.getAttribute(attr) || fallback;
  }

  function setBadge(n) {
    if (!badge) return;
    n = Math.max(0, n | 0);
    if (n <= 0) {
      badge.hidden = true;
      badge.textContent = '0';
    } else {
      badge.hidden = false;
      badge.textContent = n > 99 ? '99+' : String(n);
    }
    // Expose for other modules
    root.dataset.unread = String(n);
  }

  function getBadgeCount() {
    if (!badge || badge.hidden) return 0;
    var txt = badge.textContent || '0';
    if (txt === '99+') return 99;
    return parseInt(txt, 10) || 0;
  }

  function escapeHtml(s) {
    return String(s || '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  function renderItems(items) {
    if (!list) return;
    if (!items || !items.length) {
      list.innerHTML = '<div class="notif-empty text-muted-dark small p-3">' +
        escapeHtml(t('data-i18n-empty', 'No notifications')) + '</div>';
      return;
    }
    list.innerHTML = items.map(function (it) {
      var cls = it.isRead ? 'notif-item is-read' : 'notif-item is-unread';
      var href = it.linkUrl || '/Notifications';
      return (
        '<a class="' + cls + '" href="' + escapeHtml(href) + '" data-id="' + it.id + '">' +
          '<div class="notif-item-title">' + escapeHtml(it.title) + '</div>' +
          (it.body ? '<div class="notif-item-body">' + escapeHtml(it.body) + '</div>' : '') +
        '</a>'
      );
    }).join('');

    list.querySelectorAll('.notif-item').forEach(function (el) {
      el.addEventListener('click', function () {
        var id = el.getAttribute('data-id');
        if (id) markRead(id);
      });
    });
  }

  function prependItem(data) {
    if (!list) return;
    var empty = list.querySelector('.notif-empty');
    if (empty) empty.remove();

    var href = data.linkUrl || '/Notifications';
    var a = document.createElement('a');
    a.className = 'notif-item is-unread';
    a.href = href;
    a.setAttribute('data-id', data.id || '');
    a.innerHTML =
      '<div class="notif-item-title">' + escapeHtml(data.title || '') + '</div>' +
      (data.body ? '<div class="notif-item-body">' + escapeHtml(data.body) + '</div>' : '');
    a.addEventListener('click', function () {
      if (data.id) markRead(data.id);
    });
    list.insertBefore(a, list.firstChild);
  }

  function loadRecent() {
    fetch('/Notifications/Recent?take=12', { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
      .then(function (r) { return r.json(); })
      .then(function (data) {
        setBadge(data.unread || 0);
        renderItems(data.items || []);
      })
      .catch(function () {
        if (list) {
          list.innerHTML = '<div class="notif-empty small p-3">' +
            escapeHtml(t('data-i18n-failed', 'Failed to load')) + '</div>';
        }
      });
  }

  function markRead(id) {
    var body = new URLSearchParams();
    body.set('id', id);
    body.set('__RequestVerificationToken', csrf);
    fetch('/Notifications/MarkRead', {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
        'Accept': 'application/json',
        'X-Requested-With': 'XMLHttpRequest',
        'X-CSRF-TOKEN': csrf
      },
      body: body.toString()
    })
      .then(function (r) { return r.json(); })
      .then(function (data) {
        if (data && typeof data.unread === 'number') setBadge(data.unread);
        loadRecent();
      })
      .catch(function () { /* ignore */ });
  }

  function markAll() {
    var body = new URLSearchParams();
    body.set('__RequestVerificationToken', csrf);
    fetch('/Notifications/MarkAllRead', {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
        'Accept': 'application/json',
        'X-Requested-With': 'XMLHttpRequest',
        'X-CSRF-TOKEN': csrf
      },
      body: body.toString()
    })
      .then(function (r) { return r.json(); })
      .then(function () {
        setBadge(0);
        loadRecent();
      })
      .catch(function () { /* ignore */ });
  }

  if (markAllBtn) markAllBtn.addEventListener('click', function (e) {
    e.preventDefault();
    markAll();
  });

  var btn = document.getElementById('notifBellBtn');
  if (btn) {
    btn.addEventListener('show.bs.dropdown', loadRecent);
  }

  fetch('/Notifications/UnreadCount', { credentials: 'same-origin' })
    .then(function (r) { return r.json(); })
    .then(function (d) { setBadge(d.count || 0); })
    .catch(function () {});

  /** When toast is dismissed without click → ensure hub list stays in sync. */
  function onToastDismissedToHub(data) {
    // Already persisted server-side; refresh dropdown if open, pulse badge
    var menuOpen = !!(root.querySelector('.dropdown-menu.show'));
    if (menuOpen) loadRecent();
    else if (data) prependItem(data); // soft-cache until next open

    // subtle badge attention
    if (badge && !badge.hidden) {
      badge.style.transform = 'scale(1.15)';
      setTimeout(function () { badge.style.transform = ''; }, 220);
    }
  }

  function showNotificationToast(data) {
    if (!window.ToastifyStack) return;

    ToastifyStack.notify({
      notificationId: data.id,
      title: data.title || 'Notification',
      body: data.body || '',
      linkUrl: data.linkUrl || '/Notifications',
      duration: 6000,
      appLabel: 'Notification',
      onClick: function (toast) {
        // Mark read before navigation when we have an id
        if (toast.notificationId) markRead(toast.notificationId);
        return true; // allow default navigate
      },
      onDismiss: function (toast, reason) {
        // Not clicked — stays in hub; refresh badge path already done via SSE
        onToastDismissedToHub({
          id: toast.notificationId,
          title: toast.title,
          body: toast.body,
          linkUrl: toast.linkUrl,
          isRead: false
        });
      }
    });
  }

  try {
    var es = new EventSource('/Notifications/Stream');
    es.onmessage = function (e) {
      var data;
      try { data = JSON.parse(e.data); } catch { return; }

      if (data.type === 'unread' && typeof data.count === 'number') {
        setBadge(data.count);
        return;
      }

      if (data.type === 'notification') {
        var cur = getBadgeCount();
        if (!(badge && badge.textContent === '99+')) setBadge(cur + 1);

        showNotificationToast(data);

        if (root.querySelector('.dropdown-menu.show')) loadRecent();
        else prependItem({
          id: data.id,
          title: data.title,
          body: data.body,
          linkUrl: data.linkUrl,
          isRead: false
        });
      }
    };
  } catch (err) {
    console.warn('Notification SSE unavailable', err);
  }

  // Public hook for other modules
  window.BlogNotifications = {
    setBadge: setBadge,
    refresh: loadRecent,
    showToast: showNotificationToast
  };
})();
