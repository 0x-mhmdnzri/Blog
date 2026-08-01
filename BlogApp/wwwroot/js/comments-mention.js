/**
 * Telegram-style @mention typeahead for comment textareas (.cmt-input-body).
 * Calls GET /Mentions/Suggest?q=… and inserts @username on select.
 */
(function () {
  'use strict';

  var mentionBox = null;
  var mentionItems = [];
  var mentionIndex = -1;
  var mentionActiveTa = null;
  var mentionRange = null;
  var mentionTimer = null;
  var mentionSeq = 0;

  function ensureBox() {
    if (mentionBox) return mentionBox;
    mentionBox = document.createElement('div');
    mentionBox.className = 'cmt-mention-dropdown';
    mentionBox.setAttribute('role', 'listbox');
    mentionBox.hidden = true;
    document.body.appendChild(mentionBox);
    return mentionBox;
  }

  function hide() {
    if (mentionBox) {
      mentionBox.hidden = true;
      mentionBox.innerHTML = '';
    }
    mentionItems = [];
    mentionIndex = -1;
    mentionActiveTa = null;
    mentionRange = null;
  }

  function escapeHtml(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  function getCaretCoordinates(ta, position) {
    var div = document.createElement('div');
    var style = window.getComputedStyle(ta);
    [
      'direction', 'boxSizing', 'width', 'height', 'overflowX', 'overflowY',
      'borderTopWidth', 'borderRightWidth', 'borderBottomWidth', 'borderLeftWidth',
      'paddingTop', 'paddingRight', 'paddingBottom', 'paddingLeft',
      'fontStyle', 'fontVariant', 'fontWeight', 'fontStretch', 'fontSize',
      'lineHeight', 'fontFamily', 'textAlign', 'textTransform', 'textIndent',
      'textDecoration', 'letterSpacing', 'wordSpacing', 'tabSize', 'MozTabSize'
    ].forEach(function (p) {
      try { div.style[p] = style[p]; } catch (e) {}
    });
    div.style.position = 'absolute';
    div.style.visibility = 'hidden';
    div.style.whiteSpace = 'pre-wrap';
    div.style.wordWrap = 'break-word';
    div.style.overflow = 'hidden';
    div.textContent = ta.value.substring(0, position);
    var span = document.createElement('span');
    span.textContent = ta.value.substring(position) || '.';
    div.appendChild(span);
    document.body.appendChild(div);
    var taRect = ta.getBoundingClientRect();
    var coords = {
      top: span.offsetTop - ta.scrollTop + taRect.top + window.scrollY,
      left: span.offsetLeft - ta.scrollLeft + taRect.left + window.scrollX,
      height: span.offsetHeight || parseInt(style.lineHeight, 10) || 18
    };
    document.body.removeChild(div);
    return coords;
  }

  function render(users) {
    var box = ensureBox();
    mentionItems = users || [];
    mentionIndex = mentionItems.length ? 0 : -1;
    if (!mentionItems.length) {
      box.hidden = true;
      box.innerHTML = '';
      return;
    }
    box.innerHTML = mentionItems.map(function (u, i) {
      var initial = ((u.displayName || u.username || '?') + '').charAt(0).toUpperCase();
      var av = u.avatarUrl
        ? '<img class="cmt-mention-av" src="' + escapeHtml(u.avatarUrl) + '" alt="" loading="lazy" />'
        : '<span class="cmt-mention-av cmt-mention-av--ph" aria-hidden="true">' + escapeHtml(initial) + '</span>';
      return (
        '<button type="button" class="cmt-mention-item' + (i === 0 ? ' is-active' : '') +
        '" role="option" data-idx="' + i + '">' + av +
        '<span class="cmt-mention-meta">' +
          '<span class="cmt-mention-name">' + escapeHtml(u.displayName || u.username) + '</span>' +
          '<span class="cmt-mention-user ltr-field">@' + escapeHtml(u.username) + '</span>' +
        '</span></button>'
      );
    }).join('');
    box.hidden = false;

    if (mentionActiveTa && mentionRange) {
      var coords = getCaretCoordinates(mentionActiveTa, mentionRange.end);
      var top = coords.top + coords.height + 6;
      var left = Math.min(coords.left, window.scrollX + window.innerWidth - 280);
      box.style.top = top + 'px';
      box.style.left = Math.max(8, left) + 'px';
    }

    box.querySelectorAll('.cmt-mention-item').forEach(function (btn) {
      btn.addEventListener('mousedown', function (e) {
        e.preventDefault();
        pick(parseInt(btn.getAttribute('data-idx'), 10));
      });
      btn.addEventListener('mouseenter', function () {
        setIndex(parseInt(btn.getAttribute('data-idx'), 10));
      });
    });
  }

  function setIndex(i) {
    if (!mentionItems.length) return;
    mentionIndex = Math.max(0, Math.min(mentionItems.length - 1, i));
    var box = ensureBox();
    box.querySelectorAll('.cmt-mention-item').forEach(function (btn, j) {
      btn.classList.toggle('is-active', j === mentionIndex);
    });
    var active = box.querySelector('.cmt-mention-item.is-active');
    if (active) active.scrollIntoView({ block: 'nearest' });
  }

  function pick(idx) {
    if (!mentionActiveTa || !mentionRange || idx < 0 || idx >= mentionItems.length) {
      hide();
      return;
    }
    var u = mentionItems[idx];
    var ta = mentionActiveTa;
    var before = ta.value.substring(0, mentionRange.start);
    var after = ta.value.substring(mentionRange.end);
    var insert = '@' + u.username + ' ';
    ta.value = before + insert + after;
    var caret = before.length + insert.length;
    ta.setSelectionRange(caret, caret);
    ta.dispatchEvent(new Event('input', { bubbles: true }));
    hide();
    ta.focus();
  }

  function detect(ta) {
    var pos = ta.selectionStart || 0;
    var text = ta.value.substring(0, pos);
    var m = text.match(/(?:^|[\s\u200c])@([a-zA-Z0-9._\-]*)$/);
    if (!m) return null;
    return { start: text.lastIndexOf('@'), end: pos, query: m[1] };
  }

  function fetchUsers(query, seq) {
    fetch('/Mentions/Suggest?q=' + encodeURIComponent(query), {
      headers: { 'Accept': 'application/json' },
      credentials: 'same-origin'
    })
      .then(function (r) { return r.ok ? r.json() : []; })
      .then(function (data) {
        if (seq !== mentionSeq) return;
        render(Array.isArray(data) ? data : []);
      })
      .catch(function () {
        if (seq === mentionSeq) hide();
      });
  }

  function onInput(ta) {
    var range = detect(ta);
    if (!range || range.query.length === 0) {
      hide();
      return;
    }
    mentionActiveTa = ta;
    mentionRange = range;
    clearTimeout(mentionTimer);
    var seq = ++mentionSeq;
    mentionTimer = setTimeout(function () { fetchUsers(range.query, seq); }, 120);
  }

  function onKeydown(e) {
    if (!mentionBox || mentionBox.hidden || !mentionItems.length) return;
    if (e.key === 'ArrowDown') { e.preventDefault(); setIndex(mentionIndex + 1); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setIndex(mentionIndex - 1); }
    else if (e.key === 'Enter' || e.key === 'Tab') {
      if (mentionIndex >= 0) { e.preventDefault(); pick(mentionIndex); }
    } else if (e.key === 'Escape') { e.preventDefault(); hide(); }
  }

  function attach(ta) {
    if (!ta || ta._mentionBound) return;
    ta._mentionBound = true;
    ta.addEventListener('input', function () { onInput(ta); });
    ta.addEventListener('keydown', onKeydown);
    ta.addEventListener('click', function () { onInput(ta); });
    ta.addEventListener('blur', function () {
      setTimeout(function () {
        if (document.activeElement !== ta) hide();
      }, 180);
    });
  }

  function scan() {
    document.querySelectorAll('.cmt-input-body').forEach(attach);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', scan);
  } else {
    scan();
  }

  var root = document.getElementById('comments');
  if (root && window.MutationObserver) {
    new MutationObserver(function () { scan(); }).observe(root, { childList: true, subtree: true });
  }

  document.addEventListener('click', function (e) {
    if (mentionBox && !mentionBox.hidden &&
        !e.target.closest('.cmt-mention-dropdown') &&
        !e.target.closest('.cmt-input-body')) {
      hide();
    }
  });
})();
