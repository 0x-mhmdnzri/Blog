/**
 * BlogApp — server-side DataTables helper (all admin tables).
 * Global search + per-column filters (text / status select).
 * Stable layout: width 100%, columns.adjust after draw.
 */
window.BlogDT = (function () {
  function isRtl() {
    return (document.documentElement.getAttribute('dir') || 'rtl') === 'rtl';
  }

  function csrfToken() {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
  }

  function i18n(key, fallback) {
    var pack = window.__i18n || {};
    return pack[key] || fallback || key;
  }

  function languagePack() {
    return {
      processing: i18n('dt.processing', 'Loading…'),
      // empty label — input already has placeholder (avoids "جست‌وجو جست‌وجو…")
      search: '',
      searchPlaceholder: i18n('dt.search_placeholder', 'جست‌وجو…'),
      lengthMenu: i18n('dt.length_menu', 'نمایش _MENU_'),
      info: i18n('dt.info', 'نمایش _START_ تا _END_ از _TOTAL_'),
      infoEmpty: i18n('dt.info_empty', 'موردی نیست'),
      infoFiltered: i18n('dt.info_filtered', '(فیلتر از _MAX_)'),
      zeroRecords: i18n('dt.zero_records', 'نتیجه‌ای یافت نشد'),
      emptyTable: i18n('dt.empty_table', 'داده‌ای نیست'),
      paginate: {
        first: i18n('dt.paginate_first', 'اول'),
        last: i18n('dt.paginate_last', 'آخر'),
        next: i18n('dt.paginate_next', 'بعدی'),
        previous: i18n('dt.paginate_previous', 'قبلی')
      }
    };
  }

  function buildExportUrl(baseUrl, table, extraParams) {
    var url = new URL(baseUrl, window.location.origin);
    var search = '';
    try {
      search = table.search() || '';
    } catch (_) {}
    if (search) url.searchParams.set('search', search);
    if (extraParams && typeof extraParams === 'object') {
      Object.keys(extraParams).forEach(function (k) {
        if (extraParams[k] != null && extraParams[k] !== '')
          url.searchParams.set(k, extraParams[k]);
      });
    }
    try {
      table.columns().every(function (i) {
        var v = this.search();
        if (v) url.searchParams.set('col' + i, v);
      });
    } catch (_) {}
    return url.pathname + url.search;
  }

  function exportIconSvg() {
    return (
      '<svg class="dt-export-ico" viewBox="0 0 24 24" width="16" height="16" fill="currentColor" aria-hidden="true">' +
      '<path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/>' +
      '</svg>'
    );
  }

  function updateClearButton(wrap, table) {
    var btn = wrap.find('.dt-clear-filters');
    if (!btn.length) return;
    var any = false;
    try {
      table.columns().every(function () {
        if (this.search()) any = true;
      });
    } catch (_) {}
    btn.toggleClass('is-visible', any);
  }

  function markActiveFilters($row) {
    $row.find('.dt-col-filter').each(function () {
      var v = (this.value || '').trim();
      this.classList.toggle('is-active', v.length > 0);
    });
  }

  function buildToolbar(wrap, table, exportUrl, exportParams) {
    if (wrap.find('.dt-toolbar-bar').length) return;

    var bar = jQuery(
      '<div class="dt-toolbar-bar" role="toolbar">' +
        '<div class="dt-toolbar-start"></div>' +
        '<div class="dt-toolbar-end"></div>' +
      '</div>'
    );

    var length = wrap.find('.dataTables_length');
    var filter = wrap.find('.dataTables_filter');
    var start = bar.find('.dt-toolbar-start');
    var end = bar.find('.dt-toolbar-end');

    if (exportUrl) {
      var label = i18n('dt.export_csv', 'خروجی CSV');
      var btn = jQuery(
        '<button type="button" class="dt-export-csv" title="' +
          label +
          '">' +
          exportIconSvg() +
          '<span>' +
          label +
          '</span></button>'
      );
      btn.on('click', function () {
        window.location.href = buildExportUrl(exportUrl, table, exportParams);
      });
      start.append(btn);
    }

    var clearLbl = i18n('dt.clear_filters', 'پاک‌کردن فیلترها');
    var clearBtn = jQuery(
      '<button type="button" class="dt-clear-filters" title="' +
        clearLbl +
        '">' +
        clearLbl +
        '</button>'
    );
    clearBtn.on('click', function () {
      table.columns().search('').draw();
      wrap.find('.dt-col-filter').each(function () {
        this.value = '';
        this.classList.remove('is-active');
      });
      clearBtn.removeClass('is-visible');
    });
    start.append(clearBtn);

    if (length.length) start.append(length);
    if (filter.length) end.append(filter);

    wrap.find('.row.dt-toolbar').remove();
    wrap.find('> .row').each(function () {
      var $r = jQuery(this);
      if (
        !$r.find('table, .dataTables_info, .dataTables_paginate, .dt-scroll').length &&
        !$r.find('.dataTables_length, .dataTables_filter').length
      ) {
        $r.remove();
      }
    });

    wrap.prepend(bar);
  }

  function ensureScroll($table) {
    if ($table.parent().hasClass('dt-scroll')) return;
    $table.wrap('<div class="dt-scroll"></div>');
  }

  function markActionCells(selector) {
    document.querySelectorAll(selector + ' tbody td').forEach(function (td) {
      if (td.classList.contains('dataTables_empty')) return;
      if (td.querySelector('form, button, a.btn, select, .icon-btn')) {
        td.classList.add('dt-actions');
      }
    });
  }

  function buildColumnFilters($table, table, columnFilters, wrap) {
    if (!columnFilters || !columnFilters.length) return;
    if ($table.find('thead tr.dt-col-filters').length) return;

    var $head = $table.find('thead');
    var colCount = $table.find('thead tr').first().children('th').length;
    var $row = jQuery('<tr class="dt-col-filters" role="row"></tr>');

    for (var i = 0; i < colCount; i++) {
      var cfg = columnFilters[i];
      var $th = jQuery('<th class="dt-filter-cell sorting_disabled"></th>');

      if (!cfg) {
        $th.html('<span class="dt-filter-empty" aria-hidden="true"></span>');
        $row.append($th);
        continue;
      }

      if (typeof cfg === 'object' && cfg.type === 'select') {
        var $sel = jQuery(
          '<select class="dt-col-filter dt-col-select" data-col="' +
            i +
            '" aria-label="' +
            (cfg.placeholder || i18n('col.status', 'وضعیت')) +
            '"></select>'
        );
        $sel.append(
          jQuery('<option value=""></option>').text(cfg.placeholder || i18n('dt.all', 'همه'))
        );
        (cfg.options || []).forEach(function (o) {
          $sel.append(jQuery('<option></option>').attr('value', o.value).text(o.label));
        });
        $th.append($sel);
      } else {
        var ph =
          (typeof cfg === 'object' && cfg.placeholder) || i18n('dt.filter', 'فیلتر…');
        var $inp = jQuery(
          '<input type="search" class="dt-col-filter dt-col-input" data-col="' +
            i +
            '" placeholder="' +
            ph +
            '" autocomplete="off" aria-label="' +
            ph +
            '" />'
        );
        $th.append($inp);
      }
      $row.append($th);
    }

    $head.append($row);

    var timers = {};
    function applyCol(col, val) {
      table.column(col).search(val).draw();
      markActiveFilters($row);
      updateClearButton(wrap, table);
    }

    $row.on('input', 'input.dt-col-input', function () {
      var col = parseInt(this.getAttribute('data-col'), 10);
      var val = this.value;
      clearTimeout(timers[col]);
      timers[col] = setTimeout(function () {
        applyCol(col, val);
      }, 350);
    });

    $row.on('change', 'select.dt-col-select', function () {
      var col = parseInt(this.getAttribute('data-col'), 10);
      applyCol(col, this.value);
    });

    $row.on('click mousedown', 'input, select, th', function (e) {
      e.stopPropagation();
    });
  }

  function init(selector, options) {
    if (!window.jQuery || !jQuery.fn.DataTable) {
      console.error('DataTables not loaded');
      return null;
    }

    var exportUrl = options && options.exportUrl;
    var exportParams = (options && options.exportParams) || {};
    var columnFilters = options && options.columnFilters;
    if (options) {
      delete options.exportUrl;
      delete options.exportParams;
      delete options.columnFilters;
    }

    var opts = Object.assign(
      {
        processing: true,
        serverSide: true,
        searching: true,
        ordering: true,
        paging: true,
        pageLength: 25,
        lengthMenu: [10, 25, 50, 100],
        stateSave: true,
        autoWidth: false,
        scrollX: false,
        dom:
          "<'row dt-toolbar'<'col-sm-12 col-md-6'l><'col-sm-12 col-md-6'f>>" +
          "<'row'<'col-sm-12'tr>>" +
          "<'row dt-footer'<'col-sm-12 col-md-5'i><'col-sm-12 col-md-7'p>>",
        language: languagePack(),
        classes: {
          sTable: 'admin-table dataTable'
        }
      },
      options || {}
    );

    if (!opts.columnDefs) opts.columnDefs = [];

    var $table = jQuery(selector);
    $table.addClass('nowrap');

    if (!$table.closest('.admin-table-wrap').length) {
      $table.wrap('<div class="admin-table-wrap"></div>');
    }

    var table = $table.DataTable(opts);
    var wrap = $table.closest('.dataTables_wrapper');

    ensureScroll($table);
    buildColumnFilters($table, table, columnFilters, wrap);

    function afterDraw() {
      document.querySelectorAll(selector + ' form[data-confirm]').forEach(function (form) {
        if (form.dataset.bound) return;
        form.dataset.bound = '1';
        form.addEventListener('submit', function (e) {
          if (!confirm(form.getAttribute('data-confirm'))) e.preventDefault();
        });
      });
      markActionCells(selector);
      updateClearButton(wrap, table);
      try {
        table.columns.adjust();
      } catch (_) {}
    }

    table.on('draw', afterDraw);

    table.one('draw', function () {
      buildToolbar(wrap, table, exportUrl, exportParams);
      ensureScroll($table);
      afterDraw();

      var $row = $table.find('thead tr.dt-col-filters');
      if ($row.length) {
        $row.find('.dt-col-filter').each(function () {
          var col = parseInt(this.getAttribute('data-col'), 10);
          try {
            var s = table.column(col).search();
            if (s) {
              this.value = s;
              this.classList.add('is-active');
            }
          } catch (_) {}
        });
        updateClearButton(wrap, table);
      }
    });

    // Recalc when sidebar / window size changes
    var resizeTimer;
    jQuery(window).on('resize.blogdt', function () {
      clearTimeout(resizeTimer);
      resizeTimer = setTimeout(function () {
        try {
          table.columns.adjust();
        } catch (_) {}
      }, 120);
    });

    return table;
  }

  return { init: init, csrfToken: csrfToken, isRtl: isRtl, i18n: i18n };
})();
