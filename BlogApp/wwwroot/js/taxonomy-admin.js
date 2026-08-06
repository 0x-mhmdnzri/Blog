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
    try { initial = localStorage.getItem('blog-tax-tab') || 'folders'; } catch (e) { initial = 'folders'; }
  }
  activate(initial);

  function bindSearch(inputId, itemSelector, emptyId, countId) {
    var input = document.getElementById(inputId);
    if (!input) return;
    var empty = emptyId ? document.getElementById(emptyId) : null;
    var countLabel = countId ? document.getElementById(countId) : null;
    input.addEventListener('input', function () {
      var q = (input.value || '').trim().toLowerCase();
      var visible = 0;
      document.querySelectorAll(itemSelector).forEach(function (el) {
        var name = el.getAttribute('data-name') || '';
        var hide = q && name.indexOf(q) === -1;
        el.classList.toggle('is-hidden', !!hide);
        if (!hide) visible++;
      });
      if (empty) empty.hidden = !(q && visible === 0);
      if (countLabel) countLabel.textContent = String(visible);
    });
  }

  bindSearch('folderSearch', '#folderGrid .ff-card', 'folderNoMatch', 'folderCountLabel');
  bindSearch('catSearch', '#catTree .tx-row', 'catNoMatch', 'catCountLabel');
  bindSearch('tagSearch', '#tagCloud .tx-chip', 'tagNoMatch', 'tagCountLabel');
  bindSearch('seriesSearch', '#seriesList .tx-row', 'seriesNoMatch', 'seriesCountLabel');
  bindSearch('topicSearch', '#topicList .tx-row', 'topicNoMatch', 'topicCountLabel');

  document.querySelectorAll('.ff-swatches').forEach(function (group) {
    group.addEventListener('change', function (e) {
      var t = e.target;
      if (!t || t.name !== 'color') return;
      group.querySelectorAll('.ff-swatch').forEach(function (s) {
        s.classList.toggle('is-selected', s.contains(t) && t.checked);
      });
    });
  });
})();
