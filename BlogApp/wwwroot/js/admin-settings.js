(function () {
  var root = document.getElementById('adminSettings');
  if (!root) return;

  var tabs = Array.prototype.slice.call(root.querySelectorAll('[data-stgs-tab]'));
  var panels = Array.prototype.slice.call(root.querySelectorAll('[data-stgs-panel]'));

  function show(id) {
    tabs.forEach(function (t) {
      var on = t.getAttribute('data-stgs-tab') === id;
      t.classList.toggle('is-active', on);
      t.setAttribute('aria-selected', on ? 'true' : 'false');
    });
    panels.forEach(function (p) {
      var on = p.getAttribute('data-stgs-panel') === id;
      p.hidden = !on;
      p.classList.toggle('is-active', on);
    });
    try {
      history.replaceState(null, '', '#' + id);
    } catch (e) {}
  }

  tabs.forEach(function (t) {
    t.addEventListener('click', function () {
      show(t.getAttribute('data-stgs-tab'));
    });
  });

  var hash = (location.hash || '').replace(/^#/, '');
  if (hash && root.querySelector('[data-stgs-panel="' + hash + '"]')) {
    show(hash);
  } else {
    show('seo');
  }

  function updateBadge(key, on) {
    var b = root.querySelector('[data-stgs-badge="' + key + '"]');
    if (!b) return;
    b.classList.remove('is-on', 'is-off', 'is-warn');
    if (key === 'maintenance' && on) b.classList.add('is-warn');
    else if (on) b.classList.add('is-on');
    else b.classList.add('is-off');
  }

  root.querySelectorAll('[data-stgs-toggle]').forEach(function (input) {
    var key = input.getAttribute('data-stgs-toggle');
    input.addEventListener('change', function () {
      updateBadge(key, input.checked);
    });
  });
})();
