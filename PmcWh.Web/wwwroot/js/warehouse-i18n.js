// Từ điển song ngữ (VI/EN) DÙNG CHUNG CHO TOÀN BỘ WEB PMC WH — nạp global trong _Layout.cshtml.
// 2 cách dùng:
//  1. HTML tĩnh (header, sidebar, login...): thêm data-i18n="key" vào phần tử — tự động đổi chữ,
//     không cần viết JS riêng. (data-i18n-placeholder / data-i18n-title cho input/title attribute)
//  2. JS dựng DOM động (vd. warehouse-map.js 3D scene): gọi PmcWhI18n.t(key) trực tiếp, và đăng ký
//     PmcWhI18n.onChange(...) để tự vẽ lại khi đổi ngôn ngữ.
window.PmcWhI18n = (function () {
  'use strict';

  var DICT = {
    vi: {
      // ----- Header / Sidebar / Login (dùng chung toàn site) -----
      subtitlePortal: 'Warehouse Portal',
      logout: 'Đăng xuất',
      menuMaterialsGroup: 'Liệu',
      menuMaterialsDb: 'Cơ sở dữ liệu của PMC',
      menuInbound: 'Quét lên kệ',
      menuIssue: 'Xuất hàng',
      menuDispose: 'Hủy liệu',
      menuOverdue: 'Hàng xuất quá 90 ngày',
      menuWarehouseMap: 'Bản đồ kho 3D',
      menuReportsGroup: 'Báo cáo',
      menuReportsIssues: 'Báo cáo xuất kho',
      menuAdminGroup: 'Quản trị',
      menuUsers: 'Người dùng',
      menuRecipients: 'Nơi nhận',
      loginTitle: 'Đăng nhập',
      loginSubtitle: 'Đăng nhập để tiếp tục vào PMC WH',
      loginUsername: 'Tên đăng nhập',
      loginPassword: 'Mật khẩu',
      loginRememberMe: 'Ghi nhớ đăng nhập',
      loginSubmit: 'Đăng nhập',

      // ----- Chung (dùng ở nhiều trang) -----
      btnCancel: 'Hủy',
      btnSave: 'Lưu',
      btnConfirm: 'Xác nhận',
      colStatus: 'Trạng thái',
      colCurrentShelf: 'Kệ hiện tại',
      colAction: 'Hành động',
      statusStaging: 'Đang chờ lên kệ',
      statusInStock: 'Trong kho',
      statusPartiallyIssued: 'Đã xuất 1 phần',
      statusIssuedOut: 'Đã xuất hết',
      statusDisposed: 'Đã hủy',
      overdue90Days: 'Quá 90 ngày',
      filterStatusAll: 'Tất cả',
      pageSizeLabel: 'Hiển thị',
      pageSizeSuffix: '/ trang',
      ofTotalRows: 'trên tổng',
      rowsWord: 'dòng',

      // ----- Materials/Index -----
      matDbTitle: 'Cơ sở dữ liệu của PMC',
      matDbSubtitle: 'Toàn bộ liệu đã nhập vào hệ thống',
      btnExportExcel: 'Xuất Excel',
      btnImport: 'Import',
      btnDownloadTemplate: 'Tải file mẫu',
      filterBarcodeLabel: 'Barcode',
      filterBarcodePlaceholder: 'Tìm theo barcode...',
      btnSearchTooltip: 'Tìm kiếm',
      filterStatusLabel: 'Trạng thái',
      filterFromDate: 'Ngày nhập từ',
      filterToDate: 'Đến ngày',
      btnFilter: 'Lọc',
      colMatlDescription: "Mat'l Description",
      colColorCode: 'Color Code',
      editTooltip: 'Sửa thông tin',
      emptyMaterials: 'Không có dữ liệu phù hợp.',
      importModalTitle: 'Import liệu từ Excel',
      chooseExcelFile: 'Chọn file Excel (.xlsx)',
      editModalTitle: 'Sửa thông tin liệu',
      editArrivalQty: "Số lượng (Q'ty)",
      editMatlDescription: 'Mô tả liệu',
      editUnit: 'Đơn vị',
      editArrivalDate: 'Ngày nhập (ATA)',
      yesOption: 'Có',
      noOption: 'Không',
      importSkippedTitle: 'Dòng bị bỏ qua ở lần import gần nhất',
      colReason: 'Lý do',
      loadMaterialInfoError: 'Không tải được thông tin liệu.',
      loadDetailError: 'Không tải được chi tiết liệu.',
      savedChangesSuccess: 'Đã lưu thay đổi.',
      saveChangesFailFallback: 'Không thể lưu thay đổi.',
      noExcelFileSelected: 'Chưa chọn file Excel.',
      importedRowsBase: 'Đã import {0}/{1} dòng vào Staging',
      importedAutoShelvedSuffix: ', tự lên kệ {0} dòng theo cột Rack No.',
      importedSkippedSuffix: ', bỏ qua {0} dòng.',
      periodOnly: '.',
      importNoRowsWarning: 'Không có dòng nào được import — xem chi tiết bên dưới.',
      importEmptyFileWarning: 'File không có dòng dữ liệu nào để import.',

      // ----- Inbound/Index -----
      inboundSubtitle: 'Lên kệ liệu mới từ khu vực chờ, hoặc nhận lại hàng đã xuất về kệ',
      scanBarcodeLabel: 'Quét barcode',
      scanBarcodePlaceholder: 'Quét hoặc gõ barcode rồi Enter...',
      needShelving: 'Cần lên kệ',
      totalLabel: 'Tổng',
      newlyAddedLabel: 'Mới nhập',
      returnableLabel: 'Nhận lại',
      colType: 'Loại',
      colQty: 'Số lượng',
      btnShelve: 'Lên kệ',
      emptyNeedShelving: 'Không có liệu nào cần lên kệ.',
      outstandingLabel: 'Đã xuất còn thiếu:',
      returnQtyLabel: 'Số lượng nhận lại',
      keepShelfPrefix: 'Liệu chưa từng rời kệ — giữ nguyên vị trí',
      rackLabel: 'Kệ',
      levelLabel: 'Tầng',
      rackPrefix: 'Kệ ',
      levelPrefix: 'Tầng ',
      notFoundInList: 'Không tìm thấy barcode "{code}" trong danh sách cần lên kệ.',
      shelvedSuccess: "Đã lên kệ barcode '{0}'.",
      shelvedFailFallback: "Không thể lên kệ barcode '{0}'.",
      returnedSuccess: "Đã nhận lại {0} cho barcode '{1}'.",
      returnedFailFallback: "Không thể nhận lại barcode '{0}'.",

      // ----- Materials/_MaterialDetail (popup chi tiết) -----
      detailTitlePrefix: 'Chi tiết liệu — ',
      detailArrivalQty: 'Số lượng nhập',
      detailCurrentStock: 'Tồn hiện tại',
      detailArrivalDate: 'Ngày nhập',
      detailShelvedAt: 'Lên kệ lúc',
      detailLastIssued: 'Xuất gần nhất',
      detailDisposedAt: 'Hủy lúc',
      detailCreatedAt: 'Ngày tạo',
      detailUpdatedAt: 'Cập nhật gần nhất',
      movementHistory: 'Lịch sử di chuyển',
      currentStockLabel: 'Tồn kho hiện tại:',
      colShelf: 'Kệ',
      colPerformedBy: 'Người thực hiện',
      colIssuedTo: 'Giao cho',
      colTime: 'Thời gian',
      noMovementHistory: 'Chưa có lịch sử di chuyển.',
      movementInbound: 'Lên kệ',
      movementIssueToWorkshop: 'Xuất',
      movementReturn: 'Nhận lại',
      movementDispose: 'Hủy',

      // ----- Issue/Index -----
      issueSubtitle: 'Xuất liệu cho DEV/Workshop, trừ tồn kho',
      issuableTitle: 'Có thể xuất',
      colBalance: 'Tồn',
      btnIssueAction: 'Xuất',
      emptyIssuable: 'Không có liệu nào có thể xuất.',
      issueModalTitlePrefix: 'Xuất — ',
      currentBalanceLabel: 'Tồn hiện tại:',
      recipientLabel: 'Nơi nhận',
      issueQtyLabel: 'Số lượng xuất',
      notFoundIssuable: 'Không tìm thấy barcode "{code}" trong danh sách có thể xuất.',
      issuedSuccess: "Đã xuất {0} cho barcode '{1}'.",
      issueFailFallback: "Không thể xuất barcode '{0}'.",

      // ----- Dispose/Index -----
      disposeSubtitle: 'Liệu hủy sẽ giữ lại 30 ngày rồi tự ẩn khỏi hệ thống',
      disposableTitle: 'Có thể hủy',
      btnDispose: 'Hủy',
      emptyDisposable: 'Không có liệu nào để hủy.',
      confirmDisposeMsg: 'Hủy barcode "{barcode}"? Liệu sẽ giữ 30 ngày rồi tự ẩn.',
      notFoundDisposable: 'Không tìm thấy barcode "{code}" trong danh sách có thể hủy.',
      disposedSuccess: "Đã hủy barcode '{0}'.",
      disposeFailFallback: "Không thể hủy barcode '{0}'.",

      // ----- Overdue/Index -----
      overduePageTitle: 'Hàng xuất quá 90 ngày chưa nhận lại',
      overdueSubtitle: "Cả xuất 1 phần lẫn xuất hết — kiểm tra thực tế bên nhận: còn liệu thì để đó, hết liệu thì bấm Xóa.",
      listTitle: 'Danh sách',
      colRemaining: 'Còn lại',
      colDaysOut: 'Số ngày',
      daysWord: ' ngày',
      btnDelete: 'Xóa',
      emptyOverdue: 'Không có liệu nào quá 90 ngày.',
      confirmOverdueDeleteMsg: 'Xác nhận "{barcode}" đã hết liệu, không còn chờ nhận lại? Sẽ hủy hẳn liệu này.',
      overdueDisposedSuccess: "Đã hủy barcode '{0}' — hết liệu, không còn chờ nhận lại.",

      // ----- Reports/Issues -----
      reportsSubtitle: 'Ai đã nhận, xuất lúc nào, mã nào — kèm cảnh báo quá 90 ngày',
      fromDateLabel: 'Từ ngày',
      issuedForLabel: 'Xuất cho',
      overdueOnlyLabel: 'Chỉ quá 90 ngày',
      colQtyIssued: 'SL xuất',
      colIssuedBy: 'Người xuất',
      colIssuedTime: 'Thời gian xuất',
      colDaysIssued: 'Số ngày đã xuất',

      // ----- Users/Index -----
      addUserTitle: 'Thêm người dùng',
      addUserSubtitle: 'Tạo tài khoản đăng nhập cho nhân viên kho',
      fullNameLabel: 'Họ tên',
      roleLabel: 'Vai trò',
      btnCreate: 'Tạo',
      userListTitle: 'Danh sách người dùng',
      colUser: 'Người dùng',
      btnResetPassword: 'Reset mật khẩu',
      confirmResetPasswordMsg: 'Đặt lại mật khẩu của "{username}" về 123456?',

      // ----- Recipients/Index -----
      addRecipientTitle: 'Thêm nơi nhận',
      addRecipientSubtitle: 'Danh sách nơi nhận hiện lên combobox lúc xuất hàng (DEV / Workshop / ...)',
      btnAdd: 'Thêm',
      recipientListTitle: 'Danh sách nơi nhận',
      btnDisable: 'Tắt',
      btnEnable: 'Bật lại',
      emptyRecipients: 'Chưa có nơi nhận nào.',
      addedRecipientSuccess: "Đã thêm nơi nhận '{0}'.",
      toggledRecipientSuccess: "Đã cập nhật trạng thái '{0}'.",
      toggleRecipientFailFallback: "Không thể cập nhật '{0}'.",
      createdUserSuccess: "Đã tạo người dùng '{0}'.",
      resetPasswordSuccess: "Đã đặt lại mật khẩu của '{0}' về '123456'.",
      resetPasswordFailFallback: "Không thể đặt lại mật khẩu của '{0}'.",

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
      // ----- Header / Sidebar / Login (site-wide) -----
      subtitlePortal: 'Warehouse Portal',
      logout: 'Log out',
      menuMaterialsGroup: 'Materials',
      menuMaterialsDb: 'PMC Materials Database',
      menuInbound: 'Scan to Shelf',
      menuIssue: 'Issue',
      menuDispose: 'Dispose',
      menuOverdue: 'Issued Over 90 Days',
      menuWarehouseMap: '3D Warehouse Map',
      menuReportsGroup: 'Reports',
      menuReportsIssues: 'Issue Report',
      menuAdminGroup: 'Admin',
      menuUsers: 'Users',
      menuRecipients: 'Recipients',
      loginTitle: 'Login',
      loginSubtitle: 'Sign in to continue to PMC WH',
      loginUsername: 'Username',
      loginPassword: 'Password',
      loginRememberMe: 'Remember me',
      loginSubmit: 'Login',

      // ----- Shared (used across pages) -----
      btnCancel: 'Cancel',
      btnSave: 'Save',
      btnConfirm: 'Confirm',
      colStatus: 'Status',
      colCurrentShelf: 'Current Shelf',
      colAction: 'Action',
      statusStaging: 'Awaiting Shelving',
      statusInStock: 'In Stock',
      statusPartiallyIssued: 'Partially Issued',
      statusIssuedOut: 'Fully Issued',
      statusDisposed: 'Disposed',
      overdue90Days: 'Over 90 Days',
      filterStatusAll: 'All',
      pageSizeLabel: 'Show',
      pageSizeSuffix: '/ page',
      ofTotalRows: 'of',
      rowsWord: 'rows',

      // ----- Materials/Index -----
      matDbTitle: 'PMC Materials Database',
      matDbSubtitle: 'All materials entered into the system',
      btnExportExcel: 'Export Excel',
      btnImport: 'Import',
      btnDownloadTemplate: 'Download Template',
      filterBarcodeLabel: 'Barcode',
      filterBarcodePlaceholder: 'Search by barcode...',
      btnSearchTooltip: 'Search',
      filterStatusLabel: 'Status',
      filterFromDate: 'Arrival Date From',
      filterToDate: 'To Date',
      btnFilter: 'Filter',
      colMatlDescription: "Mat'l Description",
      colColorCode: 'Color Code',
      editTooltip: 'Edit info',
      emptyMaterials: 'No matching data.',
      importModalTitle: 'Import Materials from Excel',
      chooseExcelFile: 'Choose Excel File (.xlsx)',
      editModalTitle: 'Edit Material Info',
      editArrivalQty: "Quantity (Q'ty)",
      editMatlDescription: 'Material Description',
      editUnit: 'Unit',
      editArrivalDate: 'Arrival Date (ATA)',
      yesOption: 'Yes',
      noOption: 'No',
      importSkippedTitle: 'Rows Skipped in Last Import',
      colReason: 'Reason',
      loadMaterialInfoError: 'Failed to load material info.',
      loadDetailError: 'Failed to load material detail.',
      savedChangesSuccess: 'Changes saved.',
      saveChangesFailFallback: 'Could not save changes.',
      noExcelFileSelected: 'No Excel file selected.',
      importedRowsBase: 'Imported {0}/{1} rows into Staging',
      importedAutoShelvedSuffix: ', auto-shelved {0} rows via Rack No. column.',
      importedSkippedSuffix: ', skipped {0} rows.',
      periodOnly: '.',
      importNoRowsWarning: 'No rows were imported — see details below.',
      importEmptyFileWarning: 'The file has no data rows to import.',

      // ----- Inbound/Index -----
      inboundSubtitle: 'Shelve new materials from staging, or return issued stock back to a shelf',
      scanBarcodeLabel: 'Scan barcode',
      scanBarcodePlaceholder: 'Scan or type barcode then press Enter...',
      needShelving: 'Needs Shelving',
      totalLabel: 'Total',
      newlyAddedLabel: 'New',
      returnableLabel: 'Return',
      colType: 'Type',
      colQty: 'Quantity',
      btnShelve: 'Shelve',
      emptyNeedShelving: 'No materials need shelving.',
      outstandingLabel: 'Outstanding issued:',
      returnQtyLabel: 'Return quantity',
      keepShelfPrefix: 'Material never left the shelf — keeping location',
      rackLabel: 'Rack',
      levelLabel: 'Level',
      rackPrefix: 'Rack ',
      levelPrefix: 'Level ',
      notFoundInList: 'Barcode "{code}" not found in the shelving list.',
      shelvedSuccess: "Shelved barcode '{0}'.",
      shelvedFailFallback: "Could not shelve barcode '{0}'.",
      returnedSuccess: "Returned {0} for barcode '{1}'.",
      returnedFailFallback: "Could not return barcode '{0}'.",

      // ----- Materials/_MaterialDetail (detail popup) -----
      detailTitlePrefix: 'Material Detail — ',
      detailArrivalQty: 'Arrival Quantity',
      detailCurrentStock: 'Current Stock',
      detailArrivalDate: 'Arrival Date',
      detailShelvedAt: 'Shelved At',
      detailLastIssued: 'Last Issued',
      detailDisposedAt: 'Disposed At',
      detailCreatedAt: 'Created At',
      detailUpdatedAt: 'Last Updated',
      movementHistory: 'Movement History',
      currentStockLabel: 'Current Stock:',
      colShelf: 'Shelf',
      colPerformedBy: 'Performed By',
      colIssuedTo: 'Issued To',
      colTime: 'Time',
      noMovementHistory: 'No movement history yet.',
      movementInbound: 'Shelved',
      movementIssueToWorkshop: 'Issued',
      movementReturn: 'Returned',
      movementDispose: 'Disposed',

      // ----- Issue/Index -----
      issueSubtitle: 'Issue materials to DEV/Workshop, deducting stock',
      issuableTitle: 'Available to Issue',
      colBalance: 'Balance',
      btnIssueAction: 'Issue',
      emptyIssuable: 'No materials available to issue.',
      issueModalTitlePrefix: 'Issue — ',
      currentBalanceLabel: 'Current balance:',
      recipientLabel: 'Recipient',
      issueQtyLabel: 'Issue quantity',
      notFoundIssuable: 'Barcode "{code}" not found in the issuable list.',
      issuedSuccess: "Issued {0} for barcode '{1}'.",
      issueFailFallback: "Could not issue barcode '{0}'.",

      // ----- Dispose/Index -----
      disposeSubtitle: 'Disposed materials are kept for 30 days then auto-hidden from the system',
      disposableTitle: 'Available to Dispose',
      btnDispose: 'Dispose',
      emptyDisposable: 'No materials to dispose.',
      confirmDisposeMsg: 'Dispose barcode "{barcode}"? It will be kept for 30 days then auto-hidden.',
      notFoundDisposable: 'Barcode "{code}" not found in the disposable list.',
      disposedSuccess: "Disposed barcode '{0}'.",
      disposeFailFallback: "Could not dispose barcode '{0}'.",

      // ----- Overdue/Index -----
      overduePageTitle: 'Issued Items Over 90 Days, Not Yet Returned',
      overdueSubtitle: 'Includes both partial and full issues — check with the recipient: if material remains, leave it; if fully used, click Delete.',
      listTitle: 'List',
      colRemaining: 'Remaining',
      colDaysOut: 'Days Out',
      daysWord: ' days',
      btnDelete: 'Delete',
      emptyOverdue: 'No materials over 90 days.',
      confirmOverdueDeleteMsg: 'Confirm "{barcode}" has been fully used and is no longer awaiting return? This will permanently dispose it.',
      overdueDisposedSuccess: "Disposed barcode '{0}' — fully used, no longer awaiting return.",

      // ----- Reports/Issues -----
      reportsSubtitle: 'Who received it, when it was issued, which barcode — includes 90-day overdue warning',
      fromDateLabel: 'From Date',
      issuedForLabel: 'Issued To',
      overdueOnlyLabel: 'Over 90 Days Only',
      colQtyIssued: 'Issued Qty',
      colIssuedBy: 'Issued By',
      colIssuedTime: 'Issued Time',
      colDaysIssued: 'Days Since Issued',

      // ----- Users/Index -----
      addUserTitle: 'Add User',
      addUserSubtitle: 'Create a login account for warehouse staff',
      fullNameLabel: 'Full Name',
      roleLabel: 'Role',
      btnCreate: 'Create',
      userListTitle: 'User List',
      colUser: 'User',
      btnResetPassword: 'Reset Password',
      confirmResetPasswordMsg: 'Reset password for "{username}" to 123456?',

      // ----- Recipients/Index -----
      addRecipientTitle: 'Add Recipient',
      addRecipientSubtitle: 'This list appears in the dropdown when issuing materials (DEV / Workshop / ...)',
      btnAdd: 'Add',
      recipientListTitle: 'Recipient List',
      btnDisable: 'Disable',
      btnEnable: 'Enable',
      emptyRecipients: 'No recipients yet.',
      addedRecipientSuccess: "Added recipient '{0}'.",
      toggledRecipientSuccess: "Updated status for '{0}'.",
      toggleRecipientFailFallback: "Could not update '{0}'.",
      createdUserSuccess: "Created user '{0}'.",
      resetPasswordSuccess: "Password for '{0}' has been reset to '123456'.",
      resetPasswordFailFallback: "Could not reset password for '{0}'.",

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

  // Áp dụng data-i18n / data-i18n-placeholder / data-i18n-title lên 1 vùng DOM — dùng cho
  // HTML tĩnh (header, sidebar, login...) thay vì phải viết JS riêng ở từng trang.
  function applyDom(root) {
    root = root || document;
    root.querySelectorAll('[data-i18n]').forEach(function (el) {
      el.textContent = t(el.getAttribute('data-i18n'));
    });
    root.querySelectorAll('[data-i18n-placeholder]').forEach(function (el) {
      el.setAttribute('placeholder', t(el.getAttribute('data-i18n-placeholder')));
    });
    root.querySelectorAll('[data-i18n-title]').forEach(function (el) {
      el.setAttribute('title', t(el.getAttribute('data-i18n-title')));
    });
  }

  document.documentElement.lang = lang;
  onChange(function () { applyDom(document); });
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () { applyDom(document); });
  } else {
    applyDom(document);
  }

  // Render thông báo flash được server đóng gói dạng JSON {segments:[{key,args}]} (xem FlashHelper.cs
  // bên C#) — tự tra từ điển + thay {0}{1}... bằng args. Nếu raw không phải JSON hợp lệ (message cũ
  // hoặc do Api trả thẳng, không qua FlashHelper) thì trả nguyên văn — không vỡ trang nào cả.
  function renderFlash(raw) {
    if (!raw) return null;
    try {
      var data = JSON.parse(raw);
      if (!data || !data.segments) return raw;
      return data.segments.map(function (seg) {
        var template = t(seg.key);
        return template.replace(/\{(\d+)\}/g, function (_, i) {
          var v = seg.args && seg.args[i];
          return v === null || v === undefined ? '' : v;
        });
      }).join('');
    } catch (e) {
      return raw;
    }
  }

  return { t: t, getLang: getLang, setLang: setLang, onChange: onChange, applyDom: applyDom, renderFlash: renderFlash };
})();
