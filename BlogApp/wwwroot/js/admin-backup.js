(function () {
  'use strict';
  const root = document.querySelector('.bk-page');
  if (!root) return;

  const statsUrl = root.getAttribute('data-stats-url');
  const listUrl = root.getAttribute('data-list-url')
    || (statsUrl ? statsUrl.replace(/Stats\/?$/, 'List') : '/AdminBackup/List');
  const pollMs = Math.max(1500, parseInt(root.getAttribute('data-poll-ms') || '3000', 10));

  const i18n = {
    creating: root.getAttribute('data-i18n-creating') || 'Creating snapshot…',
    ioNa: root.getAttribute('data-i18n-io-na') || 'I/O N/A',
    files: root.getAttribute('data-i18n-files') || '{0} files',
    used: root.getAttribute('data-i18n-used') || 'used',
    download: root.getAttribute('data-i18n-download') || 'Download',
    del: root.getAttribute('data-i18n-delete') || 'Delete',
    confirmDelete: root.getAttribute('data-i18n-confirm-delete') || 'Delete this backup?'
  };

  const live = document.getElementById('bkLive');
  const canvas = document.getElementById('bkIoChart');
  const ctx = canvas ? canvas.getContext('2d') : null;
  const tokenInput = root.querySelector('input[name="__RequestVerificationToken"]')
    || document.querySelector('input[name="__RequestVerificationToken"]');

  let prev = null;
  let prevAt = 0;
  const histR = [];
  const histW = [];
  const histMax = 36;
  let lastListSig = '';

  function pick(s, camel, pascal) {
    if (!s) return undefined;
    if (s[camel] !== undefined && s[camel] !== null) return s[camel];
    if (s[pascal] !== undefined && s[pascal] !== null) return s[pascal];
    return undefined;
  }

  function fmt(bytes) {
    if (bytes == null || isNaN(bytes)) return '—';
    const u = ['B', 'KB', 'MB', 'GB', 'TB'];
    let v = Number(bytes);
    let i = 0;
    while (v >= 1024 && i < u.length - 1) { v /= 1024; i++; }
    return (i === 0 ? v.toFixed(0) : v.toFixed(v >= 10 ? 1 : 2)) + ' ' + u[i];
  }

  function rate(delta, ms) {
    if (ms <= 0 || delta < 0) return '—';
    return fmt(delta / (ms / 1000)) + '/s';
  }

  function setText(id, t) {
    const el = document.getElementById(id);
    if (el && el.textContent !== t) el.textContent = t;
  }

  function setBar(id, part, total) {
    const el = document.getElementById(id);
    if (!el) return;
    const pct = total > 0 ? Math.min(100, (100 * part) / total) : 0;
    const w = pct.toFixed(1) + '%';
    if (el.style.width !== w) el.style.width = w;
  }

  function setRing(pct) {
    const ring = document.getElementById('bkVolRing');
    const label = document.getElementById('bkVolPct');
    const p = Math.max(0, Math.min(100, Number(pct) || 0));
    if (label) label.textContent = p.toFixed(1) + '%';
    if (!ring) return;
    const c = 2 * Math.PI * 52;
    ring.style.strokeDasharray = String(c);
    ring.style.strokeDashoffset = String(c * (1 - p / 100));
    if (p > 90) ring.style.stroke = '#ff453a';
    else if (p > 75) ring.style.stroke = '#ff9f0a';
    else {
      const accent = getComputedStyle(document.documentElement).getPropertyValue('--accent').trim();
      ring.style.stroke = accent || '#e3b341';
    }
  }

  function drawChart() {
    if (!ctx || !canvas) return;
    const dpr = window.devicePixelRatio || 1;
    const w = canvas.clientWidth || 640;
    const h = 96;
    if (canvas.width !== w * dpr || canvas.height !== h * dpr) {
      canvas.width = w * dpr;
      canvas.height = h * dpr;
    }
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, w, h);
    const max = Math.max(1, ...histR, ...histW);
    function line(arr, color) {
      if (!arr.length) return;
      ctx.beginPath();
      ctx.strokeStyle = color;
      ctx.lineWidth = 1.5;
      arr.forEach(function (v, i) {
        const x = (i / Math.max(arr.length - 1, 1)) * (w - 4) + 2;
        const y = h - 4 - (v / max) * (h - 12);
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      });
      ctx.stroke();
    }
    line(histR, '#6fb3d2');
    line(histW, '#e3b341');
  }

  function escapeHtml(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&')
      .replace(/</g, '<')
      .replace(/>/g, '>')
      .replace(/"/g, '"');
  }

  async function tickStats() {
    if (!statsUrl) return;
    try {
      const res = await fetch(statsUrl, {
        headers: { Accept: 'application/json' },
        credentials: 'same-origin',
        cache: 'no-store'
      });
      if (!res.ok) throw new Error('stats ' + res.status);
      const s = await res.json();
      const now = Date.now();

      setRing(pick(s, 'volumeUsedPercent', 'VolumeUsedPercent'));
      setText('bkVolTotal', fmt(pick(s, 'volumeTotalBytes', 'VolumeTotalBytes')));
      setText('bkVolFree', fmt(pick(s, 'volumeFreeBytes', 'VolumeFreeBytes')));
      const volRoot = pick(s, 'volumeRoot', 'VolumeRoot');
      if (volRoot) setText('bkVolRoot', volRoot);

      setText('bkDbBytes', fmt(pick(s, 'databaseBytes', 'DatabaseBytes')));
      setText('bkWalBytes', fmt(pick(s, 'databaseWalBytes', 'DatabaseWalBytes')));
      setText('bkMediaBytes', fmt(pick(s, 'mediaBytes', 'MediaBytes')));
      setText('bkDataBytes', fmt(pick(s, 'dataRootBytes', 'DataRootBytes')));

      const backupBytes = Number(pick(s, 'backupDirBytes', 'BackupDirBytes')) || 0;
      const backupCount = Number(pick(s, 'backupFileCount', 'BackupFileCount')) || 0;
      setText('bkBackupBytes', fmt(backupBytes) + ' · ' + backupCount);
      const backupDir = pick(s, 'backupDirectory', 'BackupDirectory');
      if (backupDir) setText('bkBackupPath', backupDir);
      setText('bkFileCount', i18n.files.replace('{0}', String(backupCount)));

      const dataRoot = Number(pick(s, 'dataRootBytes', 'DataRootBytes')) || 0;
      const dbBytes = Number(pick(s, 'databaseBytes', 'DatabaseBytes')) || 0;
      const scale = Math.max(dataRoot, backupBytes, dbBytes, 1);
      setBar('bkDbBar', dbBytes, scale);
      setBar('bkWalBar', Number(pick(s, 'databaseWalBytes', 'DatabaseWalBytes')) || 0, scale);
      setBar('bkMediaBar', Number(pick(s, 'mediaBytes', 'MediaBytes')) || 0, scale);
      setBar('bkDataBar', dataRoot, scale);
      setBar('bkBackupBar', backupBytes, scale);

      const pread = Number(pick(s, 'processReadBytes', 'ProcessReadBytes')) || 0;
      const pwrite = Number(pick(s, 'processWriteBytes', 'ProcessWriteBytes')) || 0;
      const ioOk = !!pick(s, 'processIoAvailable', 'ProcessIoAvailable');
      setText('bkIoRead', fmt(pread));
      setText('bkIoWrite', fmt(pwrite));

      if (!ioOk) {
        setText('bkIoHint', i18n.ioNa);
        setText('bkIoReadRate', '—');
        setText('bkIoWriteRate', '—');
      } else if (prev) {
        const dt = now - prevAt;
        const pr = Number(pick(prev, 'processReadBytes', 'ProcessReadBytes')) || 0;
        const pw = Number(pick(prev, 'processWriteBytes', 'ProcessWriteBytes')) || 0;
        setText('bkIoReadRate', rate(pread - pr, dt));
        setText('bkIoWriteRate', rate(pwrite - pw, dt));
        histR.push(Math.max(0, (pread - pr) / Math.max(dt / 1000, 0.001)));
        histW.push(Math.max(0, (pwrite - pw) / Math.max(dt / 1000, 0.001)));
        while (histR.length > histMax) histR.shift();
        while (histW.length > histMax) histW.shift();
        drawChart();
      }

      prev = s;
      prevAt = now;
      if (live) live.classList.remove('is-off');
    } catch (e) {
      if (live) live.classList.add('is-off');
    }
  }

  async function tickList() {
    const tbody = document.getElementById('bkTableBody');
    if (!tbody || !listUrl) return;
    try {
      const res = await fetch(listUrl, {
        headers: { Accept: 'application/json' },
        credentials: 'same-origin',
        cache: 'no-store'
      });
      if (!res.ok) return;
      const data = await res.json();
      const items = data.items || data.Items || [];
      if (!Array.isArray(items)) return;

      // Stable signature — skip DOM rewrite when nothing changed (avoids +2/−1 flicker)
      const sig = items.map(function (b) {
        return (b.id != null ? b.id : b.Id) + ':' + (b.sizeBytes != null ? b.sizeBytes : b.SizeBytes);
      }).join('|');
      if (sig === lastListSig) return;
      lastListSig = sig;

      const token = tokenInput ? tokenInput.value : '';
      const confirmJs = JSON.stringify(String(i18n.confirmDelete || ''));

      if (items.length === 0) {
        tbody.innerHTML = '<tr class="bk-empty-row"><td colspan="5" class="bk-empty">—</td></tr>';
        return;
      }

      tbody.innerHTML = items.map(function (b) {
        const id = b.id != null ? b.id : b.Id;
        const fileName = b.fileName || b.FileName || '';
        const kind = b.kind || b.Kind || '';
        const size = b.sizeBytes != null ? b.sizeBytes : b.SizeBytes;
        const created = b.createdAtUtc || b.CreatedAtUtc || '';
        const downloadUrl = b.downloadUrl || b.DownloadUrl || ('/AdminBackup/Download/' + id);
        const createdStr = created ? String(created).replace('T', ' ').slice(0, 16) : '';
        return (
          '<tr data-id="' + id + '">' +
            '<td class="ltr-field bk-mono">' + escapeHtml(fileName) + '</td>' +
            '<td><span class="bk-pill">' + escapeHtml(kind) + '</span></td>' +
            '<td class="ltr-field">' + escapeHtml(fmt(size)) + '</td>' +
            '<td class="ltr-field">' + escapeHtml(createdStr) + '</td>' +
            '<td class="bk-row-actions">' +
              '<a class="bk-btn bk-btn-sm bk-btn-primary" href="' + escapeHtml(downloadUrl) + '">' +
                escapeHtml(i18n.download) + '</a> ' +
              '<form method="post" action="/AdminBackup/Delete" class="d-inline" onsubmit="return confirm(' + confirmJs + ');">' +
                (token ? '<input type="hidden" name="__RequestVerificationToken" value="' + escapeHtml(token) + '" />' : '') +
                '<input type="hidden" name="id" value="' + id + '" />' +
                '<button type="submit" class="bk-btn bk-btn-sm bk-btn-ghost">' + escapeHtml(i18n.del) + '</button>' +
              '</form>' +
            '</td>' +
          '</tr>'
        );
      }).join('');
    } catch (_) { /* keep previous rows */ }
  }

  async function tick() {
    await tickStats();
    await tickList();
  }

  const createBtn = document.getElementById('bkCreateBtn');
  if (createBtn) {
    createBtn.closest('form')?.addEventListener('submit', function () {
      createBtn.classList.add('is-busy');
      createBtn.textContent = i18n.creating;
    });
  }

  tick();
  setInterval(tick, pollMs);
  window.addEventListener('resize', drawChart);
})();
