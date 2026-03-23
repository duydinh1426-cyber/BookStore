(function () {

    const API = "https://localhost:7108/api";
    const BASE_URL = "https://localhost:7108";

    /* ── MENUS ── */
    const MENUS = {
        guest: [
            { label: "Trang chủ", href: "../html/index.html", icon: "bi-house-door" },
            { label: "Sách", href: "../html/books.html", icon: "bi-journals" },
        ],
        Customer: [
            { label: "Trang chủ", href: "../html/index.html", icon: "bi-house-door" },
            { label: "Sách", href: "../html/books.html", icon: "bi-journals" },
            { label: "Đơn hàng", href: "orders.html", icon: "bi-bag" },
        ],
        Admin: [
            { label: "Dashboard", href: "admin.html", icon: "bi-speedometer2" },
            { label: "Quản lý sách", href: "admin-books.html", icon: "bi-journals" },
            { label: "Người dùng", href: "admin-users.html", icon: "bi-people" },
            { label: "Đơn hàng", href: "admin-orders.html", icon: "bi-bag-check" },
            { label: "Đánh giá", href: "admin-reviews.html", icon: "bi-star-half" },
            { label: "Thống kê", href: "admin-stats.html", icon: "bi-bar-chart" },
        ],
    };

    const LOGIN_PAGE = "../html/login.html";
    const HOME_PAGE = "../html/index.html";

    /* ── TOKEN HELPERS ── */
    function getTokenPayload() {
        try {
            const token = sessionStorage.getItem("token");
            if (!token) return null;
            // Giải mã UTF-8 đúng cách → fix lỗi tiếng Việt trong tên
            const base64 = token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
            const json = decodeURIComponent(
                atob(base64).split("").map(c =>
                    "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2)
                ).join("")
            );
            return JSON.parse(json);
        } catch { return null; }
    }
    function getRole() {
        const p = getTokenPayload(); if (!p) return null;
        return p.role
            || p["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"]
            || null;
    }
    function getUsername() {
        const p = getTokenPayload(); if (!p) return "Tài khoản";
        return p.name
            || p.unique_name
            || p["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"]
            || p.sub
            || "Tài khoản";
    }
    function getToken() { return sessionStorage.getItem("token"); }

    // Kiểm tra token còn hạn không (so sánh exp trong JWT với thời gian hiện tại)
    function isTokenExpired() {
        const p = getTokenPayload();
        if (!p || !p.exp) return true;
        // p.exp là Unix timestamp (giây), Date.now() là ms
        return p.exp * 1000 < Date.now();
    }

    function isLoggedIn() {
        if (!getToken()) return false;
        if (isTokenExpired()) {
            // Token hết hạn → tự động đăng xuất
            sessionStorage.removeItem("token");
            return false;
        }
        return true;
    }

    function authHeaders() {
        return { "Content-Type": "application/json", "Authorization": `Bearer ${getToken()}` };
    }

    function currentPage() {
        return window.location.pathname.split("/").pop() || "index.html";
    }

    // Trang chỉ dành cho Admin
    const ADMIN_PAGES = [
        "admin.html", "admin-books.html", "admin-orders.html",
        "admin-users.html", "admin-stats.html", "admin-reviews.html"
    ];

    // Trang Customer — Admin KHÔNG được vào
    const CUSTOMER_PAGES = [
        "index.html", "books.html", "checkout.html",
        "order-history.html", "favorites.html",
        "orders.html"
    ];

    // Trang cần đăng nhập
    const LOGIN_REQUIRED_PAGES = [
        "checkout.html", "order-history.html",
        "favorites.html", "profile.html", "orders.html"
    ];

    function guardPage() {
        const page = currentPage();
        const loggedIn = isLoggedIn();
        const role = loggedIn ? getRole() : null;

        // Chưa đăng nhập vào trang Admin → login
        if (!loggedIn && ADMIN_PAGES.includes(page)) {
            window.location.replace("login.html");
            return;
        }

        // Chưa đăng nhập vào trang cần login → login
        if (!loggedIn && LOGIN_REQUIRED_PAGES.includes(page)) {
            window.location.replace("login.html");
            return;
        }

        // Đã đăng nhập nhưng không đọc được role (token lỗi) → không redirect
        if (loggedIn && !role) return;

        // Admin vào trang Customer → dashboard
        if (role === "Admin" && CUSTOMER_PAGES.includes(page)) {
            window.location.replace("admin.html");
            return;
        }

        // Customer vào trang Admin → trang chủ
        if (role === "Customer" && ADMIN_PAGES.includes(page)) {
            window.location.replace("index.html");
            return;
        }
    }

    /* ── UI HELPERS ── */
    function fmtPrice(n) {
        return new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(n);
    }
    function makeImgSrc(raw) {
        if (!raw) return "";
        if (/^https?:\/\//i.test(raw)) return raw;
        return BASE_URL + "/images/" + raw.replace(/^\/?(images\/)?/, "");
    }
    function showToast(msg, type = "success") {
        let c = document.getElementById("headerToastContainer");
        if (!c) {
            c = document.createElement("div"); c.id = "headerToastContainer";
            c.style.cssText = "position:fixed;bottom:24px;right:24px;z-index:9999;display:flex;flex-direction:column;gap:8px;";
            document.body.appendChild(c);
        }
        const icons = { success: "bi-check-circle", error: "bi-x-circle", info: "bi-info-circle" };
        const colors = { success: "#4e8a35", error: "#e74c3c", info: "#3498db" };
        const el = document.createElement("div");
        el.style.cssText = `background:${colors[type] || colors.info};color:#fff;padding:.75rem 1.2rem;border-radius:12px;font-size:.88rem;font-weight:500;display:flex;align-items:center;gap:8px;box-shadow:0 4px 20px rgba(0,0,0,.2);animation:headerSlideIn .3s ease;max-width:300px;font-family:'DM Sans',sans-serif;`;
        el.innerHTML = `<i class="bi ${icons[type] || icons.info}"></i>${msg}`;
        c.appendChild(el);
        setTimeout(() => el.remove(), 3000);
    }

    /* ── BUILD NAV ── */
    function buildNavLinks(role) {
        const items = MENUS[role] || MENUS["guest"];
        const page = currentPage();
        return items.map(item => {
            const active = page === item.href.split("/").pop() ? "active" : "";
            return `<li class="nav-item">
                        <a class="nav-link ${active}" href="${item.href}">
                            <i class="bi ${item.icon} me-1"></i>${item.label}
                        </a>
                    </li>`;
        }).join("");
    }
    function buildCartButton(role) {
        if (!role || role === "Admin") return "";
        return `<li class="nav-item d-flex align-items-center me-1" id="cartNavItem">
                    <button class="cart-nav-btn" id="cartNavBtn" onclick="window._headerCart.open()">
                        <i class="bi bi-bag"></i><span>Giỏ hàng</span>
                        <span class="cart-nav-badge" id="cartBadge" style="display:none">0</span>
                    </button>
                </li>`;
    }
    function buildAuthBtn(role) {
        if (!role) return `<li class="nav-item">
                <button class="btn-login-nav" onclick="window.location.href='${LOGIN_PAGE}'">
                    <i class="bi bi-person me-1"></i>Đăng nhập
                </button></li>`;
        const u = getUsername();
        return `<li class="nav-item dropdown">
                    <button class="btn-login-nav dropdown-toggle"
                            data-bs-toggle="dropdown" aria-expanded="false">
                        <i class="bi bi-person-circle me-1"></i>${u}
                    </button>
                    <ul class="dropdown-menu dropdown-menu-end shadow-sm">
                        <li><span class="dropdown-item-text small text-muted">Xin chào, <strong>${u}</strong></span></li>
                        <li><hr class="dropdown-divider"></li>
                        <li><a class="dropdown-item" href="profile.html"><i class="bi bi-person me-2"></i>Trang cá nhân</a></li>
                        <li><hr class="dropdown-divider"></li>
                        <li><a class="dropdown-item text-danger" href="#" onclick="headerLogout();return false;"><i class="bi bi-box-arrow-right me-2"></i>Đăng xuất</a></li>
                    </ul>
                </li>`;
    }

    /* ── RENDER HEADER ── */
    function renderHeader() {
        const placeholder = document.getElementById("header-placeholder");
        if (!placeholder) return;
        const role = getRole();
        placeholder.innerHTML = `
        <style>
            @keyframes headerSlideIn{from{opacity:0;transform:translateX(40px)}to{opacity:1;transform:translateX(0)}}
            @keyframes cartDrawerFadeUp{from{opacity:0;transform:translateY(8px)}to{opacity:1;transform:translateY(0)}}
            .green-nav{background:#6ab04c}
            .green-nav .navbar{padding:0;min-height:46px}
            .green-nav .nav-link{color:#fff!important;font-weight:600;font-size:.93rem;padding:.72rem 1.2rem;position:relative}
            .green-nav .nav-link::after{content:'';position:absolute;bottom:0;left:50%;right:50%;height:3px;background:rgba(255,255,255,.75);transition:left .2s,right .2s}
            .green-nav .nav-link:hover::after,.green-nav .nav-link.active::after{left:0;right:0}
            .green-nav .nav-link:hover,.green-nav .nav-link.active{background:rgba(0,0,0,.1)}
            .btn-login-nav{color:#fff!important;font-weight:600;font-size:.93rem;padding:.72rem 1.2rem;background:transparent;border:none;cursor:pointer;position:relative;white-space:nowrap}
            .btn-login-nav::after{content:'';position:absolute;bottom:0;left:50%;right:50%;height:3px;background:rgba(255,255,255,.75);transition:left .2s,right .2s}
            .btn-login-nav:hover{background:rgba(0,0,0,.1)}.btn-login-nav:hover::after{left:0;right:0}
            .logo-icon{display:flex;align-items:flex-end;gap:3px;height:34px}
            .logo-icon span{display:block;width:8px;border-radius:2px 2px 0 0}
            .logo-icon span:nth-child(1){height:26px;background:#e74c3c}
            .logo-icon span:nth-child(2){height:34px;background:#f39c12}
            .logo-icon span:nth-child(3){height:22px;background:#2ecc71}
            .logo-icon span:nth-child(4){height:30px;background:#3498db}
            .logo-name{font-size:1.45rem;font-weight:800;color:#222;line-height:1.1}
            .logo-sub{font-size:.68rem;letter-spacing:2.5px;text-transform:uppercase;color:#aaa}
            .cart-nav-btn{position:relative;background:rgba(255,255,255,.15);border:1.5px solid rgba(255,255,255,.4);color:#fff;border-radius:10px;padding:.42rem .9rem;font-size:.88rem;font-weight:600;cursor:pointer;display:flex;align-items:center;gap:6px;transition:background .2s;font-family:inherit}
            .cart-nav-btn:hover{background:rgba(255,255,255,.28)}
            .cart-nav-badge{position:absolute;top:-7px;right:-7px;background:#e74c3c;color:#fff;font-size:.65rem;font-weight:700;min-width:18px;height:18px;border-radius:9px;display:flex;align-items:center;justify-content:center;padding:0 4px;border:2px solid #6ab04c}
            #headerCartOverlay{position:fixed;inset:0;background:rgba(0,0,0,.45);z-index:2000;opacity:0;pointer-events:none;transition:opacity .3s}
            #headerCartOverlay.open{opacity:1;pointer-events:all}
            #headerCartDrawer{position:fixed;top:0;right:0;bottom:0;width:420px;max-width:95vw;background:#fff;z-index:2001;transform:translateX(100%);transition:transform .35s cubic-bezier(.4,0,.2,1);display:flex;flex-direction:column;box-shadow:-8px 0 40px rgba(0,0,0,.15);font-family:'DM Sans',sans-serif}
            #headerCartDrawer.open{transform:translateX(0)}
            .hc-drawer-header{padding:1.4rem 1.5rem;border-bottom:1px solid #e8edf2;display:flex;align-items:center;justify-content:space-between}
            .hc-drawer-header h5{font-size:1.2rem;font-weight:700;margin:0;font-family:'Playfair Display',serif}
            .hc-close-btn{background:#e8edf2;border:none;width:34px;height:34px;border-radius:50%;display:flex;align-items:center;justify-content:center;cursor:pointer;font-size:1.1rem;color:#4a5568;transition:background .2s}
            .hc-close-btn:hover{background:#dde3ea}
            .hc-drawer-body{flex:1;overflow-y:auto;padding:1rem 1.5rem}
            .hc-drawer-body::-webkit-scrollbar{width:4px}
            .hc-drawer-body::-webkit-scrollbar-thumb{background:#e8edf2;border-radius:2px}
            .hc-empty{text-align:center;padding:3rem 1rem;color:#8a9bb0}
            .hc-empty i{font-size:3.5rem;margin-bottom:1rem;display:block}
            .hc-empty p{margin:0 0 4px;font-weight:500}
            .hc-empty small{font-size:.82rem}
            .hc-item{display:flex;gap:12px;padding:.9rem 0;border-bottom:1px solid #e8edf2;animation:cartDrawerFadeUp .2s ease}
            /* Fix ảnh cart drawer */
            .hc-item-img{width:64px;height:86px;object-fit:cover;object-position:center top;border-radius:6px;flex-shrink:0;background:#f0f7eb;display:block}
            .hc-item-img-ph{width:64px;height:86px;border-radius:6px;background:#f0f7eb;display:flex;align-items:center;justify-content:center;color:#6ab04c;font-size:1.5rem;flex-shrink:0}
            .hc-item-info{flex:1;min-width:0}
            .hc-item-title{font-weight:600;font-size:.88rem;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
            .hc-item-author{font-size:.78rem;color:#8a9bb0;margin:2px 0 6px}
            .hc-item-price{font-weight:700;color:#6ab04c;font-size:.92rem}
            .hc-qty-ctrl{display:flex;align-items:center;gap:6px;margin-top:8px}
            .hc-qty-btn{width:26px;height:26px;border-radius:6px;border:1.5px solid #e8edf2;background:#fff;cursor:pointer;display:flex;align-items:center;justify-content:center;font-size:.9rem;color:#4a5568;transition:all .15s}
            .hc-qty-btn:hover{border-color:#6ab04c;color:#6ab04c}
            .hc-qty-val{font-weight:600;font-size:.88rem;min-width:24px;text-align:center}
            .hc-item-rm{background:none;border:none;color:#8a9bb0;cursor:pointer;font-size:1rem;padding:4px;border-radius:4px;align-self:flex-start;transition:color .15s}
            .hc-item-rm:hover{color:#e74c3c}
            .hc-drawer-footer{padding:1.2rem 1.5rem;border-top:1px solid #e8edf2;background:#fff}
            .hc-total-row{display:flex;justify-content:space-between;font-weight:700;font-size:1.05rem;margin-bottom:1rem}
            .hc-total-price{color:#6ab04c;font-size:1.2rem}
            .hc-btn-checkout{width:100%;background:#6ab04c;color:#fff;border:none;padding:.85rem;border-radius:12px;font-weight:700;font-size:1rem;cursor:pointer;transition:background .2s,transform .1s;font-family:inherit}
            .hc-btn-checkout:hover{background:#4e8a35;transform:translateY(-1px)}
            .hc-btn-clear{width:100%;background:none;border:1.5px solid #e8edf2;color:#4a5568;padding:.6rem;border-radius:10px;font-size:.88rem;font-weight:500;cursor:pointer;margin-top:.6rem;transition:all .15s;font-family:inherit}
            .hc-btn-clear:hover{border-color:#e74c3c;color:#e74c3c}
            @media(max-width:768px){#headerCartDrawer{width:100%}}
        </style>

        <div class="bg-white border-bottom py-2">
            <div class="container-fluid px-4">
                <a href="${HOME_PAGE}" class="text-decoration-none d-inline-flex align-items-center gap-2">
                    <div class="logo-icon"><span></span><span></span><span></span><span></span></div>
                    <div>
                        <div class="logo-name">Book Store</div>
                        <div class="logo-sub">Mua s&aacute;ch &ndash; T&agrave;ng tri th&#7913;c</div>
                    </div>
                </a>
            </div>
        </div>

        <div class="green-nav">
            <div class="container-fluid px-4">
                <nav class="navbar navbar-expand-lg p-0">
                    <button class="navbar-toggler border-0 text-white py-2" type="button"
                            data-bs-toggle="collapse" data-bs-target="#greenNav">
                        <i class="bi bi-list fs-4"></i>
                    </button>
                    <div class="collapse navbar-collapse" id="greenNav">
                        <ul class="navbar-nav me-auto">${buildNavLinks(role)}</ul>
                        <ul class="navbar-nav ms-auto align-items-center">
                            ${buildCartButton(role)}
                            ${buildAuthBtn(role)}
                        </ul>
                    </div>
                </nav>
            </div>
        </div>

        <div id="headerCartOverlay" onclick="window._headerCart.close()"></div>

        <div id="headerCartDrawer">
            <div class="hc-drawer-header">
                <h5><i class="bi bi-bag me-2 text-success"></i>Gi&#7887; h&agrave;ng c&#7911;a b&#7841;n</h5>
                <button class="hc-close-btn" onclick="window._headerCart.close()">
                    <i class="bi bi-x"></i>
                </button>
            </div>
            <div class="hc-drawer-body" id="hcBody">
                <div class="hc-empty"><i class="bi bi-bag-x"></i><p>Gi&#7887; h&agrave;ng tr&#7889;ng</p></div>
            </div>
            <div class="hc-drawer-footer" id="hcFooter" style="display:none">
                <div class="hc-total-row">
                    <span>T&#7893;ng c&#7897;ng</span>
                    <span class="hc-total-price" id="hcTotalPrice">0&#8363;</span>
                </div>
                <button class="hc-btn-checkout" onclick="window._headerCart.checkout()">
                    <i class="bi bi-lightning-charge me-1"></i>Ti&#7871;n h&agrave;nh thanh to&aacute;n
                </button>
                <button class="hc-btn-clear" onclick="window._headerCart.clearAll()">
                    <i class="bi bi-trash me-1"></i>X&oacute;a to&agrave;n b&#7897; gi&#7887; h&agrave;ng
                </button>
            </div>
        </div>`;

        if (isLoggedIn()) _cartLoadCount();
    }

    /* ── CART LOGIC ── */
    function _cartUpdateBadge(n) {
        const b = document.getElementById("cartBadge"); if (!b) return;
        if (n > 0) { b.style.display = "flex"; b.textContent = n > 99 ? "99+" : n; }
        else b.style.display = "none";
    }
    async function _cartLoadCount() {
        if (!isLoggedIn()) return;
        try {
            const res = await fetch(`${API}/cart`, { headers: authHeaders() });
            if (!res.ok) return;
            const d = await res.json();
            _cartUpdateBadge(d.totalItems || 0);
        } catch { }
    }
    async function _cartFetch() {
        const body = document.getElementById("hcBody"); if (!body) return;
        body.innerHTML = `<div style="text-align:center;padding:3rem"><div class="spinner-border text-success"></div></div>`;
        try {
            const res = await fetch(`${API}/cart`, { headers: authHeaders() });
            if (!res.ok) throw new Error();
            const d = await res.json();
            _cartRender(d);
            _cartUpdateBadge(d.totalItems || 0);
        } catch {
            body.innerHTML = `<div class="hc-empty"><i class="bi bi-wifi-off"></i><p>Không tải được giỏ hàng</p></div>`;
        }
    }
    function _cartRender(data) {
        const body = document.getElementById("hcBody");
        const foot = document.getElementById("hcFooter");
        if (!body || !foot) return;
        if (!data.items || data.items.length === 0) {
            body.innerHTML = `<div class="hc-empty"><i class="bi bi-bag-x"></i><p>Giỏ hàng trống</p><small>Hãy thêm sách bạn yêu thích!</small></div>`;
            foot.style.display = "none"; return;
        }
        body.innerHTML = data.items.map(item => {
            const src = makeImgSrc(item.image);
            return `
            <div class="hc-item">
                ${src
                    ? `<img class="hc-item-img" src="${src}" alt=""
                            onerror="this.style.display='none';this.nextElementSibling.style.display='flex'">`
                    : ""}
                <div class="hc-item-img-ph" ${src ? 'style="display:none"' : ""}>
                    <i class="bi bi-book"></i>
                </div>
                <div class="hc-item-info">
                    <div class="hc-item-title" title="${item.title}">${item.title}</div>
                    <div class="hc-item-author">${item.author || ""}</div>
                    <div class="hc-item-price">${fmtPrice(item.subTotal)}</div>
                    <div class="hc-qty-ctrl">
                        <button class="hc-qty-btn" onclick="window._headerCart.updateQty(${item.bookId},${item.quantity - 1})"><i class="bi bi-dash"></i></button>
                        <span class="hc-qty-val">${item.quantity}</span>
                        <button class="hc-qty-btn" onclick="window._headerCart.updateQty(${item.bookId},${item.quantity + 1})"><i class="bi bi-plus"></i></button>
                    </div>
                </div>
                <button class="hc-item-rm" onclick="window._headerCart.removeItem(${item.bookId})">
                    <i class="bi bi-x-lg"></i>
                </button>
            </div>`;
        }).join("");
        document.getElementById("hcTotalPrice").textContent = fmtPrice(data.totalPrice);
        foot.style.display = "block";
    }

    /* ── PUBLIC CART API ── */
    window._headerCart = {
        open() {
            if (!isLoggedIn()) { showToast("Vui lòng đăng nhập để xem giỏ hàng", "error"); return; }
            document.getElementById("headerCartOverlay")?.classList.add("open");
            document.getElementById("headerCartDrawer")?.classList.add("open");
            document.body.style.overflow = "hidden";
            _cartFetch();
        },
        close() {
            document.getElementById("headerCartOverlay")?.classList.remove("open");
            document.getElementById("headerCartDrawer")?.classList.remove("open");
            document.body.style.overflow = "";
        },
        async addToCart(bookId, bookTitle) {
            if (!isLoggedIn()) { showToast("Vui lòng đăng nhập để thêm vào giỏ hàng", "error"); return; }
            try {
                const res = await fetch(`${API}/cart`, { method: "POST", headers: authHeaders(), body: JSON.stringify({ bookId, quantity: 1 }) });
                const d = await res.json();
                if (!res.ok) { showToast(d.message || "Lỗi thêm vào giỏ", "error"); return; }
                showToast(`Đã thêm "${bookTitle}" vào giỏ hàng!`);
                _cartLoadCount();
            } catch { showToast("Lỗi kết nối", "error"); }
        },
        async updateQty(bookId, newQty) {
            try {
                const res = await fetch(`${API}/cart/${bookId}`, { method: "PUT", headers: authHeaders(), body: JSON.stringify({ quantity: newQty }) });
                const d = await res.json();
                if (!res.ok) { showToast(d.message || "Lỗi cập nhật", "error"); return; }
                _cartFetch();
            } catch { showToast("Lỗi kết nối", "error"); }
        },
        async removeItem(bookId) {
            try {
                const res = await fetch(`${API}/cart/${bookId}`, { method: "DELETE", headers: authHeaders() });
                if (!res.ok) { showToast("Lỗi xóa sách khỏi giỏ", "error"); return; }
                showToast("Đã xóa khỏi giỏ hàng");
                _cartFetch(); _cartLoadCount();
            } catch { showToast("Lỗi kết nối", "error"); }
        },
        async clearAll() {
            if (!confirm("Xóa toàn bộ giỏ hàng?")) return;
            try {
                await fetch(`${API}/cart`, { method: "DELETE", headers: authHeaders() });
                showToast("Đã xóa toàn bộ giỏ hàng");
                _cartFetch(); _cartLoadCount();
            } catch { showToast("Lỗi kết nối", "error"); }
        },
        checkout() { this.close(); window.location.href = "checkout.html"; },
        refreshBadge() { _cartLoadCount(); }
    };
    window.addToCart = (id, title) => window._headerCart.addToCart(id, title);

    /* ── LOGOUT ── */
    window.headerLogout = function () {
        sessionStorage.removeItem("token");
        window.location.href = HOME_PAGE;
    };

    /* ── CSS DEPS ── */
    function injectCSS(href, id) {
        if (document.getElementById(id)) return;
        const l = document.createElement("link");
        l.id = id; l.rel = "stylesheet"; l.href = href;
        document.head.appendChild(l);
    }
    injectCSS("https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css", "bootstrap-css");
    injectCSS("https://cdnjs.cloudflare.com/ajax/libs/bootstrap-icons/1.11.3/font/bootstrap-icons.min.css", "bootstrap-icons-css");
    injectCSS("https://fonts.googleapis.com/css2?family=DM+Sans:ital,wght@0,400;0,500;0,600;0,700&family=Playfair+Display:wght@500;600;700&display=swap", "google-fonts");

    // Chạy guard NGAY LẬP TỨC — trước khi DOM render để tránh flash nội dung sai
    guardPage();

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", renderHeader);
    else renderHeader();
})();