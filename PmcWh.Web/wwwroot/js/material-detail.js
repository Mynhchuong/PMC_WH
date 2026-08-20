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
                fetch('Materials/' + materialId + '/Detail').then(function (r) {
                    if (!r.ok) throw new Error('Không tải được chi tiết.');
                    return r.text();
                })
            );
            PmcUI.popup(PmcWhI18n.t('detailTitlePrefix') + (barcode || ''), html, { size: 'xl' });
            PmcWhI18n.applyDom(document.getElementById('pmc-popup-body'));
        } catch (e) {
            PmcUI.error(PmcWhI18n.t('loadDetailError'));
        }
    }

    document.addEventListener('dblclick', function (e) {
        var row = e.target.closest('.js-detail-row');
        if (!row) return;
        show(row.dataset.materialId, row.dataset.barcode);
    });

    // Copy nội dung popup "Chi tiết liệu" dạng text để dán vào Zalo/Messenger... — build lại từ
    // chính DOM đang hiển thị (đọc label/value trong .pmc-detail-cell) nên tự động theo đúng ngôn
    // ngữ đang chọn và không lệch khi popup thêm/bớt trường sau này.
    function copyText(text) {
        function onDone() { PmcUI.success(PmcWhI18n.t('copiedToClipboard')); }
        function onFail() { PmcUI.error(PmcWhI18n.t('copyFailed')); }
        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(text).then(onDone, onFail);
            return;
        }
        var ta = document.createElement('textarea');
        ta.value = text;
        ta.style.position = 'fixed';
        ta.style.opacity = '0';
        document.body.appendChild(ta);
        ta.focus();
        ta.select();
        try {
            document.execCommand('copy') ? onDone() : onFail();
        } catch (e) {
            onFail();
        }
        document.body.removeChild(ta);
    }

    // Barcode không còn nằm trong nội dung popup nữa (bỏ trùng với tiêu đề modal) — lấy lại từ
    // tiêu đề "Chi tiết liệu — {barcode}" để build nội dung copy.
    function currentBarcode() {
        var titleEl = document.getElementById('pmc-popup-title');
        if (!titleEl) return '';
        var prefix = PmcWhI18n.t('detailTitlePrefix');
        var text = titleEl.textContent || '';
        return text.indexOf(prefix) === 0 ? text.slice(prefix.length).trim() : text.trim();
    }

    function buildDetailText(root) {
        var lines = [];
        var statusEl = root.querySelector('.pmc-detail-header .badge');
        var barcode = currentBarcode();
        var status = statusEl ? statusEl.textContent.trim() : '';
        if (barcode || status) lines.push(('Barcode: ' + barcode + (status ? ' — ' + status : '')).trim());
        // Copy đủ hết mọi field, kể cả field chưa có data ("—") — giống hệt những gì đang hiện
        // trên popup, không tự ý lọc bớt.
        root.querySelectorAll('.pmc-detail-cell').forEach(function (cell) {
            var label = cell.querySelector('.text-muted');
            var value = cell.querySelector('.fw-semibold');
            if (!label || !value) return;
            var valueText = value.textContent.trim().replace(/\s+/g, ' ');
            lines.push(label.textContent.trim() + ': ' + (valueText || '—'));
        });
        return lines.join('\n');
    }

    function buildHistoryText(root) {
        var lines = [];
        var heading = root.querySelector('h6');
        var barcode = currentBarcode();
        var title = heading ? heading.textContent.trim().replace(/\s+/g, ' ') : '';
        if (barcode) title += (title ? ' — ' : '') + barcode;
        if (title) lines.push(title, '');
        var table = root.querySelector('table');
        if (!table) return lines.join('\n').trim();
        var headers = Array.prototype.map.call(table.querySelectorAll('thead th'), function (th) {
            return th.textContent.trim();
        });
        // Mỗi lần di chuyển 1 đoạn riêng, xuống dòng — dễ đọc khi dán vào Zalo/Messenger hơn là
        // dồn hết vào 1 dòng nối bằng dấu "|". Copy đủ hết cột, kể cả cột chưa có data ("—").
        table.querySelectorAll('tbody tr').forEach(function (tr, idx) {
            var cells = tr.querySelectorAll('td');
            if (cells.length === 0) return;
            var values = Array.prototype.map.call(cells, function (td) {
                return td.textContent.trim().replace(/\s+/g, ' ');
            });
            lines.push((idx + 1) + '. ' + (values[0] || '—') + ' — ' + (values[1] || '—'));
            for (var i = 2; i < values.length; i++) {
                lines.push('   ' + (headers[i] || '') + ': ' + (values[i] || '—'));
            }
            lines.push('');
        });
        return lines.join('\n').trim();
    }

    document.addEventListener('click', function (e) {
        var copyDetailBtn = e.target.closest('.js-copy-detail');
        if (copyDetailBtn) {
            copyText(buildDetailText(document.getElementById('pmc-popup-body')));
            return;
        }
        var copyHistoryBtn = e.target.closest('.js-copy-history');
        if (copyHistoryBtn) {
            copyText(buildHistoryText(document.getElementById('pmc-popup-body')));
        }
    });

    window.PmcMaterialDetail = { show: show };
})();
