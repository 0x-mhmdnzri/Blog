/**
 * BlogApp — server-side DataTables helper.
 * Usage:
 *   BlogDT.init('#postsTable', {
 *     ajax: '/Admin/PostsData',
 *     columns: [ { data: 'title' }, ... ],
 *     order: [[7, 'desc']],
 *     columnDefs: [{ orderable: false, targets: [0, 8] }]
 *   });
 */
window.BlogDT = (function () {
  function isRtl() {
    return (document.documentElement.getAttribute('dir') || 'rtl') === 'rtl';
  }

  function csrfToken() {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
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
      responsive: true,
      language: isRtl() ? {
        processing: 'در حال بارگذاری…',
        search: 'جست‌وجو:',
        lengthMenu: 'نمایش _MENU_ ردیف',
        info: 'نمایش _START_ تا _END_ از _TOTAL_',
        infoEmpty: 'موردی نیست',
        infoFiltered: '(فیلتر از _MAX_)',
        zeroRecords: 'نتیجه‌ای یافت نشد',
        emptyTable: 'جدولی خالی است',
        paginate: { first: 'اول', last: 'آخر', next: 'بعدی', previous: 'قبلی' }
      } : {
        processing: 'Loading…',
        search: 'Search:',
        lengthMenu: 'Show _MENU_ rows',
        info: 'Showing _START_ to _END_ of _TOTAL_',
        infoEmpty: 'No entries',
        infoFiltered: '(filtered from _MAX_)',
        zeroRecords: 'No matching records',
        emptyTable: 'No data',
        paginate: { first: 'First', last: 'Last', next: 'Next', previous: 'Previous' }
      }
    }, options || {});

    // Default: first column (often # / id index display) not orderable if marked
    if (!opts.columnDefs) opts.columnDefs = [];

    var table = jQuery(selector).DataTable(opts);

    // Re-bind after draw for any action buttons that need live handlers
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
