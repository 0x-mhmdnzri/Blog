(function () {
  const tabs = document.querySelectorAll('[data-ent-tab]');
  const panels = document.querySelectorAll('[data-ent-panel]');
  if (!tabs.length) return;

  function activate(id) {
    tabs.forEach(t => t.classList.toggle('is-active', t.getAttribute('data-ent-tab') === id));
    panels.forEach(p => p.classList.toggle('is-active', p.getAttribute('data-ent-panel') === id));
    try { history.replaceState(null, '', '#' + id); } catch (e) {}
  }

  tabs.forEach(t => {
    t.addEventListener('click', () => activate(t.getAttribute('data-ent-tab')));
  });

  const hash = (location.hash || '#tenants').replace('#', '');
  const valid = Array.from(tabs).some(t => t.getAttribute('data-ent-tab') === hash);
  activate(valid ? hash : 'tenants');
})();
