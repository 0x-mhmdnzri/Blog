(function () {
  var root = document.getElementById('mailInbox');
  if (!root) return;
  var csrf = (document.querySelector('input[name="__RequestVerificationToken"]') || {}).value
    || (document.querySelector('meta[name="csrf-token"]') || {}).content || '';

  function post(url, body) {
    var form = new URLSearchParams();
    Object.keys(body || {}).forEach(function (k) { form.set(k, body[k]); });
    if (csrf) form.set('__RequestVerificationToken', csrf);
    return fetch(url, {
      method: 'POST', credentials: 'same-origin',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest', 'X-CSRF-TOKEN': csrf },
      body: form.toString()
    }).then(function (r) { return r.json().catch(function () { return { ok: r.ok }; }); });
  }

  root.addEventListener('click', function (e) {
    var starBtn = e.target.closest('[data-star]');
    if (starBtn) {
      e.preventDefault(); e.stopPropagation();
      post('/Notifications/ToggleStar', { id: starBtn.getAttribute('data-star') }).then(function (res) { if (res && res.ok) location.reload(); });
      return;
    }
    var act = e.target.closest('[data-action]');
    if (!act) return;
    e.preventDefault();
    var map = { star: '/Notifications/ToggleStar', archive: '/Notifications/Archive', unarchive: '/Notifications/Unarchive', read: '/Notifications/MarkRead', unread: '/Notifications/MarkUnread', 'delete': '/Notifications/Delete' };
    var url = map[act.getAttribute('data-action')];
    if (!url) return;
    post(url, { id: act.getAttribute('data-id') }).then(function (res) {
      if (res && typeof res.unread === 'number' && window.BlogNotifications) window.BlogNotifications.setBadge(res.unread);
      location.href = '/Notifications?folder=' + (root.getAttribute('data-folder') || 'inbox');
    });
  });

  var markAll = document.getElementById('mailMarkAll');
  if (markAll) markAll.addEventListener('click', function () {
    post('/Notifications/MarkAllRead', {}).then(function () {
      if (window.BlogNotifications) window.BlogNotifications.setBadge(0);
      location.reload();
    });
  });

  try {
    var es = new EventSource('/Notifications/Stream');
    es.onmessage = function (e) {
      var data; try { data = JSON.parse(e.data); } catch { return; }
      if (data.type === 'notification') {
        var list = document.getElementById('mailList');
        if (!list) return;
        var empty = list.querySelector('.mail-empty'); if (empty) empty.remove();
        var a = document.createElement('a');
        a.className = 'mail-row is-unread';
        a.href = '?folder=' + (root.getAttribute('data-folder') || 'inbox') + '&id=' + (data.id || '');
        a.setAttribute('data-id', data.id || '');
        a.innerHTML = '<button type="button" class="mail-star" data-star="' + (data.id || '') + '">☆</button><div class="mail-row-main"><div class="mail-row-top"><span class="mail-row-kind">' + (data.kind || '') + '</span><time class="mail-row-time">now</time></div><div class="mail-row-title"></div>' + (data.body ? '<div class="mail-row-preview"></div>' : '') + '</div>';
        a.querySelector('.mail-row-title').textContent = data.title || '';
        var prev = a.querySelector('.mail-row-preview'); if (prev) prev.textContent = data.body || '';
        list.insertBefore(a, list.firstChild);
        var uf = document.getElementById('mailUnreadFolder');
        if (uf) uf.textContent = String((parseInt(uf.textContent, 10) || 0) + 1);
      }
      if (data.type === 'unread' && typeof data.count === 'number') {
        var uf2 = document.getElementById('mailUnreadFolder');
        if (uf2) uf2.textContent = String(data.count);
      }
    };
  } catch (err) {}
})();
