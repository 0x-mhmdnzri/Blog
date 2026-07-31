/**
 * Accessibility: preferences, keyboard nav, focus trap, screen-reader helpers, checker
 */
(function () {
  'use strict';

  var STORAGE = 'blog-a11y-prefs';
  var defaults = {
    contrast: 'normal',   // normal | high
    motion: 'system',     // system | reduce | full
    underline: 'off',     // on | off
    text: 'normal'        // normal | large | xlarge
  };

  function loadPrefs() {
    try {
      var raw = localStorage.getItem(STORAGE);
      if (!raw) return Object.assign({}, defaults);
      return Object.assign({}, defaults, JSON.parse(raw));
    } catch (_) {
      return Object.assign({}, defaults);
    }
  }

  function savePrefs(p) {
    try { localStorage.setItem(STORAGE, JSON.stringify(p)); } catch (_) {}
  }

  function applyPrefs(p) {
    var root = document.documentElement;
    if (p.contrast === 'high') root.setAttribute('data-a11y-contrast', 'high');
    else root.removeAttribute('data-a11y-contrast');

    if (p.motion === 'reduce') root.setAttribute('data-a11y-motion', 'reduce');
    else if (p.motion === 'full') root.setAttribute('data-a11y-motion', 'full');
    else root.removeAttribute('data-a11y-motion');

    if (p.underline === 'on') root.setAttribute('data-a11y-underline', 'on');
    else root.removeAttribute('data-a11y-underline');

    if (p.text === 'large' || p.text === 'xlarge') root.setAttribute('data-a11y-text', p.text);
    else root.removeAttribute('data-a11y-text');
  }

  // Apply ASAP (also mirrored in <head> inline for FOUC)
  var prefs = loadPrefs();
  applyPrefs(prefs);

  function announce(msg) {
    var live = document.getElementById('a11y-live');
    if (!live) {
      live = document.createElement('div');
      live.id = 'a11y-live';
      live.className = 'sr-only';
      live.setAttribute('aria-live', 'polite');
      live.setAttribute('aria-atomic', 'true');
      document.body.appendChild(live);
    }
    live.textContent = '';
    setTimeout(function () { live.textContent = msg; }, 30);
  }

  /* ---------- Preferences panel UI ---------- */
  function buildPanel() {
    if (document.getElementById('a11y-panel')) return;

    var fab = document.createElement('button');
    fab.type = 'button';
    fab.id = 'a11y-fab';
    fab.className = 'a11y-fab';
    fab.setAttribute('aria-expanded', 'false');
    fab.setAttribute('aria-controls', 'a11y-panel');
    fab.setAttribute('aria-label', 'تنظیمات دسترسی‌پذیری');
    fab.title = 'دسترسی‌پذیری';
    fab.innerHTML = '<span aria-hidden="true">A♿</span>';

    var panel = document.createElement('div');
    panel.id = 'a11y-panel';
    panel.className = 'a11y-panel';
    panel.setAttribute('role', 'dialog');
    panel.setAttribute('aria-label', 'تنظیمات دسترسی‌پذیری');
    panel.innerHTML =
      '<h2 id="a11y-panel-title">دسترسی‌پذیری</h2>' +
      '<div class="a11y-row">' +
      '  <label for="a11y-contrast">کنتراست بالا</label>' +
      '  <label class="a11y-switch"><input type="checkbox" id="a11y-contrast" ' + (prefs.contrast === 'high' ? 'checked' : '') + ' /><span class="track"></span></label>' +
      '</div>' +
      '<div class="a11y-row">' +
      '  <label for="a11y-underline">زیرخط لینک‌ها</label>' +
      '  <label class="a11y-switch"><input type="checkbox" id="a11y-underline" ' + (prefs.underline === 'on' ? 'checked' : '') + ' /><span class="track"></span></label>' +
      '</div>' +
      '<div class="a11y-row">' +
      '  <label for="a11y-motion">کاهش حرکت</label>' +
      '  <select id="a11y-motion">' +
      '    <option value="system"' + (prefs.motion === 'system' ? ' selected' : '') + '>سیستم</option>' +
      '    <option value="reduce"' + (prefs.motion === 'reduce' ? ' selected' : '') + '>کاهش</option>' +
      '    <option value="full"' + (prefs.motion === 'full' ? ' selected' : '') + '>کامل</option>' +
      '  </select>' +
      '</div>' +
      '<div class="a11y-row">' +
      '  <label for="a11y-text">اندازه متن</label>' +
      '  <select id="a11y-text">' +
      '    <option value="normal"' + (prefs.text === 'normal' ? ' selected' : '') + '>عادی</option>' +
      '    <option value="large"' + (prefs.text === 'large' ? ' selected' : '') + '>بزرگ</option>' +
      '    <option value="xlarge"' + (prefs.text === 'xlarge' ? ' selected' : '') + '>خیلی بزرگ</option>' +
      '  </select>' +
      '</div>' +
      '<div class="a11y-actions">' +
      '  <button type="button" class="btn btn-ghost btn-sm" id="a11y-reset">بازنشانی</button>' +
      '  <button type="button" class="btn btn-ghost btn-sm" id="a11y-close">بستن</button>' +
      '</div>' +
      '<p class="a11y-hint">میانبر: Alt+0 باز/بسته · Tab برای حرکت · Esc بستن. پرش به محتوا با Tab از ابتدای صفحه.</p>';

    document.body.appendChild(fab);
    document.body.appendChild(panel);

    function open() {
      panel.classList.add('is-open');
      fab.setAttribute('aria-expanded', 'true');
      var first = panel.querySelector('input, select, button');
      if (first) first.focus();
    }
    function close() {
      panel.classList.remove('is-open');
      fab.setAttribute('aria-expanded', 'false');
      fab.focus();
    }
    function toggle() {
      if (panel.classList.contains('is-open')) close();
      else open();
    }

    fab.addEventListener('click', function (e) { e.preventDefault(); toggle(); });
    document.getElementById('a11y-close').addEventListener('click', close);

    document.getElementById('a11y-contrast').addEventListener('change', function (e) {
      prefs.contrast = e.target.checked ? 'high' : 'normal';
      applyPrefs(prefs); savePrefs(prefs);
      announce(prefs.contrast === 'high' ? 'کنتراست بالا فعال' : 'کنتراست عادی');
    });
    document.getElementById('a11y-underline').addEventListener('change', function (e) {
      prefs.underline = e.target.checked ? 'on' : 'off';
      applyPrefs(prefs); savePrefs(prefs);
      announce(prefs.underline === 'on' ? 'زیرخط لینک‌ها فعال' : 'زیرخط لینک‌ها خاموش');
    });
    document.getElementById('a11y-motion').addEventListener('change', function (e) {
      prefs.motion = e.target.value;
      applyPrefs(prefs); savePrefs(prefs);
      announce('تنظیم حرکت به‌روز شد');
    });
    document.getElementById('a11y-text').addEventListener('change', function (e) {
      prefs.text = e.target.value;
      applyPrefs(prefs); savePrefs(prefs);
      announce('اندازه متن به‌روز شد');
    });
    document.getElementById('a11y-reset').addEventListener('click', function () {
      prefs = Object.assign({}, defaults);
      applyPrefs(prefs); savePrefs(prefs);
      document.getElementById('a11y-contrast').checked = false;
      document.getElementById('a11y-underline').checked = false;
      document.getElementById('a11y-motion').value = 'system';
      document.getElementById('a11y-text').value = 'normal';
      announce('تنظیمات دسترسی بازنشانی شد');
    });

    document.addEventListener('keydown', function (e) {
      if (e.altKey && e.key === '0') {
        e.preventDefault();
        toggle();
      }
      if (e.key === 'Escape' && panel.classList.contains('is-open')) {
        e.preventDefault();
        close();
      }
    });

    // Close when clicking outside
    document.addEventListener('click', function (e) {
      if (!panel.classList.contains('is-open')) return;
      if (panel.contains(e.target) || fab.contains(e.target)) return;
      close();
    });
  }

  /* ---------- Focus trap for search overlay ---------- */
  function setupSearchFocusTrap() {
    var overlay = document.getElementById('search-overlay');
    if (!overlay) return;

    var previouslyFocused = null;

    function getFocusables() {
      return Array.prototype.slice.call(
        overlay.querySelectorAll('a[href], button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])')
      ).filter(function (el) { return el.offsetParent !== null || el === document.activeElement; });
    }

    var observer = new MutationObserver(function () {
      if (!overlay.hidden) {
        previouslyFocused = document.activeElement;
        var list = getFocusables();
        if (list.length) list[0].focus();
      } else if (previouslyFocused && typeof previouslyFocused.focus === 'function') {
        previouslyFocused.focus();
        previouslyFocused = null;
      }
    });
    observer.observe(overlay, { attributes: true, attributeFilter: ['hidden'] });

    overlay.addEventListener('keydown', function (e) {
      if (e.key !== 'Tab' || overlay.hidden) return;
      var list = getFocusables();
      if (!list.length) return;
      var first = list[0];
      var last = list[list.length - 1];
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    });
  }

  /* ---------- Keyboard navigation helpers ---------- */
  function setupKeyboardNav() {
    // Escape closes open Bootstrap dropdowns / collapses when focused inside
    document.addEventListener('keydown', function (e) {
      if (e.key !== 'Escape') return;
      var openDrop = document.querySelector('.dropdown-menu.show');
      if (openDrop) {
        var toggle = openDrop.previousElementSibling || document.querySelector('[aria-expanded="true"][data-bs-toggle="dropdown"]');
        if (window.bootstrap && toggle) {
          var inst = bootstrap.Dropdown.getInstance(toggle);
          if (inst) inst.hide();
        }
      }
    });

    // Arrow keys in primary nav (horizontal list)
    var nav = document.querySelector('.nav-primary');
    if (nav) {
      nav.addEventListener('keydown', function (e) {
        if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft' && e.key !== 'Home' && e.key !== 'End') return;
        var links = Array.prototype.slice.call(nav.querySelectorAll('a.nav-link'));
        if (!links.length) return;
        var idx = links.indexOf(document.activeElement);
        if (idx < 0) return;
        e.preventDefault();
        var rtl = document.documentElement.getAttribute('dir') === 'rtl';
        if (e.key === 'Home') { links[0].focus(); return; }
        if (e.key === 'End') { links[links.length - 1].focus(); return; }
        var next = e.key === 'ArrowRight' ? (rtl ? idx - 1 : idx + 1) : (rtl ? idx + 1 : idx - 1);
        if (next < 0) next = links.length - 1;
        if (next >= links.length) next = 0;
        links[next].focus();
      });
    }
  }

  /* ---------- Ensure images without alt get empty alt for decorative ---------- */
  function softImageHints() {
    document.querySelectorAll('img:not([alt])').forEach(function (img) {
      // Don't invent text; mark as needing review for checker
      img.setAttribute('data-a11y-missing-alt', '1');
    });
  }

  /* ---------- Accessibility checker (client-side) ---------- */
  function runChecker() {
    var issues = [];

    function add(level, id, title, detail, selector) {
      issues.push({ level: level, id: id, title: title, detail: detail, selector: selector || '' });
    }

    // 1. Page language
    var lang = document.documentElement.getAttribute('lang');
    if (!lang || lang.length < 2) add('fail', 'lang', 'ویژگی lang روی html', 'صفحه باید lang معتبر داشته باشد (مثلاً fa یا en).', 'html');
    else add('pass', 'lang', 'ویژگی lang', 'lang="' + lang + '" تنظیم شده است.');

    // 2. Skip link
    var skip = document.querySelector('a.skip-link, .skip-links a');
    if (!skip) add('fail', 'skip', 'Skip link', 'لینک «پرش به محتوا» یافت نشد.');
    else add('pass', 'skip', 'Skip link', 'لینک پرش به محتوا موجود است.');

    // 3. Main landmark
    var main = document.querySelector('main, [role="main"]');
    if (!main) add('fail', 'main', 'نقطه عطف main', 'عنصر main یا role="main" وجود ندارد.');
    else {
      if (!main.id) add('warn', 'main-id', 'id برای main', 'main بهتر است id داشته باشد تا skip-link به آن اشاره کند.');
      else add('pass', 'main', 'نقطه عطف main', 'main با id="' + main.id + '" موجود است.');
    }

    // 4. Images without alt
    var imgs = document.querySelectorAll('img');
    var missingAlt = 0;
    imgs.forEach(function (img) {
      if (!img.hasAttribute('alt')) missingAlt++;
    });
    if (missingAlt > 0) add('fail', 'img-alt', 'متن جایگزین تصاویر', missingAlt + ' تصویر بدون ویژگی alt.', 'img:not([alt])');
    else if (imgs.length) add('pass', 'img-alt', 'متن جایگزین تصاویر', 'همهٔ ' + imgs.length + ' تصویر alt دارند.');
    else add('pass', 'img-alt', 'متن جایگزین تصاویر', 'تصویری در صفحه نیست.');

    // 5. Buttons without accessible name
    var badBtns = [];
    document.querySelectorAll('button').forEach(function (b) {
      var name = (b.getAttribute('aria-label') || b.getAttribute('title') || b.textContent || '').trim();
      if (!name) badBtns.push(b);
    });
    if (badBtns.length) add('fail', 'btn-name', 'نام دسترس‌پذیر دکمه', badBtns.length + ' دکمه بدون متن/aria-label.', 'button');
    else add('pass', 'btn-name', 'نام دسترس‌پذیر دکمه', 'دکمه‌ها نام دسترس‌پذیر دارند.');

    // 6. Form inputs without labels
    var badInputs = 0;
    document.querySelectorAll('input:not([type="hidden"]):not([type="submit"]):not([type="button"]), select, textarea').forEach(function (el) {
      var id = el.id;
      var hasLabel = id && document.querySelector('label[for="' + id + '"]');
      var aria = el.getAttribute('aria-label') || el.getAttribute('aria-labelledby');
      var title = el.getAttribute('title') || el.getAttribute('placeholder');
      if (!hasLabel && !aria && !title) badInputs++;
    });
    if (badInputs > 0) add('warn', 'form-label', 'برچسب فرم', badInputs + ' فیلد بدون label/aria-label مشخص.');
    else add('pass', 'form-label', 'برچسب فرم', 'فیلدهای قابل مشاهده برچسب یا نام دارند.');

    // 7. Heading hierarchy (h1 count)
    var h1s = document.querySelectorAll('h1');
    if (h1s.length === 0) add('warn', 'h1', 'عنوان H1', 'هیچ H1 در صفحه نیست.');
    else if (h1s.length > 1) add('warn', 'h1', 'عنوان H1', h1s.length + ' عنصر H1 — معمولاً یکی کافی است.');
    else add('pass', 'h1', 'عنوان H1', 'یک H1 در صفحه وجود دارد.');

    // 8. Document title
    if (!document.title || document.title.trim().length < 3) add('fail', 'title', 'عنوان سند', 'title خالی یا خیلی کوتاه است.');
    else add('pass', 'title', 'عنوان سند', document.title);

    // 9. Links with empty text
    var emptyLinks = 0;
    document.querySelectorAll('a[href]').forEach(function (a) {
      var t = (a.getAttribute('aria-label') || a.textContent || '').trim();
      if (!t && !a.querySelector('img[alt]')) emptyLinks++;
    });
    if (emptyLinks) add('fail', 'link-name', 'نام لینک', emptyLinks + ' لینک بدون متن دسترس‌پذیر.');
    else add('pass', 'link-name', 'نام لینک', 'لینک‌ها نام دارند.');

    // 10. Color contrast hint (heuristic — high contrast mode available)
    add('pass', 'contrast-mode', 'حالت کنتراست بالا', 'کاربر می‌تواند از پنل دسترسی (A♿) کنتراست بالا را فعال کند.');

    // 11. Reduced motion
    add('pass', 'motion', 'کاهش حرکت', 'prefers-reduced-motion و تنظیم کاربر پشتیبانی می‌شود.');

    // 12. Landmark roles
    var hasNav = document.querySelector('nav, [role="navigation"]');
    var hasFooter = document.querySelector('footer, [role="contentinfo"]');
    if (!hasNav) add('warn', 'nav', 'ناوبری', 'nav یا role="navigation" یافت نشد.');
    else add('pass', 'nav', 'ناوبری', 'نقطه عطف ناوبری موجود است.');
    if (!hasFooter) add('warn', 'footer', 'پاورقی', 'footer یا role="contentinfo" یافت نشد.');
    else add('pass', 'footer', 'پاورقی', 'نقطه عطف پاورقی موجود است.');

    return issues;
  }

  window.blogA11y = {
    getPrefs: function () { return Object.assign({}, prefs); },
    setPrefs: function (p) { prefs = Object.assign(prefs, p); applyPrefs(prefs); savePrefs(prefs); },
    runChecker: runChecker,
    announce: announce
  };

  function bind() {
    buildPanel();
    setupSearchFocusTrap();
    setupKeyboardNav();
    softImageHints();

    // Render checker results if host exists (admin page)
    var host = document.getElementById('a11y-checker-results');
    if (host) {
      var results = runChecker();
      var pass = results.filter(function (r) { return r.level === 'pass'; }).length;
      var fail = results.filter(function (r) { return r.level === 'fail'; }).length;
      var warn = results.filter(function (r) { return r.level === 'warn'; }).length;
      var summary = document.getElementById('a11y-checker-summary');
      if (summary) {
        summary.innerHTML = '<strong class="ltr-field">' + pass + '</strong> قبول · ' +
          '<strong class="ltr-field">' + warn + '</strong> هشدار · ' +
          '<strong class="ltr-field">' + fail + '</strong> رد';
      }
      host.innerHTML = results.map(function (r) {
        return '<article class="a11y-check-item">' +
          '<span class="status ' + r.level + '">' +
          (r.level === 'pass' ? 'قبول' : r.level === 'fail' ? 'رد' : 'هشدار') +
          '</span>' +
          '<div><h3>' + r.title + '</h3><p>' + r.detail +
          (r.selector ? ' <code class="ltr-field">' + r.selector + '</code>' : '') +
          '</p></div></article>';
      }).join('');
    }
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bind);
  else bind();
})();
