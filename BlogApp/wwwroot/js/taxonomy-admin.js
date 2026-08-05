(function () {
  function activate(tab) {
    document.querySelectorAll('[data-tx-tab]').forEach(function (el) {
      el.classList.toggle('is-active', el.getAttribute('data-tx-tab') === tab);
      if (el.getAttribute('role') === 'tab') {
        el.setAttribute('aria-selected', el.getAttribute('data-tx-tab') === tab ? 'true' : 'false');
      }
    });
    document.querySelectorAll('.tx-panel').forEach(function (p) {
      var on = p.getAttribute('data-panel') === tab;
      p.classList.toggle('is-active', on);
      p.hidden = !on;
    });
    try { localStorage.setItem('blog-tax-tab', tab); } catch (e) {}
    if (location.hash !== '#' + tab) {
      history.replaceState(null, '', '#' + tab);
    }
  }

  document.querySelectorAll('[data-tx-tab]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      activate(btn.getAttribute('data-tx-tab'));
    });
  });

  var initial = (location.hash || '').replace('#', '');
  if (!initial || !document.querySelector('.tx-panel[data-panel="' + initial + '"]')) {
    try { initial = localStorage.getItem('blog-tax-tab') || 'cats'; } catch (e) { initial = 'cats'; }
  }
  activate(initial);

  function bindSearch(inputId, itemSelector) {
    var input = document.getElementById(inputId);
    if (!input) return;
    input.addEventListener('input', function () {
      var q = (input.value || '').trim().toLowerCase();
      document.querySelectorAll(itemSelector).forEach(function (el) {
        var name = el.getAttribute('data-name') || '';
        el.classList.toggle('is-hidden', q && name.indexOf(q) === -1);
      });
    });
  }
  bindSearch('catSearch', '#catTree .tx-tree-item');
  bindSearch('tagSearch', '#tagCloud .tx-tag');
})();
