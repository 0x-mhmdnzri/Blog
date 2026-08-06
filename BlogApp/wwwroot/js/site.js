/* Site scripts — nav + admin sidebar. Code highlighting is handled by highlight.js (CDN) + code-onedark.css */
(function () {
  // Navbar collapse toggle
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

  // Language label on fenced blocks (from class="language-csharp")
  function tagCodeLang() {
    document.querySelectorAll('pre.md-code-block code, pre code').forEach(function (code) {
      var pre = code.closest('pre');
      if (!pre || pre.getAttribute('data-lang')) return;
      var cls = code.className || '';
      var m = cls.match(/language-([\w+-]+)/i) || cls.match(/hljs\s+([\w+-]+)/i);
      if (m) pre.setAttribute('data-lang', m[1]);
    });
  }

  function runHljs() {
    if (window.hljs && typeof window.hljs.highlightAll === 'function') {
      try { window.hljs.highlightAll(); } catch (e) { /* ignore */ }
    }
    tagCodeLang();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', runHljs);
  } else {
    runHljs();
  }
})();
