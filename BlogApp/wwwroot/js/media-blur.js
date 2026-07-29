/**
 * Blur-up progressive media: keep filter:blur until the resource is ready,
 * then fade to sharp. Works for post images and HTML5 video.
 */
(function () {
  function reveal(el) {
    el.classList.add('is-loaded');
    var wrap = el.closest('.media-blur-wrap');
    if (wrap) wrap.classList.add('is-ready');
  }

  function bindImage(img) {
    if (img.dataset.blurBound === '1') return;
    img.dataset.blurBound = '1';
    if (img.complete && img.naturalWidth > 0) {
      reveal(img);
      return;
    }
    img.addEventListener('load', function () { reveal(img); }, { once: true });
    img.addEventListener('error', function () { reveal(img); }, { once: true });
  }

  function bindVideo(video) {
    if (video.dataset.blurBound === '1') return;
    video.dataset.blurBound = '1';
    var done = function () { reveal(video); };
    if (video.readyState >= 2) {
      done();
      return;
    }
    video.addEventListener('loadeddata', done, { once: true });
    video.addEventListener('canplay', done, { once: true });
    video.addEventListener('error', done, { once: true });
  }

  function scan(root) {
    (root || document).querySelectorAll('img.media-blur, .readme-content img').forEach(function (img) {
      if (!img.classList.contains('media-blur')) img.classList.add('media-blur');
      if (!img.closest('.media-blur-wrap')) {
        var wrap = document.createElement('span');
        wrap.className = 'media-blur-wrap';
        img.parentNode.insertBefore(wrap, img);
        wrap.appendChild(img);
      }
      if (!img.hasAttribute('loading')) img.setAttribute('loading', 'lazy');
      if (!img.hasAttribute('decoding')) img.setAttribute('decoding', 'async');
      bindImage(img);
    });

    (root || document).querySelectorAll('video.media-blur, .post-video-embed video').forEach(function (v) {
      if (!v.classList.contains('media-blur')) v.classList.add('media-blur');
      var embed = v.closest('.post-video-embed') || v.parentElement;
      if (embed && !embed.classList.contains('media-blur-wrap')) {
        embed.classList.add('media-blur-wrap');
      }
      if (!v.hasAttribute('preload')) v.setAttribute('preload', 'metadata');
      bindVideo(v);
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () { scan(); });
  } else {
    scan();
  }

  // Live preview in editor
  window.BlogMediaBlur = { scan: scan };
})();
