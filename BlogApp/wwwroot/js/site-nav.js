/**
 * Public site navbar — mobile drawer
 * Drawer is moved to document.body so it is not trapped by the header's
 * backdrop-filter stacking context (which would clip fixed children).
 */
(function () {
  'use strict';

  var header = document.getElementById('site-header');
  var drawer = document.getElementById('siteNavDrawer');
  var openBtn = document.querySelector('[data-nav-drawer-open]');
  var closeBtns;

  if (!header || !drawer) return;

  // Ensure drawer is a direct child of <body> (layout may already place it there)
  if (drawer.parentElement !== document.body) {
    document.body.appendChild(drawer);
  }

  closeBtns = document.querySelectorAll('[data-nav-drawer-close]');

  function open() {
    drawer.classList.add('is-open');
    drawer.setAttribute('aria-hidden', 'false');
    header.classList.add('is-drawer-open');
    document.documentElement.classList.add('nav-drawer-open');
    document.body.style.overflow = 'hidden';
    if (openBtn) openBtn.setAttribute('aria-expanded', 'true');
  }

  function close() {
    drawer.classList.remove('is-open');
    drawer.setAttribute('aria-hidden', 'true');
    header.classList.remove('is-drawer-open');
    document.documentElement.classList.remove('nav-drawer-open');
    document.body.style.overflow = '';
    if (openBtn) openBtn.setAttribute('aria-expanded', 'false');
  }

  function toggle() {
    if (drawer.classList.contains('is-open')) close();
    else open();
  }

  if (openBtn) {
    openBtn.addEventListener('click', function (e) {
      e.preventDefault();
      e.stopPropagation();
      toggle();
    });
  }

  closeBtns.forEach(function (btn) {
    btn.addEventListener('click', function (e) {
      e.preventDefault();
      close();
    });
  });

  drawer.querySelectorAll('a.site-nav-link').forEach(function (a) {
    a.addEventListener('click', function () { close(); });
  });

  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && drawer.classList.contains('is-open')) close();
  });

  window.addEventListener('resize', function () {
    if (window.matchMedia('(min-width: 992px)').matches) close();
  });

  window.BlogSiteNav = { open: open, close: close, toggle: toggle };
})();
