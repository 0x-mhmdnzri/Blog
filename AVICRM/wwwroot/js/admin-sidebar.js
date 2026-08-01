(function () {
  var COOKIE = 'AVICRM.AdminSidebarLocked';
  var sidebar = document.getElementById('adminSidebar');
  var lockBtn = document.getElementById('sidebarLockBtn');
  var nav = document.getElementById('adminNav');
  var backdrop = document.getElementById('adminSidebarBackdrop');
  var pageOverlay = document.getElementById('adminPageOverlay');

  function getCookie(name) {
    var m = document.cookie.match(new RegExp('(?:^|; )' + name.replace(/([.$?*|{}()\[\]\\/+^])/g, '\\$1') + '=([^;]*)'));
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
  if (cookieVal === null) cookieVal = getCookie('Blog.AdminSidebarLocked');
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
    if (!pageOverlay) {
      pageOverlay = document.createElement('div');
      pageOverlay.id = 'adminPageOverlay';
      pageOverlay.className = 'admin-page-overlay';
      pageOverlay.innerHTML = '<div class="admin-page-spinner"></div>';
      document.body.appendChild(pageOverlay);
    }
    requestAnimationFrame(function () {
      pageOverlay.classList.add('is-visible');
    });
  }

  function hidePageOverlay() {
    if (!pageOverlay) return;
    pageOverlay.classList.remove('is-visible');
  }

  if (nav) {
    nav.addEventListener('click', function (e) {
      var link = e.target.closest('.admin-nav-link');
      if (!link || link.getAttribute('href') === '#') return;
      if (link.href && link.origin === location.origin && link.pathname === location.pathname && link.search === location.search) return;
      nav.querySelectorAll('.admin-nav-link.active').forEach(function (el) {
        el.classList.remove('active');
      });
      link.classList.add('active');
      showPageOverlay();
    });
  }

  document.querySelectorAll('[data-nav-accordion]').forEach(function (btn) {
    btn.addEventListener('click', function (e) {
      e.preventDefault();
      var section = btn.closest('.admin-nav-section');
      if (!section) return;
      var open = !section.classList.contains('is-open');
      var parent = section.parentElement;
      if (parent) {
        parent.querySelectorAll(':scope > .admin-nav-section.is-open').forEach(function (s) {
          if (s !== section) {
            s.classList.remove('is-open');
            var b = s.querySelector('[data-nav-accordion]');
            var c = s.querySelector('.admin-nav-children');
            if (b) b.setAttribute('aria-expanded', 'false');
            if (c) c.hidden = true;
          }
        });
      }
      section.classList.toggle('is-open', open);
      btn.setAttribute('aria-expanded', open ? 'true' : 'false');
      var kids = section.querySelector('.admin-nav-children');
      if (kids) kids.hidden = !open;
      try {
        var key = btn.getAttribute('data-nav-accordion');
        if (key) localStorage.setItem('avicrm-nav-' + key, open ? '1' : '0');
      } catch (err) {}
    });
  });

  document.querySelectorAll('.admin-nav-section[data-nav-section]').forEach(function (section) {
    if (section.classList.contains('is-open')) return;
    var key = section.getAttribute('data-nav-section');
    try {
      if (localStorage.getItem('avicrm-nav-' + key) === '1') {
        section.classList.add('is-open');
        var b = section.querySelector('[data-nav-accordion]');
        var c = section.querySelector('.admin-nav-children');
        if (b) b.setAttribute('aria-expanded', 'true');
        if (c) c.hidden = false;
      }
    } catch (err) {}
  });

  window.addEventListener('pageshow', function () { hidePageOverlay(); });
  window.addEventListener('load', function () { hidePageOverlay(); });
})();
