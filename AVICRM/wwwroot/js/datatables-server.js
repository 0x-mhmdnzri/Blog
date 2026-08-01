/**
 * AVICRM — server-side DataTables (all admin tables)
 *
 * Column filters live OUTSIDE <thead> in a dedicated bar so they never
 * fight DataTables column-width calculation (the previous thead approach
 * caused the RTL / sparse / stacked layout you saw).
 */
window.BlogDT = (function () {
  var STATE_VERSION = 3; // bump to invalidate broken localStorage state

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
      processing: i18n('dt.processing', 'Loading\u2026'),
      search: '',
      searchPlaceholder: i18n('dt.search_placeholder', '\u062c\u0633\u062a\u200c\u0648\u062c\u0648\u2026'),
      lengthMenu: i18n('dt.length_menu', '\u0646\u0645\u0627\u06cc\u0634 _MENU_'),
      info: i18n('dt.info', '\u0646\u0645\u0627\u06cc\u0634 _START_ \u062a\u0627 _END_ \u0627\u0632 _TOTAL_'),
      infoEmpty: i18n('dt.info_empty', '\u0645\u0648\u0631\u062f\u06cc \u0646\u06cc\u0633\u062a'),
      infoFiltered: i18n('dt.info_filtered', '(\u0641\u06cc\u0644\u062a\u0631 \u0627\u0632 _MAX_)'),
      zeroRecords: i18n('dt.zero_records', '\u0646\u062a\u06cc\u062c\u0647\u200c\u0627\u06cc \u06cc\u0627\u0641\u062a \u0646\u0634\u062f'),
      emptyTable: i18n('dt.empty_table', '\u062f\u0627\u062f\u0647\u200c\u0627\u06cc \u0646\u06cc\u0633\u062a'),
      paginate: {
        first: i18n('dt.paginate_first', '\u0627\u0648\u0644'),
        last: i18n('dt.paginate_last', '\u0622\u062e\u0631'),
        next: i18n('dt.paginate_next', '\u0628\u0639\u062f\u06cc'),
        previous: i18n('dt.paginate_previous', '\u0642\u0628\u0644\u06cc')
      }
    };
  }

  function buildExportUrl(baseUrl, table, extraParams) {
    var url = new URL(baseUrl, window.location.origin);
    try {
      var search = table.search() || '';
      if (search) url.searchParams.set('search', search);
    } catch (_) {}
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
    btn.toggleClass('is-visible', !!any);
  }

  function markActiveFilters(bar) {
    if (!bar || !bar.length) return;
    bar.find('.dt-col-filter').each(function () {
      var v = (this.value || '').trim();
      this.classList.toggle('is-active', v.length > 0);
    });
  }

  /**
   * Build a filter BAR above the table (not inside thead).
   * Each control maps to a column index via data-col.
   */
  function buildFilterBar(wrap, table, columnFilters, headerLabels) {
    if (!columnFilters || !columnFilters.length) return null;

    var hasAny = columnFilters.some(function (c) {
      return !!c;
    });
    if (!hasAny) return null;
    if (wrap.find('.dt-filter-bar').length) return wrap.find('.dt-filter-bar');

    var bar = jQuery('<div class="dt-filter-bar" role="search"></div>');
    var inner = jQuery('<div class="dt-filter-bar-inner"></div>');

    columnFilters.forEach(function (cfg, i) {
      if (!cfg) return;

      var label =
        (headerLabels && headerLabels[i]) ||
        (typeof cfg === 'object' && cfg.placeholder) ||
        '';

      var cell = jQuery(
        '<div class="dt-filter-item" data-col="' + i + '"></div>'
      );
      if (label) {
        cell.append(
          jQuery('<label class="dt-filter-label"></label>').text(label)
        );
      }

      if (typeof cfg === 'object' && cfg.type === 'select') {
        var $sel = jQuery(
          '<select class="dt-col-filter dt-col-select" data-col="' +
            i +
            '"></select>'
        );
        $sel.append(
          jQuery('<option value=""></option>').text(
            cfg.placeholder || i18n('dt.all', '\u0647\u0645\u0647')
          )
        );
        (cfg.options || []).forEach(function (o) {
          $sel.append(
            jQuery('<option></option>').attr('value', o.value).text(o.label)
          );
        });
        cell.append($sel);
      } else {
        var ph =
          (typeof cfg === 'object' && cfg.placeholder) ||
          i18n('dt.filter', '\u0641\u06cc\u0644\u062a\u0631\u2026');
        var $inp = jQuery(
          '<input type="search" class="dt-col-filter dt-col-input" data-col="' +
            i +
            '" placeholder="' +
            ph +
            '" autocomplete="off" />'
        );
        cell.append($inp);
      }

      inner.append(cell);
    });

    bar.append(inner);

    // Place filter bar after toolbar, before the table scroll area
    var toolbar = wrap.find('.dt-toolbar-bar');
    if (toolbar.length) toolbar.after(bar);
    else wrap.prepend(bar);

    var timers = {};
    function applyCol(col, val) {
      table.column(col).search(val).draw();
      markActiveFilters(bar);
      updateClearButton(wrap, table);
    }

    bar.on('input', 'input.dt-col-input', function () {
      var col = parseInt(this.getAttribute('data-col'), 10);
      var val = this.value;
      clearTimeout(timers[col]);
      timers[col] = setTimeout(function () {
        applyCol(col, val);
      }, 350);
    });

    bar.on('change', 'select.dt-col-select', function () {
      var col = parseInt(this.getAttribute('data-col'), 10);
      applyCol(col, this.value);
    });

    return bar;
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
      var label = i18n('dt.export_csv', '\u062e\u0631\u0648\u062c\u06cc CSV');
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

    var clearLbl = i18n('dt.clear_filters', '\u067e\u0627\u06a9\u200c\u06a9\u0631\u062f\u0646 \u0641\u06cc\u0644\u062a\u0631\u0647\u0627');
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
        !$r.find('table, .dataTables_info, .dataTables_paginate, .dt-scroll')
          .length &&
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

  function readHeaderLabels($table) {
    var labels = [];
    $table
      .find('thead tr')
      .first()
      .children('th')
      .each(function () {
        labels.push(jQuery(this).text().trim());
      });
    return labels;
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
        stateDuration: 60 * 60 * 24 * 7,
        stateSaveParams: function (_s, data) {
          data.blogDtVersion = STATE_VERSION;
        },
        stateLoadParams: function (_s, data) {
          // Drop corrupted state from older broken layouts
          if (!data || data.blogDtVersion !== STATE_VERSION) return false;
        },
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

    // Strip any leftover filter rows from previous broken builds
    $table.find('thead tr.dt-col-filters').remove();

    if (!$table.closest('.admin-table-wrap').length) {
      $table.wrap('<div class="admin-table-wrap"></div>');
    }

    var headerLabels = readHeaderLabels($table);
    var table = $table.DataTable(opts);
    var wrap = $table.closest('.dataTables_wrapper');

    ensureScroll($table);

    function afterDraw() {
      document
        .querySelectorAll(selector + ' form[data-confirm]')
        .forEach(function (form) {
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
      var filterBar = buildFilterBar(wrap, table, columnFilters, headerLabels);
      ensureScroll($table);
      afterDraw();

      // Restore column filter values from state
      if (filterBar) {
        filterBar.find('.dt-col-filter').each(function () {
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

  return { init: init, csrfToken: csrfToken, i18n: i18n };
})();
