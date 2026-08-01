/* Dark Pro — local site scripts (no CDN). Navbar toggle + light code highlighting. */
(function () {
  // Navbar collapse toggle (replaces Bootstrap JS collapse)
  document.querySelectorAll('[data-nav-toggle]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var targetId = btn.getAttribute('data-nav-toggle');
      var target = document.getElementById(targetId);
      if (target) target.classList.toggle('show');
    });
  });

  // Admin sidebar toggle + backdrop
  var sidebar = document.getElementById('adminSidebar');
  var backdrop = document.getElementById('adminSidebarBackdrop');
  document.querySelectorAll('[data-admin-toggle]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      if (!sidebar) return;
      sidebar.classList.toggle('open');
      if (backdrop) backdrop.classList.toggle('show', sidebar.classList.contains('open'));
    });
  });
  if (backdrop) {
    backdrop.addEventListener('click', function () {
      if (sidebar) sidebar.classList.remove('open');
      backdrop.classList.remove('show');
    });
  }

  // Lightweight code highlighter (keywords only — no external lib)
  function highlightCodeBlocks() {
    var keywords = /\b(function|return|var|let|const|if|else|for|while|class|new|this|import|export|from|async|await|try|catch|throw|public|private|protected|static|void|int|string|bool|true|false|null|undefined|using|namespace|def|print|self)\b/g;
    document.querySelectorAll('pre code').forEach(function (el) {
      if (el.dataset.hl === '1') return;
      var html = el.textContent
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
      html = html.replace(keywords, '<span class="hl-kw">$1</span>');
      html = html.replace(/("(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*')/g, '<span class="hl-str">$1</span>');
      html = html.replace(/(\/\/[^\n]*|#(?!!).*)/g, '<span class="hl-cmt">$1</span>');
      el.innerHTML = html;
      el.dataset.hl = '1';
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', highlightCodeBlocks);
  } else {
    highlightCodeBlocks();
  }
  window.hljs = { highlightAll: highlightCodeBlocks, highlightElement: function (el) {
    if (el) { el.dataset.hl = ''; highlightCodeBlocks(); }
  }};
})();
