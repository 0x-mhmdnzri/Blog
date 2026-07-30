/**
 * Notification bell — SSE realtime + dropdown.
 * Chrome strings come from data-i18n-* on #notifBell (seeded by parrot T[]).
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
  }

  function escapeHtml(s) {
    return String(s || '')
      .replace(/&/g, '&')
      .replace(/</g, '<')
      .replace(/>/g, '>')
      .replace(/"/g, '"');
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
        var cur = parseInt(badge && !badge.hidden ? badge.textContent : '0', 10) || 0;
        if (badge && badge.textContent === '99+') return;
        setBadge(cur + 1);
        if (root.querySelector('.dropdown-menu.show')) loadRecent();
      }
    };
  } catch (err) {
    console.warn('Notification SSE unavailable', err);
  }
})();
