/**
 * Public site — mobile off-canvas nav drawer + sticky header state
 */
(function () {
  'use strict';

  var header = document.getElementById('site-header');
  var drawer = document.getElementById('siteNavDrawer');
  var toggle = document.getElementById('navMenuToggle');
  var panel = drawer && drawer.querySelector('.site-nav-drawer-panel');
  var lastFocus = null;

  function isOpen() {
    return !!(drawer && drawer.classList.contains('is-open'));
  }

  function openDrawer() {
    if (!drawer || !header) return;
    lastFocus = document.activeElement;
    drawer.hidden = false;
    void drawer.offsetWidth;
    drawer.classList.add('is-open');
    header.classList.add('is-drawer-open');
    document.documentElement.style.overflow = 'hidden';
    if (toggle) {
      toggle.setAttribute('aria-expanded', 'true');
      toggle.setAttribute(
        'aria-label',
        toggle.getAttribute('data-label-close') || 'Close menu'
      );
    }
    var focusable = panel && panel.querySelector('a, button');
    if (focusable) setTimeout(function () { focusable.focus(); }, 40);
  }

  function closeDrawer() {
    if (!drawer || !header || !isOpen()) return;
    drawer.classList.remove('is-open');
    header.classList.remove('is-drawer-open');
    document.documentElement.style.overflow = '';
    if (toggle) {
      toggle.setAttribute('aria-expanded', 'false');
      toggle.setAttribute(
        'aria-label',
        toggle.getAttribute('data-label-open') || 'Open menu'
      );
    }
    setTimeout(function () {
      if (!isOpen()) drawer.hidden = true;
    }, 320);
    if (lastFocus && lastFocus.focus) {
      try { lastFocus.focus(); } catch (_) {}
    }
  }

  function onScroll() {
    if (!header) return;
    header.classList.toggle('is-scrolled', (window.scrollY || 0) > 8);
  }

  function bind() {
    if (toggle) {
      toggle.addEventListener('click', function (e) {
        e.preventDefault();
        if (isOpen()) closeDrawer();
        else openDrawer();
      });
    }

    if (drawer) {
      drawer.addEventListener('click', function (e) {
        var t = e.target;
        if (t.classList && t.classList.contains('site-nav-drawer-backdrop')) {
          closeDrawer();
          return;
        }
        if (t.closest && t.closest('[data-nav-close]')) {
          closeDrawer();
        }
      });
      drawer.querySelectorAll('a.site-nav-link, a.btn-nav-login').forEach(function (a) {
        a.addEventListener('click', function () { closeDrawer(); });
      });
    }

    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && isOpen()) {
        e.preventDefault();
        closeDrawer();
      }
    });

    var mq = window.matchMedia('(min-width: 992px)');
    function onMq(ev) {
      if (ev.matches) closeDrawer();
    }
    if (mq.addEventListener) mq.addEventListener('change', onMq);
    else if (mq.addListener) mq.addListener(onMq);

    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bind);
  else bind();
})();
