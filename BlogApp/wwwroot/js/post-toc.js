/**
 * Post TOC — accordion + scroll-spy + smooth jump.
 * Sidebar mode (.post-toc--sidebar) stays open and highlights the active section.
 */
(function () {
  'use strict';

  var HEADER_OFFSET = 96;

  function initOne(nav) {
    if (!nav || nav.dataset.tocReady) return;
    nav.dataset.tocReady = '1';

    var isSidebar = nav.classList.contains('post-toc--sidebar');
    var toggle = nav.querySelector('[data-toc-toggle]');
    var body = nav.querySelector('.toc-body');

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
      // Sidebar: always open; mobile accordion: open on desktop only
      if (isSidebar) setOpen(true);
      else setOpen(window.matchMedia('(min-width: 768px)').matches);

      toggle.addEventListener('click', function () {
        if (isSidebar && window.matchMedia('(min-width: 1101px)').matches) return;
        setOpen(!nav.classList.contains('is-open'));
      });
    }

    var links = Array.prototype.slice.call(nav.querySelectorAll('a.toc-link[href^="#"]'));
    var map = links.map(function (a) {
      var id = decodeURIComponent((a.getAttribute('href') || '').slice(1));
      return { a: a, id: id, el: id ? document.getElementById(id) : null };
    }).filter(function (x) { return x.el; });

    function clearActive() {
      links.forEach(function (a) { a.classList.remove('is-active'); });
      map.forEach(function (m) { m.el.classList.remove('is-section-active'); });
    }

    function setActive(item) {
      clearActive();
      if (!item) return;
      item.a.classList.add('is-active');
      item.el.classList.add('is-section-active');
      try {
        item.a.scrollIntoView({ block: 'nearest', inline: 'nearest' });
      } catch (_) {}
    }

    // Smooth scroll on click
    links.forEach(function (a) {
      a.addEventListener('click', function (e) {
        var id = decodeURIComponent((a.getAttribute('href') || '').slice(1));
        var target = id ? document.getElementById(id) : null;
        if (!target) return;
        e.preventDefault();

        var top = target.getBoundingClientRect().top + window.pageYOffset - HEADER_OFFSET;
        window.scrollTo({ top: top, behavior: 'smooth' });

        try { history.replaceState(null, '', '#' + id); } catch (_) {}

        var hit = map.find(function (m) { return m.id === id; });
        if (hit) setActive(hit);

        if (!isSidebar && window.matchMedia('(max-width: 767px)').matches) setOpen(false);
      });
    });

    if (!map.length) return;

    var ticking = false;
    function onScroll() {
      if (ticking) return;
      ticking = true;
      requestAnimationFrame(function () {
        ticking = false;
        var y = window.scrollY + HEADER_OFFSET + 8;
        var active = map[0];
        for (var i = 0; i < map.length; i++) {
          if (map[i].el.offsetTop <= y) active = map[i];
        }
        setActive(active);
      });
    }

    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();

    // Honor hash on load
    if (location.hash) {
      var hid = decodeURIComponent(location.hash.slice(1));
      var h = map.find(function (m) { return m.id === hid; });
      if (h) {
        setTimeout(function () {
          var top = h.el.getBoundingClientRect().top + window.pageYOffset - HEADER_OFFSET;
          window.scrollTo({ top: top, behavior: 'smooth' });
          setActive(h);
        }, 80);
      }
    }
  }

  function boot() {
    document.querySelectorAll('[data-toc]').forEach(initOne);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
