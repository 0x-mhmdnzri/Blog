/**
 * BlogApp — server-side DataTables helper (single entry point).
 *
 * Usage:
 *   BlogDT.init('#postsTable', {
 *     ajax: '/Admin/PostsData',
 *     columns: [ { data: 'title' }, ... ],
 *     order: [[7, 'desc']],
 *     columnDefs: [{ orderable: false, targets: [0, 8] }]
 *   });
 *
 * Styling lives in ~/css/datatables-admin.css (one file for all tables).
 */
window.BlogDT = (function () {
  function isRtl() {
    return (document.documentElement.getAttribute('dir') || 'rtl') === 'rtl';
  }

  function csrfToken() {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
  }

  function languagePack() {
    if (isRtl()) {
      return {
        processing: 'در حال بارگذاری…',
        search: 'جست‌وجو',
        searchPlaceholder: 'جست‌وجو…',
        lengthMenu: 'نمایش _MENU_',
        info: 'نمایش _START_ تا _END_ از _TOTAL_',
        infoEmpty: 'موردی نیست',
        infoFiltered: '(فیلتر از _MAX_)',
        zeroRecords: 'نتیجه‌ای یافت نشد',
        emptyTable: 'جدولی خالی است',
        paginate: { first: 'اول', last: 'آخر', next: 'بعدی', previous: 'قبلی' }
      };
    }
    return {
      processing: 'Loading…',
      search: 'Search',
      searchPlaceholder: 'Search…',
      lengthMenu: 'Show _MENU_',
      info: 'Showing _START_ to _END_ of _TOTAL_',
      infoEmpty: 'No entries',
      infoFiltered: '(filtered from _MAX_)',
      zeroRecords: 'No matching records',
      emptyTable: 'No data',
      paginate: { first: 'First', last: 'Last', next: 'Next', previous: 'Previous' }
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
      // Bootstrap 5 layout: top toolbar + bottom footer inside our card shell
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
    // Ensure table sits in the card wrap even if markup forgot the class
    if (!$table.closest('.admin-table-wrap').length) {
      $table.wrap('<div class="admin-table-wrap"></div>');
    }

    var table = $table.DataTable(opts);

    // Confirm dialogs on action forms after each draw
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

  return { init: init, csrfToken: csrfToken, isRtl: isRtl };
})();
