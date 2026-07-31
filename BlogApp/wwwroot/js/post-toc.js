/**
 * Post TOC — floating sticky sidebar + scroll-spy + smooth section jump.
 * Never scrolls the page when updating the active TOC link.
 */
(function () {
  'use strict';

  var HEADER_OFFSET = 100;
  var clicking = false;
  var clickTimer = null;

  function initOne(nav) {
    if (!nav || nav.dataset.tocReady) return;
    nav.dataset.tocReady = '1';

    var isSidebar = nav.classList.contains('post-toc--sidebar');
    var toggle = nav.querySelector('[data-toc-toggle]');
    var body = nav.querySelector('.toc-body');
    var sticky = nav.closest('.post-aside-sticky');

    function setOpen(open) {
      if (!toggle || !body) return;
      nav.classList.toggle('is-open', open);
      toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
      if (open) body.removeAttribute('hidden');
      else body.setAttribute('hidden', '');
      var label = open
        ? (toggle.getAttribute('data-label-close') || 'Collapse')
        : (toggle.getAttribute('data-label-open') || 'Expand');
      toggle.setAttribute('aria-label', label);
    }

    if (toggle && body) {
      if (isSidebar) setOpen(true);
      else setOpen(window.matchMedia('(min-width: 768px)').matches);

      toggle.addEventListener('click', function () {
        if (isSidebar && window.matchMedia('(min-width: 1101px)').matches) return;
        setOpen(!nav.classList.contains('is-open'));
      });
    }

    var links = Array.prototype.slice.call(nav.querySelectorAll('a.toc-link[href^="#"]'));
    var map = links.map(function (a) {
      var raw = (a.getAttribute('href') || '').slice(1);
      var id = raw ? decodeURIComponent(raw) : '';
      return { a: a, id: id, el: id ? document.getElementById(id) : null };
    }).filter(function (x) { return x.el; });

    function clearActive() {
      links.forEach(function (a) { a.classList.remove('is-active'); });
      map.forEach(function (m) { m.el.classList.remove('is-section-active'); });
    }

    /** Highlight active link WITHOUT scrolling the window. */
    function setActive(item) {
      if (!item) return;
      clearActive();
      item.a.classList.add('is-active');
      item.el.classList.add('is-section-active');

      // Keep active link visible inside TOC only (never the page)
      var scrollParent = sticky || body;
      if (scrollParent && item.a) {
        var linkRect = item.a.getBoundingClientRect();
        var parentRect = scrollParent.getBoundingClientRect();
        if (linkRect.top < parentRect.top + 8) {
          scrollParent.scrollTop -= (parentRect.top + 8 - linkRect.top);
        } else if (linkRect.bottom > parentRect.bottom - 8) {
          scrollParent.scrollTop += (linkRect.bottom - parentRect.bottom + 8);
        }
      }
    }

    // Smooth scroll on click — never jumps to top
    links.forEach(function (a) {
      a.addEventListener('click', function (e) {
        e.preventDefault();
        e.stopPropagation();

        var raw = (a.getAttribute('href') || '').slice(1);
        var id = raw ? decodeURIComponent(raw) : '';
        var target = id ? document.getElementById(id) : null;
        if (!target) return;

        clicking = true;
        if (clickTimer) clearTimeout(clickTimer);

        var top = target.getBoundingClientRect().top + window.pageYOffset - HEADER_OFFSET;
        if (top < 0) top = 0;

        window.scrollTo({ top: top, behavior: 'smooth' });

        try {
          history.replaceState(null, '', '#' + encodeURIComponent(id));
        } catch (_) {}

        var hit = map.find(function (m) { return m.id === id; });
        if (hit) setActive(hit);

        // Ignore scroll-spy while smooth scroll is running
        clickTimer = setTimeout(function () {
          clicking = false;
        }, 800);

        if (!isSidebar && window.matchMedia('(max-width: 767px)').matches) setOpen(false);
      });
    });

    if (!map.length) return;

    // Scroll spy via IntersectionObserver — more reliable than offsetTop
    var currentId = null;

    function pickFromScroll() {
      if (clicking) return;
      var y = window.scrollY + HEADER_OFFSET + 12;
      var active = map[0];
      for (var i = 0; i < map.length; i++) {
        var rect = map[i].el.getBoundingClientRect();
        var top = rect.top + window.pageYOffset;
        if (top <= y) active = map[i];
      }
      if (active && active.id !== currentId) {
        currentId = active.id;
        setActive(active);
      }
    }

    var ticking = false;
    function onScroll() {
      if (ticking) return;
      ticking = true;
      requestAnimationFrame(function () {
        ticking = false;
        pickFromScroll();
      });
    }

    window.addEventListener('scroll', onScroll, { passive: true });
    pickFromScroll();

    // Hash on load
    if (location.hash) {
      var hid = decodeURIComponent(location.hash.slice(1));
      var h = map.find(function (m) { return m.id === hid; });
      if (h) {
        setTimeout(function () {
          var top = h.el.getBoundingClientRect().top + window.pageYOffset - HEADER_OFFSET;
          window.scrollTo({ top: Math.max(0, top), behavior: 'smooth' });
          setActive(h);
          currentId = h.id;
        }, 100);
      }
    }
  }

  function boot() {
    document.querySelectorAll('[data-toc]').forEach(initOne);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
