/**
 * BlogApp — server-side DataTables helper.
 * Supports global search + per-column filters (input or select for status).
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
      search: i18n('dt.search', 'Search'),
      searchPlaceholder: i18n('dt.search_placeholder', 'Search…'),
      lengthMenu: i18n('dt.length_menu', 'Show _MENU_'),
      info: i18n('dt.info', 'Showing _START_ to _END_ of _TOTAL_'),
      infoEmpty: i18n('dt.info_empty', 'No entries'),
      infoFiltered: i18n('dt.info_filtered', '(filtered from _MAX_)'),
      zeroRecords: i18n('dt.zero_records', 'No matching records'),
      emptyTable: i18n('dt.empty_table', 'No data'),
      paginate: {
        first: i18n('dt.paginate_first', 'First'),
        last: i18n('dt.paginate_last', 'Last'),
        next: i18n('dt.paginate_next', 'Next'),
        previous: i18n('dt.paginate_previous', 'Previous')
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
    // include column filters
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

    if (length.length) start.append(length);
    if (filter.length) end.append(filter);

    wrap.find('.row.dt-toolbar').remove();
    wrap.find('> .row').each(function () {
      var $r = jQuery(this);
      if (!$r.find('table, .dataTables_info, .dataTables_paginate, .dt-scroll').length &&
          !$r.find('.dataTables_length, .dataTables_filter').length) {
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

  /**
   * columnFilters: array parallel to columns, each item:
   *   false / null  → no filter
   *   true / 'text' → text input
   *   { type: 'select', options: [{value, label}, ...] } → dropdown
   */
  function buildColumnFilters($table, table, columnFilters) {
    if (!columnFilters || !columnFilters.length) return;
    if ($table.find('thead tr.dt-col-filters').length) return;

    var $head = $table.find('thead');
    var colCount = $table.find('thead tr').first().children('th').length;
    var $row = jQuery('<tr class="dt-col-filters" role="row"></tr>');

    for (var i = 0; i < colCount; i++) {
      var cfg = columnFilters[i];
      var $th = jQuery('<th class="dt-filter-cell"></th>');

      if (!cfg) {
        $th.html('<span class="dt-filter-empty"></span>');
        $row.append($th);
        continue;
      }

      if (typeof cfg === 'object' && cfg.type === 'select') {
        var $sel = jQuery('<select class="dt-col-filter dt-col-select" data-col="' + i + '"></select>');
        $sel.append(jQuery('<option value=""></option>').text(cfg.placeholder || i18n('dt.all', 'همه')));
        (cfg.options || []).forEach(function (o) {
          $sel.append(jQuery('<option></option>').attr('value', o.value).text(o.label));
        });
        $th.append($sel);
      } else {
        var ph = (typeof cfg === 'object' && cfg.placeholder) || i18n('dt.filter', 'فیلتر…');
        var $inp = jQuery(
          '<input type="search" class="dt-col-filter dt-col-input" data-col="' +
            i +
            '" placeholder="' +
            ph +
            '" autocomplete="off" />'
        );
        $th.append($inp);
      }
      $row.append($th);
    }

    $head.append($row);

    // debounce text inputs
    var timers = {};
    $row.on('input', 'input.dt-col-input', function () {
      var col = parseInt(this.getAttribute('data-col'), 10);
      var val = this.value;
      clearTimeout(timers[col]);
      timers[col] = setTimeout(function () {
        table.column(col).search(val).draw();
      }, 350);
    });

    $row.on('change', 'select.dt-col-select', function () {
      var col = parseInt(this.getAttribute('data-col'), 10);
      table.column(col).search(this.value).draw();
    });

    // stop sort when clicking filter controls
    $row.on('click mousedown', 'input, select', function (e) {
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

    var opts = Object.assign({
      processing: true,
      serverSide: true,
      searching: true,
      ordering: true,
      paging: true,
      pageLength: 25,
      lengthMenu: [10, 25, 50, 100],
      stateSave: true,
      autoWidth: true,
      scrollX: false,
      dom:
        "<'row dt-toolbar'<'col-sm-12 col-md-6'l><'col-sm-12 col-md-6'f>>" +
        "<'row'<'col-sm-12'tr>>" +
        "<'row dt-footer'<'col-sm-12 col-md-5'i><'col-sm-12 col-md-7'p>>",
      language: languagePack(),
      classes: {
        sTable: 'admin-table dataTable'
      }
    }, options || {});

    if (!opts.columnDefs) opts.columnDefs = [];

    var $table = jQuery(selector);
    $table.addClass('nowrap');

    if (!$table.closest('.admin-table-wrap').length) {
      $table.wrap('<div class="admin-table-wrap"></div>');
    }

    var table = $table.DataTable(opts);
    var wrap = $table.closest('.dataTables_wrapper');

    ensureScroll($table);
    buildColumnFilters($table, table, columnFilters);

    table.on('draw', function () {
      document.querySelectorAll(selector + ' form[data-confirm]').forEach(function (form) {
        if (form.dataset.bound) return;
        form.dataset.bound = '1';
        form.addEventListener('submit', function (e) {
          if (!confirm(form.getAttribute('data-confirm'))) e.preventDefault();
        });
      });
      markActionCells(selector);
    });

    table.one('draw', function () {
      buildToolbar(wrap, table, exportUrl, exportParams);
      ensureScroll($table);
      markActionCells(selector);
    });

    return table;
  }

  return { init: init, csrfToken: csrfToken, isRtl: isRtl, i18n: i18n };
})();
