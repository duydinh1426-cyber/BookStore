/**
 * bookModal.js – Modal chi tiết sách + bình luận dùng chung
 *
 * Cách dùng:
 *   <script src="../js/bookModal.js"></script>
 *   window._bookModal.open(bookId)
 *
 * Tự động inject modal HTML vào body nếu chưa có.
 */

(function () {

    const API = "https://localhost:7108/api";
    const BASE_URL = "https://localhost:7108";

    /* ══════════════════════════════════════════
       TOAST HELPER
    ══════════════════════════════════════════ */
    function showToast(msg, type) {
        let wrap = document.getElementById("_bmToastWrap");
        if (!wrap) {
            wrap = document.createElement("div");
            wrap.id = "_bmToastWrap";
            wrap.style.cssText = "position:fixed;top:80px;right:20px;z-index:99999;display:flex;flex-direction:column;gap:8px;pointer-events:none";
            document.body.appendChild(wrap);
        }
        const colors = {
            error: { bg: "#fff0f0", border: "#f5c6c6", icon: "bi-x-circle-fill", iconColor: "#e74c3c" },
            success: { bg: "#f0fff4", border: "#b7e4c7", icon: "bi-check-circle-fill", iconColor: "#27ae60" },
            warn: { bg: "#fffbf0", border: "#ffe0a0", icon: "bi-exclamation-circle-fill", iconColor: "#f39c12" },
        };
        const c = colors[type] || colors.error;
        const t = document.createElement("div");
        t.style.cssText = `
            background:${c.bg}; border:1.5px solid ${c.border}; border-radius:10px;
            padding:.65rem 1rem; font-size:.84rem; font-family:'DM Sans',sans-serif;
            box-shadow:0 4px 16px rgba(0,0,0,.1); display:flex; align-items:flex-start;
            gap:8px; min-width:260px; max-width:340px; pointer-events:all;
            animation:_bmSlideIn .2s ease;
        `;
        t.innerHTML = `
            <i class="bi ${c.icon}" style="color:${c.iconColor};font-size:1rem;flex-shrink:0;margin-top:1px"></i>
            <span style="flex:1;line-height:1.4">${msg}</span>
            <i class="bi bi-x" style="cursor:pointer;color:#aaa;font-size:1rem;flex-shrink:0"
               onclick="this.closest('div').remove()"></i>
        `;
        wrap.appendChild(t);
        setTimeout(() => {
            t.style.opacity = "0";
            t.style.transition = "opacity .3s";
            setTimeout(() => t.remove(), 300);
        }, 4000);
    }

    if (!document.getElementById("_bmToastStyle")) {
        const s = document.createElement("style");
        s.id = "_bmToastStyle";
        s.textContent = "@keyframes _bmSlideIn{from{opacity:0;transform:translateX(20px)}to{opacity:1;transform:translateX(0)}}";
        document.head.appendChild(s);
    }

    /* ══════════════════════════════════════════
       HELPERS
    ══════════════════════════════════════════ */
    function makeImgSrc(raw) {
        if (!raw) return "";
        if (/^https?:\/\//i.test(raw)) return raw;
        return BASE_URL + "/images/" + raw.replace(/^\/?(images\/)?/, "");
    }

    function gf(obj, ...keys) {
        for (const k of keys)
            if (obj[k] !== undefined && obj[k] !== null) return obj[k];
        return null;
    }

    function starsHtml(avg, size = ".95rem") {
        return Array.from({ length: 5 }, (_, i) =>
            `<i class="bi ${i < Math.round(avg) ? "bi-star-fill" : "bi-star"}"
                style="color:${i < Math.round(avg) ? "#f39c12" : "#ddd"};font-size:${size}"></i>`
        ).join("");
    }

    function isLoggedIn() { return !!sessionStorage.getItem("token"); }
    function getToken() { return sessionStorage.getItem("token"); }
    function authHeaders() {
        return {
            "Content-Type": "application/json",
            "Authorization": `Bearer ${getToken()}`
        };
    }
    function getRole() {
        try {
            const p = JSON.parse(atob(getToken().split(".")[1]));
            return p.role
                || p["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"]
                || null;
        } catch { return null; }
    }

    /* ══════════════════════════════════════════
       INJECT MODAL HTML (1 LẦN)
    ══════════════════════════════════════════ */
    function injectModalShell() {
        if (document.getElementById("bookDetailModal")) return;

        const el = document.createElement("div");
        el.innerHTML = `
        <style id="bookModalStyles">
            #bookDetailModal .modal-content { border-radius:14px; border:none; }
            #bookDetailModal .modal-header  { border:none; padding-bottom:0; }

            .bm-qty-btn {
                width:36px; height:36px; border-radius:6px;
                border:1.5px solid #e8edf2; background:#fff;
                cursor:pointer; font-size:1.1rem; font-weight:700;
                display:flex; align-items:center; justify-content:center;
                transition:border-color .15s; font-family:inherit;
            }
            .bm-qty-btn:hover { border-color:#6ab04c; color:#6ab04c; }
            .bm-qty-inp {
                width:52px; height:36px; border:1.5px solid #e8edf2;
                border-radius:6px; text-align:center;
                font-size:.95rem; font-weight:600; outline:none; font-family:inherit;
            }
            .bm-qty-inp:focus { border-color:#6ab04c; }

            .bm-add-btn {
                width:100%; background:#6ab04c; color:#fff; border:none;
                border-radius:8px; padding:.72rem 1.5rem;
                font-size:.96rem; font-weight:700; cursor:pointer;
                font-family:inherit; transition:background .15s;
                display:flex; align-items:center; justify-content:center; gap:6px;
            }
            .bm-add-btn:hover    { background:#4e8a35; }
            .bm-add-btn:disabled { background:#e0e0e0; color:#aaa; cursor:not-allowed; }

            .bm-row {
                display:flex; align-items:baseline; gap:8px;
                padding:.42rem 0; border-bottom:1px solid #f2f2f2; font-size:.875rem;
            }
            .bm-lbl { color:#8a9bb0; min-width:120px; flex-shrink:0; }
            .bm-val { color:#1a1a1a; font-weight:500; }

            .rv-item { padding:.85rem 0; border-bottom:1px solid #f2f2f2; }
            .rv-item:last-child { border-bottom:none; }
            .rv-name  { font-weight:600; font-size:.88rem; }
            .rv-date  { font-size:.75rem; color:#aaa; margin-left:6px; }
            .rv-stars { margin:.3rem 0; }
            .rv-text  { font-size:.84rem; color:#4a5568; line-height:1.6; }

            .rv-form-wrap {
                background:#fafafa; border-radius:10px;
                padding:1rem 1.1rem; margin-top:.8rem;
            }
            .rv-star-pick { display:flex; gap:4px; margin:.5rem 0; }
            .rv-star-pick i { font-size:1.5rem; color:#ddd; transition:color .1s; cursor:pointer; }
            .rv-star-pick i.on { color:#f39c12; }
            .rv-textarea {
                width:100%; border:1.5px solid #e8edf2; border-radius:8px;
                padding:.6rem .8rem; font-family:inherit; font-size:.85rem;
                resize:vertical; min-height:80px; outline:none;
                background:#fff; transition:border-color .15s;
            }
            .rv-textarea:focus { border-color:#6ab04c; }
            .rv-submit {
                background:#6ab04c; color:#fff; border:none;
                border-radius:8px; padding:.5rem 1.4rem;
                font-weight:600; font-size:.85rem; cursor:pointer;
                font-family:inherit; transition:background .15s; margin-top:.5rem;
            }
            .rv-submit:hover    { background:#4e8a35; }
            .rv-submit:disabled { background:#ccc; cursor:not-allowed; }

            .rv-pager { display:flex; justify-content:center; gap:4px; margin-top:.8rem; }
            .rv-pg-btn {
                width:30px; height:30px; border-radius:5px;
                border:1.5px solid #e8edf2; background:#fff;
                cursor:pointer; font-size:.8rem; font-weight:600;
                display:flex; align-items:center; justify-content:center;
                font-family:inherit; transition:all .15s;
            }
            .rv-pg-btn:hover  { border-color:#6ab04c; color:#6ab04c; }
            .rv-pg-btn.active { background:#6ab04c; border-color:#6ab04c; color:#fff; }
            .rv-pg-btn:disabled { opacity:.35; cursor:not-allowed; }
        </style>

        <div class="modal fade" id="bookDetailModal" tabindex="-1">
            <div class="modal-dialog modal-xl modal-dialog-centered modal-dialog-scrollable">
                <div class="modal-content shadow">
                    <div class="modal-header">
                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body px-4 pb-4" id="bookDetailModalBody">
                        <div class="text-center py-5">
                            <div class="spinner-border text-success"></div>
                        </div>
                    </div>
                </div>
            </div>
        </div>`;

        document.body.appendChild(el.firstElementChild);
        document.body.appendChild(el.lastElementChild);
    }

    /* ══════════════════════════════════════════
       RENDER BOOK DETAIL
    ══════════════════════════════════════════ */
    function renderDetail(b) {
        const stock = gf(b, "numberStock", "NumberStock") ?? 0;
        const sold = gf(b, "numberSold", "NumberSold") ?? 0;
        const bid = gf(b, "bookId", "bookID", "BookId", "BookID");
        const src = makeImgSrc(gf(b, "image", "Image") ?? "");
        const title = gf(b, "title", "Title");
        const auth = gf(b, "author", "Author");
        const price = gf(b, "price", "Price");
        const cat = gf(b, "categoryName", "CategoryName") ?? "";
        const desc = gf(b, "description", "Description") ?? "";
        const pyear = gf(b, "publisherYear", "PublisherYear");
        const npages = gf(b, "numberPage", "NumberPage");
        const avg = parseFloat(gf(b, "avgRating", "AvgRating") ?? 0);
        const rcnt = parseInt(gf(b, "reviewCount", "ReviewCount") ?? 0);

        return `
        <div class="row g-0">
            <div class="col-12 col-md-4" style="padding-right:1.5rem;padding-bottom:1rem">
                <div style="border-radius:10px;overflow:hidden;background:#f7f7f7;
                            box-shadow:0 2px 18px rgba(0,0,0,.08);max-width:250px;margin:0 auto">
                    ${src
                ? `<img src="${src}" alt=""
                                style="width:100%;display:block;object-fit:contain;
                                       max-height:340px;padding:12px;box-sizing:border-box"
                                onerror="this.src='https://placehold.co/250x340?text=No+Image'">`
                : `<div style="height:300px;display:flex;align-items:center;
                                        justify-content:center;color:#ccc;font-size:4rem">
                                <i class="bi bi-book"></i></div>`}
                </div>
                <div class="text-center mt-3">
                    <div>${starsHtml(avg)}</div>
                    <div style="font-size:.78rem;color:#8a9bb0;margin-top:3px">
                        ${avg > 0 ? avg.toFixed(1) + " / 5" : "Chưa có đánh giá"}
                        ${rcnt > 0 ? " · " + rcnt + " đánh giá" : ""}
                    </div>
                </div>
            </div>

            <div class="col-12 col-md-8">
                ${cat ? `<div style="font-size:.72rem;color:#8a9bb0;font-weight:700;
                                text-transform:uppercase;letter-spacing:.9px;
                                margin-bottom:.4rem">${cat}</div>` : ""}
                <h4 style="font-weight:800;line-height:1.3;color:#1a1a1a;margin-bottom:.6rem">
                    ${title}
                </h4>
                <div style="font-size:1.75rem;font-weight:800;color:#1a1a1a;
                            margin-bottom:1rem;letter-spacing:-.5px">
                    ${price.toLocaleString("vi-VN")} VNĐ
                </div>

                <div style="margin-bottom:1rem">
                    <div class="bm-row">
                        <span class="bm-lbl">Tác giả</span>
                        <span class="bm-val">${auth}</span>
                    </div>
                    ${npages ? `<div class="bm-row">
                        <span class="bm-lbl">Số trang</span>
                        <span class="bm-val">${npages}</span></div>` : ""}
                    ${pyear ? `<div class="bm-row">
                        <span class="bm-lbl">Năm xuất bản</span>
                        <span class="bm-val">${pyear}</span></div>` : ""}
                    <div class="bm-row">
                        <span class="bm-lbl">Đã bán</span>
                        <span class="bm-val">${sold.toLocaleString("vi-VN")}</span>
                    </div>
                    <div class="bm-row" style="border-bottom:none">
                        <span class="bm-lbl">Còn lại</span>
                        <span class="bm-val"
                              style="color:${stock > 0 ? "#6ab04c" : "#e74c3c"};font-weight:700">
                            ${stock > 0 ? stock.toLocaleString("vi-VN") : "Hết hàng"}
                        </span>
                    </div>
                </div>

                ${desc ? `<p style="font-size:.84rem;color:#4a5568;line-height:1.78;
                                background:#fafafa;border-radius:8px;
                                padding:.7rem .9rem;margin-bottom:1rem;
                                max-height:90px;overflow-y:auto">${desc}</p>` : ""}

                ${stock > 0 ? `
                <div style="display:flex;align-items:center;gap:8px;margin-bottom:.65rem">
                    <button class="bm-qty-btn" onclick="_bmChgQty(-1,${stock})">−</button>
                    <input  class="bm-qty-inp" id="bmQty" type="number"
                            value="1" min="1" max="${stock}"
                            onchange="_bmClampQty(${stock})">
                    <button class="bm-qty-btn" onclick="_bmChgQty(1,${stock})">+</button>
                    <span style="font-size:.76rem;color:#aaa">/ ${stock} còn</span>
                </div>
                <button class="bm-add-btn"
                        onclick="_bmAddCart(${bid},'${title.replace(/'/g, "\\'")}',${stock})">
                    <i class="bi bi-bag-plus"></i> Thêm Vào Giỏ
                </button>` : `
                <button class="bm-add-btn" disabled>
                    <i class="bi bi-x-circle"></i> Hết Hàng
                </button>`}

                <div style="margin-top:.9rem;display:flex;flex-direction:column;gap:.35rem">
                    <div style="font-size:.82rem;color:#4a5568;display:flex;align-items:center;gap:8px">
                        <i class="bi bi-rocket" style="color:#6ab04c;font-size:16px"></i>
                        Vận chuyển toàn quốc
                    </div>
                    <div style="font-size:.82rem;color:#4a5568;display:flex;align-items:center;gap:8px">
                        <i class="bi bi-check-circle" style="color:#6ab04c;font-size:16px"></i>
                        Giao hàng nhanh 2–5 ngày
                    </div>
                </div>
            </div>
        </div>

        <!-- REVIEWS -->
        <div id="bmReviewsSection"
             style="margin-top:1.5rem;padding-top:1.1rem;border-top:2px solid #f0f0f0">
            <div class="text-center py-3">
                <div class="spinner-border text-success spinner-border-sm"></div>
            </div>
        </div>`;
    }

    /* ══════════════════════════════════════════
       LOAD & RENDER REVIEWS
    ══════════════════════════════════════════ */
    let _rv = { bookId: 0, page: 1, total: 0, totalPages: 1, pickedStar: 0 };

    async function loadReviews(bookId, page = 1) {
        _rv.bookId = bookId;
        _rv.page = page;

        const sec = document.getElementById("bmReviewsSection");
        if (!sec) return;

        const params = new URLSearchParams({ page, pageSize: 5 });

        try {
            const res = await fetch(`${API}/reviews/book/${bookId}?${params}`);
            const data = await res.json();
            _rv.total = data.total ?? 0;
            _rv.totalPages = data.totalPages ?? 1;

            // Kiểm tra trạng thái review của Customer
            const isCustomer = isLoggedIn() && getRole() === "Customer";
            let reviewStatus = null;
            if (isCustomer) {
                try {
                    const sRes = await fetch(`${API}/reviews/status/${bookId}`,
                        { headers: authHeaders() });
                    reviewStatus = await sRes.json();
                } catch { }
            }

            sec.innerHTML = `
            <h6 style="font-weight:800;font-size:1rem;margin-bottom:.8rem">
                <i class="bi bi-star me-2 text-warning" style="font-size:16px"></i>
                Đánh Giá & Nhận Xét
                ${_rv.total > 0
                    ? `<span style="font-weight:400;font-size:.82rem;color:#8a9bb0;margin-left:6px">
                           (${_rv.total} đánh giá)</span>`
                    : ""}
            </h6>

            <!-- Danh sách -->
            <div id="bmRvList">
                ${data.data && data.data.length > 0
                    ? data.data.map(r => `
                        <div class="rv-item">
                            <div>
                                <span class="rv-name">${r.customerName ?? "Ẩn danh"}</span>
                                <span class="rv-date">
                                    ${new Date(r.createdAt).toLocaleDateString("vi-VN")}
                                </span>
                            </div>
                            <div class="rv-stars">${starsHtml(r.rating, ".82rem")}</div>
                            ${r.comment ? `<div class="rv-text">${r.comment}</div>` : ""}
                        </div>`).join("")
                    : `<p style="color:#bbb;font-size:.85rem;margin:.5rem 0">
                           Chưa có đánh giá nào.</p>`}
            </div>

            <!-- Pager -->
            ${_rv.totalPages > 1 ? `
            <div class="rv-pager">
                <button class="rv-pg-btn" ${page === 1 ? "disabled" : ""}
                        onclick="_bmRvPage(${page - 1})">
                    <i class="bi bi-chevron-left"></i>
                </button>
                ${Array.from({ length: _rv.totalPages }, (_, i) => i + 1).map(pg => `
                    <button class="rv-pg-btn ${pg === page ? "active" : ""}"
                            onclick="_bmRvPage(${pg})">${pg}</button>`).join("")}
                <button class="rv-pg-btn" ${page === _rv.totalPages ? "disabled" : ""}
                        onclick="_bmRvPage(${page + 1})">
                    <i class="bi bi-chevron-right"></i>
                </button>
            </div>` : ""}

            ${renderReviewFormArea(bookId, isCustomer, reviewStatus)}`;

        } catch {
            sec.innerHTML = `<p style="color:#aaa;font-size:.85rem">
                Không tải được đánh giá.</p>`;
        }
    }

    /* ══════════════════════════════════════════
       FORM / THÔNG BÁO
    ══════════════════════════════════════════ */
    function renderReviewFormArea(bookId, isCustomer, status) {
        if (!isLoggedIn()) return "";
        if (!isCustomer) return "";
        if (!status || status.reason === "not_purchased") return "";
        if (status.reason === "already_reviewed") {
            return `
            <div style="font-size:.83rem;color:#8a9bb0;margin-top:.8rem;
                        padding:.6rem .9rem;background:#f5f5f5;border-radius:8px;
                        display:flex;align-items:center;gap:7px">
                <i class="bi bi-check-circle-fill" style="color:#27ae60"></i>
                Bạn đã đánh giá cuốn sách này.
            </div>`;
        }
        // canReview = true → hiện nút dẫn sang trang review riêng
        return `
        <div style="margin-top:.9rem;padding:.9rem 1rem;
                    background:#f0fff4;border-radius:10px;
                    border:1.5px solid #b7e4c7;
                    display:flex;align-items:center;justify-content:space-between;gap:12px;
                    flex-wrap:wrap">
            <div>
                <div style="font-weight:600;font-size:.88rem;color:#1a1a1a">
                    <i class="bi bi-star me-1" style="color:#f39c12"></i>
                    Bạn có thể đánh giá cuốn sách này
                </div>
                <div style="font-size:.76rem;color:#8a9bb0;margin-top:2px">
                    Chia sẻ cảm nhận sau khi đọc xong nhé!
                </div>
            </div>
            <a href="review.html?bookId=${bookId}"
               style="display:inline-flex;align-items:center;gap:6px;
                      background:#6ab04c;color:#fff;border-radius:9px;
                      padding:.5rem 1.1rem;font-weight:700;font-size:.84rem;
                      text-decoration:none;white-space:nowrap;transition:background .15s;flex-shrink:0"
               onmouseover="this.style.background='#4e8a35'"
               onmouseout="this.style.background='#6ab04c'">
                <i class="bi bi-pencil-square"></i> Viết đánh giá
            </a>
        </div>`;
    }

    /* ══════════════════════════════════════════
       GLOBAL HANDLERS
    ══════════════════════════════════════════ */
    window._bmRvPage = function (p) {
        loadReviews(_rv.bookId, p);
        document.getElementById("bookDetailModal")
            ?.querySelector(".modal-body")
            ?.scrollTo({ top: 999, behavior: "smooth" });
    };

    window._bmPickStar = function (s) {
        _rv.pickedStar = s;
        document.querySelectorAll("#bmStarPick i")
            .forEach((el, i) => el.classList.toggle("on", i < s));
    };

    window._bmSubmitReview = async function (bookId) {
        function showErr(msg) {
            const wrap = document.getElementById("bmRvError");
            const txt = document.getElementById("bmRvErrorMsg");
            if (wrap && txt) {
                txt.textContent = msg;
                wrap.style.display = "flex";
                wrap.scrollIntoView({ behavior: "smooth", block: "nearest" });
            }
            showToast(msg, "error");
        }
        function clearErr() {
            const wrap = document.getElementById("bmRvError");
            if (wrap) wrap.style.display = "none";
        }

        clearErr();
        if (!_rv.pickedStar) { showErr("Vui lòng chọn số sao trước khi gửi."); return; }

        const comment = document.getElementById("bmRvText")?.value?.trim() ?? "";
        const btn = document.getElementById("bmRvSubmit");
        if (btn) { btn.disabled = true; btn.innerHTML = '<i class="bi bi-hourglass-split me-1"></i>Đang gửi...'; }

        try {
            const res = await fetch(`${API}/reviews`, {
                method: "POST",
                headers: authHeaders(),
                body: JSON.stringify({ bookId, rating: _rv.pickedStar, comment })
            });
            const data = await res.json();

            if (!res.ok) {
                const msg = data.message || data.Message || data.title
                    || Object.values(data.errors || {})[0]?.[0]
                    || "Gửi đánh giá thất bại.";
                showErr(msg);
                if (btn) { btn.disabled = false; btn.innerHTML = '<i class="bi bi-send me-1"></i>Gửi đánh giá'; }
                return;
            }

            showToast("Đánh giá của bạn đã được gửi!", "success");
            _rv.pickedStar = 0;
            loadReviews(bookId, 1);
        } catch {
            showErr("Không thể kết nối đến máy chủ. Vui lòng thử lại.");
            if (btn) { btn.disabled = false; btn.innerHTML = '<i class="bi bi-send me-1"></i>Gửi đánh giá'; }
        }
    };

    /* ══════════════════════════════════════════
       QTY HELPERS
    ══════════════════════════════════════════ */
    window._bmChgQty = function (d, max) {
        const el = document.getElementById("bmQty"); if (!el) return;
        el.value = Math.min(max, Math.max(1, (parseInt(el.value) || 1) + d));
    };

    window._bmClampQty = function (max) {
        const el = document.getElementById("bmQty"); if (!el) return;
        el.value = Math.min(max, Math.max(1, parseInt(el.value) || 1));
    };

    window._bmAddCart = async function (bookId, title, max) {
        const qty = Math.min(max, Math.max(1,
            parseInt(document.getElementById("bmQty")?.value) || 1));
        if (!window._headerCart) return;
        for (let i = 0; i < qty; i++)
            await window._headerCart.addToCart(bookId, title);
        bootstrap.Modal.getInstance(
            document.getElementById("bookDetailModal"))?.hide();
    };

    /* ══════════════════════════════════════════
       PUBLIC API
    ══════════════════════════════════════════ */
    window._bookModal = {
        async open(bookId) {
            injectModalShell();

            const modal = new bootstrap.Modal(
                document.getElementById("bookDetailModal"));
            modal.show();

            const body = document.getElementById("bookDetailModalBody");
            body.innerHTML = `<div class="text-center py-5">
                <div class="spinner-border text-success"></div></div>`;
            _rv.pickedStar = 0;
            _rv.page = 1;

            try {
                const res = await fetch(`${API}/books/${bookId}`);
                if (!res.ok) throw new Error();
                const b = await res.json();

                body.innerHTML = renderDetail(b);

                const bid = gf(b, "bookId", "bookID", "BookId", "BookID");
                loadReviews(bid, 1);

            } catch {
                body.innerHTML = `<div class="text-center py-4 text-muted">
                    Không tải được thông tin sách.</div>`;
            }
        }
    };

    window.openBookDetail = (id) => window._bookModal.open(id);

})();