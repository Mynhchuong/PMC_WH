/*
 * PmcMaterialDetail — helper dùng chung cho mọi trang cần xem "Chi tiết liệu".
 * Cách dùng: thêm class "js-detail-row" + data-material-id + data-barcode vào <tr>
 * (hoặc bất kỳ phần tử nào), double-click vào sẽ tự mở popup chi tiết — không cần
 * viết JS riêng ở từng trang. Dùng event delegation trên document nên vẫn hoạt
 * động với nội dung render động (vd. feed realtime ở Bản đồ kho).
 */
(function () {
    "use strict";

    async function show(materialId, barcode) {
        if (!materialId) return;
        try {
            var html = await PmcUI.withLoading(() =>
                fetch('/Materials/' + materialId + '/Detail').then(function (r) {
                    if (!r.ok) throw new Error('Không tải được chi tiết.');
                    return r.text();
                })
            );
            PmcUI.popup('Chi tiết liệu — ' + (barcode || ''), html, { size: 'xl' });
        } catch (e) {
            PmcUI.error('Không tải được chi tiết liệu.');
        }
    }

    document.addEventListener('dblclick', function (e) {
        var row = e.target.closest('.js-detail-row');
        if (!row) return;
        show(row.dataset.materialId, row.dataset.barcode);
    });

    window.PmcMaterialDetail = { show: show };
})();
