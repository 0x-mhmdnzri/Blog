/* Ranking filter for AdminAnalytics geography */
(function () {
  var filterEl = document.getElementById('anaGeoFilter');
  var rankList = document.getElementById('anaGeoRank');
  if (!filterEl || !rankList) return;
  filterEl.addEventListener('input', function () {
    var q = (filterEl.value || '').trim().toLowerCase();
    rankList.querySelectorAll('.ana-geo-rank-item').forEach(function (li) {
      var nameEl = li.querySelector('.name');
      var name = (nameEl && nameEl.textContent || '').toLowerCase();
      var code = (li.getAttribute('data-code') || '').toLowerCase();
      var hay = name + ' ' + code;
      li.style.display = (!q || hay.indexOf(q) !== -1) ? '' : 'none';
    });
  });
})();
