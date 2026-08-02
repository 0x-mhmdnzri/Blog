(function(){if(window.__adminLiveBooted)return;var s=document.createElement('script');s.src='/js/admin-live.js';s.defer=true;document.head.appendChild(s);})();
(function () {
  var COOKIE = 'Blog.AdminSidebarLocked';
  var sidebar = document.getElementById('adminSidebar');
  var lockBtn = document.getElementById('sidebarLockBtn');
  var backdrop = document.getElementById('adminSidebarBackdrop');
  var toggleBtn = document.querySelector('[data-admin-toggle]');
  if (!sidebar) return;

  function getCookie(name) {
    var m = document.cookie.match(new RegExp('(?:^|; )' + name.replace(/([.$?*|{}()\[\]\\\/\+^])/g, '\\$1') + '=([^;]*)'));
    return m ? decodeURIComponent(m[1]) : '';
  }
  function setCookie(name, value, days) {
    var max = days ? '; max-age=' + (days * 86400) : '';
    document.cookie = name + '=' + encodeURIComponent(value) + max + '; path=/; SameSite=Lax';
  }

  function isLocked() {
    return getCookie(COOKIE) === '1';
  }

  function applyLockState() {
    var locked = isLocked();
    document.body.classList.toggle('sidebar-locked', locked);
    sidebar.classList.toggle('is-collapsed', !locked && !sidebar.classList.contains('is-open-mobile'));
    if (lockBtn) {
      lockBtn.setAttribute('aria-pressed', locked ? 'true' : 'false');
      var title = locked ? (lockBtn.getAttribute('data-title-unlock') || 'Unlock') : (lockBtn.getAttribute('data-title-lock') || 'Lock');
      lockBtn.title = title;
    }
  }

  if (lockBtn) {
    lockBtn.addEventListener('click', function () {
      setCookie(COOKIE, isLocked() ? '0' : '1', 365);
      applyLockState();
    });
  }

  function openMobile() {
    sidebar.classList.add('is-open-mobile');
    sidebar.classList.remove('is-collapsed');
    document.body.classList.add('sidebar-mobile-open');
  }
  function closeMobile() {
    sidebar.classList.remove('is-open-mobile');
    document.body.classList.remove('sidebar-mobile-open');
    applyLockState();
  }

  if (toggleBtn) toggleBtn.addEventListener('click', function () {
    if (sidebar.classList.contains('is-open-mobile')) closeMobile();
    else openMobile();
  });
  if (backdrop) backdrop.addEventListener('click', closeMobile);

  applyLockState();
  document.documentElement.classList.remove('sidebar-lock-pending');
})();
