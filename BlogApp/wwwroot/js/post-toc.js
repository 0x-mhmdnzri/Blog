/**
 * Post TOC accordion + scroll spy.
 * Works with markup from MarkdownService.GenerateTableOfContents.
 */
(function () {
  'use strict';

  function initOne(nav) {
    if (!nav || nav.dataset.tocReady) return;
    nav.dataset.tocReady = '1';

    var toggle = nav.querySelector('[data-toc-toggle]');
    var body = nav.querySelector('.toc-body');
    if (!toggle || !body) return;

    function setOpen(open) {
      nav.classList.toggle('is-open', open);
      toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
      if (open) body.removeAttribute('hidden');
      else body.setAttribute('hidden', '');
      var label = open
        ? (toggle.getAttribute('data-label-close') || 'Collapse')
        : (toggle.getAttribute('data-label-open') || 'Expand');
      toggle.setAttribute('aria-label', label);
    }

    // Default: open on desktop, collapsed on small screens
    var preferOpen = window.matchMedia('(min-width: 768px)').matches;
    setOpen(preferOpen);

    toggle.addEventListener('click', function () {
      setOpen(!nav.classList.contains('is-open'));
    });

    // Smooth scroll for in-page links
    nav.querySelectorAll('a.toc-link[href^="#"]').forEach(function (a) {
      a.addEventListener('click', function (e) {
        var id = a.getAttribute('href').slice(1);
        if (!id) return;
        var target = document.getElementById(id);
        if (!target) return;
        e.preventDefault();
        target.scrollIntoView({ behavior: 'smooth', block: 'start' });
        try {
          history.replaceState(null, '', '#' + id);
        } catch (_) {}
        // On mobile, collapse after jump
        if (window.matchMedia('(max-width: 767px)').matches) setOpen(false);
      });
    });

    // Scroll spy
    var links = Array.prototype.slice.call(nav.querySelectorAll('a.toc-link[href^="#"]'));
    var map = links.map(function (a) {
      var id = a.getAttribute('href').slice(1);
      return { a: a, el: document.getElementById(id) };
    }).filter(function (x) { return x.el; });

    if (!map.length) return;

    function onScroll() {
      var y = window.scrollY + 120;
      var active = null;
      for (var i = 0; i < map.length; i++) {
        if (map[i].el.offsetTop <= y) active = map[i];
      }
      links.forEach(function (a) { a.classList.remove('is-active'); });
      if (active) {
        active.a.classList.add('is-active');
        // Keep active link visible inside toc body
        try {
          active.a.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        } catch (_) {}
      }
    }

    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();
  }

  function boot() {
    document.querySelectorAll('[data-toc]').forEach(initOne);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
