(function () {
  const textarea = document.getElementById('markdown-input');
  const preview = document.getElementById('preview-pane');
  const dropzone = document.getElementById('dropzone');
  const fileInput = document.getElementById('file-input');
  if (!textarea) return;

  const previewUrl = textarea.dataset.previewUrl;
  const uploadUrl = textarea.dataset.uploadUrl;
  const autosaveUrl = textarea.dataset.autosaveUrl;
  let postId = parseInt(textarea.dataset.postId || '0', 10);
  const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
  const draftKey = 'blog.draft.' + (postId > 0 ? postId : 'new');

  function insertAtCursor(text) {
    const start = textarea.selectionStart, end = textarea.selectionEnd;
    textarea.value = textarea.value.substring(0, start) + text + textarea.value.substring(end);
    textarea.setSelectionRange(start + text.length, start + text.length);
    textarea.focus(); scheduleRender(); markDirty();
  }
  function wrapSelection(prefix, suffix) {
    const start = textarea.selectionStart, end = textarea.selectionEnd;
    const selected = textarea.value.substring(start, end) || 'text';
    textarea.value = textarea.value.substring(0, start) + prefix + selected + suffix + textarea.value.substring(end);
    scheduleRender(); markDirty();
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
    try {
      const res = await fetch(previewUrl, {
        method: 'POST',
        body,
        headers: { 'X-No-Toast': '1' }
      });
      if (res.ok) {
        preview.innerHTML = await res.text();
        if (window.hljs) preview.querySelectorAll('pre code').forEach(el => hljs.highlightElement(el));
      }
    } catch (e) { console.warn('preview failed', e); }
  }
  textarea.addEventListener('input', function () { scheduleRender(); markDirty(); });

  async function uploadFile(file) {
    if (!uploadUrl) return;
    const form = new FormData();
    form.append('file', file);
    form.append('__RequestVerificationToken', token || '');
    dropzone.textContent = 'Uploading ' + file.name + '…';
    try {
      const res = await fetch(uploadUrl, {
        method: 'POST',
        body: form,
        headers: { 'X-No-Toast': '1' }
      });
      if (!res.ok) throw new Error('fail');
      const data = await res.json();
      insertAtCursor('\n' + (data.markdownSnippet || data.url || '') + '\n');
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

  // —— Continuous autosave (server + localStorage) ——
  let lastSaved = textarea.value;
  let dirty = false;
  let autosaveTimer = null;
  const statusEl = document.getElementById('autosave-status');

  function markDirty() {
    dirty = true;
    try {
      localStorage.setItem(draftKey, JSON.stringify({
        title: (document.querySelector('input[name="Title"]') || {}).value || '',
        summary: (document.getElementById('summary-input') || {}).value || '',
        content: textarea.value,
        at: Date.now()
      }));
    } catch (e) {}
  }

  function setStatus(msg) {
    if (statusEl) statusEl.textContent = msg;
  }

  /** Keep hidden Id + form action in sync after first autosave create. */
  function bindDraftToForm(id) {
    postId = id;
    textarea.dataset.postId = String(id);
    var idInput = document.querySelector('#postCreateForm input[name="Id"], #postEditForm input[name="Id"], input[name="Id"]');
    if (idInput) idInput.value = String(id);
    var form = document.getElementById('postCreateForm') || document.getElementById('postEditForm');
    if (form) {
      form.action = '/Posts/Edit/' + id;
      form.setAttribute('action', '/Posts/Edit/' + id);
      // After first autosave, subsequent full submits must hit Edit
      form.id = 'postEditForm';
    }
    try {
      history.replaceState(null, '', '/Posts/Edit/' + id);
      localStorage.removeItem('blog.draft.new');
    } catch (e) {}
  }

  async function doAutosave() {
    if (!autosaveUrl) return;
    syncRichToTextarea();
    if (!dirty && textarea.value === lastSaved && postId > 0) return;

    const titleInput = document.querySelector('input[name="Title"]');
    const summaryInput = document.getElementById('summary-input');
    const langSelect = document.querySelector('select[name="LanguageCode"]');
    const body = new URLSearchParams();
    body.set('id', String(postId));
    body.set('title', titleInput ? titleInput.value : '');
    body.set('contentMarkdown', textarea.value);
    body.set('summary', summaryInput ? summaryInput.value : '');
    body.set('languageCode', langSelect ? langSelect.value : '');
    body.set('__RequestVerificationToken', token || '');

    try {
      setStatus('Saving…');
      const res = await fetch(autosaveUrl, {
        method: 'POST',
        body,
        headers: { 'X-No-Toast': '1' }
      });
      if (res.ok) {
        const data = await res.json();
        if (data.ok) {
          lastSaved = textarea.value;
          dirty = false;
          if (data.created && data.id) bindDraftToForm(data.id);
          else if (data.id && postId <= 0) bindDraftToForm(data.id);
          const rt = document.getElementById('reading-time-display');
          if (rt && data.readingTimeMinutes) rt.textContent = data.readingTimeMinutes;
          const t = data.updatedAtUtc ? new Date(data.updatedAtUtc) : new Date();
          setStatus('Saved ' + t.toLocaleTimeString());
        }
      } else setStatus('Save failed');
    } catch (e) {
      console.warn('autosave failed', e);
      setStatus('Offline — local draft kept');
    }
  }

  textarea.addEventListener('input', () => {
    clearTimeout(autosaveTimer);
    autosaveTimer = setTimeout(doAutosave, 8000);
  });
  const titleEl = document.querySelector('input[name="Title"]');
  if (titleEl) titleEl.addEventListener('input', () => {
    markDirty();
    clearTimeout(autosaveTimer);
    autosaveTimer = setTimeout(doAutosave, 8000);
  });

  const autosaveBtn = document.getElementById('btn-autosave');
  if (autosaveBtn) autosaveBtn.addEventListener('click', doAutosave);
  setInterval(doAutosave, 45000);

  window.addEventListener('beforeunload', function (e) {
    if (!dirty) return;
    e.preventDefault();
    e.returnValue = '';
  });

  // Restore local draft if newer than server content (create page)
  try {
    const raw = localStorage.getItem(draftKey);
    if (raw && postId <= 0) {
      const d = JSON.parse(raw);
      if (d && d.content && d.content.length > 20 && !textarea.value.trim()) {
        if (confirm('Restore local draft from this browser?')) {
          textarea.value = d.content || '';
          if (titleEl && d.title) titleEl.value = d.title;
          const s = document.getElementById('summary-input');
          if (s && d.summary) s.value = d.summary;
          markDirty();
        }
      }
    }
  } catch (e) {}

  // —— Rich text optional pane ——
  const rich = document.getElementById('rich-editor');
  const modeBtn = document.getElementById('btn-editor-mode');
  let richMode = false;

  function syncRichToTextarea() {
    if (richMode && rich && !rich.hidden) {
      textarea.value = htmlToRoughMd(rich.innerHTML);
    }
  }

  if (modeBtn && rich) {
    modeBtn.addEventListener('click', function () {
      richMode = !richMode;
      if (richMode) {
        rich.innerHTML = mdToRoughHtml(textarea.value);
        rich.hidden = false;
        textarea.style.display = 'none';
        modeBtn.textContent = 'Markdown';
      } else {
        textarea.value = htmlToRoughMd(rich.innerHTML);
        rich.hidden = true;
        textarea.style.display = '';
        modeBtn.textContent = 'Rich text';
        scheduleRender(); markDirty();
      }
    });
    rich.addEventListener('input', function () {
      textarea.value = htmlToRoughMd(rich.innerHTML);
      markDirty();
      scheduleRender();
    });
  }

  function mdToRoughHtml(md) {
    var h = (md || '')
      .replace(/&/g, '&amp;').replace(/</g, '&lt;')
      .replace(/^### (.+)$/gm, '<h3>$1</h3>')
      .replace(/^## (.+)$/gm, '<h2>$1</h2>')
      .replace(/^# (.+)$/gm, '<h1>$1</h1>')
      .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
      .replace(/_(.+?)_/g, '<em>$1</em>')
      .replace(/`([^`]+)`/g, '<code>$1</code>')
      .replace(/\n\n/g, '</p><p>')
      .replace(/\n/g, '<br>');
    return '<p>' + h + '</p>';
  }
  function htmlToRoughMd(html) {
    var d = document.createElement('div');
    d.innerHTML = html || '';
    function walk(node) {
      if (node.nodeType === 3) return node.textContent;
      if (node.nodeType !== 1) return '';
      var tag = node.tagName.toLowerCase();
      var inner = Array.from(node.childNodes).map(walk).join('');
      if (tag === 'h1') return '\n# ' + inner + '\n';
      if (tag === 'h2') return '\n## ' + inner + '\n';
      if (tag === 'h3') return '\n### ' + inner + '\n';
      if (tag === 'strong' || tag === 'b') return '**' + inner + '**';
      if (tag === 'em' || tag === 'i') return '_' + inner + '_';
      if (tag === 'code') return '`' + inner + '`';
      if (tag === 'br') return '\n';
      if (tag === 'p' || tag === 'div') return '\n\n' + inner;
      if (tag === 'img') {
        var src = node.getAttribute('src') || '';
        var alt = node.getAttribute('alt') || '';
        return '![' + alt + '](' + src + ')';
      }
      return inner;
    }
    return walk(d).replace(/\n{3,}/g, '\n\n').trim();
  }

  // Critical: before any form submit, sync rich→textarea and force ContentMarkdown name
  function onFormSubmit(e) {
    var form = e.target;
    if (!form || (form.id !== 'postCreateForm' && form.id !== 'postEditForm')) return;
    syncRichToTextarea();
    // Ensure the field is not disabled and has the current value
    textarea.disabled = false;
    textarea.removeAttribute('disabled');
    // If still empty, try localStorage draft as last resort
    if (!textarea.value.trim()) {
      try {
        var raw = localStorage.getItem(draftKey) || localStorage.getItem('blog.draft.new');
        if (raw) {
          var d = JSON.parse(raw);
          if (d && d.content && d.content.trim()) textarea.value = d.content;
        }
      } catch (err) {}
    }
  }
  document.addEventListener('submit', onFormSubmit, true);

  function antiforgery() { return token || document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''; }

  const btnSummarize = document.getElementById('btn-ai-summarize');
  if (btnSummarize) btnSummarize.addEventListener('click', async () => {
    const body = new URLSearchParams(); body.set('content', textarea.value); body.set('__RequestVerificationToken', antiforgery());
    const res = await fetch('/Posts/AiSummarize', { method: 'POST', body, headers: { 'X-No-Toast': '1' } });
    if (res.ok) { const data = await res.json(); const input = document.getElementById('summary-input'); if (input) input.value = data.summary || ''; }
  });

  const btnAssist = document.getElementById('btn-ai-assist');
  if (btnAssist) btnAssist.addEventListener('click', async () => {
    const body = new URLSearchParams(); body.set('content', textarea.value); body.set('__RequestVerificationToken', antiforgery());
    const res = await fetch('/Posts/AiAssist', { method: 'POST', body, headers: { 'X-No-Toast': '1' } });
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
    const res = await fetch('/Posts/AiGrammarCheck', { method: 'POST', body, headers: { 'X-No-Toast': '1' } });
    const hintsEl = document.getElementById('ai-hints');
    if (res.ok && hintsEl) {
      const data = await res.json();
      hintsEl.classList.remove('d-none');
      hintsEl.innerHTML = '<strong>Hints:</strong><ul class="mb-0 mt-1">' + (data.hints || []).map(h => '<li>' + h + '</li>').join('') + '</ul>';
    }
  });

  scheduleRender();
})();
