(function () {
  const tabs = document.querySelectorAll('[data-ent-tab]');
  const panels = document.querySelectorAll('[data-ent-panel]');
  if (tabs.length) {
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
  }

  const helpOpen = document.getElementById('entHelpOpen');
  const helpClose = document.getElementById('entHelpClose');
  const helpOk = document.getElementById('entHelpOk');
  const helpOverlay = document.getElementById('entHelpOverlay');
  function openHelp() {
    if (!helpOverlay) return;
    helpOverlay.hidden = false;
    helpOk?.focus();
  }
  function closeHelp() {
    if (!helpOverlay) return;
    helpOverlay.hidden = true;
    helpOpen?.focus();
  }
  helpOpen?.addEventListener('click', openHelp);
  helpClose?.addEventListener('click', closeHelp);
  helpOk?.addEventListener('click', closeHelp);
  helpOverlay?.addEventListener('click', (e) => { if (e.target === helpOverlay) closeHelp(); });
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && helpOverlay && !helpOverlay.hidden) closeHelp();
  });
})();
