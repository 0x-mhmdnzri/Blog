/** Related posts rail — arrow-only slider (no dots). */
(function () {
  'use strict';

  function init(root) {
    if (!root || root.dataset.railReady) return;
    root.dataset.railReady = '1';

    var track = root.querySelector('[data-rail-track]');
    var prev = root.querySelector('[data-rail-prev]');
    var next = root.querySelector('[data-rail-next]');
    if (!track) return;

    var cards = track.querySelectorAll('.related-rail-card');
    var index = 0;
    var total = cards.length;
    if (total <= 1) {
      if (prev) prev.disabled = true;
      if (next) next.disabled = true;
      return;
    }

    function go(i) {
      index = Math.max(0, Math.min(total - 1, i));
      track.style.transform = 'translateX(' + (-index * 100) + '%)';
      if (prev) prev.disabled = index === 0;
      if (next) next.disabled = index >= total - 1;
    }

    if (prev) prev.addEventListener('click', function () { go(index - 1); });
    if (next) next.addEventListener('click', function () { go(index + 1); });

    // RTL track: still uses physical translate; layout is direction:ltr on post-layout
    go(0);
  }

  function boot() {
    document.querySelectorAll('[data-related-rail]').forEach(init);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
