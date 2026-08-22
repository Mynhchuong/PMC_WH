/*
 * PmcUI — dùng chung cho toàn site (cần bootstrap.bundle.min.js load trước).
 *
 * Thông báo:   PmcUI.success("Lưu thành công");
 *              PmcUI.error("Có lỗi xảy ra");
 *              PmcUI.warning("Vui lòng kiểm tra lại");
 *              PmcUI.info("Đang xử lý...");
 *
 * Popup xác nhận (trả về Promise<boolean>):
 *              const ok = await PmcUI.confirm("Bạn có chắc muốn lưu?");
 *              const ok = await PmcUI.confirmDelete("Xoá nhân viên này?");
 *
 * Popup thông thường (hiện nội dung tuỳ ý, có nút đóng):
 *              PmcUI.popup("Chi tiết đơn hàng", "<p>...</p>");
 *              PmcUI.popup("Chi tiết đơn hàng", "<p>...</p>", { size: "lg" }); // hoặc "xl"
 *
 * Loading khi gọi API/query (overlay toàn màn hình):
 *              PmcUI.showLoading();
 *              // ... fetch/ajax ...
 *              PmcUI.hideLoading();
 *
 *              // hoặc gọn hơn — tự show/hide quanh 1 async function:
 *              const data = await PmcUI.withLoading(() => fetch("/api/...").then(r => r.json()));
 */
