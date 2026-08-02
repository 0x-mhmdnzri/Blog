/** Related posts rail — arrow slider with optional dots. */
(function () {
  'use strict';

  function init(root) {
    if (!root || root.dataset.railReady) return;
    root.dataset.railReady = '1';

    var track = root.querySelector('[data-rail-track]') || root.querySelector('.related-rail-track');
    var prev = root.querySelector('[data-rail-prev]');
    var next = root.querySelector('[data-rail-next]');
    var dotsWrap = root.querySelector('[data-rail-dots]');
    if (!track) return;

    var cards = track.querySelectorAll('.related-rail-card');
    var index = 0;
    var total = cards.length;
    if (total <= 1) {
      if (prev) prev.disabled = true;
      if (next) next.disabled = true;
      if (dotsWrap) dotsWrap.style.display = 'none';
      return;
    }

    var dots = [];
    if (dotsWrap) {
      dotsWrap.innerHTML = '';
      for (var d = 0; d < total; d++) {
        var s = document.createElement('span');
        if (d === 0) s.className = 'is-on';
        dotsWrap.appendChild(s);
        dots.push(s);
      }
    }

    function go(i) {
      index = Math.max(0, Math.min(total - 1, i));
      track.style.transform = 'translateX(' + (-index * 100) + '%)';
      if (prev) prev.disabled = index === 0;
      if (next) next.disabled = index >= total - 1;
      dots.forEach(function (el, n) {
        el.classList.toggle('is-on', n === index);
      });
    }

    if (prev) prev.addEventListener('click', function () { go(index - 1); });
    if (next) next.addEventListener('click', function () { go(index + 1); });

    var startX = 0;
    track.addEventListener('touchstart', function (e) {
      if (e.touches && e.touches[0]) startX = e.touches[0].clientX;
    }, { passive: true });
    track.addEventListener('touchend', function (e) {
      if (!e.changedTouches || !e.changedTouches[0]) return;
      var dx = e.changedTouches[0].clientX - startX;
      if (Math.abs(dx) < 40) return;
      if (dx < 0) go(index + 1);
      else go(index - 1);
    }, { passive: true });

    go(0);
  }

  function boot() {
    document.querySelectorAll('[data-related-rail]').forEach(init);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
