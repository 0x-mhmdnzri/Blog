/**
 * Shared file-upload dropzone — single point of failure for all uploaders.
 * Markup: .fu-zone[data-fu]
 * Optional:
 *   data-fu-auto-submit="1"
 *   data-fu-preview="#selector"  external avatar/preview target
 *   data-fu-kind="image|media|theme|csv|editor"
 * window.__fuI18n = { select, dropTitle, invalidType, clear, image, video, file }
 */
(function () {
  var i18n = function () {
    return window.__fuI18n || {};
  };

  function fmtSize(n) {
    if (n < 1024) return n + ' B';
    if (n < 1024 * 1024) return (n / 1024).toFixed(1) + ' KB';
    return (n / (1024 * 1024)).toFixed(2) + ' MB';
  }

  function extLabel(name) {
    var m = /\.([a-z0-9]+)$/i.exec(name || '');
    return (m ? m[1] : 'FILE').toUpperCase().slice(0, 4);
  }

  function acceptMatches(file, acceptAttr) {
    if (!acceptAttr || !acceptAttr.trim()) return true;
    var parts = acceptAttr.split(',').map(function (s) { return s.trim().toLowerCase(); }).filter(Boolean);
    if (!parts.length) return true;
    var name = (file.name || '').toLowerCase();
    var type = (file.type || '').toLowerCase();
    for (var i = 0; i < parts.length; i++) {
      var p = parts[i];
      if (p.startsWith('.')) {
        if (name.endsWith(p)) return true;
      } else if (p.endsWith('/*')) {
        var prefix = p.slice(0, -1);
        if (type.indexOf(prefix) === 0) return true;
      } else if (p.indexOf('/') !== -1) {
        if (type === p) return true;
      } else if (name.endsWith('.' + p) || type === p) {
        return true;
      }
    }
    return false;
  }

  function filterFiles(fileList, acceptAttr) {
    var ok = [];
    var bad = [];
    Array.prototype.forEach.call(fileList || [], function (f) {
      if (acceptMatches(f, acceptAttr)) ok.push(f);
      else bad.push(f);
    });
    return { ok: ok, bad: bad };
  }

  function assignFiles(input, files) {
    try {
      var dt = new DataTransfer();
      files.forEach(function (f) { dt.items.add(f); });
      input.files = dt.files;
      return true;
    } catch (e) {
      try { input.files = files; return true; } catch (e2) { return false; }
    }
  }

  function renderPreview(zone, files) {
    var box = zone.querySelector('.fu-zone-preview');
    if (!box) return;
    if (zone.__fuUrls) {
      zone.__fuUrls.forEach(function (u) { try { URL.revokeObjectURL(u); } catch (e) {} });
    }
    zone.__fuUrls = [];
    box.innerHTML = '';
    if (!files || !files.length) {
      box.classList.remove('is-visible');
      return;
    }
    box.classList.add('is-visible');
    var L = i18n();
    Array.prototype.forEach.call(files, function (f) {
      var item = document.createElement('div');
      item.className = 'fu-preview-item';
      var type = (f.type || '');
      var meta = document.createElement('div');
      meta.className = 'fu-preview-meta';
      var nameEl = document.createElement('div');
      nameEl.className = 'fu-preview-name';
      nameEl.textContent = f.name;
      nameEl.title = f.name;
      var sub = document.createElement('div');
      sub.className = 'fu-preview-sub';
      var kindLabel = type.indexOf('image/') === 0 ? (L.image || 'Image')
        : type.indexOf('video/') === 0 ? (L.video || 'Video')
        : (L.file || 'File');
      sub.textContent = kindLabel + ' · ' + fmtSize(f.size);

      if (type.indexOf('image/') === 0) {
        var img = document.createElement('img');
        img.className = 'fu-preview-thumb';
        img.alt = '';
        var url = URL.createObjectURL(f);
        zone.__fuUrls.push(url);
        img.src = url;
        item.appendChild(img);
      } else if (type.indexOf('video/') === 0) {
        var vid = document.createElement('video');
        vid.className = 'fu-preview-thumb-video';
        vid.muted = true;
        vid.playsInline = true;
        vid.preload = 'metadata';
        var vurl = URL.createObjectURL(f);
        zone.__fuUrls.push(vurl);
        vid.src = vurl;
        item.appendChild(vid);
      } else {
        var ico = document.createElement('div');
        ico.className = 'fu-preview-file-ico';
        ico.textContent = extLabel(f.name);
        item.appendChild(ico);
      }
      meta.appendChild(nameEl);
      meta.appendChild(sub);
      item.appendChild(meta);
      box.appendChild(item);
    });
  }

  function updateExternalPreview(zone, files) {
    var sel = zone.getAttribute('data-fu-preview');
    if (!sel) return;
    var target = document.querySelector(sel);
    if (!target) return;
    var f = files && files[0];
    if (f && (f.type || '').indexOf('image/') === 0) {
      var url = URL.createObjectURL(f);
      if (zone.__fuExtUrl) try { URL.revokeObjectURL(zone.__fuExtUrl); } catch (e) {}
      zone.__fuExtUrl = url;
      target.innerHTML = '';
      var img = document.createElement('img');
      img.src = url;
      img.alt = '';
      target.appendChild(img);
    }
  }

  function showError(zone, msg) {
    var el = zone.querySelector('.fu-zone-error');
    if (!el) {
      el = document.createElement('p');
      el.className = 'fu-zone-error';
      zone.appendChild(el);
    }
    if (msg) {
      el.textContent = msg;
      el.hidden = false;
      zone.classList.add('is-error');
    } else {
      el.textContent = '';
      el.hidden = true;
      zone.classList.remove('is-error');
    }
  }

  function applyFiles(zone, input, fileList) {
    var accept = input.getAttribute('accept') || '';
    var filtered = filterFiles(fileList, accept);
    var L = i18n();
    if (filtered.bad.length && !filtered.ok.length) {
      showError(zone, L.invalidType || 'File type not allowed');
      return;
    }
    if (filtered.bad.length) {
      showError(zone, L.invalidType || 'Some files were skipped (type not allowed)');
    } else {
      showError(zone, '');
    }
    if (!filtered.ok.length) return;
    assignFiles(input, filtered.ok);
    var picked = zone.querySelector('.fu-zone-picked');
    if (picked) {
      picked.textContent = filtered.ok.map(function (f) { return f.name; }).join(', ');
      picked.hidden = false;
    }
    renderPreview(zone, filtered.ok);
    updateExternalPreview(zone, filtered.ok);
    var form = zone.closest('form') || zone.querySelector('form');
    var autoSubmit = zone.getAttribute('data-fu-auto-submit') === '1';
    zone.dispatchEvent(new CustomEvent('fu:change', { detail: { files: filtered.ok }, bubbles: true }));
    if (autoSubmit && form) form.submit();
  }

  function initZone(zone) {
    if (zone.__fuBound) return;
    zone.__fuBound = true;
    var input = zone.querySelector('input[type="file"]');
    if (!input) return;
    var btn = zone.querySelector('.fu-zone-btn');
    if (!zone.querySelector('.fu-zone-preview')) {
      var prev = document.createElement('div');
      prev.className = 'fu-zone-preview';
      zone.appendChild(prev);
    }
    if (!zone.querySelector('.fu-zone-error')) {
      var err = document.createElement('p');
      err.className = 'fu-zone-error';
      err.hidden = true;
      zone.appendChild(err);
    }

    function openPicker(e) {
      if (e) { e.preventDefault(); e.stopPropagation(); }
      input.click();
    }

    zone.addEventListener('click', function (e) {
      if (e.target.closest('button, a, label, input, .fu-preview-item')) return;
      openPicker(e);
    });
    if (btn) btn.addEventListener('click', openPicker);

    input.addEventListener('change', function () {
      if (input.files && input.files.length) applyFiles(zone, input, input.files);
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
      applyFiles(zone, input, files);
    });

    zone.addEventListener('keydown', function (e) {
      if (e.key === 'Enter' || e.key === ' ') openPicker(e);
    });
  }

  function boot() {
    document.querySelectorAll('[data-fu]').forEach(initZone);
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
  window.FileUploadZone = { init: initZone, boot: boot };
})();
