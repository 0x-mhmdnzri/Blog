/**
 * FormSubmitObserver — single global point for submit feedback via ToastifyStack.
 *
 * - Intercepts <form> submit (POST/PUT/PATCH/DELETE) → fetch → green/red toast
 * - Patches window.fetch for same-origin mutating requests
 * - On page load: promotes TempData / flash banners into toasts
 *
 * Opt-out:
 *   form[data-no-toast] | form[data-native-submit]
 *   fetch headers: X-No-Toast: 1
 */
(function (global) {
  'use strict';

  var DEFAULT_OK = 'انجام شد';
  var DEFAULT_ERR = 'خطا در انجام عملیات';
  var SKIP_ACTIONS = /\/(Notifications\/(MarkRead|MarkAllRead|Stream)|Account\/Logout|Culture\/|Admin\/Stream)/i;

  function toastOk(msg) {
    var m = (msg && String(msg).trim()) || DEFAULT_OK;
    if (global.ToastifyStack) return ToastifyStack.success(m, { duration: 3200, linkUrl: null });
    if (global.blogToast) return blogToast(m, 'success');
  }

  function toastErr(msg) {
    var m = (msg && String(msg).trim()) || DEFAULT_ERR;
    if (global.ToastifyStack) return ToastifyStack.error(m, { duration: 5200, linkUrl: null });
    if (global.blogToast) return blogToast(m, 'error');
  }

  function isMutating(method) {
    var m = (method || 'GET').toUpperCase();
    return m === 'POST' || m === 'PUT' || m === 'PATCH' || m === 'DELETE';
  }

  function headerGet(headers, name) {
    if (!headers) return null;
    if (typeof headers.get === 'function') return headers.get(name) || headers.get(name.toLowerCase());
    if (typeof headers === 'object') {
      var key = Object.keys(headers).find(function (k) { return k.toLowerCase() === name.toLowerCase(); });
      return key ? headers[key] : null;
    }
    return null;
  }

  function shouldSkipUrl(url) {
    try {
      var u = typeof url === 'string' ? url : (url && url.url) || '';
      if (!u) return false;
      if (SKIP_ACTIONS.test(u)) return true;
      if (/^https?:/i.test(u)) {
        var a = document.createElement('a');
        a.href = u;
        if (a.origin !== location.origin) return true;
      }
      return false;
    } catch (_) {
      return false;
    }
  }

  function extractMessageFromJson(data) {
    if (data == null) return null;
    if (typeof data === 'string') return data;
    if (typeof data !== 'object') return null;
    var keys = ['message', 'msg', 'error', 'title', 'detail', 'saved', 'errorMessage', 'ErrorMessage'];
    for (var i = 0; i < keys.length; i++) {
      var k = keys[i];
      if (typeof data[k] === 'string' && data[k].trim()) return data[k].trim();
    }
    if (data.errors && typeof data.errors === 'object') {
      var parts = [];
      Object.keys(data.errors).forEach(function (key) {
        var arr = data.errors[key];
        if (Array.isArray(arr)) arr.forEach(function (x) { if (x) parts.push(String(x)); });
        else if (arr) parts.push(String(arr));
      });
      if (parts.length) return parts.slice(0, 3).join(' · ');
    }
    if (data.ok === true) return data.message || DEFAULT_OK;
    if (data.ok === false) return data.message || data.error || DEFAULT_ERR;
    return null;
  }

  /** Reject form chrome / report UI text that used to leak into success toasts. */
  function isUsableFlashText(text) {
    if (!text) return false;
    var t = String(text).replace(/\s+/g, ' ').trim();
    if (!t || t.length > 240) return false;
    if (/دلیل گزارش|ارسال گزارش|هرزنامه|محتوای توهین|نقض حق نشر/.test(t)) return false;
    if (/report reason|submit report|\bspam\b/i.test(t)) return false;
    return true;
  }

  function extractMessageFromHtml(html) {
    if (!html || typeof html !== 'string') return null;
    try {
      var doc = new DOMParser().parseFromString(html, 'text/html');
      // Only explicit flash / alert nodes — never generic cards or forms (e.g. #reportBox)
      var selectors = [
        '[data-toast-message]',
        '[data-toast-flash]',
        '.js-toast-flash',
        '.wizard-alert',
        '.alert-success', '.alert-danger', '.alert-error', '.alert',
        '.validation-summary-errors li',
        '.text-danger.field-validation-error'
      ];
      for (var s = 0; s < selectors.length; s++) {
        var nodes = doc.querySelectorAll(selectors[s]);
        for (var i = 0; i < nodes.length; i++) {
          var el = nodes[i];
          if (el.closest && (el.closest('form') || el.closest('#reportBox'))) continue;
          var text = (el.getAttribute('data-toast-message') || el.textContent || '').trim();
          text = text.replace(/\s+/g, ' ').slice(0, 280);
          if (isUsableFlashText(text)) return text;
        }
      }
    } catch (_) {}
    return null;
  }

  function handleResponse(res, opts) {
    opts = opts || {};
    if (opts.silent) return Promise.resolve(res);

    var ct = (res.headers && res.headers.get && res.headers.get('content-type')) || '';
    var ok = res.ok;

    if (res.status >= 300 && res.status < 400) {
      toastOk(opts.successMessage || DEFAULT_OK);
      return Promise.resolve(res);
    }

    if (ct.indexOf('application/json') !== -1) {
      return res.clone().json().then(function (data) {
        var msg = extractMessageFromJson(data);
        if (ok && data && data.ok === false) toastErr(msg || DEFAULT_ERR);
        else if (ok) toastOk(msg || opts.successMessage || DEFAULT_OK);
        else toastErr(msg || ('HTTP ' + res.status));
        return res;
      }).catch(function () {
        if (ok) toastOk(opts.successMessage || DEFAULT_OK);
        else toastErr(DEFAULT_ERR + ' (' + res.status + ')');
        return res;
      });
    }

    return res.clone().text().then(function (text) {
      var msg = extractMessageFromHtml(text);
      if (ok) toastOk(msg || opts.successMessage || DEFAULT_OK);
      else toastErr(msg || DEFAULT_ERR + (res.status ? ' (' + res.status + ')' : ''));
      return res;
    }).catch(function () {
      if (ok) toastOk(opts.successMessage || DEFAULT_OK);
      else toastErr(DEFAULT_ERR);
      return res;
    });
  }

  /* ---------- fetch patch ---------- */
  var nativeFetch = global.fetch;
  if (typeof nativeFetch === 'function' && !nativeFetch.__toastPatched) {
    global.fetch = function (input, init) {
      init = init || {};
      var method = (init.method || (typeof Request !== 'undefined' && input instanceof Request && input.method) || 'GET').toUpperCase();
      var url = typeof input === 'string' ? input : (input && input.url) || '';
      var noToast =
        !!headerGet(init.headers, 'X-No-Toast') ||
        shouldSkipUrl(url) ||
        !isMutating(method) ||
        !!init.__formObserverHandled;

      return nativeFetch.apply(this, arguments).then(function (res) {
        if (noToast) return res;
        return handleResponse(res, {}).then(function () { return res; });
      }).catch(function (err) {
        if (!noToast) toastErr((err && err.message) || DEFAULT_ERR);
        throw err;
      });
    };
    global.fetch.__toastPatched = true;
  }

  /* ---------- form submit intercept ---------- */
  function formWantsNative(form) {
    if (!form) return true;
    if (form.hasAttribute('data-no-toast')) return true;
    if (form.hasAttribute('data-native-submit')) return true;
    if (form.getAttribute('target') === '_blank') return true;
    // Login / external: prefer native for cookie schemes that need full navigation
    if (form.hasAttribute('data-confirm') && !form.dataset.confirmPassed) return true;
    var method = (form.getAttribute('method') || 'get').toUpperCase();
    if (!isMutating(method)) return true;
    if (shouldSkipUrl(form.getAttribute('action') || location.href)) return true;
    return false;
  }

  function onSubmit(e) {
    var form = e.target;
    if (!form || form.tagName !== 'FORM') return;
    if (formWantsNative(form)) return;

    e.preventDefault();
    e.stopPropagation();

    var action = form.getAttribute('action') || location.href;
    var method = (form.getAttribute('method') || 'POST').toUpperCase();
    var fd = new FormData(form);
    var submitter = e.submitter;
    if (submitter && submitter.name) {
      fd.append(submitter.name, submitter.value || '');
    }

    var buttons = form.querySelectorAll('button[type="submit"], input[type="submit"]');
    buttons.forEach(function (b) { b.disabled = true; });

    var successMsg = form.getAttribute('data-toast-success') || DEFAULT_OK;

    nativeFetch(action, {
      method: method,
      body: fd,
      credentials: 'same-origin',
      headers: {
        'X-Requested-With': 'XMLHttpRequest',
        'Accept': 'application/json, text/html, */*'
      },
      redirect: 'follow',
      __formObserverHandled: true
    }).then(function (res) {
      var ct = (res.headers.get('content-type') || '');
      var ok = res.ok;

      if (ct.indexOf('application/json') !== -1) {
        return res.json().then(function (data) {
          var msg = extractMessageFromJson(data);
          if (ok && !(data && data.ok === false)) {
            toastOk(msg || successMsg);
            var redirect = (data && (data.redirect || data.redirectUrl)) || form.getAttribute('data-toast-redirect');
            if (redirect) setTimeout(function () { location.href = redirect; }, 400);
            else if (form.hasAttribute('data-toast-reload')) setTimeout(function () { location.reload(); }, 400);
          } else {
            toastErr(msg || DEFAULT_ERR);
          }
        });
      }

      return res.text().then(function (html) {
        var msg = extractMessageFromHtml(html);
        if (ok) {
          toastOk(msg || successMsg);
          if (html && /<html[\s>]/i.test(html)) {
            setTimeout(function () {
              if (res.url && res.url !== location.href) location.href = res.url;
              else location.reload();
            }, 450);
          } else if (form.hasAttribute('data-toast-reload')) {
            setTimeout(function () { location.reload(); }, 400);
          }
        } else {
          toastErr(msg || DEFAULT_ERR + ' (' + res.status + ')');
        }
      });
    }).catch(function (err) {
      toastErr((err && err.message) || DEFAULT_ERR);
    }).finally(function () {
      buttons.forEach(function (b) { b.disabled = false; });
    });
  }

  function bindForms() {
    document.addEventListener('submit', onSubmit, true);
  }

  function promoteFlashes() {
    var nodes = document.querySelectorAll(
      '[data-toast-flash], [data-toast-message], .js-toast-flash, .wizard-alert'
    );
    nodes.forEach(function (el) {
      if (el.dataset.toastHandled) return;
      if (el.closest && (el.closest('form') || el.closest('#reportBox'))) return;
      var msg = (el.getAttribute('data-toast-message') || el.textContent || '').trim().replace(/\s+/g, ' ');
      if (!isUsableFlashText(msg)) return;
      var type = (el.getAttribute('data-toast-type') || el.getAttribute('data-toast-flash') || el.className || '').toLowerCase();
      el.dataset.toastHandled = '1';
      if (type.indexOf('error') !== -1 || type.indexOf('danger') !== -1 || type.indexOf('err') !== -1)
        toastErr(msg);
      else
        toastOk(msg);
      el.style.display = 'none';
    });
  }

  function boot() {
    bindForms();
    setTimeout(promoteFlashes, 30);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();

  global.FormSubmitObserver = {
    success: toastOk,
    error: toastErr,
    promoteFlashes: promoteFlashes
  };
})(typeof window !== 'undefined' ? window : this);
