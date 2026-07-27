(function () {
  const textarea = document.getElementById('markdown-input');
  const preview = document.getElementById('preview-pane');
  const dropzone = document.getElementById('dropzone');
  const fileInput = document.getElementById('file-input');
  if (!textarea) return;

  const previewUrl = textarea.dataset.previewUrl;
  const uploadUrl = textarea.dataset.uploadUrl;
  const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

  function insertAtCursor(text) {
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const before = textarea.value.substring(0, start);
    const after = textarea.value.substring(end);
    textarea.value = before + text + after;
    const cursor = start + text.length;
    textarea.setSelectionRange(cursor, cursor);
    textarea.focus();
    scheduleRender();
  }

  function wrapSelection(prefix, suffix) {
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const selected = textarea.value.substring(start, end) || 'text';
    insertReplace(start, end, prefix + selected + suffix);
  }

  function insertReplace(start, end, text) {
    textarea.value = textarea.value.substring(0, start) + text + textarea.value.substring(end);
    scheduleRender();
  }

  document.querySelectorAll('[data-md-action]').forEach(btn => {
    btn.addEventListener('click', () => {
      const action = btn.dataset.mdAction;
      switch (action) {
        case 'bold': wrapSelection('**', '**'); break;
        case 'italic': wrapSelection('_', '_'); break;
        case 'code-inline': wrapSelection('`', '`'); break;
        case 'code-block': insertAtCursor('\n```csharp\n// code here\n```\n'); break;
        case 'h2': insertAtCursor('\n## Heading\n'); break;
        case 'link': insertAtCursor('[link text](https://example.com)'); break;
        case 'table':
          insertAtCursor('\n| Column A | Column B |\n| --- | --- |\n| value | value |\n');
          break;
        case 'quote': insertAtCursor('\n> quoted text\n'); break;
      }
    });
  });

  // ---- Live preview (debounced) ----
  let renderTimer = null;
  function scheduleRender() {
    clearTimeout(renderTimer);
    renderTimer = setTimeout(renderPreview, 350);
  }

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

  // ---- Media upload (drag & drop + file picker) ----
  async function uploadFile(file) {
    if (!uploadUrl) return;
    const form = new FormData();
    form.append('file', file);
    form.append('__RequestVerificationToken', token || '');

    dropzone.textContent = `Uploading ${file.name}…`;
    try {
      const res = await fetch(uploadUrl, { method: 'POST', body: form });
      if (!res.ok) throw new Error('Upload failed');
      const data = await res.json();
      insertAtCursor('\n' + data.markdownSnippet + '\n');
      dropzone.textContent = 'Drop an image or video here, or click to upload';
    } catch (err) {
      dropzone.textContent = 'Upload failed — try again';
      console.error(err);
    }
  }

  if (dropzone && fileInput) {
    dropzone.addEventListener('click', () => fileInput.click());
    fileInput.addEventListener('change', () => {
      if (fileInput.files[0]) uploadFile(fileInput.files[0]);
    });
    ['dragover', 'dragenter'].forEach(evt =>
      dropzone.addEventListener(evt, e => { e.preventDefault(); dropzone.style.borderColor = 'var(--accent)'; }));
    ['dragleave', 'drop'].forEach(evt =>
      dropzone.addEventListener(evt, e => { e.preventDefault(); dropzone.style.borderColor = 'var(--border)'; }));
    dropzone.addEventListener('drop', e => {
      const file = e.dataTransfer.files[0];
      if (file) uploadFile(file);
    });
  }

  // Initial render on load (Edit page starts with existing content)
  scheduleRender();
})();
