// Từ điển song ngữ (VI/EN) cho trang Bản đồ kho 3D / màn hình giám sát TV.
// Nạp TRƯỚC warehouse-map.js và warehouse-dashboard.js — cả 2 file đó gọi PmcWhI18n.t(key)
// thay vì hardcode chuỗi tiếng Việt, và đăng ký PmcWhI18n.onChange(...) để tự cập nhật khi đổi ngôn ngữ.
window.PmcWhI18n = (function () {
  'use strict';

  var DICT = {
    vi: {
      pageTitle: 'Bản đồ kho 3D',
      bannerTitle: 'KHO PMC — GIÁM SÁT TRỰC TIẾP',
      bannerSub: 'Dữ liệu thật, cập nhật realtime — chỉ hiển thị hàng đang có trong kho',
      tvMode: 'Chế độ TV',
      bigInStock: 'Tổng số hàng trong kho',
      bigIssuedToday: 'Đã xuất hôm nay',
      bigInboundToday: 'Lên kệ hôm nay',
      dragHint: 'Kéo <b>xoay</b> · Cuộn <b>zoom</b> · Rê chuột lên tầng để xem',
      searchPlaceholder: 'Tìm theo barcode…',
      searchBtn: 'Tìm',
      tierHint: 'Bấm 1 tầng để bay tới xem mã QR',
      resetBtn: '↺ Reset góc nhìn',
      spinBtnStart: '▶ Tự xoay',
      spinBtnStop: '■ Dừng xoay',
      loading3d: 'Đang dựng kho 3D…',
      feedInboundTitle: '5 mã vừa lên kệ',
      feedIssueTitle: '5 mã vừa xuất kho',
      feedEmpty: 'Chưa có hoạt động.',
      feedLoading: 'Đang tải…',
      justNow: 'Vừa xong',
      minutesAgo: ' phút trước',
      hoursAgo: ' giờ trước',
      daysAgo: ' ngày trước',
      rackWord: 'Kệ ',
      levelWord: ' · Tầng ',
      whereLoc: 'Kệ ',
      statusHasStock: 'Đang có hàng',
      statusEmpty: 'Trống',
      qrCountLabel: 'Số mã QR',
      emptyTier: 'Ô trống',
      noQrInTier: 'Tầng này hiện chưa có mã QR nào.',
      loadError: 'Lỗi tải dữ liệu.',
      qrInTierSuffix: ' mã QR đang trong tầng',
      pageWord: 'Trang ',
      colBarcode: 'Barcode', colDev: 'Dev', colModel: 'Model', colQty: 'SL',
      searching: 'Đang tìm…',
      notFound: 'Không tìm thấy.',
      cannotLocate: 'Không định vị được ô kệ.',
      foundAt: 'Tìm thấy tại Kệ ',
      searchError: 'Lỗi tìm kiếm.',
      days: ['Chủ nhật', 'Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7'],
      doorSignText: 'CỬA RA VÀO',
      exitSignText: 'THOÁT HIỂM →',
      deskScreenTitle: 'KHO PMC',
      deskScreenSub: 'GIÁM SÁT KHO',
      updatedPrefix: 'Cập nhật ',
    },
    en: {
      pageTitle: '3D Warehouse Map',
      bannerTitle: 'PMC WAREHOUSE — LIVE MONITORING',
      bannerSub: 'Live data, realtime updates — shows only stock currently on hand',
      tvMode: 'TV Mode',
      bigInStock: 'Total items in stock',
      bigIssuedToday: 'Issued today',
      bigInboundToday: 'Shelved today',
      dragHint: 'Drag to <b>rotate</b> · Scroll to <b>zoom</b> · Hover a shelf to preview',
      searchPlaceholder: 'Search by barcode…',
      searchBtn: 'Search',
      tierHint: 'Click a shelf to fly in and view its QR codes',
      resetBtn: '↺ Reset view',
      spinBtnStart: '▶ Auto-rotate',
      spinBtnStop: '■ Stop rotating',
      loading3d: 'Building 3D warehouse…',
      feedInboundTitle: '5 latest shelved',
      feedIssueTitle: '5 latest issued',
      feedEmpty: 'No activity yet.',
      feedLoading: 'Loading…',
      justNow: 'Just now',
      minutesAgo: ' min ago',
      hoursAgo: ' hr ago',
      daysAgo: ' d ago',
      rackWord: 'Rack ',
      levelWord: ' · Level ',
      whereLoc: 'Rack ',
      statusHasStock: 'In stock',
      statusEmpty: 'Empty',
      qrCountLabel: 'QR count',
      emptyTier: 'Empty slot',
      noQrInTier: 'This shelf currently has no QR codes.',
      loadError: 'Failed to load data.',
      qrInTierSuffix: ' QR codes on this shelf',
      pageWord: 'Page ',
      colBarcode: 'Barcode', colDev: 'Dev', colModel: 'Model', colQty: 'Qty',
      searching: 'Searching…',
      notFound: 'Not found.',
      cannotLocate: 'Could not locate the shelf.',
      foundAt: 'Found at Rack ',
      searchError: 'Search error.',
      days: ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'],
      doorSignText: 'ENTRANCE',
      exitSignText: 'EXIT →',
      deskScreenTitle: 'PMC WAREHOUSE',
      deskScreenSub: 'MONITORING',
      updatedPrefix: 'Updated ',
    },
  };

  var STORAGE_KEY = 'pmc_wh_lang';
  var lang = localStorage.getItem(STORAGE_KEY) === 'en' ? 'en' : 'vi';
  var listeners = [];

  function t(key) { return (DICT[lang] && DICT[lang][key] !== undefined) ? DICT[lang][key] : (DICT.vi[key] !== undefined ? DICT.vi[key] : key); }
  function getLang() { return lang; }
  function onChange(fn) { listeners.push(fn); }
  function setLang(l) {
    if (l !== 'vi' && l !== 'en') return;
    if (l === lang) return;
    lang = l;
    localStorage.setItem(STORAGE_KEY, l);
    document.documentElement.lang = l;
    listeners.forEach(function (fn) { try { fn(l); } catch (e) { console.warn('i18n listener error', e); } });
  }

  return { t: t, getLang: getLang, setLang: setLang, onChange: onChange };
})();
