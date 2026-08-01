/**
 * Twitter-style comments: reply slots, char counter, scroll-to-parent.
 */
(function () {
  'use strict';

  var root = document.getElementById('comments');
  if (!root) return;

  var maxBody = parseInt(root.getAttribute('data-max-body') || '2000', 10) || 2000;
  var mainComposer = document.getElementById('cmtMainComposer');
  var parentField = document.getElementById('cmtParentId');
  var banner = document.getElementById('cmtReplyBanner');
  var replyName = document.getElementById('cmtReplyName');
  var cancelBtn = document.getElementById('cmtReplyCancel');
  var bodyField = document.getElementById('cmtBody');
  var counter = document.getElementById('cmtCounter');
  var template = document.getElementById('cmtReplyTemplate');

  function updateCounter(ta, el) {
    if (!ta || !el) return;
    var n = (ta.value || '').length;
    el.textContent = n + ' / ' + maxBody;
    el.classList.toggle('is-warn', n > maxBody * 0.85);
    el.classList.toggle('is-over', n > maxBody);
  }

  if (bodyField && counter) {
    bodyField.addEventListener('input', function () { updateCounter(bodyField, counter); });
    updateCounter(bodyField, counter);
  }

  function clearMainReply() {
    if (parentField) parentField.value = '';
    if (banner) banner.hidden = true;
    if (replyName) replyName.textContent = '';
  }

  if (cancelBtn) cancelBtn.addEventListener('click', clearMainReply);

  function openMainReply(id, name) {
    if (parentField) parentField.value = String(id);
    if (replyName) replyName.textContent = name || '';
    if (banner) banner.hidden = false;
    if (mainComposer) {
      mainComposer.scrollIntoView({ behavior: 'smooth', block: 'center' });
      var ta = mainComposer.querySelector('.cmt-input-body');
      if (ta) {
        ta.focus();
        if (name && !(ta.value || '').trim()) {
          ta.value = '@' + name.replace(/\s+/g, '') + ' ';
          updateCounter(ta, counter);
        }
      }
    }
  }

  function closeAllInlineReplies() {
    root.querySelectorAll('.cmt-reply-slot').forEach(function (slot) {
      slot.hidden = true;
      slot.innerHTML = '';
    });
  }

  function openInlineReply(id, name) {
    closeAllInlineReplies();
    clearMainReply();
    var slot = document.getElementById('reply-slot-' + id);
    if (!slot || !template) {
      openMainReply(id, name);
      return;
    }
    var node = template.content.cloneNode(true);
    var form = node.querySelector('form');
    var pf = node.querySelector('[data-parent-field]');
    if (pf) pf.value = String(id);
    var ta = node.querySelector('.cmt-input-body');
    if (ta && name) {
      ta.placeholder = 'پاسخ به ' + name + '\u2026';
      ta.value = '@' + name.replace(/\s+/g, '') + ' ';
    }
    slot.appendChild(node);
    slot.hidden = false;
    var dismiss = slot.querySelector('[data-reply-dismiss]');
    if (dismiss) {
      dismiss.addEventListener('click', function () {
        slot.hidden = true;
        slot.innerHTML = '';
      });
    }
    if (ta) {
      ta.focus();
      ta.setSelectionRange(ta.value.length, ta.value.length);
    }
  }

  root.addEventListener('click', function (e) {
    var replyBtn = e.target.closest('[data-reply-to]');
    if (replyBtn) {
      e.preventDefault();
      openInlineReply(replyBtn.getAttribute('data-reply-to'), replyBtn.getAttribute('data-reply-name'));
      return;
    }

    var editBtn = e.target.closest('[data-edit-toggle]');
    if (editBtn) {
      e.preventDefault();
      var form = document.getElementById('edit-' + editBtn.getAttribute('data-edit-toggle'));
      if (form) form.hidden = !form.hidden;
      return;
    }

    var scrollBtn = e.target.closest('[data-scroll-to]');
    if (scrollBtn) {
      e.preventDefault();
      var target = document.getElementById('comment-' + scrollBtn.getAttribute('data-scroll-to'));
      if (target) target.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  });

  // Deep-link highlight
  if (location.hash && location.hash.indexOf('#comment-') === 0) {
    var el = document.querySelector(location.hash);
    if (el) {
      el.classList.add('is-pinned');
      setTimeout(function () { el.scrollIntoView({ behavior: 'smooth', block: 'center' }); }, 80);
    }
  }
})();

/* load Telegram-style @mention typeahead */
(function(){
  if (document.querySelector('script[data-cmt-mention]')) return;
  var s = document.createElement('script');
  s.src = '/js/comments-mention.js';
  s.defer = true;
  s.setAttribute('data-cmt-mention', '1');
  document.head.appendChild(s);
  var l = document.createElement('link');
  l.rel = 'stylesheet';
  l.href = '/css/comments-mention.css';
  document.head.appendChild(l);
})();
