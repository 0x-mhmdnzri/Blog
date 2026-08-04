(function () {
  var T = window.__mediaI18n || { selected: '{0} selected', selectedZero: 'Nothing selected', copied: 'Copied', usageNone: '' };

  function toast(msg) {
    var el = document.getElementById('mediaToast');
    if (!el) return;
    el.textContent = msg;
    el.hidden = false;
    clearTimeout(el._t);
    el._t = setTimeout(function () { el.hidden = true; }, 1600);
  }

  function copyText(t) {
    if (navigator.clipboard && navigator.clipboard.writeText)
      return navigator.clipboard.writeText(t);
    var i = document.createElement('input');
    i.value = t; document.body.appendChild(i); i.select();
    try { document.execCommand('copy'); } catch (e) {}
    i.remove();
    return Promise.resolve();
  }

  var zone = document.getElementById('mediaDropzone');
  var input = document.getElementById('mediaFileInput');
  var pickBtn = document.getElementById('mediaPickBtn');
  var pickTop = document.getElementById('mediaPickBtnTop');
  var emptyPick = document.getElementById('mediaEmptyPick');
  var nameEl = document.getElementById('mediaPickedName');
  var submitBtn = document.getElementById('mediaUploadSubmit');
  var form = document.getElementById('mediaUploadForm');

  function openPicker() { if (input) input.click(); }
  [pickBtn, pickTop, emptyPick].forEach(function (btn) {
    if (btn) btn.addEventListener('click', function (e) { e.preventDefault(); e.stopPropagation(); openPicker(); });
  });
  if (zone) {
    zone.addEventListener('click', function (e) {
      if (e.target.closest('button') || e.target.closest('input')) return;
      openPicker();
    });
    zone.addEventListener('keydown', function (e) {
      if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); openPicker(); }
    });
    ['dragenter', 'dragover'].forEach(function (ev) {
      zone.addEventListener(ev, function (e) {
        e.preventDefault(); e.stopPropagation();
        zone.classList.add('is-dragover');
      });
    });
    ['dragleave', 'drop'].forEach(function (ev) {
      zone.addEventListener(ev, function (e) {
        e.preventDefault(); e.stopPropagation();
        zone.classList.remove('is-dragover');
      });
    });
    zone.addEventListener('drop', function (e) {
      var files = e.dataTransfer && e.dataTransfer.files;
      if (!files || !files.length || !input) return;
      input.files = files;
      onFilePicked();
      if (form) form.submit();
    });
  }
  function onFilePicked() {
    if (!input || !input.files || !input.files[0]) return;
    if (nameEl) { nameEl.hidden = false; nameEl.textContent = input.files[0].name; }
    if (submitBtn) submitBtn.hidden = false;
  }
  if (input) input.addEventListener('change', function () {
    onFilePicked();
    if (input.files && input.files[0] && form) form.submit();
  });

  var bulkBtn = document.getElementById('bulkDeleteBtn');
  var clearBtn = document.getElementById('mediaClearSel');
  var selectAll = document.getElementById('mediaSelectAll');
  var countEl = document.getElementById('mediaSelCount');
  var selBar = document.getElementById('mediaSelectionBar');

  function visibleCbs() {
    return Array.prototype.slice.call(document.querySelectorAll('.ml-card:not(.is-hidden) .media-cb'));
  }
  function syncSel() {
    var all = visibleCbs();
    var n = all.filter(function (c) { return c.checked; }).length;
    if (bulkBtn) bulkBtn.disabled = n === 0;
    if (clearBtn) clearBtn.disabled = n === 0;
    if (countEl) countEl.textContent = n === 0 ? T.selectedZero : T.selected.replace('{0}', String(n));
    if (selectAll && all.length) {
      selectAll.checked = n === all.length;
      selectAll.indeterminate = n > 0 && n < all.length;
    }
    if (selBar) selBar.setAttribute('data-active', n > 0 ? '1' : '0');
    document.querySelectorAll('.ml-card').forEach(function (card) {
      var cb = card.querySelector('.media-cb');
      card.classList.toggle('is-selected', !!(cb && cb.checked));
    });
  }
  document.querySelectorAll('.media-cb').forEach(function (c) { c.addEventListener('change', syncSel); });
  if (selectAll) {
    selectAll.addEventListener('change', function () {
      visibleCbs().forEach(function (c) { c.checked = selectAll.checked; });
      syncSel();
    });
  }
  if (clearBtn) {
    clearBtn.addEventListener('click', function () {
      document.querySelectorAll('.media-cb').forEach(function (c) { c.checked = false; });
      if (selectAll) { selectAll.checked = false; selectAll.indeterminate = false; }
      syncSel();
    });
  }
  syncSel();

  var searchInput = document.getElementById('mediaSearchInput');
  var hint = document.getElementById('mediaVisibleHint');
  function filterLocal() {
    var q = (searchInput && searchInput.value || '').trim().toLowerCase();
    var cards = document.querySelectorAll('.ml-card');
    var shown = 0;
    cards.forEach(function (card) {
      var name = card.getAttribute('data-name') || '';
      var ok = !q || name.indexOf(q) !== -1;
      card.classList.toggle('is-hidden', !ok);
      if (ok) shown++;
    });
    if (hint) {
      if (q) {
        hint.hidden = false;
        hint.textContent = shown + ' / ' + cards.length;
      } else {
        hint.hidden = true;
      }
    }
    syncSel();
  }
  if (searchInput) searchInput.addEventListener('input', filterLocal);

  var grid = document.getElementById('mediaGrid');
  document.querySelectorAll('.ml-view-btn').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var v = btn.getAttribute('data-view');
      if (!grid) return;
      grid.setAttribute('data-view', v);
      document.querySelectorAll('.ml-view-btn').forEach(function (b) {
        var on = b.getAttribute('data-view') === v;
        b.classList.toggle('is-active', on);
        b.setAttribute('aria-pressed', on ? 'true' : 'false');
      });
      try { localStorage.setItem('blog-media-view', v); } catch (e) {}
    });
  });
  try {
    var saved = localStorage.getItem('blog-media-view');
    if (saved && grid) {
      var b = document.querySelector('.ml-view-btn[data-view="' + saved + '"]');
      if (b) b.click();
    }
  } catch (e) {}

  document.querySelectorAll('[data-copy]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      copyText(btn.getAttribute('data-copy') || '').then(function () {
        toast(T.copied);
        var old = btn.textContent;
        btn.textContent = '\u2713';
        setTimeout(function () { btn.textContent = old; }, 900);
      });
    });
  });
  document.querySelectorAll('[data-md]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      copyText(btn.getAttribute('data-md') || '').then(function () {
        toast(T.copied);
        var old = btn.textContent;
        btn.textContent = '\u2713';
        setTimeout(function () { btn.textContent = old; }, 900);
      });
    });
  });

  var usageModal = document.getElementById('usageModal');
  var usageBody = document.getElementById('usageBody');
  document.querySelectorAll('[data-usage]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var id = btn.getAttribute('data-usage');
      usageBody.innerHTML = '<p class="text-muted-dark small">\u2026</p>';
      fetch('/Admin/MediaUsage?id=' + id)
        .then(function (r) { return r.json(); })
        .then(function (data) {
          var list = data.usages || [];
          if (!list.length) {
            usageBody.innerHTML = '<p class="text-muted-dark small mb-0">' + (T.usageNone || '') + '</p>';
          } else {
            var html = '<ul class="mb-0" style="padding-inline-start:1.1rem;">';
            list.forEach(function (u) {
              var title = u.title || u.Title || ('#' + (u.id || u.Id));
              var href = u.editUrl || u.EditUrl || '#';
              var cover = u.isCover || u.IsCover;
              html += '<li class="mb-1"><a href="' + href + '">' + title + '</a>'
                + (cover ? ' <span class="soon-tag">cover</span>' : '') + '</li>';
            });
            html += '</ul>';
            usageBody.innerHTML = html;
          }
          if (window.bootstrap) new bootstrap.Modal(usageModal).show();
        })
        .catch(function () { usageBody.innerHTML = '<p class="text-muted-dark small">\u2014</p>'; });
    });
  });

  var lb = document.getElementById('mediaLightbox');
  var lbBody = document.getElementById('mediaLbBody');
  var lbCap = document.getElementById('mediaLbCaption');
  var lbClose = document.getElementById('mediaLbClose');
  function closeLb() {
    if (!lb) return;
    lb.hidden = true;
    if (lbBody) lbBody.innerHTML = '';
  }
  document.querySelectorAll('[data-lightbox]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var url = btn.getAttribute('data-lightbox');
      var kind = btn.getAttribute('data-kind');
      var title = btn.getAttribute('data-title') || '';
      if (!lb || !lbBody) return;
      if (kind === 'Video') {
        lbBody.innerHTML = '<video src="' + url + '" controls autoplay playsinline></video>';
      } else {
        lbBody.innerHTML = '<img src="' + url + '" alt="" />';
      }
      if (lbCap) lbCap.textContent = title + ' \u00b7 ' + url;
      lb.hidden = false;
    });
  });
  if (lbClose) lbClose.addEventListener('click', closeLb);
  if (lb) lb.addEventListener('click', function (e) { if (e.target === lb) closeLb(); });
  document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeLb(); });
})();
