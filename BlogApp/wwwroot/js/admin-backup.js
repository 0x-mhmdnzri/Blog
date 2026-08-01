(function () {
  const root = document.querySelector('.bk-page');
  if (!root) return;
  const url = root.getAttribute('data-stats-url');
  const pollMs = parseInt(root.getAttribute('data-poll-ms') || '2500', 10);
  const live = document.getElementById('bkLive');
  const canvas = document.getElementById('bkIoChart');
  const ctx = canvas ? canvas.getContext('2d') : null;

  let prev = null;
  let prevAt = 0;
  const histR = [];
  const histW = [];
  const histMax = 40;

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
    const bps = delta / (ms / 1000);
    return fmt(bps) + '/s';
  }

  function setText(id, t) {
    const el = document.getElementById(id);
    if (el) el.textContent = t;
  }

  function setBar(id, part, total) {
    const el = document.getElementById(id);
    if (!el) return;
    const pct = total > 0 ? Math.min(100, (100 * part) / total) : 0;
    el.style.width = pct.toFixed(1) + '%';
  }

  function setRing(pct) {
    const ring = document.getElementById('bkVolRing');
    const label = document.getElementById('bkVolPct');
    if (label) label.textContent = (pct || 0).toFixed(1) + '%';
    if (!ring) return;
    const c = 2 * Math.PI * 52;
    const p = Math.max(0, Math.min(100, pct || 0)) / 100;
    ring.style.strokeDasharray = String(c);
    ring.style.strokeDashoffset = String(c * (1 - p));
    if (p > 0.9) ring.style.stroke = '#ff453a';
    else if (p > 0.75) ring.style.stroke = '#ff9f0a';
    else ring.style.stroke = getComputedStyle(document.documentElement).getPropertyValue('--accent').trim() || '#e3b341';
  }

  function drawChart() {
    if (!ctx || !canvas) return;
    const dpr = window.devicePixelRatio || 1;
    const w = canvas.clientWidth || 640;
    const h = 100;
    canvas.width = w * dpr;
    canvas.height = h * dpr;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, w, h);
    const max = Math.max(1, ...histR, ...histW);
    function series(arr, color) {
      if (arr.length < 2) return;
      ctx.beginPath();
      arr.forEach((v, i) => {
        const x = (i / (histMax - 1)) * (w - 8) + 4;
        const y = h - 6 - (v / max) * (h - 16);
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      });
      ctx.strokeStyle = color;
      ctx.lineWidth = 1.75;
      ctx.stroke();
    }
    series(histR, '#0a84ff');
    series(histW, '#e3b341');
  }

  async function tick() {
    try {
      const res = await fetch(url, { headers: { Accept: 'application/json' }, credentials: 'same-origin' });
      if (!res.ok) throw new Error('stats ' + res.status);
      const s = await res.json();
      const now = Date.now();

      setRing(s.volumeUsedPercent);
      setText('bkVolTotal', fmt(s.volumeTotalBytes));
      setText('bkVolFree', fmt(s.volumeFreeBytes));
      if (s.volumeRoot) setText('bkVolRoot', s.volumeRoot);
      setText('bkDbBytes', fmt(s.databaseBytes));
      setText('bkWalBytes', fmt(s.databaseWalBytes));
      setText('bkMediaBytes', fmt(s.mediaBytes));
      setText('bkDataBytes', fmt(s.dataRootBytes));
      setText('bkBackupBytes', fmt(s.backupDirBytes) + ' · ' + (s.backupFileCount || 0) + ' files');
      if (s.backupDirectory) setText('bkBackupPath', s.backupDirectory);
      setText('bkFileCount', (s.backupFileCount || 0) + ' files');

      const scale = Math.max(s.dataRootBytes, s.backupDirBytes, s.databaseBytes, 1);
      setBar('bkDbBar', s.databaseBytes, scale);
      setBar('bkWalBar', s.databaseWalBytes, scale);
      setBar('bkMediaBar', s.mediaBytes, scale);
      setBar('bkDataBar', s.dataRootBytes, scale);
      setBar('bkBackupBar', s.backupDirBytes, scale);

      setText('bkIoRead', fmt(s.processReadBytes));
      setText('bkIoWrite', fmt(s.processWriteBytes));
      if (!s.processIoAvailable) {
        setText('bkIoHint', 'I/O N/A');
        setText('bkIoReadRate', '—');
        setText('bkIoWriteRate', '—');
      } else if (prev) {
        const dt = now - prevAt;
        const rRate = (s.processReadBytes - prev.processReadBytes) / Math.max(dt / 1000, 0.001);
        const wRate = (s.processWriteBytes - prev.processWriteBytes) / Math.max(dt / 1000, 0.001);
        setText('bkIoReadRate', rate(s.processReadBytes - prev.processReadBytes, dt));
        setText('bkIoWriteRate', rate(s.processWriteBytes - prev.processWriteBytes, dt));
        histR.push(Math.max(0, rRate));
        histW.push(Math.max(0, wRate));
        while (histR.length > histMax) histR.shift();
        while (histW.length > histMax) histW.shift();
        drawChart();
      }

      prev = s;
      prevAt = now;
      if (live) { live.classList.remove('is-off'); }
    } catch (e) {
      if (live) live.classList.add('is-off');
    }
  }

  document.getElementById('bkCreateBtn')?.addEventListener('click', function () {
    this.classList.add('is-busy');
    this.textContent = 'Creating snapshot…';
  });

  tick();
  setInterval(tick, pollMs);
  window.addEventListener('resize', drawChart);
})();
