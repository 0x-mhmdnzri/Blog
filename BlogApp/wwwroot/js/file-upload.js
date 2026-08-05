/**
 * Shared file-upload dropzone.
 * Markup: .fu-zone[data-fu] containing input[type=file], .fu-zone-btn, optional .fu-zone-picked
 */
(function () {
  function initZone(zone) {
    if (zone.__fuBound) return;
    zone.__fuBound = true;
    var input = zone.querySelector('input[type="file"]');
    if (!input) return;
    var btn = zone.querySelector('.fu-zone-btn');
    var picked = zone.querySelector('.fu-zone-picked');
    var form = zone.closest('form') || zone.querySelector('form');
    var autoSubmit = zone.getAttribute('data-fu-auto-submit') === '1';

    function showName() {
      if (!picked) return;
      if (input.files && input.files.length) {
        var names = Array.prototype.map.call(input.files, function (f) { return f.name; }).join(', ');
        picked.textContent = names;
        picked.hidden = false;
      } else {
        picked.textContent = '';
        picked.hidden = true;
      }
    }

    function openPicker(e) {
      if (e) { e.preventDefault(); e.stopPropagation(); }
      input.click();
    }

    zone.addEventListener('click', function (e) {
      if (e.target.closest('button, a, label, input')) return;
      openPicker(e);
    });
    if (btn) btn.addEventListener('click', openPicker);

    input.addEventListener('change', function () {
      showName();
      if (autoSubmit && input.files && input.files.length && form) form.submit();
      zone.dispatchEvent(new CustomEvent('fu:change', { detail: { files: input.files }, bubbles: true }));
    });

    ['dragenter', 'dragover'].forEach(function (ev) {
      zone.addEventListener(ev, function (e) {
        e.preventDefault();
        e.stopPropagation();
        zone.classList.add('is-dragover');
      });
    });
    ['dragleave', 'drop'].forEach(function (ev) {
      zone.addEventListener(ev, function (e) {
        e.preventDefault();
        e.stopPropagation();
        if (ev === 'dragleave' && zone.contains(e.relatedTarget)) return;
        zone.classList.remove('is-dragover');
      });
    });
    zone.addEventListener('drop', function (e) {
      var files = e.dataTransfer && e.dataTransfer.files;
      if (!files || !files.length) return;
      try {
        input.files = files;
      } catch (err) { /* Safari may block */ }
      showName();
      if (autoSubmit && form) form.submit();
      zone.dispatchEvent(new CustomEvent('fu:change', { detail: { files: files }, bubbles: true }));
    });

    zone.addEventListener('keydown', function (e) {
      if (e.key === 'Enter' || e.key === ' ') { openPicker(e); }
    });
  }

  function boot() {
    document.querySelectorAll('[data-fu]').forEach(initZone);
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
  window.FileUploadZone = { init: initZone, boot: boot };
})();
