// Bảng giám sát TV cho Bản đồ kho 3D — đồng hồ sống, thống kê lớn, 5 hoạt động gần nhất
// (lên kệ / xuất kho). Cập nhật realtime qua SignalR (chung hub /warehouseHub với warehouse-map.js),
// và tự tải lại toàn trang mỗi 5 phút làm phương án dự phòng cho màn hình chạy liên tục nhiều giờ.
(function () {
  'use strict';

  var RELOAD_MS = 5 * 60 * 1000;
  var I18N = window.PmcWhI18n || { t: function (k) { return k; }, getLang: function () { return 'vi'; }, setLang: function () {}, onChange: function () {} };

  function setTxt(id, v) { var el = document.getElementById(id); if (el) el.textContent = v; }

  function tickClock() {
    var now = new Date();
    var hh = String(now.getHours()).padStart(2, '0');
    var mm = String(now.getMinutes()).padStart(2, '0');
    var ss = String(now.getSeconds()).padStart(2, '0');
    setTxt('wh3d-clock-time', hh + ':' + mm + ':' + ss);
    var days = I18N.t('days');
    var dd = String(now.getDate()).padStart(2, '0');
    var mo = String(now.getMonth() + 1).padStart(2, '0');
    setTxt('wh3d-clock-date', days[now.getDay()] + ', ' + dd + '/' + mo + '/' + now.getFullYear());
  }
  tickClock();
  setInterval(tickClock, 1000);

  // ----- Combobox chọn ngôn ngữ (VI/EN) -----
  var langSelect = document.getElementById('wh3d-lang-select');
  function applyStaticTranslations() {
    document.title = I18N.t('pageTitle');
    setTxt('wh3d-banner-title', I18N.t('bannerTitle'));
    setTxt('wh3d-tvbtn-label', I18N.t('tvMode'));
    setTxt('wh3d-big-instock-label', I18N.t('bigInStock'));
    setTxt('wh3d-big-issued-today-label', I18N.t('bigIssuedToday'));
    setTxt('wh3d-big-inbound-today-label', I18N.t('bigInboundToday'));
    setTxt('wh3d-feed-inbound-title', I18N.t('feedInboundTitle'));
    setTxt('wh3d-feed-issue-title', I18N.t('feedIssueTitle'));
  }
  if (langSelect) {
    langSelect.value = I18N.getLang();
    langSelect.addEventListener('change', function () { I18N.setLang(langSelect.value); });
  }
  applyStaticTranslations();
  I18N.onChange(function () {
    if (langSelect) langSelect.value = I18N.getLang();
    applyStaticTranslations();
    tickClock();
    loadDashboard(); // vẽ lại feed để đổi luôn chữ "Kệ"/"Rack" và thời gian tương đối
  });

  var fsBtn = document.getElementById('wh3d-btnFullscreen');
  if (fsBtn) {
    fsBtn.addEventListener('click', function () {
      if (!document.fullscreenElement) {
        document.documentElement.requestFullscreen().catch(function () {});
      } else {
        document.exitFullscreen().catch(function () {});
      }
    });
  }

  // Ép toàn bộ trang (banner + số liệu + bản đồ 3D + feed) gọn trong đúng 1 màn hình, không
  // cần kéo lên/xuống — đo chiều cao còn lại từ vị trí trang đến đáy viewport rồi gán cứng.
  // Dưới 1100px (laptop nhỏ / màn dọc) thì bỏ ép, cho phép cuộn tự nhiên (CSS @media lo phần layout).
  var pageEl = document.getElementById('wh3d-page');
  var lastPageHeight = null;
  function fitPageHeight() {
    if (!pageEl) return;
    var newHeight;
    if (window.innerWidth < 1100) {
      newHeight = '';
    } else {
      var top = pageEl.getBoundingClientRect().top;
      var h = window.innerHeight - top - 14;
      newHeight = Math.max(480, h) + 'px';
    }
    if (newHeight === lastPageHeight) return; // tránh vòng lặp resize->dispatch->resize vô hạn
    lastPageHeight = newHeight;
    pageEl.style.height = newHeight;
    window.dispatchEvent(new Event('resize')); // để warehouse-map.js resize lại canvas/camera đúng kích thước mới
  }
  fitPageHeight();
  window.addEventListener('resize', fitPageHeight);
  document.addEventListener('fullscreenchange', function () { setTimeout(fitPageHeight, 60); });

  function relTime(iso) {
    var diffSec = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 1000));
    if (diffSec < 60) return I18N.t('justNow');
    var mins = Math.floor(diffSec / 60);
    if (mins < 60) return mins + I18N.t('minutesAgo');
    var hrs = Math.floor(mins / 60);
    if (hrs < 24) return hrs + I18N.t('hoursAgo');
    var days = Math.floor(hrs / 24);
    return days + I18N.t('daysAgo');
  }

  function renderFeed(containerId, items, locLabel) {
    var el = document.getElementById(containerId);
    if (!el) return;
    if (!items || items.length === 0) {
      el.innerHTML = '<div class="wh3d-feed-empty">' + I18N.t('feedEmpty') + '</div>';
      return;
    }
    el.innerHTML = items.map(function (it) {
      var devModel = [it.dev, it.model].filter(Boolean).join(' / ');
      var where = locLabel === 'loc' ? (it.locationCode ? I18N.t('whereLoc') + it.locationCode : '—') : (it.recipientName || '—');
      return '' +
        '<div class="wh3d-feed-item js-detail-row" data-material-id="' + it.materialId + '" data-barcode="' + it.barcode + '" title="Double-click để xem chi tiết">' +
          '<div class="fi-main">' +
            '<div class="fi-code">' + it.barcode + '</div>' +
            '<div class="fi-sub">' + (devModel || '—') + ' · ' + where + '</div>' +
          '</div>' +
          '<div class="fi-right">' +
            '<div class="fi-qty">' + it.qty + (it.unit ? ' ' + it.unit : '') + '</div>' +
            '<div class="fi-time">' + relTime(it.occurredAt) + '</div>' +
          '</div>' +
        '</div>';
    }).join('');
  }

  var summaryUpdatedAt = null;
  function tickSummaryUpdated() {
    if (!summaryUpdatedAt) return;
    setTxt('wh3d-summary-updated', I18N.t('updatedPrefix') + relTime(summaryUpdatedAt));
  }

  function loadDashboard() {
    fetch('/Warehouse/Dashboard').then(function (r) { return r.json(); }).then(function (d) {
      if (!d) return;
      var s = d.summary || {};
      setTxt('wh3d-big-instock', s.totalInStock);
      setTxt('wh3d-big-issued-today', s.issuedToday);
      setTxt('wh3d-big-inbound-today', s.inboundToday);
      renderFeed('wh3d-feed-inbound', d.recentInbound, 'loc');
      renderFeed('wh3d-feed-issue', d.recentIssue, 'recipient');
      summaryUpdatedAt = new Date().toISOString();
      tickSummaryUpdated();
    }).catch(function () {});
  }

  loadDashboard();
  // Cập nhật lại thời gian tương đối ("X phút trước") mỗi phút dù không có sự kiện mới.
  setInterval(loadDashboard, 60 * 1000);
  // Tick riêng dòng "Cập nhật X trước" mỗi giây cho mượt, không cần đợi loadDashboard chạy lại.
  setInterval(tickSummaryUpdated, 1000);

  if (window.signalR) {
    try {
      // Dùng chung 1 kết nối /warehouseHub với warehouse-map.js (window.PmcWhHub) — xem ghi chú
      // cùng chỗ bên đó. warehouse-map.js chạy trước nên thường đã tạo sẵn, ở đây chỉ gắn thêm handler.
      var conn = window.PmcWhHub || new signalR.HubConnectionBuilder().withUrl('/warehouseHub').withAutomaticReconnect().build();
      window.PmcWhHub = conn;
      conn.on('warehouseChanged', loadDashboard);
      if (conn.state === signalR.HubConnectionState.Disconnected) {
        conn.start().catch(function (e) { console.warn('SignalR (dashboard) connect fail', e); });
      }
    } catch (e) { console.warn('SignalR (dashboard) init fail', e); }
  }

  // Tự tải lại toàn trang mỗi 5 phút — dự phòng cho màn hình TV chạy liên tục nhiều giờ/ngày
  // (làm mới kết nối SignalR, giải phóng bộ nhớ WebGL, tránh trôi/dồn lỗi theo thời gian).
  setTimeout(function () { location.reload(); }, RELOAD_MS);
})();
