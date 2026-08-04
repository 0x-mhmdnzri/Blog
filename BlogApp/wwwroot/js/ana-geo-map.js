/**
 * AdminAnalytics geography — choropleth world map + accent data-flow particles.
 * Expects window.__anaGeo = { countries: [{ code, name, count }], totalViews, hub: 'IR' }
 */
(function () {
  'use strict';

  var data = window.__anaGeo;
  if (!data || !document.getElementById('anaGeoMap')) return;

  var countries = (data.countries || []).filter(function (c) {
    return c && c.code && c.count > 0;
  });
  var total = countries.reduce(function (s, c) { return s + c.count; }, 0) || data.totalViews || 1;
  var max = countries.reduce(function (m, c) { return Math.max(m, c.count); }, 1);
  var byCode = {};
  countries.forEach(function (c) {
    byCode[String(c.code).toUpperCase()] = c;
  });

  var accent = (getComputedStyle(document.documentElement).getPropertyValue('--accent') || '#e3b341').trim();
  var mapEl = document.getElementById('anaGeoMap');
  var tip = document.getElementById('anaGeoTip');
  var flowCanvas = document.getElementById('anaGeoFlow');
  var rankList = document.getElementById('anaGeoRank');
  var emptyEl = document.getElementById('anaGeoEmpty');

  if (!countries.length) {
    if (emptyEl) emptyEl.hidden = false;
    return;
  }
  if (emptyEl) emptyEl.hidden = true;

  /* —— Rank list —— */
  if (rankList) {
    rankList.innerHTML = countries
      .slice()
      .sort(function (a, b) { return b.count - a.count; })
      .map(function (c, i) {
        var pct = Math.round((c.count / max) * 100);
        return (
          '<li class="ana-geo-rank-item" data-code="' + esc(c.code) + '">' +
          '<span class="idx">' + (i + 1) + '</span>' +
          '<span class="name" title="' + esc(c.name || c.code) + '">' + esc(c.name || c.code) + '</span>' +
          '<span class="cnt ltr-field">' + fmt(c.count) + '</span>' +
          '<span class="bar"><i style="width:' + pct + '%"></i></span>' +
          '</li>'
        );
      })
      .join('');
  }

  /* —— Region values for choropleth —— */
  var regionValues = {};
  countries.forEach(function (c) {
    regionValues[String(c.code).toUpperCase()] = c.count;
  });

  var map = null;
  var regionCoords = {}; // code -> {x,y} in map container pixels (approx)

  function buildMap() {
    if (typeof jsVectorMap === 'undefined') {
      mapEl.innerHTML =
        '<p class="text-muted-dark small text-center py-5">Map library unavailable</p>';
      return;
    }

    map = new jsVectorMap({
      selector: '#anaGeoMap',
      map: 'world',
      backgroundColor: 'transparent',
      draggable: true,
      zoomButtons: true,
      zoomOnScroll: false,
      regionsSelectable: false,
      visualizeData: {
        scale: [
          colorMix(accent, 0.18),
          colorMix(accent, 0.45),
          colorMix(accent, 0.75),
          accent
        ],
        values: regionValues
      },
      regionStyle: {
        initial: {
          fill: 'var(--geo-land, #1a1f2b)',
          stroke: 'var(--geo-stroke, #2a3142)',
          strokeWidth: 0.4,
          fillOpacity: 1
        },
        hover: {
          fillOpacity: 1,
          cursor: 'pointer'
        },
        selected: {
          fill: accent
        }
      },
      series: {
        regions: [
          {
            attribute: 'fill',
            scale: [
              colorMix(accent, 0.2),
              colorMix(accent, 0.55),
              accent
            ],
            values: regionValues,
            min: 0,
            max: max
          }
        ]
      },
      onRegionTooltipShow: function (event, tooltip, code) {
        event.preventDefault();
        var c = byCode[code];
        if (!c) {
          tooltip.text(code);
          return;
        }
        var pct = total ? ((c.count / total) * 100).toFixed(1) : '0';
        tooltip.text(
          '<strong>' + esc(c.name || code) + '</strong><br/>' +
          fmt(c.count) + ' · ' + pct + '%',
          true
        );
      },
      onRegionClick: function (event, code) {
        showTipFor(code, event);
        highlightRank(code);
      }
    });

    // Approximate marker positions from region paths for flow animation
    try {
      var paths = mapEl.querySelectorAll('path.jvm-region, path[data-code]');
      paths.forEach(function (p) {
        var code = (p.getAttribute('data-code') || p.id || '').replace(/^.*-/, '').toUpperCase();
        if (!byCode[code]) return;
        var b = p.getBBox();
        var svg = p.ownerSVGElement;
        if (!svg) return;
        var pt = svg.createSVGPoint();
        pt.x = b.x + b.width / 2;
        pt.y = b.y + b.height / 2;
        var ctm = p.getScreenCTM();
        if (!ctm) return;
        var sp = pt.matrixTransform(ctm);
        var rect = mapEl.getBoundingClientRect();
        regionCoords[code] = {
          x: sp.x - rect.left,
          y: sp.y - rect.top
        };
      });
    } catch (_) {}

    startFlow();
  }

  function showTipFor(code, event) {
    if (!tip) return;
    var c = byCode[code];
    if (!c) {
      tip.classList.remove('is-visible');
      return;
    }
    var pct = total ? Math.round((c.count / total) * 1000) / 10 : 0;
    var barPct = Math.round((c.count / max) * 100);
    tip.innerHTML =
      '<div class="tip-code ltr-field">' + esc(code) + '</div>' +
      '<div class="tip-name">' + esc(c.name || code) + '</div>' +
      '<div class="tip-row"><span>Views</span><strong class="ltr-field">' + fmt(c.count) + '</strong></div>' +
      '<div class="tip-row"><span>Share</span><strong class="ltr-field">' + pct + '%</strong></div>' +
      '<div class="tip-bar"><i style="width:' + barPct + '%"></i></div>';

    var wrap = mapEl.parentElement;
    var wr = wrap.getBoundingClientRect();
    var x = 16;
    var y = 16;
    if (event && event.clientX) {
      x = event.clientX - wr.left + 14;
      y = event.clientY - wr.top + 14;
    } else if (regionCoords[code]) {
      x = regionCoords[code].x + 12;
      y = regionCoords[code].y + 12;
    }
    x = Math.min(x, wr.width - 180);
    y = Math.min(y, wr.height - 120);
    tip.style.left = Math.max(8, x) + 'px';
    tip.style.top = Math.max(8, y) + 'px';
    tip.classList.add('is-visible');
  }

  function highlightRank(code) {
    if (!rankList) return;
    rankList.querySelectorAll('.ana-geo-rank-item').forEach(function (el) {
      el.classList.toggle('is-active', el.getAttribute('data-code') === code);
    });
  }

  if (rankList) {
    rankList.addEventListener('click', function (e) {
      var item = e.target.closest('.ana-geo-rank-item');
      if (!item) return;
      var code = item.getAttribute('data-code');
      showTipFor(code);
      highlightRank(code);
    });
  }

  mapEl.addEventListener('mouseleave', function () {
    if (tip) tip.classList.remove('is-visible');
  });

  /* —— Data flow particles (accent color) —— */
  var particles = [];
  var raf = 0;
  var hubCode = (data.hub || 'IR').toUpperCase();

  function startFlow() {
    if (!flowCanvas) return;
    var reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (reduce) return;

    function resize() {
      var r = mapEl.getBoundingClientRect();
      flowCanvas.width = r.width * (window.devicePixelRatio || 1);
      flowCanvas.height = r.height * (window.devicePixelRatio || 1);
      flowCanvas.style.width = r.width + 'px';
      flowCanvas.style.height = r.height + 'px';
      var ctx = flowCanvas.getContext('2d');
      ctx.setTransform(window.devicePixelRatio || 1, 0, 0, window.devicePixelRatio || 1, 0, 0);
    }
    resize();
    window.addEventListener('resize', resize);

    var hub = regionCoords[hubCode] || {
      x: mapEl.clientWidth * 0.55,
      y: mapEl.clientHeight * 0.42
    };

    var sources = Object.keys(regionCoords).filter(function (k) {
      return k !== hubCode && byCode[k];
    });
    if (!sources.length) {
      sources = Object.keys(regionCoords);
    }

    function spawn() {
      if (!sources.length) return;
      var code = sources[Math.floor(Math.random() * sources.length)];
      var from = regionCoords[code];
      if (!from) return;
      var weight = (byCode[code] && byCode[code].count) || 1;
      particles.push({
        x: from.x,
        y: from.y,
        tx: hub.x + (Math.random() - 0.5) * 12,
        ty: hub.y + (Math.random() - 0.5) * 12,
        t: 0,
        speed: 0.004 + Math.random() * 0.006 + Math.min(weight / max, 1) * 0.004,
        r: 1.6 + Math.random() * 1.8,
        alpha: 0.85
      });
      if (particles.length > 48) particles.shift();
    }

    var lastSpawn = 0;
    function tick(now) {
      raf = requestAnimationFrame(tick);
      var ctx = flowCanvas.getContext('2d');
      var w = mapEl.clientWidth;
      var h = mapEl.clientHeight;
      ctx.clearRect(0, 0, w, h);

      if (now - lastSpawn > 280) {
        spawn();
        if (Math.random() > 0.4) spawn();
        lastSpawn = now;
      }

      // hub glow
      var g = ctx.createRadialGradient(hub.x, hub.y, 0, hub.x, hub.y, 28);
      g.addColorStop(0, hexAlpha(accent, 0.35));
      g.addColorStop(1, hexAlpha(accent, 0));
      ctx.fillStyle = g;
      ctx.beginPath();
      ctx.arc(hub.x, hub.y, 28, 0, Math.PI * 2);
      ctx.fill();

      for (var i = particles.length - 1; i >= 0; i--) {
        var p = particles[i];
        p.t += p.speed;
        if (p.t >= 1) {
          particles.splice(i, 1);
          continue;
        }
        // ease out cubic along quadratic curve
        var t = p.t;
        var cx = (p.x + p.tx) / 2;
        var cy = Math.min(p.y, p.ty) - 40 - Math.abs(p.x - p.tx) * 0.08;
        var x = (1 - t) * (1 - t) * p.x + 2 * (1 - t) * t * cx + t * t * p.tx;
        var y = (1 - t) * (1 - t) * p.y + 2 * (1 - t) * t * cy + t * t * p.ty;
        var a = p.alpha * (t < 0.15 ? t / 0.15 : t > 0.85 ? (1 - t) / 0.15 : 1);

        ctx.beginPath();
        ctx.fillStyle = hexAlpha(accent, a);
        ctx.arc(x, y, p.r, 0, Math.PI * 2);
        ctx.fill();

        // trail
        ctx.beginPath();
        ctx.strokeStyle = hexAlpha(accent, a * 0.35);
        ctx.lineWidth = 1;
        ctx.moveTo(x, y);
        var t2 = Math.max(0, t - 0.08);
        var x2 = (1 - t2) * (1 - t2) * p.x + 2 * (1 - t2) * t2 * cx + t2 * t2 * p.tx;
        var y2 = (1 - t2) * (1 - t2) * p.y + 2 * (1 - t2) * t2 * cy + t2 * t2 * p.ty;
        ctx.lineTo(x2, y2);
        ctx.stroke();
      }
    }
    raf = requestAnimationFrame(tick);
  }

  function colorMix(hex, a) {
    return hexAlpha(hex, a);
  }
  function hexAlpha(hex, a) {
    hex = String(hex).trim();
    if (hex.charAt(0) === '#') hex = hex.slice(1);
    if (hex.length === 3) {
      hex = hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
    }
    var n = parseInt(hex, 16);
    if (isNaN(n)) return 'rgba(227,179,65,' + a + ')';
    var r = (n >> 16) & 255;
    var g = (n >> 8) & 255;
    var b = n & 255;
    return 'rgba(' + r + ',' + g + ',' + b + ',' + a + ')';
  }
  function fmt(n) {
    return Number(n).toLocaleString();
  }
  function esc(s) {
    return String(s || '').replace(/[&<>"']/g, function (c) {
      if (c === '&') return String.fromCharCode(38) + 'amp;';
      if (c === '<') return String.fromCharCode(38) + 'lt;';
      if (c === '>') return String.fromCharCode(38) + 'gt;';
      if (c === '"') return String.fromCharCode(38) + 'quot;';
      return String.fromCharCode(38) + '#39;';
    });
  }

  // Country display names (subset + passthrough)
  var NAMES = {
    IR: 'Iran', US: 'United States', DE: 'Germany', GB: 'United Kingdom', FR: 'France',
    CA: 'Canada', AU: 'Australia', NL: 'Netherlands', SE: 'Sweden', NO: 'Norway',
    TR: 'Turkey', AE: 'UAE', IN: 'India', CN: 'China', JP: 'Japan', KR: 'South Korea',
    BR: 'Brazil', RU: 'Russia', IT: 'Italy', ES: 'Spain', PL: 'Poland', UA: 'Ukraine',
    IQ: 'Iraq', AF: 'Afghanistan', PK: 'Pakistan', SA: 'Saudi Arabia', QA: 'Qatar',
    FI: 'Finland', DK: 'Denmark', CH: 'Switzerland', AT: 'Austria', BE: 'Belgium'
  };
  countries.forEach(function (c) {
    var code = String(c.code).toUpperCase();
    if (!c.name || c.name === c.code) c.name = NAMES[code] || c.code;
    c.code = code;
  });

  // Rebuild rank names after localization
  if (rankList) {
    rankList.querySelectorAll('.ana-geo-rank-item').forEach(function (el) {
      var code = el.getAttribute('data-code');
      var c = byCode[code];
      if (c) {
        var nameEl = el.querySelector('.name');
        if (nameEl) {
          nameEl.textContent = c.name;
          nameEl.title = c.name;
        }
      }
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', buildMap);
  } else {
    // maps script may load async
    if (typeof jsVectorMap !== 'undefined') buildMap();
    else {
      var tries = 0;
      var iv = setInterval(function () {
        tries++;
        if (typeof jsVectorMap !== 'undefined') {
          clearInterval(iv);
          buildMap();
        } else if (tries > 40) {
          clearInterval(iv);
          mapEl.innerHTML = '<p class="text-muted-dark small text-center py-5">Map failed to load</p>';
        }
      }, 100);
    }
  }
})();
