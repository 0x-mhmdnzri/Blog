/**
 * BlogApp — server-side DataTables helper.
 * Alignment-safe table-layout, toolbar with CSV export, cell title tooltips.
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
    return url.pathname + url.search;
  }

  function exportIconSvg() {
    return (
      '<svg class="dt-export-ico" viewBox="0 0 24 24" width="16" height="16" fill="currentColor" aria-hidden="true">' +
      '<path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/>' +
      '</svg>'
    );
  }

  /** Build a clean toolbar: [Export] [Length] …… [Search] */
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
          ' — تمام نتایج فیلترشده">' +
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

    // Remove empty Bootstrap row that held length/filter
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

  /** Wrap table in horizontal scroll container once. */
  function ensureScroll($table) {
    if ($table.parent().hasClass('dt-scroll')) return;
    $table.wrap('<div class="dt-scroll"></div>');
  }

  /** Set title= plain text for truncated cells so hover shows full value. */
  function decorateCells(selector) {
    document.querySelectorAll(selector + ' tbody td').forEach(function (td) {
      if (td.classList.contains('dt-actions') || td.classList.contains('dataTables_empty')) return;
      // Skip cells that already have interactive content as primary
      if (td.querySelector('form, button, a.btn, select')) {
        td.classList.add('dt-actions');
        return;
      }
      var text = (td.textContent || '').replace(/\s+/g, ' ').trim();
      if (text.length > 24) {
        td.setAttribute('title', text);
      } else {
        td.removeAttribute('title');
      }
    });
  }

  function init(selector, options) {
    if (!window.jQuery || !jQuery.fn.DataTable) {
      console.error('DataTables not loaded');
      return null;
    }

    var exportUrl = options && options.exportUrl;
    var exportParams = (options && options.exportParams) || {};
    if (options) {
      delete options.exportUrl;
      delete options.exportParams;
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
      autoWidth: false, // critical — with table-layout:fixed
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
    // Drop legacy nowrap that fights ellipsis layout
    $table.removeClass('nowrap');

    if (!$table.closest('.admin-table-wrap').length) {
      $table.wrap('<div class="admin-table-wrap"></div>');
    }

    var table = $table.DataTable(opts);
    var wrap = $table.closest('.dataTables_wrapper');

    ensureScroll($table);

    table.on('draw', function () {
      document.querySelectorAll(selector + ' form[data-confirm]').forEach(function (form) {
        if (form.dataset.bound) return;
        form.dataset.bound = '1';
        form.addEventListener('submit', function (e) {
          if (!confirm(form.getAttribute('data-confirm'))) e.preventDefault();
        });
      });
      decorateCells(selector);
    });

    table.one('draw', function () {
      buildToolbar(wrap, table, exportUrl, exportParams);
      ensureScroll($table);
      decorateCells(selector);
    });

    return table;
  }

  return { init: init, csrfToken: csrfToken, isRtl: isRtl, i18n: i18n };
})();
