/**
 * Post TOC — accordion (mobile) + sticky sidebar (desktop) + scroll-spy.
 * Critical: never call scrollIntoView on TOC links in a way that moves the window
 * (mobile TOC sits in document flow; that caused jump-to-top loops).
 */
(function () {
  'use strict';

  var HEADER_OFFSET = 110;
  var clicking = false;
  var clickTimer = null;

  function isDesktop() {
    return window.matchMedia('(min-width: 1101px)').matches;
  }

  function initOne(nav) {
    if (!nav || nav.dataset.tocReady) return;
    nav.dataset.tocReady = '1';

    var toggle = nav.querySelector('[data-toc-toggle]');
    var body = nav.querySelector('.toc-body');
    var isSidebar = nav.classList.contains('post-toc--sidebar');

    function setOpen(open) {
      if (!toggle || !body) return;
      // Desktop sidebar always expanded
      if (isSidebar && isDesktop()) open = true;
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
      // Mobile: start collapsed so content isn't pushed / jump-prone
      // Desktop sidebar: always open
      setOpen(isSidebar && isDesktop());
      if (!isSidebar) {
        toggle.addEventListener('click', function (e) {
          e.preventDefault();
          e.stopPropagation();
          setOpen(!nav.classList.contains('is-open'));
        });
      }
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

    /**
     * Scroll active TOC link only inside .toc-body overflow panel.
     * Never use element.scrollIntoView — that scrolls the window and causes
     * jump-to-top loops when the mobile TOC is in document flow.
     */
    function ensureLinkVisibleInPanel(link) {
      if (!body || !link) return;
      // Only for open, overflow panels (sidebar or expanded mobile accordion)
      if (body.hasAttribute('hidden')) return;
      if (body.scrollHeight <= body.clientHeight + 2) return;

      var linkTop = link.offsetTop;
      var linkBottom = linkTop + link.offsetHeight;
      var viewTop = body.scrollTop;
      var viewBottom = viewTop + body.clientHeight;

      if (linkTop < viewTop + 4) {
        body.scrollTop = Math.max(0, linkTop - 8);
      } else if (linkBottom > viewBottom - 4) {
        body.scrollTop = linkBottom - body.clientHeight + 8;
      }
    }

    function setActive(item, scrollPanel) {
      if (!item) return;
      clearActive();
      item.a.classList.add('is-active');
      item.a.setAttribute('aria-current', 'true');
      item.el.classList.add('is-section-active');

      // Only nudge the TOC panel on desktop sidebar — never on mobile doc-flow TOC
      if (scrollPanel && isSidebar && isDesktop()) {
        ensureLinkVisibleInPanel(item.a);
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

        var top = target.getBoundingClientRect().top + window.pageYOffset - HEADER_OFFSET;
        if (top < 0) top = 0;
        window.scrollTo({ top: top, behavior: 'smooth' });

        try { history.replaceState(null, '', '#' + encodeURIComponent(id)); } catch (_) {}

        var hit = map.find(function (m) { return m.id === id; });
        if (hit) {
          currentId = hit.id;
          setActive(hit, false);
        }

        // Collapse mobile accordion after jump so content height is stable
        if (!isSidebar && !isDesktop()) {
          setOpen(false);
        }

        clickTimer = setTimeout(function () { clicking = false; }, 700);
      });
    });

    var currentId = null;

    function activateById(id) {
      if (!id || id === currentId) return;
      var item = map.find(function (m) { return m.id === id; });
      if (!item) return;
      currentId = id;
      setActive(item, true);
    }

    function pickFromScroll() {
      if (clicking) return;
      var y = window.scrollY + HEADER_OFFSET + 12;
      var active = map[0];
      for (var i = 0; i < map.length; i++) {
        var top = map[i].el.getBoundingClientRect().top + window.pageYOffset;
        if (top <= y) active = map[i];
        else break;
      }
      if (active) activateById(active.id);
    }

    // Single source of truth: scroll listener only (no IO fighting it)
    var ticking = false;
    window.addEventListener('scroll', function () {
      if (ticking || clicking) return;
      ticking = true;
      requestAnimationFrame(function () {
        ticking = false;
        pickFromScroll();
      });
    }, { passive: true });

    // Initial highlight without moving the page
    pickFromScroll();

    if (location.hash) {
      var hid = decodeURIComponent(location.hash.slice(1));
      var h = map.find(function (m) { return m.id === hid; });
      if (h) {
        setTimeout(function () {
          clicking = true;
          var top = h.el.getBoundingClientRect().top + window.pageYOffset - HEADER_OFFSET;
          window.scrollTo({ top: Math.max(0, top), behavior: 'smooth' });
          currentId = h.id;
          setActive(h, false);
          setTimeout(function () { clicking = false; }, 700);
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
