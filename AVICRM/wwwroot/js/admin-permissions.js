(function () {
  'use strict';

  document.querySelectorAll('[data-group-toggle]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var group = btn.closest('[data-group]');
      if (!group) return;
      var open = !group.classList.contains('is-collapsed');
      group.classList.toggle('is-collapsed', open);
      btn.setAttribute('aria-expanded', open ? 'false' : 'true');
    });
  });

  function setAll(on) {
    document.querySelectorAll('.perm-check').forEach(function (c) {
      c.checked = on;
    });
  }

  var sel = document.getElementById('permSelectAll');
  var clr = document.getElementById('permClearAll');
  if (sel) sel.addEventListener('click', function () { setAll(true); });
  if (clr) clr.addEventListener('click', function () { setAll(false); });
})();
