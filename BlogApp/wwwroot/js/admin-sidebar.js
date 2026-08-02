(function(){if(window.__adminLiveBooted)return;var s=document.createElement('script');s.src='/js/admin-live.js';s.defer=true;document.head.appendChild(s);})();
(function () {
  var COOKIE = 'Blog.AdminSidebarLocked';
  var sidebar = document.getElementById('adminSidebar');
  var lockBtn = document.getElementById('sidebarLockBtn');
  var nav = document.getElementById('adminNav');
  var backdrop = document.getElementById('adminSidebarBackdrop');
  var pageOverlay = document.getElementById('adminPageOverlay');

  function getCookie(name) {
    var m = document.cookie.match(new RegExp('(?:^|; )' + name.replace(/([.$?*|{}()\[\]\\\/+^])/g, '\\$1') + '=([^;]*)'));
    return m ? decodeURIComponent(m[1]) : null;
  }

  function setCookie(name, value, days) {
    var max = days ? '; max-age=' + (days * 86400) : '';
    document.cookie = name + '=' + encodeURIComponent(value) + max + '; path=/; SameSite=Lax';
  }

  function isLocked() {
    return sidebar && sidebar.classList.contains('is-locked');
  }

  function applyLock(locked, animate) {
    if (!sidebar) return;
    if (!animate) {
      sidebar.style.transition = 'none';
      document.body.style.transition = 'none';
    }
    sidebar.classList.toggle('is-locked', locked);
    sidebar.classList.toggle('is-collapsed', !locked);
    document.body.classList.toggle('sidebar-locked', locked);
    document.body.classList.toggle('sidebar-collapsed', !locked);
    if (lockBtn) {
      lockBtn.setAttribute('aria-pressed', locked ? 'true' : 'false');
      lockBtn.title = locked ? (lockBtn.dataset.titleUnlock || 'Unlock') : (lockBtn.dataset.titleLock || 'Lock');
    }
    setCookie(COOKIE, locked ? '1' : '0', 365);
    if (!animate) {
      void sidebar.offsetWidth;
      sidebar.style.transition = '';
      document.body.style.transition = '';
    }
  }

  var cookieVal = getCookie(COOKIE);
  applyLock(cookieVal === '1', false);

  if (lockBtn) {
    lockBtn.addEventListener('click', function () {
      applyLock(!isLocked(), true);
    });
  }

  document.querySelectorAll('[data-admin-toggle]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      if (!sidebar) return;
      sidebar.classList.toggle('open');
      if (backdrop) backdrop.classList.toggle('show', sidebar.classList.contains('open'));
    });
  });
  if (backdrop) {
    backdrop.addEventListener('click', function () {
      if (sidebar) sidebar.classList.remove('open');
      backdrop.classList.remove('show');
    });
  }

  function showPageOverlay() {
    if (pageOverlay) pageOverlay.classList.add('is-visible');
  }
  function hidePageOverlay() {
    if (pageOverlay) pageOverlay.classList.remove('is-visible');
  }

  function scrollActiveIntoView() {
    if (!nav) return;
    var active = nav.querySelector('.admin-nav-link.active');
    if (active && active.scrollIntoView) {
      try { active.scrollIntoView({ block: 'nearest', behavior: 'smooth' }); } catch (_) {
        active.scrollIntoView(false);
      }
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
      scrollActiveIntoView();
      hidePageOverlay();
    });
  } else {
    setTimeout(scrollActiveIntoView, 60);
    hidePageOverlay();
  }

  if (nav) {
    nav.addEventListener('click', function (e) {
      var link = e.target.closest('.admin-nav-link');
      if (!link || link.getAttribute('href') === '#' || link.hasAttribute('download')) return;
      if (link.href && link.origin === location.origin && link.pathname === location.pathname && link.search === location.search) return;

      nav.querySelectorAll('.admin-nav-link.active').forEach(function (el) {
        el.classList.remove('active');
      });
      link.classList.add('active');
      showPageOverlay();
    });
  }

  document.addEventListener('click', function (e) {
    var a = e.target.closest('a[data-admin-nav]');
    if (a) showPageOverlay();
  });

  window.BlogSkeleton = {
    show: function (el) {
      if (!el) return;
      el.classList.add('skel-host');
      if (!el.querySelector('.skel-overlay')) {
        var o = document.createElement('div');
        o.className = 'skel-overlay';
        o.innerHTML =
          '<div class="skel-line skel-w-40"></div>' +
          '<div class="skel-line skel-w-80"></div>' +
          '<div class="skel-line skel-w-60"></div>' +
          '<div class="skel-line skel-w-90"></div>' +
          '<div class="skel-line skel-w-50"></div>';
        el.appendChild(o);
      }
    },
    hide: function (el) {
      if (!el) return;
      el.classList.remove('skel-host');
      var o = el.querySelector('.skel-overlay');
      if (o) o.remove();
    }
  };

  document.querySelectorAll('.admin-table-wrap').forEach(function (wrap) {
    var table = wrap.querySelector('table.display');
    if (table) BlogSkeleton.show(wrap);
  });

  if (window.jQuery) {
    jQuery(document).on('init.dt draw.dt', function (e) {
      var table = e.target;
      var wrap = table.closest && table.closest('.admin-table-wrap');
      if (wrap) BlogSkeleton.hide(wrap);
    });
  }

  window.addEventListener('pageshow', function () {
    hidePageOverlay();
    var content = document.querySelector('.admin-content');
    if (content) content.classList.remove('is-loading');
  });

  window.addEventListener('load', function () {
    hidePageOverlay();
    var content = document.querySelector('.admin-content');
    if (content) content.classList.remove('is-loading');
    document.body.classList.add('app-ready');
  });
})();
