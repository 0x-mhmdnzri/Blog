/**
 * Post TOC — sticky follows scroll + accordion collapse + scroll-spy + smooth jump.
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
      // Default open so sections are reachable; user can collapse anytime
      setOpen(true);

      toggle.addEventListener('click', function (e) {
        e.preventDefault();
        e.stopPropagation();
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

    function setActive(item) {
      if (!item) return;
      clearActive();
      item.a.classList.add('is-active');
      item.el.classList.add('is-section-active');

      // Scroll active link into view inside TOC panel only (never the page)
      var panel = body || sticky;
      if (panel && item.a) {
        var linkRect = item.a.getBoundingClientRect();
        var parentRect = panel.getBoundingClientRect();
        if (linkRect.top < parentRect.top + 8) {
          panel.scrollTop -= (parentRect.top + 8 - linkRect.top);
        } else if (linkRect.bottom > parentRect.bottom - 8) {
          panel.scrollTop += (linkRect.bottom - parentRect.bottom + 8);
        }
      }
    }

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

        // Ensure TOC is open when navigating
        if (!nav.classList.contains('is-open')) setOpen(true);

        var top = target.getBoundingClientRect().top + window.pageYOffset - HEADER_OFFSET;
        if (top < 0) top = 0;
        window.scrollTo({ top: top, behavior: 'smooth' });

        try {
          history.replaceState(null, '', '#' + encodeURIComponent(id));
        } catch (_) {}

        var hit = map.find(function (m) { return m.id === id; });
        if (hit) setActive(hit);

        clickTimer = setTimeout(function () { clicking = false; }, 800);

        // On small screens, collapse after jump to free space
        if (window.matchMedia('(max-width: 1100px)').matches) {
          setTimeout(function () { setOpen(false); }, 500);
        }
      });
    });

    if (!map.length) return;

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
