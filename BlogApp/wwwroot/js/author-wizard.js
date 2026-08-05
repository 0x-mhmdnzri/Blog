(function () {
  var form = document.getElementById('authorWizardForm');
  if (!form) return;
  var steps = Array.prototype.slice.call(document.querySelectorAll('#authorWizardSteps .wizard-step'));
  var next = document.getElementById('authorWizardNext');
  var prev = document.getElementById('authorWizardPrev');
  var submit = document.getElementById('authorWizardSubmit');
  var bio = document.getElementById('authorBio');
  var bioCount = document.getElementById('bioCount');
  var displayName = form.querySelector('[name="DisplayName"]');
  var initial = document.getElementById('authorInitial');
  var step = 1;

  function show(n) {
    step = n;
    form.querySelectorAll('.wizard-pane').forEach(function (p) {
      var pn = +p.getAttribute('data-pane');
      p.hidden = pn !== n;
      p.classList.toggle('active', pn === n);
    });
    steps.forEach(function (s) {
      var sn = +s.getAttribute('data-step');
      s.classList.toggle('active', sn === n);
      s.setAttribute('aria-selected', sn === n ? 'true' : 'false');
    });
    if (prev) prev.hidden = n <= 1;
    if (next) next.hidden = n >= 3;
    if (submit) submit.hidden = n < 3;
    if (n === 3) {
      form.querySelectorAll('[data-review]').forEach(function (el) {
        var name = el.getAttribute('data-review');
        var input = form.querySelector('[name="' + name + '"]');
        el.textContent = (input && input.value) ? input.value : '\u2014';
      });
    }
  }

  function errorSlot(el) {
    var name = el.getAttribute('name');
    if (!name) return null;
    return form.querySelector('[data-val-for="' + name + '"]');
  }

  function setError(el, msg) {
    var slot = errorSlot(el);
    if (slot) slot.textContent = msg || '';
    el.classList.toggle('is-invalid', !!msg);
    if (msg) el.setAttribute('aria-invalid', 'true');
    else el.removeAttribute('aria-invalid');
  }

  function clearError(el) {
    setError(el, '');
  }

  function msg(el, key, fallback) {
    return el.getAttribute('data-msg-' + key) || fallback || '';
  }

  function validateField(el) {
    if (!el || el.disabled) return true;
    var type = (el.getAttribute('type') || el.tagName || '').toLowerCase();
    if (type === 'hidden' || type === 'file' || type === 'button' || type === 'submit') return true;

    var value = (el.value || '').trim();
    if (el.required && !value) {
      setError(el, msg(el, 'required', 'Required'));
      return false;
    }
    if (!value) {
      clearError(el);
      return true;
    }
    if (type === 'email' || el.getAttribute('type') === 'email') {
      var emailOk = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
      if (!emailOk) {
        setError(el, msg(el, 'type', 'Invalid email'));
        return false;
      }
    }
    var minL = parseInt(el.getAttribute('minlength') || '0', 10);
    if (minL && value.length < minL) {
      setError(el, msg(el, 'minlength', 'Too short'));
      return false;
    }
    var maxL = parseInt(el.getAttribute('maxlength') || '0', 10);
    if (maxL && value.length > maxL) {
      setError(el, msg(el, 'maxlength', 'Too long'));
      return false;
    }
    var pattern = el.getAttribute('pattern');
    if (pattern) {
      try {
        var re = new RegExp('^(?:' + pattern + ')$');
        if (!re.test(value)) {
          setError(el, msg(el, 'pattern', 'Invalid format'));
          return false;
        }
      } catch (e) { /* ignore */ }
    }
    if (el.name === 'ConfirmPassword') {
      var p = form.querySelector('[name="Password"]');
      if (p && p.value !== el.value) {
        setError(el, msg(el, 'match', 'Passwords do not match'));
        return false;
      }
    }
    if (el.name === 'Password') {
      var c = form.querySelector('[name="ConfirmPassword"]');
      if (c && c.value) {
        if (c.value !== el.value) setError(c, msg(c, 'match', 'Passwords do not match'));
        else clearError(c);
      }
    }
    clearError(el);
    return true;
  }

  function fieldsInPane(n) {
    var pane = form.querySelector('.wizard-pane[data-pane="' + n + '"]');
    if (!pane) return [];
    return Array.prototype.slice.call(pane.querySelectorAll('input,textarea,select'))
      .filter(function (el) {
        var t = (el.getAttribute('type') || '').toLowerCase();
        return t !== 'hidden' && t !== 'file' && t !== 'button' && t !== 'submit';
      });
  }

  function validatePane(n) {
    var ok = true;
    var firstInvalid = null;
    fieldsInPane(n).forEach(function (el) {
      if (!validateField(el)) {
        ok = false;
        if (!firstInvalid) firstInvalid = el;
      }
    });
    if (firstInvalid) {
      try { firstInvalid.focus({ preventScroll: false }); } catch (e) { firstInvalid.focus(); }
    }
    return ok;
  }

  form.addEventListener('input', function (e) {
    var el = e.target;
    if (el && el.matches && el.matches('input,textarea,select')) {
      if (el.classList.contains('is-invalid') || (errorSlot(el) && errorSlot(el).textContent)) {
        validateField(el);
      }
    }
  });
  form.addEventListener('blur', function (e) {
    var el = e.target;
    if (el && el.matches && el.matches('input,textarea,select')) validateField(el);
  }, true);

  if (next) next.addEventListener('click', function () {
    if (!validatePane(step)) return;
    if (step < 3) show(step + 1);
  });
  if (prev) prev.addEventListener('click', function () {
    if (step > 1) show(step - 1);
  });
  steps.forEach(function (s) {
    s.addEventListener('click', function () {
      var target = +s.getAttribute('data-step');
      if (target === step) return;
      if (target < step) {
        show(target);
        return;
      }
      for (var i = step; i < target; i++) {
        if (!validatePane(i)) {
          show(i);
          return;
        }
      }
      show(target);
    });
  });

  form.addEventListener('submit', function (e) {
    var firstBad = null;
    for (var i = 1; i <= 3; i++) {
      if (!validatePane(i)) {
        if (firstBad === null) firstBad = i;
      }
    }
    if (firstBad !== null) {
      e.preventDefault();
      show(firstBad);
      return;
    }
  });

  if (bio && bioCount) {
    var upd = function () { bioCount.textContent = String((bio.value || '').length); };
    bio.addEventListener('input', upd);
    upd();
  }
  if (displayName && initial) {
    displayName.addEventListener('input', function () {
      var v = (displayName.value || '').trim();
      if (initial && initial.parentElement && !initial.parentElement.querySelector('img')) {
        initial.textContent = v ? v.charAt(0).toUpperCase() : '?';
      }
    });
  }

  var startStep = parseInt(form.getAttribute('data-error-step') || '1', 10) || 1;
  show(startStep);
})();
