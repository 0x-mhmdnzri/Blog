/**
 * BlogApp — server-side DataTables helper (single entry point).
 *
 * Language strings come from window.__i18n (seeded by _AdminLayout from parrot DB).
 * Optional exportUrl: server endpoint that exports ALL matching rows as CSV (not page-limited).
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

  /** Build export URL with current DataTables search + optional extra query params. */
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

  function injectExportButton($table, table, exportUrl, exportParams) {
    if (!exportUrl) return;
    var wrap = $table.closest('.dataTables_wrapper');
    if (!wrap.length) return;
    var toolbar = wrap.find('.dt-toolbar, .row').first();
    if (!toolbar.length) toolbar = wrap;

    var label = i18n('dt.export_csv', 'خروجی CSV');
    var btn = jQuery(
      '<button type="button" class="btn btn-sm btn-ghost dt-export-csv" title="' +
        label +
        '">' +
        '<span aria-hidden="true">↓</span> ' +
        label +
        '</button>'
    );
    btn.on('click', function () {
      window.location.href = buildExportUrl(exportUrl, table, exportParams);
    });

    // Place next to length/search toolbar
    var lengthDiv = wrap.find('.dataTables_length').parent();
    if (lengthDiv.length) {
      lengthDiv.prepend(btn.css({ marginInlineEnd: '0.5rem', marginBottom: '0.35rem' }));
    } else {
      toolbar.prepend(btn);
    }
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
      autoWidth: false,
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
    if (!$table.closest('.admin-table-wrap').length) {
      $table.wrap('<div class="admin-table-wrap"></div>');
    }

    var table = $table.DataTable(opts);

    table.on('draw', function () {
      document.querySelectorAll(selector + ' form[data-confirm]').forEach(function (form) {
        if (form.dataset.bound) return;
        form.dataset.bound = '1';
        form.addEventListener('submit', function (e) {
          if (!confirm(form.getAttribute('data-confirm'))) e.preventDefault();
        });
      });
    });

    // Export button after first draw so wrapper exists
    table.one('draw', function () {
      injectExportButton($table, table, exportUrl, exportParams);
    });

    return table;
  }

  return { init: init, csrfToken: csrfToken, isRtl: isRtl, i18n: i18n };
})();
