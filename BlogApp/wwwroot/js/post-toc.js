/**
 * Post TOC — sticky + accordion + IntersectionObserver scroll-spy (deep posts) + smooth jump.
 */
(function () {
  'use strict';

  var HEADER_OFFSET = 110;
  var clicking = false;
  var clickTimer = null;

  function initOne(nav) {
    if (!nav || nav.dataset.tocReady) return;
    nav.dataset.tocReady = '1';

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

    if (!map.length) return;

    function clearActive() {
      links.forEach(function (a) {
        a.classList.remove('is-active');
        a.removeAttribute('aria-current');
      });
      map.forEach(function (m) { m.el.classList.remove('is-section-active'); });
    }

    function setActive(item) {
      if (!item) return;
      clearActive();
      item.a.classList.add('is-active');
      item.a.setAttribute('aria-current', 'true');
      item.el.classList.add('is-section-active');

      // Keep active link visible inside TOC panel
      var panel = body || sticky;
      if (panel && item.a && typeof item.a.scrollIntoView === 'function') {
        try {
          item.a.scrollIntoView({ block: 'nearest', inline: 'nearest', behavior: 'smooth' });
        } catch (_) {
          var linkRect = item.a.getBoundingClientRect();
          var parentRect = panel.getBoundingClientRect();
          if (linkRect.top < parentRect.top + 8) {
            panel.scrollTop -= (parentRect.top + 8 - linkRect.top);
          } else if (linkRect.bottom > parentRect.bottom - 8) {
            panel.scrollTop += (linkRect.bottom - parentRect.bottom + 8);
          }
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
        if (!nav.classList.contains('is-open')) setOpen(true);

        var top = target.getBoundingClientRect().top + window.pageYOffset - HEADER_OFFSET;
        if (top < 0) top = 0;
        window.scrollTo({ top: top, behavior: 'smooth' });

        try { history.replaceState(null, '', '#' + encodeURIComponent(id)); } catch (_) {}

        var hit = map.find(function (m) { return m.id === id; });
        if (hit) setActive(hit);

        clickTimer = setTimeout(function () { clicking = false; }, 900);

        if (window.matchMedia('(max-width: 1100px)').matches) {
          setTimeout(function () { setOpen(false); }, 550);
        }
      });
    });

    var currentId = null;

    function activateById(id) {
      if (!id || id === currentId) return;
      var item = map.find(function (m) { return m.id === id; });
      if (!item) return;
      currentId = id;
      setActive(item);
    }

    // Preferred: IntersectionObserver — stable on deep posts with tall sections
    if (typeof IntersectionObserver === 'function') {
      var ratios = {};
      map.forEach(function (m) { ratios[m.id] = 0; });

      var io = new IntersectionObserver(function (entries) {
        if (clicking) return;
        entries.forEach(function (en) {
          var id = en.target.id;
          if (!id) return;
          ratios[id] = en.isIntersecting ? en.intersectionRatio : 0;
        });

        // Highest ratio among visible; fall back to last section above the line
        var bestId = null;
        var bestRatio = 0;
        Object.keys(ratios).forEach(function (id) {
          if (ratios[id] > bestRatio) {
            bestRatio = ratios[id];
            bestId = id;
          }
        });

        if (bestId && bestRatio > 0) {
          activateById(bestId);
          return;
        }

        // Fallback scroll position when nothing intersects the band
        pickFromScroll();
      }, {
        root: null,
        rootMargin: '-' + HEADER_OFFSET + 'px 0px -55% 0px',
        threshold: [0, 0.1, 0.25, 0.5, 0.75, 1]
      });

      map.forEach(function (m) { io.observe(m.el); });
    }

    function pickFromScroll() {
      if (clicking) return;
      var y = window.scrollY + HEADER_OFFSET + 16;
      var active = map[0];
      for (var i = 0; i < map.length; i++) {
        var top = map[i].el.getBoundingClientRect().top + window.pageYOffset;
        if (top <= y) active = map[i];
      }
      if (active) activateById(active.id);
    }

    var ticking = false;
    window.addEventListener('scroll', function () {
      if (ticking) return;
      ticking = true;
      requestAnimationFrame(function () {
        ticking = false;
        if (!clicking) pickFromScroll();
      });
    }, { passive: true });

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
        }, 120);
      }
    }
  }

  function boot() {
    document.querySelectorAll('[data-toc]').forEach(initOne);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