const PmcUI = (function () {
    const TOAST_META = {
        success: { icon: "check_circle", bg: "success" },
        error: { icon: "cancel", bg: "danger" },
        warning: { icon: "warning", bg: "warning" },
        info: { icon: "info", bg: "info" },
    };

    function ensureToastContainer() {
        let container = document.getElementById("pmc-toast-container");
        if (!container) {
            container = document.createElement("div");
            container.id = "pmc-toast-container";
            container.className = "toast-container position-fixed top-0 end-0 p-3";
            container.style.zIndex = 1080;
            document.body.appendChild(container);
        }
        return container;
    }

    function toast(message, type, options) {
        options = options || {};
        const meta = TOAST_META[type] || TOAST_META.info;
        const container = ensureToastContainer();

        const el = document.createElement("div");
        el.className = "toast align-items-center border-0 text-bg-" + meta.bg + " mb-2";
        el.setAttribute("role", "alert");
        el.setAttribute("aria-live", "assertive");
        el.setAttribute("aria-atomic", "true");
        el.innerHTML =
            '<div class="d-flex">' +
            '<div class="toast-body d-flex align-items-center gap-2">' +
            '<span class="material-icons" style="font-size:1.2rem;">' + meta.icon + "</span>" +
            "<span></span>" +
            "</div>" +
            '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>' +
            "</div>";
        el.querySelector(".toast-body span:last-child").textContent = message;

        container.appendChild(el);
        el.addEventListener("hidden.bs.toast", function () { el.remove(); });

        const instance = new bootstrap.Toast(el, { delay: options.delay || 3500 });
        instance.show();
        return instance;
    }

    function success(message, options) { return toast(message, "success", options); }
    function error(message, options) { return toast(message, "error", options); }
    function warning(message, options) { return toast(message, "warning", options); }
    function info(message, options) { return toast(message, "info", options); }

    function ensureConfirmModal() {
        let modalEl = document.getElementById("pmc-confirm-modal");
        if (!modalEl) {
            modalEl = document.createElement("div");
            modalEl.id = "pmc-confirm-modal";
            modalEl.className = "modal fade";
            modalEl.tabIndex = -1;
            modalEl.innerHTML =
                '<div class="modal-dialog modal-dialog-centered">' +
                '<div class="modal-content border-0 rounded-4 shadow">' +
                '<div class="modal-body p-4 text-center">' +
                '<div id="pmc-confirm-icon-wrap" class="mx-auto mb-3 d-flex align-items-center justify-content-center rounded-circle" style="width:56px;height:56px;">' +
                '<span id="pmc-confirm-icon" class="material-icons" style="font-size:28px;color:#fff;"></span>' +
                "</div>" +
                '<h5 id="pmc-confirm-title" class="mb-2"></h5>' +
                '<p id="pmc-confirm-message" class="text-muted mb-4"></p>' +
                '<div class="d-flex justify-content-center gap-2">' +
                '<button type="button" id="pmc-confirm-cancel" class="btn btn-light px-4"></button>' +
                '<button type="button" id="pmc-confirm-ok" class="btn px-4"></button>' +
                "</div>" +
                "</div></div></div>";
            document.body.appendChild(modalEl);
        }
        return modalEl;
    }

    function confirm(message, options) {
        options = options || {};
        const title = options.title || "Xác nhận";
        const confirmText = options.confirmText || "Xác nhận";
        const cancelText = options.cancelText || "Huỷ";
        const variant = options.variant || "primary"; // "primary" | "danger"
        const icon = options.icon || (variant === "danger" ? "delete" : "help");

        return new Promise(function (resolve) {
            const modalEl = ensureConfirmModal();
            modalEl.querySelector("#pmc-confirm-title").textContent = title;
            modalEl.querySelector("#pmc-confirm-message").textContent = message;

            const okBtn = modalEl.querySelector("#pmc-confirm-ok");
            const cancelBtn = modalEl.querySelector("#pmc-confirm-cancel");
            const iconWrap = modalEl.querySelector("#pmc-confirm-icon-wrap");
            const iconEl = modalEl.querySelector("#pmc-confirm-icon");

            okBtn.textContent = confirmText;
            cancelBtn.textContent = cancelText;
            okBtn.className = "btn px-4 " + (variant === "danger" ? "btn-danger" : "pmc-btn-dark");
            iconWrap.style.background = variant === "danger"
                ? "#F44335"
                : "linear-gradient(135deg, #495057 0%, #212529 100%)";
            iconEl.textContent = icon;

            const modal = bootstrap.Modal.getOrCreateInstance(modalEl, { backdrop: "static", keyboard: false });

            function onOk() { modal.hide(); resolve(true); cleanup(); }
            function onCancel() { modal.hide(); resolve(false); cleanup(); }
            function cleanup() {
                okBtn.removeEventListener("click", onOk);
                cancelBtn.removeEventListener("click", onCancel);
            }

            okBtn.addEventListener("click", onOk);
            cancelBtn.addEventListener("click", onCancel);
            modal.show();
        });
    }

    function confirmDelete(message, options) {
        return confirm(message, Object.assign({
            title: "Xoá dữ liệu",
            confirmText: "Xoá",
            cancelText: "Huỷ",
            variant: "danger",
            icon: "delete",
        }, options));
    }

    function ensurePopupModal() {
        let modalEl = document.getElementById("pmc-popup-modal");
        if (!modalEl) {
            modalEl = document.createElement("div");
            modalEl.id = "pmc-popup-modal";
            modalEl.className = "modal fade";
            modalEl.tabIndex = -1;
            modalEl.innerHTML =
                '<div class="modal-dialog modal-dialog-centered modal-dialog-scrollable">' +
                '<div class="modal-content border-0 rounded-4 shadow">' +
                '<div class="modal-header border-0">' +
                '<h5 id="pmc-popup-title" class="modal-title"></h5>' +
                '<button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>' +
                "</div>" +
                '<div id="pmc-popup-body" class="modal-body"></div>' +
                "</div></div>";
            document.body.appendChild(modalEl);
        }
        return modalEl;
    }

    function popup(title, bodyHtml, options) {
        options = options || {};
        const modalEl = ensurePopupModal();
        const dialogEl = modalEl.querySelector(".modal-dialog");
        dialogEl.classList.remove("modal-lg", "modal-xl");
        if (options.size === "lg" || options.size === "xl") {
            dialogEl.classList.add("modal-" + options.size);
        }
        modalEl.querySelector("#pmc-popup-title").textContent = title;
        modalEl.querySelector("#pmc-popup-body").innerHTML = bodyHtml;
        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        modal.show();
        return modal;
    }

    function ensureLoadingOverlay() {
        let el = document.getElementById("pmc-loading-overlay");
        if (!el) {
            el = document.createElement("div");
            el.id = "pmc-loading-overlay";
            el.innerHTML =
                '<div class="pmc-loading-spinner"></div>' +
                '<div class="pmc-loading-text">Đang tải dữ liệu...</div>';
            document.body.appendChild(el);
        }
        return el;
    }

    // Đếm số lượt gọi đang chờ, để 2 query chạy song song không tắt overlay sớm
    // khi mới có 1 cái xong.
    let loadingCount = 0;

    function showLoading() {
        loadingCount++;
        ensureLoadingOverlay().classList.add("show");
    }

    function hideLoading(force) {
        loadingCount = force ? 0 : Math.max(0, loadingCount - 1);
        if (loadingCount === 0) {
            ensureLoadingOverlay().classList.remove("show");
        }
    }

    async function withLoading(work) {
        showLoading();
        try {
            return typeof work === "function" ? await work() : await work;
        } finally {
            hideLoading();
        }
    }

    // Escape chuỗi trước khi nhét vào innerHTML (VD danh sách barcode/lý do lỗi trong popup) —
    // tránh nội dung chứa ký tự đặc biệt (<, >, &...) phá layout hoặc chạy nhầm thành HTML/script.
    function escapeHtml(s) {
        var div = document.createElement("div");
        div.textContent = s == null ? "" : String(s);
        return div.innerHTML;
    }

    return {
        toast, success, error, warning, info,
        confirm, confirmDelete, popup,
        showLoading, hideLoading, withLoading,
        escapeHtml,
    };
})();

window.PmcUI = PmcUI;
