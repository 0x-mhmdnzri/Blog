/**
 * BlogApp — server-side DataTables helper (single entry point).
 *
 * Language strings come from window.__i18n (seeded by _AdminLayout from parrot DB).
 * Fallback English if missing.
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

  function init(selector, options) {
    if (!window.jQuery || !jQuery.fn.DataTable) {
      console.error('DataTables not loaded');
      return null;
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

    return table;
  }

  return { init: init, csrfToken: csrfToken, isRtl: isRtl, i18n: i18n };
})();
