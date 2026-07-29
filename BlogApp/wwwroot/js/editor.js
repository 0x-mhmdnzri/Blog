(function () {
  const textarea = document.getElementById('markdown-input');
  const preview = document.getElementById('preview-pane');
  const dropzone = document.getElementById('dropzone');
  const fileInput = document.getElementById('file-input');
  if (!textarea) return;

  const previewUrl = textarea.dataset.previewUrl;
  const uploadUrl = textarea.dataset.uploadUrl;
  const autosaveUrl = textarea.dataset.autosaveUrl;
  const postId = parseInt(textarea.dataset.postId || '0', 10);
  const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

  function insertAtCursor(text) {
    const start = textarea.selectionStart, end = textarea.selectionEnd;
    textarea.value = textarea.value.substring(0, start) + text + textarea.value.substring(end);
    textarea.setSelectionRange(start + text.length, start + text.length);
    textarea.focus(); scheduleRender();
  }
  function wrapSelection(prefix, suffix) {
    const start = textarea.selectionStart, end = textarea.selectionEnd;
    const selected = textarea.value.substring(start, end) || 'text';
    textarea.value = textarea.value.substring(0, start) + prefix + selected + suffix + textarea.value.substring(end);
    scheduleRender();
  }

  document.querySelectorAll('[data-md-action]').forEach(btn => {
    btn.addEventListener('click', () => {
      const a = btn.dataset.mdAction;
      if (a === 'bold') wrapSelection('**', '**');
      else if (a === 'italic') wrapSelection('_', '_');
      else if (a === 'code-inline') wrapSelection('`', '`');
      else if (a === 'code-block') insertAtCursor('\n```\n// code\n```\n');
      else if (a === 'h2') insertAtCursor('\n## heading\n');
      else if (a === 'link') insertAtCursor('[text](https://example.com)');
      else if (a === 'table') insertAtCursor('\n| A | B |\n| --- | --- |\n| x | y |\n');
      else if (a === 'quote') insertAtCursor('\n> quote\n');
    });
  });

  let renderTimer = null;
  function scheduleRender() { clearTimeout(renderTimer); renderTimer = setTimeout(renderPreview, 350); }
  async function renderPreview() {
    if (!preview || !previewUrl) return;
    const body = new URLSearchParams();
    body.set('content', textarea.value);
    body.set('__RequestVerificationToken', token || '');
    const res = await fetch(previewUrl, { method: 'POST', body });
    if (res.ok) {
      preview.innerHTML = await res.text();
      if (window.hljs) preview.querySelectorAll('pre code').forEach(el => hljs.highlightElement(el));
    }
  }
  textarea.addEventListener('input', scheduleRender);

  async function uploadFile(file) {
    if (!uploadUrl) return;
    const form = new FormData();
    form.append('file', file);
    form.append('__RequestVerificationToken', token || '');
    dropzone.textContent = 'Uploading ' + file.name + '\u2026';
    try {
      const res = await fetch(uploadUrl, { method: 'POST', body: form });
      if (!res.ok) throw new Error('fail');
      const data = await res.json();
      insertAtCursor('\n' + data.markdownSnippet + '\n');
      dropzone.textContent = 'Drop image/video or click';
    } catch (e) { dropzone.textContent = 'Upload failed'; console.error(e); }
  }
  if (dropzone && fileInput) {
    dropzone.addEventListener('click', () => fileInput.click());
    fileInput.addEventListener('change', () => { if (fileInput.files[0]) uploadFile(fileInput.files[0]); });
    ['dragover','dragenter'].forEach(evt => dropzone.addEventListener(evt, e => { e.preventDefault(); dropzone.style.borderColor = 'var(--accent)'; }));
    ['dragleave','drop'].forEach(evt => dropzone.addEventListener(evt, e => { e.preventDefault(); dropzone.style.borderColor = 'var(--border)'; }));
    dropzone.addEventListener('drop', e => { const f = e.dataTransfer.files[0]; if (f) uploadFile(f); });
  }

  let lastSaved = textarea.value, autosaveTimer = null;
  async function doAutosave() {
    if (!autosaveUrl || postId <= 0 || textarea.value === lastSaved) return;
    const titleInput = document.querySelector('input[name="Title"]');
    const summaryInput = document.getElementById('summary-input');
    const body = new URLSearchParams();
    body.set('id', postId);
    body.set('title', titleInput ? titleInput.value : '');
    body.set('contentMarkdown', textarea.value);
    body.set('summary', summaryInput ? summaryInput.value : '');
    body.set('__RequestVerificationToken', token || '');
    try {
      const res = await fetch(autosaveUrl, { method: 'POST', body });
      if (res.ok) {
        const data = await res.json();
        lastSaved = textarea.value;
        const rt = document.getElementById('reading-time-display');
        if (rt && data.readingTimeMinutes) rt.textContent = data.readingTimeMinutes;
        const btn = document.getElementById('btn-autosave');
        if (btn) { const o = btn.textContent; btn.textContent = 'Saved'; setTimeout(() => btn.textContent = o, 2000); }
      }
    } catch (e) { console.warn('autosave failed', e); }
  }
  if (postId > 0) {
    textarea.addEventListener('input', () => { clearTimeout(autosaveTimer); autosaveTimer = setTimeout(doAutosave, 30000); });
    const autosaveBtn = document.getElementById('btn-autosave');
    if (autosaveBtn) autosaveBtn.addEventListener('click', doAutosave);
  }

  function antiforgery() { return token || document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''; }

  const btnSummarize = document.getElementById('btn-ai-summarize');
  if (btnSummarize) btnSummarize.addEventListener('click', async () => {
    const body = new URLSearchParams(); body.set('content', textarea.value); body.set('__RequestVerificationToken', antiforgery());
    const res = await fetch('/Posts/AiSummarize', { method: 'POST', body });
    if (res.ok) { const data = await res.json(); const input = document.getElementById('summary-input'); if (input) input.value = data.summary || ''; }
  });

  const btnAssist = document.getElementById('btn-ai-assist');
  if (btnAssist) btnAssist.addEventListener('click', async () => {
    const body = new URLSearchParams(); body.set('content', textarea.value); body.set('__RequestVerificationToken', antiforgery());
    const res = await fetch('/Posts/AiAssist', { method: 'POST', body });
    if (res.ok) {
      const data = await res.json();
      const titleInput = document.querySelector('input[name="Title"]');
      if (titleInput && data.suggestedTitle && !titleInput.value.trim()) titleInput.value = data.suggestedTitle;
      const tagsInput = document.getElementById('tags-input');
      if (tagsInput && data.suggestedTags?.length) tagsInput.value = data.suggestedTags.join(', ');
    }
  });

  const btnGrammar = document.getElementById('btn-ai-grammar');
  if (btnGrammar) btnGrammar.addEventListener('click', async () => {
    const body = new URLSearchParams(); body.set('content', textarea.value); body.set('__RequestVerificationToken', antiforgery());
    const res = await fetch('/Posts/AiGrammarCheck', { method: 'POST', body });
    const hintsEl = document.getElementById('ai-hints');
    if (res.ok && hintsEl) {
      const data = await res.json();
      hintsEl.classList.remove('d-none');
      hintsEl.innerHTML = '<strong>Hints:</strong><ul class="mb-0 mt-1">' + (data.hints || []).map(h => '<li>' + h + '</li>').join('') + '</ul>';
    }
  });

  scheduleRender();
})();
