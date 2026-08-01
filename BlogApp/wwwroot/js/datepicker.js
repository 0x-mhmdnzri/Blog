/**
 * Single point of failure for all date/datetime pickers.
 * FA → Shamsi (Jalali), EN → Gregorian, AR → Hijri (Islamic).
 *
 * UX: datetime fields are always edited in the USER'S local timezone.
 * On form submit, values are converted to UTC wall-clock (YYYY-MM-DDTHH:mm)
 * so the backend can store Instant/UTC safely.
 *
 * Server should set data-utc-iso="yyyy-MM-ddTHH:mm:ssZ" when binding stored UTC.
 */
(function (global) {
  'use strict';

  var culture = (document.documentElement.getAttribute('data-culture') || 'fa').toLowerCase();
  var calendar =
    culture === 'fa' ? 'jalali' :
    culture === 'ar' ? 'hijri' :
    'gregorian';

  function pad(n) { return n < 10 ? '0' + n : '' + n; }

  /** Parse datetime-local / ISO without Z as LOCAL wall-clock. */
  function parseIsoLocal(str) {
    if (!str) return null;
    var m = String(str).match(/^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2})(?::(\d{2}))?)?/);
    if (!m) return null;
    return new Date(+m[1], +m[2] - 1, +m[3], +(m[4] || 0), +(m[5] || 0), +(m[6] || 0));
  }

  /** Parse ISO with Z or offset as absolute instant → Date. */
  function parseIsoUtc(str) {
    if (!str) return null;
    var s = String(str).trim();
    if (/Z$/i.test(s) || /[+-]\d{2}:?\d{2}$/.test(s)) {
      var d = new Date(s);
      return isNaN(d.getTime()) ? null : d;
    }
    // Bare UTC wall-clock from server attribute
    var m = s.match(/^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2})(?::(\d{2}))?)?/);
    if (!m) return null;
    return new Date(Date.UTC(+m[1], +m[2] - 1, +m[3], +(m[4] || 0), +(m[5] || 0), +(m[6] || 0)));
  }

  function toIsoLocal(d, withTime) {
    if (!(d instanceof Date) || isNaN(d.getTime())) return '';
    var s = d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate());
    if (withTime) s += 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes());
    return s;
  }

  /** UTC wall-clock for form post (backend treats Unspecified as UTC). */
  function toIsoUtcWallClock(d, withTime) {
    if (!(d instanceof Date) || isNaN(d.getTime())) return '';
    var s = d.getUTCFullYear() + '-' + pad(d.getUTCMonth() + 1) + '-' + pad(d.getUTCDate());
    if (withTime) s += 'T' + pad(d.getUTCHours()) + ':' + pad(d.getUTCMinutes());
    return s;
  }

  function tzLabel() {
    try {
      var parts = new Intl.DateTimeFormat(undefined, { timeZoneName: 'short' }).formatToParts(new Date());
      var p = parts.find(function (x) { return x.type === 'timeZoneName'; });
      if (p && p.value) return p.value;
    } catch (_) {}
    var off = -new Date().getTimezoneOffset();
    var sign = off >= 0 ? '+' : '-';
    var abs = Math.abs(off);
    return 'UTC' + sign + pad(Math.floor(abs / 60)) + ':' + pad(abs % 60);
  }

  /* ─── Jalali (Shamsi) helpers ─── */
  function g2j(gy, gm, gd) {
    var g_d_m = [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334];
    var gy2 = gm > 2 ? gy + 1 : gy;
    var days = 355666 + (365 * gy) + Math.floor((gy2 + 3) / 4) - Math.floor((gy2 + 99) / 100)
      + Math.floor((gy2 + 399) / 400) + gd + g_d_m[gm - 1];
    var jy = -1595 + (33 * Math.floor(days / 12053));
    days %= 12053;
    jy += 4 * Math.floor(days / 1461);
    days %= 1461;
    if (days > 365) {
      jy += Math.floor((days - 1) / 365);
      days = (days - 1) % 365;
    }
    var jm, jd;
    if (days < 186) {
      jm = 1 + Math.floor(days / 31);
      jd = 1 + (days % 31);
    } else {
      jm = 7 + Math.floor((days - 186) / 30);
      jd = 1 + ((days - 186) % 30);
    }
    return [jy, jm, jd];
  }

  function j2g(jy, jm, jd) {
    var jy2 = jy + 1595;
    var days = -355668 + (365 * jy2) + Math.floor(jy2 / 33) * 8 + Math.floor(((jy2 % 33) + 3) / 4)
      + jd + (jm < 7 ? (jm - 1) * 31 : ((jm - 7) * 30 + 186));
    var gy = 400 * Math.floor(days / 146097);
    days %= 146097;
    if (days > 36524) {
      gy += 100 * Math.floor(--days / 36524);
      days %= 36524;
      if (days >= 365) days++;
    }
    gy += 4 * Math.floor(days / 1461);
    days %= 1461;
    if (days > 365) {
      gy += Math.floor((days - 1) / 365);
      days = (days - 1) % 365;
    }
    var gd = days + 1;
    var sal_a = [0, 31, (gy % 4 === 0 && gy % 100 !== 0) || (gy % 400 === 0) ? 29 : 28,
      31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    var gm = 0;
    while (gm < 13 && gd > sal_a[gm]) { gd -= sal_a[gm]; gm++; }
    return [gy, gm, gd];
  }

  function dateToJalali(d) {
    return g2j(d.getFullYear(), d.getMonth() + 1, d.getDate());
  }

  function jalaliToDate(jy, jm, jd, h, mi) {
    var g = j2g(jy, jm, jd);
    return new Date(g[0], g[1] - 1, g[2], h || 0, mi || 0, 0);
  }

  function g2h(gy, gm, gd) {
    var jd = gregorianToJd(gy, gm, gd);
    return jdToHijri(jd);
  }

  function h2g(hy, hm, hd) {
    var jd = hijriToJd(hy, hm, hd);
    return jdToGregorian(jd);
  }

  function gregorianToJd(y, m, d) {
    if (m <= 2) { y--; m += 12; }
    var A = Math.floor(y / 100);
    var B = 2 - A + Math.floor(A / 4);
    return Math.floor(365.25 * (y + 4716)) + Math.floor(30.6001 * (m + 1)) + d + B - 1524.5;
  }

  function jdToGregorian(jd) {
    var z = Math.floor(jd + 0.5);
    var a = Math.floor((z - 1867216.25) / 36524.25);
    var A = z + 1 + a - Math.floor(a / 4);
    var B = A + 1524;
    var C = Math.floor((B - 122.1) / 365.25);
    var D = Math.floor(365.25 * C);
    var E = Math.floor((B - D) / 30.6001);
    var day = B - D - Math.floor(30.6001 * E);
    var month = E < 14 ? E - 1 : E - 13;
    var year = month > 2 ? C - 4716 : C - 4715;
    return [year, month, day];
  }

  function hijriToJd(y, m, d) {
    return Math.floor((11 * y + 3) / 30) + 354 * y + 30 * m - Math.floor((m - 1) / 2) + d + 1948440 - 385;
  }

  function jdToHijri(jd) {
    jd = Math.floor(jd) + 0.5;
    var year = Math.floor((30 * (jd - 1948439.5) + 10656) / 10631);
    var month = Math.min(12, Math.ceil((jd - (29 + hijriToJd(year, 1, 1))) / 29.5) + 1);
    var day = Math.floor(jd - hijriToJd(year, month, 1)) + 1;
    return [year, month, day];
  }

  function dateToHijri(d) {
    return g2h(d.getFullYear(), d.getMonth() + 1, d.getDate());
  }

  function hijriToDate(hy, hm, hd, h, mi) {
    var g = h2g(hy, hm, hd);
    return new Date(g[0], g[1] - 1, g[2], h || 0, mi || 0, 0);
  }

  var MONTHS = {
    gregorian: {
      en: ['January','February','March','April','May','June','July','August','September','October','November','December'],
      fa: ['ژانویه','فوریه','مارس','آوریل','مه','ژوئن','ژوئیه','اوت','سپتامبر','اکتبر','نوامبر','دسامبر'],
      ar: ['يناير','فبراير','مارس','أبريل','مايو','يونيو','يوليو','أغسطس','سبتمبر','أكتوبر','نوفمبر','ديسمبر']
    },
    jalali: ['فروردین','اردیبهشت','خرداد','تیر','مرداد','شهریور','مهر','آبان','آذر','دی','بهمن','اسفند'],
    hijri: ['محرم','صفر','ربيع الأول','ربيع الآخر','جمادى الأولى','جمادى الآخرة','رجب','شعبان','رمضان','شوال','ذو القعدة','ذو الحجة']
  };

  var WEEKDAYS = {
    fa: ['ش','ی','د','س','چ','پ','ج'],
    en: ['Su','Mo','Tu','We','Th','Fr','Sa'],
    ar: ['ح','ن','ث','ر','خ','ج','س']
  };

  var LABELS = {
    fa: { today: 'امروز', clear: 'پاک', close: 'بستن', time: 'ساعت' },
    en: { today: 'Today', clear: 'Clear', close: 'Close', time: 'Time' },
    ar: { today: 'اليوم', clear: 'مسح', close: 'إغلاق', time: 'الوقت' }
  };

  function getParts(d) {
    if (calendar === 'jalali') return dateToJalali(d);
    if (calendar === 'hijri') return dateToHijri(d);
    return [d.getFullYear(), d.getMonth() + 1, d.getDate()];
  }

  function fromParts(y, m, day, h, mi) {
    if (calendar === 'jalali') return jalaliToDate(y, m, day, h, mi);
    if (calendar === 'hijri') return hijriToDate(y, m, day, h, mi);
    return new Date(y, m - 1, day, h || 0, mi || 0, 0);
  }

  function monthName(m) {
    if (calendar === 'jalali') return MONTHS.jalali[m - 1];
    if (calendar === 'hijri') return MONTHS.hijri[m - 1];
    var g = MONTHS.gregorian[culture] || MONTHS.gregorian.en;
    return g[m - 1];
  }

  function daysInMonth(y, m) {
    if (calendar === 'jalali') {
      if (m <= 6) return 31;
      if (m <= 11) return 30;
      var r = (y + 12) % 33;
      return [1,5,9,13,17,22,26,30].indexOf(r) >= 0 ? 30 : 29;
    }
    if (calendar === 'hijri') {
      return ((m % 2) === 1 || m === 12) ? 30 : 29;
    }
    return new Date(y, m, 0).getDate();
  }

  function formatDisplay(d, withTime) {
    if (!d || isNaN(d.getTime())) return '';
    var p = getParts(d);
    var s = p[0] + '/' + pad(p[1]) + '/' + pad(p[2]);
    if (withTime) s += ' ' + pad(d.getHours()) + ':' + pad(d.getMinutes());
    return s;
  }

  var activePopup = null;

  function closePopup() {
    if (activePopup) {
      activePopup.remove();
      activePopup = null;
    }
    document.removeEventListener('mousedown', onDocDown, true);
  }

  function onDocDown(e) {
    if (activePopup && !activePopup.contains(e.target) && e.target !== activePopup._trigger) {
      closePopup();
    }
  }

  function openPopup(input, withTime) {
    closePopup();
    var current = parseIsoLocal(input.value) || new Date();
    var parts = getParts(current);
    var viewY = parts[0], viewM = parts[1];
    var selected = { y: parts[0], m: parts[1], d: parts[2] };
    var hour = current.getHours();
    var minute = current.getMinutes();

    var popup = document.createElement('div');
    popup.className = 'blog-dp-popup calendar-' + calendar + ' lang-' + culture;
    popup.setAttribute('dir', culture === 'en' ? 'ltr' : 'rtl');
    popup._trigger = input;

    function render() {
      var labels = LABELS[culture] || LABELS.en;
      var wds = WEEKDAYS[culture] || WEEKDAYS.en;
      var html = '';
      html += '<div class="blog-dp-head">';
      html += '<button type="button" class="blog-dp-nav" data-nav="-1" aria-label="prev">‹</button>';
      html += '<div class="blog-dp-title">' + monthName(viewM) + ' <span class="ltr-field">' + viewY + '</span></div>';
      html += '<button type="button" class="blog-dp-nav" data-nav="1" aria-label="next">›</button>';
      html += '</div>';
      html += '<div class="blog-dp-week">';
      for (var i = 0; i < 7; i++) html += '<span>' + wds[i] + '</span>';
      html += '</div><div class="blog-dp-grid">';

      var first = fromParts(viewY, viewM, 1);
      var startWd = first.getDay();
      if (calendar === 'jalali') startWd = (startWd + 1) % 7;
      var dim = daysInMonth(viewY, viewM);
      for (var s = 0; s < startWd; s++) html += '<span class="blog-dp-empty"></span>';
      for (var day = 1; day <= dim; day++) {
        var isSel = selected.y === viewY && selected.m === viewM && selected.d === day;
        html += '<button type="button" class="blog-dp-day' + (isSel ? ' is-selected' : '') + '" data-day="' + day + '">' + day + '</button>';
      }
      html += '</div>';

      if (withTime) {
        html += '<div class="blog-dp-time">';
        html += '<label>' + labels.time + ' <span class="blog-dp-tz ltr-field">' + tzLabel() + '</span></label>';
        html += '<input type="number" class="blog-dp-hour" min="0" max="23" value="' + pad(hour) + '" />';
        html += '<span>:</span>';
        html += '<input type="number" class="blog-dp-min" min="0" max="59" value="' + pad(minute) + '" />';
        html += '</div>';
      }

      html += '<div class="blog-dp-foot">';
      html += '<button type="button" class="blog-dp-btn" data-act="today">' + labels.today + '</button>';
      html += '<button type="button" class="blog-dp-btn" data-act="clear">' + labels.clear + '</button>';
      html += '<button type="button" class="blog-dp-btn blog-dp-primary" data-act="close">' + labels.close + '</button>';
      html += '</div>';

      popup.innerHTML = html;

      popup.querySelector('[data-nav="-1"]').onclick = function () {
        viewM--;
        if (viewM < 1) { viewM = 12; viewY--; }
        render();
      };
      popup.querySelector('[data-nav="1"]').onclick = function () {
        viewM++;
        if (viewM > 12) { viewM = 1; viewY++; }
        render();
      };

      popup.querySelectorAll('.blog-dp-day').forEach(function (btn) {
        btn.onclick = function () {
          selected = { y: viewY, m: viewM, d: +btn.getAttribute('data-day') };
          apply();
          if (!withTime) closePopup();
          else render();
        };
      });

      var hourEl = popup.querySelector('.blog-dp-hour');
      var minEl = popup.querySelector('.blog-dp-min');
      if (hourEl) hourEl.onchange = hourEl.oninput = function () {
        hour = Math.max(0, Math.min(23, +hourEl.value || 0));
        apply();
      };
      if (minEl) minEl.onchange = minEl.oninput = function () {
        minute = Math.max(0, Math.min(59, +minEl.value || 0));
        apply();
      };

      popup.querySelector('[data-act="today"]').onclick = function () {
        var now = new Date();
        var p = getParts(now);
        selected = { y: p[0], m: p[1], d: p[2] };
        viewY = p[0]; viewM = p[1];
        hour = now.getHours(); minute = now.getMinutes();
        apply();
        render();
      };
      popup.querySelector('[data-act="clear"]').onclick = function () {
        input.value = '';
        input.dataset.localValue = '';
        if (input._display) input._display.value = '';
        input.dispatchEvent(new Event('change', { bubbles: true }));
        closePopup();
      };
      popup.querySelector('[data-act="close"]').onclick = function () { closePopup(); };
    }

    function apply() {
      var d = fromParts(selected.y, selected.m, selected.d, hour, minute);
      // Keep native input in LOCAL until form submit conversion
      input.value = toIsoLocal(d, withTime);
      input.dataset.localValue = input.value;
      if (input._display) input._display.value = formatDisplay(d, withTime);
      input.dispatchEvent(new Event('change', { bubbles: true }));
      input.dispatchEvent(new Event('input', { bubbles: true }));
    }

    render();
    document.body.appendChild(popup);
    activePopup = popup;

    var rect = (input._display || input).getBoundingClientRect();
    var top = rect.bottom + window.scrollY + 6;
    var left = rect.left + window.scrollX;
    popup.style.top = top + 'px';
    popup.style.left = Math.min(left, window.scrollX + window.innerWidth - 300) + 'px';

    setTimeout(function () {
      document.addEventListener('mousedown', onDocDown, true);
    }, 0);
  }

  function enhance(input) {
    if (!input || input.dataset.dpReady === '1') return;
    input.dataset.dpReady = '1';

    var withTime = (input.type === 'datetime-local') || input.dataset.dpTime === '1';
    var isDateOnly = input.type === 'date' || input.dataset.dpDate === '1';

    input.classList.add('blog-dp-native');
    input.setAttribute('tabindex', '-1');
    input.setAttribute('aria-hidden', 'true');

    // UTC from server → show local
    var utcAttr = input.getAttribute('data-utc-iso');
    if (withTime && !isDateOnly && utcAttr) {
      var utcDate = parseIsoUtc(utcAttr);
      if (utcDate) {
        input.value = toIsoLocal(utcDate, true);
        input.dataset.localValue = input.value;
      }
    } else if (withTime && !isDateOnly && input.value) {
      // asp-for rendered UTC components as-if local; treat as UTC wall-clock
      var asUtc = parseIsoUtc(input.value);
      if (asUtc) {
        input.value = toIsoLocal(asUtc, true);
        input.dataset.localValue = input.value;
        input.setAttribute('data-utc-iso', toIsoUtcWallClock(asUtc, true) + ':00Z');
      }
    }

    var display = document.createElement('input');
    display.type = 'text';
    display.className = (input.className || '').replace(/blog-dp-native/g, '') + ' blog-dp-display ltr-field';
    display.className = display.className.replace(/\s+/g, ' ').trim();
    display.readOnly = true;
    display.placeholder = input.placeholder || (withTime && !isDateOnly ? 'YYYY/MM/DD HH:mm (' + tzLabel() + ')' : 'YYYY/MM/DD');
    display.autocomplete = 'off';
    if (input.required) display.required = true;
    if (input.disabled) display.disabled = true;

    var initial = parseIsoLocal(input.value);
    if (initial) display.value = formatDisplay(initial, withTime && !isDateOnly);

    input._display = display;
    input.parentNode.insertBefore(display, input.nextSibling);

    // Annotate nearby label/hint with timezone for datetime fields
    if (withTime && !isDateOnly) {
      var hint = input.parentNode.querySelector('.form-text, .blog-dp-hint');
      if (!hint) {
        hint = document.createElement('div');
        hint.className = 'form-text text-muted-dark blog-dp-hint';
        input.parentNode.appendChild(hint);
      }
      if (!hint.dataset.tzDone) {
        hint.dataset.tzDone = '1';
        var extra = (culture === 'fa')
          ? 'زمان به‌وقت محلی شما (' + tzLabel() + ') — در سرور به UTC ذخیره می‌شود'
          : 'Local time (' + tzLabel() + ') — stored as UTC on the server';
        hint.textContent = (hint.textContent ? hint.textContent + ' · ' : '') + extra;
      }
    }

    display.addEventListener('click', function () {
      if (!input.disabled) openPopup(input, withTime && !isDateOnly);
    });
    display.addEventListener('focus', function () {
      if (!input.disabled) openPopup(input, withTime && !isDateOnly);
    });
  }

  /**
   * Convert all datetime-local fields in a form from local → UTC wall-clock.
   * Call before native submit / fetch body serialization.
   */
  function prepareForm(form) {
    if (!form || !form.querySelectorAll) return;

    // Offset for backend fallback
    var off = String(new Date().getTimezoneOffset());
    var existing = form.querySelector('input[name="__timezoneOffset"]');
    if (!existing) {
      existing = document.createElement('input');
      existing.type = 'hidden';
      existing.name = '__timezoneOffset';
      form.appendChild(existing);
    }
    existing.value = off;

    var flag = form.querySelector('input[name="__dt_utc_converted"]');
    if (!flag) {
      flag = document.createElement('input');
      flag.type = 'hidden';
      flag.name = '__dt_utc_converted';
      form.appendChild(flag);
    }
    flag.value = '1';

    form.querySelectorAll('input[type="datetime-local"], input[data-dp-time="1"]').forEach(function (input) {
      if (input.type === 'date' || input.dataset.dpDate === '1') return;
      var localStr = input.dataset.localValue || input.value;
      if (!localStr) return;
      var d = parseIsoLocal(localStr);
      if (!d) return;
      input.dataset.localValue = localStr;
      input.value = toIsoUtcWallClock(d, true);
    });
  }

  function restoreForm(form) {
    if (!form) return;
    form.querySelectorAll('input[type="datetime-local"], input[data-dp-time="1"]').forEach(function (input) {
      if (input.dataset.localValue) {
        input.value = input.dataset.localValue;
        if (input._display) {
          var d = parseIsoLocal(input.value);
          if (d) input._display.value = formatDisplay(d, true);
        }
      }
    });
  }

  function init(root) {
    root = root || document;
    root.querySelectorAll('input[type="datetime-local"], input[type="date"], input.js-datepicker').forEach(enhance);
  }

  // Capture-phase: convert before any other submit handler / browser navigation
  document.addEventListener('submit', function (e) {
    var form = e.target;
    if (!(form instanceof HTMLFormElement)) return;
    prepareForm(form);
  }, true);

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () { init(); });
  } else {
    init();
  }

  global.BlogDatePicker = {
    init: init,
    culture: culture,
    calendar: calendar,
    enhance: enhance,
    prepareForm: prepareForm,
    restoreForm: restoreForm,
    tzLabel: tzLabel,
    toIsoUtcWallClock: toIsoUtcWallClock,
    parseIsoLocal: parseIsoLocal
  };
})(window);
