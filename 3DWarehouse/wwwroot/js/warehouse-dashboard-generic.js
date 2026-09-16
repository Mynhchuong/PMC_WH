// Banner tổng quan + đồng hồ sống + 2 feed (nhập/xuất) cho schema "generic" — bố cục CỐ TÌNH
// giống hệt warehouse-dashboard.js bên PMC (đồng hồ, 3 số lớn, feed cuộn), chỉ khác nguồn dữ liệu:
// dữ liệu thật từ MES.MTL_BAR_BARCODE@inf_m_e / MES.MTL_BAR_SCANNING@inf_m_e (qua
// WarehouseController.GenericDashboard), không phải bảng riêng của app như PMC_StockMovements —
// không có SignalR nên chỉ polling AJAX mỗi 60s (đủ dùng, không cần realtime tức thì).
//
// Song ngữ VI/EN qua window.PmcWhI18n (warehouse-i18n.js, nạp global ở _Layout.cshtml) — phần chữ
// TĨNH (nhãn, tiêu đề cột...) đã có sẵn data-i18n trong GenericMap.cshtml nên tự đổi, chỉ phần chữ
// JS TỰ DỰNG (feed, bảng kết quả, thông báo lỗi/trống) mới cần gọi I18N.t() + vẽ lại khi đổi ngôn ngữ.
(function () {
  'use strict';
  var I18N = window.PmcWhI18n || { t: function (k) { return k; }, onChange: function () {} };
  var WH_ID = window.PMC_WAREHOUSE_ID || '';
  var WH_NAME = window.PMC_WAREHOUSE_NAME || '';

  function setTxt(id, v) { var el = document.getElementById(id); if (el) el.textContent = v; }
  function esc(s) { return (s == null ? '' : String(s)).replace(/[&<>]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;' }[c]; }); }

  function tickClock() {
    var now = new Date();
    var hh = String(now.getHours()).padStart(2, '0');
    var mm = String(now.getMinutes()).padStart(2, '0');
    var ss = String(now.getSeconds()).padStart(2, '0');
    setTxt('whg-clock-time', hh + ':' + mm + ':' + ss);
    var dd = String(now.getDate()).padStart(2, '0');
    var mo = String(now.getMonth() + 1).padStart(2, '0');
    var days = I18N.t('days');
    setTxt('whg-clock-date', days[now.getDay()] + ', ' + dd + '/' + mo + '/' + now.getFullYear());
  }
  tickClock();
  setInterval(tickClock, 1000);

  function applyStaticTranslations() {
    document.title = WH_NAME ? (WH_NAME + ' — ' + I18N.t('pageTitle')) : I18N.t('pageTitle');
    var sub = document.getElementById('whg-banner-sub');
    if (sub) {
      sub.textContent = sub.getAttribute('data-racks') + ' ' + I18N.t('wgRacksSuffix') + ' · ' +
        sub.getAttribute('data-cells') + ' ' + I18N.t('wgCellsSuffix') + ' · ' +
        sub.getAttribute('data-filled') + ' ' + I18N.t('wgFilledSuffix');
    }
  }
  applyStaticTranslations();

  function relTime(gatherOrYmd) {
    // CREATE_DATE = 'YYYYMMDD' (8 ký tự), D_GATHER = 'YYYYMMDDHH24MISS' (14 ký tự) — cả 2 đều
    // chuỗi thô từ MES, không phải kiểu DATE thật, nên parse tay thay vì new Date(str).
    var s = (gatherOrYmd || '').toString();
    if (s.length < 8) return '—';
    var y = +s.substr(0, 4), mo = +s.substr(4, 2) - 1, d = +s.substr(6, 2);
    var hh = s.length >= 14 ? +s.substr(8, 2) : 0, mi = s.length >= 14 ? +s.substr(10, 2) : 0, se = s.length >= 14 ? +s.substr(12, 2) : 0;
    var dt = new Date(y, mo, d, hh, mi, se);
    var diffSec = Math.max(0, Math.floor((Date.now() - dt.getTime()) / 1000));
    if (s.length < 14) return d + '/' + (mo + 1); // chỉ có ngày (CREATE_DATE) — không tính "X phút trước" được
    if (diffSec < 60) return I18N.t('wgJustNow');
    var mins = Math.floor(diffSec / 60);
    if (mins < 60) return mins + I18N.t('wgMinAgo');
    var hrs = Math.floor(mins / 60);
    if (hrs < 24) return hrs + I18N.t('wgHourAgo');
    return Math.floor(hrs / 24) + I18N.t('wgDayAgo');
  }

  function renderInbound(items) {
    var el = document.getElementById('whg-feed-inbound');
    if (!el) return;
    if (!items || !items.length) { el.innerHTML = '<div class="whg-feed-empty">' + I18N.t('wgFeedEmpty') + '</div>'; return; }
    el.innerHTML = items.map(function (it) {
      var sub = [it.style, it.area ? (I18N.t('wgColLocation') + ' ' + it.area) : null].filter(Boolean).join(' · ');
      return '' +
        '<div class="whg-feed-item">' +
          '<div class="fi-main">' +
            '<div class="fi-code">' + esc(it.barcode) + '</div>' +
            '<div class="fi-sub">' + esc(sub || '—') + '</div>' +
          '</div>' +
          '<div class="fi-right">' +
            '<div class="fi-qty">' + esc(it.poNum || '') + '</div>' +
            '<div class="fi-time">' + relTime(it.createDate) + '</div>' +
          '</div>' +
        '</div>';
    }).join('');
  }

  function renderOutbound(items) {
    var el = document.getElementById('whg-feed-outbound');
    if (!el) return;
    if (!items || !items.length) { el.innerHTML = '<div class="whg-feed-empty">' + I18N.t('wgFeedEmpty') + '</div>'; return; }
    el.innerHTML = items.map(function (it) {
      var sub = [it.itemCode, it.vendorName].filter(Boolean).join(' · ');
      return '' +
        '<div class="whg-feed-item" title="' + esc(it.description || '') + '">' +
          '<div class="fi-main">' +
            '<div class="fi-code">' + esc(it.barcode) + '</div>' +
            '<div class="fi-sub">' + esc(sub || '—') + '</div>' +
          '</div>' +
          '<div class="fi-right">' +
            '<div class="fi-qty">×' + it.txnQty + '</div>' +
            '<div class="fi-time">' + relTime(it.gather) + '</div>' +
          '</div>' +
        '</div>';
    }).join('');
  }

  var updatedAt = null;
  function tickUpdated() {
    if (!updatedAt) return;
    setTxt('whg-updated', I18N.t('wgUpdatedPrefix') + relTimeAgoSimple(updatedAt));
  }
  function relTimeAgoSimple(iso) {
    var diffSec = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 1000));
    if (diffSec < 60) return I18N.t('wgJustNow');
    var mins = Math.floor(diffSec / 60);
    return mins + I18N.t('wgMinAgo');
  }

  var lastDashboardData = null;
  function renderDashboard(d) {
    if (!d || d.available === false) {
      setTxt('whg-big-instock', '—'); setTxt('whg-big-out-today', '—'); setTxt('whg-big-in-today', '—');
      var errHtml = '<div class="whg-feed-err">' + I18N.t('wgFeedErr') + '</div>';
      var inEl = document.getElementById('whg-feed-inbound'); if (inEl) inEl.innerHTML = errHtml;
      var outEl = document.getElementById('whg-feed-outbound'); if (outEl) outEl.innerHTML = errHtml;
      return;
    }
    setTxt('whg-big-instock', d.totalInStock);
    setTxt('whg-big-out-today', d.outboundToday);
    setTxt('whg-big-in-today', d.inboundToday);
    renderInbound(d.recentInbound);
    renderOutbound(d.recentOutbound);
  }

  function loadDashboard() {
    fetch('Warehouse/' + WH_ID + '/GenericDashboard')
      .then(function (r) { return r.json(); })
      .then(function (d) {
        lastDashboardData = d;
        renderDashboard(d);
        updatedAt = new Date().toISOString();
        tickUpdated();
      })
      .catch(function () {});
  }

  // Lùi 1 tick — script này chạy ngay sau warehouse-map-generic.js, lúc đó scene 3D (35 kệ, hàng
  // trăm mesh) đang dựng xong nhưng vòng lặp requestAnimationFrame vừa khởi động có thể chiếm hết
  // main thread 1 nhịp, làm fetch() bị trễ đến mức tưởng như "treo".
  setTimeout(loadDashboard, 50);
  setInterval(loadDashboard, 60 * 1000);
  setInterval(tickUpdated, 1000);

  // ---------- Kiếm liệu (tra theo mã hàng SEGMENT1) — 2 bảng: tổng hợp theo vị trí + tách theo
  // lịch cắt CUT_ORIGINAL. Gõ mã hàng vô ô, bấm Kiếm (hoặc Enter) là ra, hiện trong modal cạnh nhau.
  var lookupInput = document.getElementById('whg-lookup-input');
  var lookupBtn = document.getElementById('whg-lookup-btn');
  var lookupMsg = document.getElementById('whg-lookup-msg');
  var lookupModal = document.getElementById('whg-lookup-modal');
  var lookupClose = document.getElementById('whg-lookup-close');
  var lastLookupCode = null;

  function fillEmpty(bodyId, emptyId, rows) {
    document.getElementById(emptyId).innerHTML = (!rows || !rows.length)
      ? '<div class="whg-modal-empty">' + I18N.t('wgLookupEmpty') + '</div>' : '';
  }
  function renderSummaryRows(rows) {
    document.getElementById('whg-lookup-summary-body').innerHTML = (rows || []).map(function (r) {
      return '<tr><td class="mono">' + esc(r.area) + '</td><td>' + esc(r.vendorName) + '</td><td>' + esc(r.status) + '</td><td class="num">' + r.totalQty + '</td></tr>';
    }).join('');
    fillEmpty('whg-lookup-summary-body', 'whg-lookup-summary-empty', rows);
  }
  function renderCutRows(rows) {
    document.getElementById('whg-lookup-cuts-body').innerHTML = (rows || []).map(function (r) {
      return '<tr><td class="mono">' + esc(r.cutOriginal) + '</td><td class="mono">' + esc(r.area) + '</td><td>' + esc(r.vendorName) + '</td><td class="num">' + r.totalQty + '</td></tr>';
    }).join('');
    fillEmpty('whg-lookup-cuts-body', 'whg-lookup-cuts-empty', rows);
  }

  function openLookupModal() { lookupModal.hidden = false; }
  function closeLookupModal() { lookupModal.hidden = true; }
  lookupClose.addEventListener('click', closeLookupModal);
  lookupModal.addEventListener('click', function (e) { if (e.target === lookupModal) closeLookupModal(); });

  function runLookup() {
    var code = (lookupInput.value || '').trim();
    if (!code) { lookupMsg.textContent = I18N.t('wgLookupEnterCode'); return; }
    lastLookupCode = code;
    lookupMsg.textContent = '';
    setTxt('whg-lookup-title', code);
    var loadingHtml = '<div class="whg-modal-empty">' + I18N.t('wgLoading') + '</div>';
    document.getElementById('whg-lookup-summary-body').innerHTML = '';
    document.getElementById('whg-lookup-cuts-body').innerHTML = '';
    document.getElementById('whg-lookup-summary-empty').innerHTML = loadingHtml;
    document.getElementById('whg-lookup-cuts-empty').innerHTML = loadingHtml;
    openLookupModal();
    fetch('Warehouse/' + WH_ID + '/GenericMaterialLookup?code=' + encodeURIComponent(code))
      .then(function (r) { return r.json(); })
      .then(function (d) {
        if (!d || d.available === false) {
          var err = '<div class="whg-modal-empty">' + I18N.t('wgLookupErr') + '</div>';
          document.getElementById('whg-lookup-summary-empty').innerHTML = err;
          document.getElementById('whg-lookup-cuts-empty').innerHTML = err;
          return;
        }
        renderSummaryRows(d.summary);
        renderCutRows(d.cuts);
      })
      .catch(function () {
        var err = '<div class="whg-modal-empty">' + I18N.t('wgLookupLoadErr') + '</div>';
        document.getElementById('whg-lookup-summary-empty').innerHTML = err;
        document.getElementById('whg-lookup-cuts-empty').innerHTML = err;
      });
  }
  lookupBtn.addEventListener('click', runLookup);
  lookupInput.addEventListener('keydown', function (e) { if (e.key === 'Enter') runLookup(); });

  // ---------- Chi tiết 1 VỊ TRÍ (tìm mã vị trí trên bản đồ 3D) — hiện ALL barcode/PO/style/SL thật
  // đang nằm đúng ô đó. Trượt ra từ bên phải NGAY TRONG khung 3D (y hệt drawer bên PMC) thay vì che
  // hết màn hình như modal — camera vẫn xoay/zoom bình thường phía sau. Được warehouse-map-generic.js
  // gọi qua window.WHG_openLocationDetail(code) ngay sau khi zoom camera vào đúng ô (đúng chỗ tag
  // trắng mã vị trí hiện trên dầm).
  var locDrawer = document.getElementById('whg-loc-drawer');
  var locClose = document.getElementById('whg-loc-close');
  var searchBoxes = document.querySelectorAll('.whg-search');
  function closeLocDrawer() {
    locDrawer.classList.remove('open');
    searchBoxes.forEach(function (el) { el.classList.remove('whg-hide'); });
  }
  locClose.addEventListener('click', closeLocDrawer);
  window.WHG_closeLocationDrawer = closeLocDrawer; // resetView (map-generic.js) đóng luôn drawer khi bấm Reset

  var lastLocCode = null;
  function renderLocRows(rows) {
    document.getElementById('whg-loc-body').innerHTML = (rows || []).map(function (r) {
      return '<tr><td class="mono">' + esc(r.barcode) + '</td><td class="mono">' + esc(r.poNum) + '</td><td>' + esc(r.style) + '</td><td class="num">' + r.qty + '</td></tr>';
    }).join('');
    document.getElementById('whg-loc-empty').innerHTML = (!rows || !rows.length)
      ? '<div class="whg-drawer-empty">' + I18N.t('wgDetailEmpty') + '</div>' : '';
  }
  window.WHG_openLocationDetail = function (code) {
    lastLocCode = code;
    setTxt('whg-loc-title', code);
    document.getElementById('whg-loc-body').innerHTML = '';
    document.getElementById('whg-loc-empty').innerHTML = '<div class="whg-drawer-empty">' + I18N.t('wgLoading') + '</div>';
    locDrawer.classList.add('open');
    searchBoxes.forEach(function (el) { el.classList.add('whg-hide'); });
    fetch('Warehouse/' + WH_ID + '/GenericLocationDetail?code=' + encodeURIComponent(code))
      .then(function (r) { return r.json(); })
      .then(function (d) {
        if (!d || d.available === false) {
          document.getElementById('whg-loc-empty').innerHTML = '<div class="whg-drawer-empty">' + I18N.t('wgDetailErr') + '</div>';
          return;
        }
        renderLocRows(d.items);
      })
      .catch(function () {
        document.getElementById('whg-loc-empty').innerHTML = '<div class="whg-drawer-empty">' + I18N.t('wgDetailErr') + '</div>';
      });
  };

  // ---------- Đổi ngôn ngữ: vẽ lại mọi phần chữ JS tự dựng (data-i18n lo phần HTML tĩnh rồi) ----------
  I18N.onChange(function () {
    applyStaticTranslations();
    tickClock();
    renderDashboard(lastDashboardData);
    if (updatedAt) tickUpdated();
    if (!lookupModal.hidden && lastLookupCode) runLookup();
    if (locDrawer.classList.contains('open') && lastLocCode) window.WHG_openLocationDetail(lastLocCode);
  });
})();
