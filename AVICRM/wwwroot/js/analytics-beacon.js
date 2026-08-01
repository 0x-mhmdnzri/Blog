(function () {
  var root = document.querySelector('[data-analytics-post-id]');
  if (!root) return;
  var postId = parseInt(root.getAttribute('data-analytics-post-id'), 10);
  if (!postId) return;

  var started = Date.now();
  var sent = false;

  function sendDuration() {
    if (sent) return;
    sent = true;
    var sec = Math.round((Date.now() - started) / 1000);
    if (sec < 3) return;
    var body = JSON.stringify({ postId: postId, seconds: sec });
    if (navigator.sendBeacon) {
      navigator.sendBeacon('/Analytics/Duration', new Blob([body], { type: 'application/json' }));
    } else {
      fetch('/Analytics/Duration', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: body, keepalive: true });
    }
  }

  document.addEventListener('visibilitychange', function () {
    if (document.visibilityState === 'hidden') sendDuration();
  });
  window.addEventListener('pagehide', sendDuration);

  var island = document.querySelector('.post-body-island') || root;
  island.addEventListener('click', function (e) {
    var rect = island.getBoundingClientRect();
    if (rect.width < 1 || rect.height < 1) return;
    var x = Math.round(((e.clientX - rect.left) / rect.width) * 1000);
    var y = Math.round(((e.clientY - rect.top) / rect.height) * 1000);
    x = Math.max(0, Math.min(1000, x));
    y = Math.max(0, Math.min(1000, y));
    fetch('/Analytics/Heatmap', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ postId: postId, x: x, y: y }),
      keepalive: true
    }).catch(function () {});
  });
})();
