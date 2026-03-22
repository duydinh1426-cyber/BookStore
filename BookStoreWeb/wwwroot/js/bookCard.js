/**
 * bookCard.js – Component card sách dùng chung
 *
 * Cách dùng:
 *   <script src="../js/bookCard.js"></script>
 *
 *   window._bookCard.render(book, options)
 *   window._bookCard.renderList(book)
 *   window._bookCard.renderHorizontal(book)
 *
 * options (grid): { showRating: true }
 */

(function () {

    const BASE_URL = "https://localhost:7108";

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

    function fmt(n) {
        return new Intl.NumberFormat("vi-VN",
            { style: "currency", currency: "VND" }).format(n);
    }

    function esc(s) { return String(s).replace(/'/g, "\\'"); }

    function starsSmall(avg) {
        return Array.from({ length: 5 }, (_, i) =>
            `<i class="bi ${i < Math.round(avg) ? "bi-star-fill" : "bi-star"}"
                style="color:${i < Math.round(avg) ? "#f39c12" : "#ddd"};font-size:.65rem"></i>`
        ).join("");
    }

    /* ══════════════════════════════════════════
       GRID CARD  (dùng trong books.html + index.html)
    ══════════════════════════════════════════ */
    function renderGrid(b, opts = {}) {
        const id = gf(b, "bookId", "bookID", "BookId", "BookID");
        const stock = gf(b, "numberStock", "NumberStock") ?? 0;
        const sold = gf(b, "numberSold", "NumberSold") ?? 0;
        const cat = gf(b, "categoryName", "CategoryName") ?? "";
        const src = makeImgSrc(gf(b, "image", "Image") ?? "");
        const title = gf(b, "title", "Title") ?? "";
        const auth = gf(b, "author", "Author") ?? "";
        const price = gf(b, "price", "Price") ?? 0;
        const avg = parseFloat(gf(b, "avgRating", "AvgRating") ?? 0);
        const rcnt = parseInt(gf(b, "reviewCount", "ReviewCount") ?? 0);
        const showRating = opts.showRating !== false;

        return `
        <div class="book-card" onclick="openBookDetail(${id})">
            <div class="book-img-wrap">
                ${src
                ? `<img class="book-img" src="${src}" alt="${esc(title)}"
                           onerror="this.style.display='none';this.nextElementSibling.style.display='flex'">`
                : ""}
                <div class="book-img-ph" ${src ? 'style="display:none"' : ""}>
                    <i class="bi bi-book"></i><span>No Image</span>
                </div>
                ${stock === 0
                ? `<span class="book-badge out">Hết hàng</span>`
                : sold > 50 ? `<span class="book-badge">Hot</span>` : ""}
                <div class="book-overlay">
                    <button class="btn-ov">
                        <i class="bi bi-eye me-1"></i>Xem chi tiết
                    </button>
                </div>
            </div>
            <div class="book-info">
                <div class="book-cat">${cat}</div>
                <div class="book-title">${title}</div>
                <div class="book-author">Tác giả: ${auth}</div>
                ${showRating && rcnt > 0 ? `
                <div style="display:flex;align-items:center;gap:4px;margin:.2rem 0;flex-shrink:0">
                    ${starsSmall(avg)}
                    <span style="font-size:.68rem;color:#aaa">(${rcnt})</span>
                </div>` : `<div style="height:18px;flex-shrink:0"></div>`}
                <div class="book-bottom">
                    <span class="book-price">${fmt(price)}</span>
                    <button class="btn-cart ${stock === 0 ? "oos" : ""}"
                            onclick="event.stopPropagation();${stock > 0
                ? `window._headerCart.addToCart(${id},'${esc(title)}')`
                : "void(0)"}"
                            ${stock === 0 ? "disabled" : ""}>
                        <i class="bi ${stock > 0 ? "bi-bag-plus" : "bi-x-circle"}"></i>
                        ${stock > 0 ? "Thêm" : "Hết"}
                    </button>
                </div>
            </div>
        </div>`;
    }

    /* ══════════════════════════════════════════
       LIST ITEM  (dùng trong books.html list view)
    ══════════════════════════════════════════ */
    function renderList(b) {
        const id = gf(b, "bookId", "bookID", "BookId", "BookID");
        const stock = gf(b, "numberStock", "NumberStock") ?? 0;
        const sold = gf(b, "numberSold", "NumberSold") ?? 0;
        const cat = gf(b, "categoryName", "CategoryName") ?? "";
        const src = makeImgSrc(gf(b, "image", "Image") ?? "");
        const title = gf(b, "title", "Title") ?? "";
        const auth = gf(b, "author", "Author") ?? "";
        const price = gf(b, "price", "Price") ?? 0;
        const avg = parseFloat(gf(b, "avgRating", "AvgRating") ?? 0);
        const rcnt = parseInt(gf(b, "reviewCount", "ReviewCount") ?? 0);

        return `
        <div class="list-item" onclick="openBookDetail(${id})">
            <div class="list-img-wrap">
                ${src
                ? `<img class="list-img" src="${src}" alt="${esc(title)}"
                           onerror="this.style.display='none'">`
                : `<div style="width:100%;height:100%;display:flex;align-items:center;
                                   justify-content:center;color:#bbb;font-size:1.5rem">
                           <i class="bi bi-book"></i></div>`}
            </div>
            <div class="list-info">
                ${cat ? `<span class="book-cat">${cat}</span>` : ""}
                <div class="book-title" style="font-size:.86rem;margin:.15rem 0">${title}</div>
                <div class="book-author">Tác giả: ${auth}</div>
                ${rcnt > 0 ? `<div style="display:flex;align-items:center;gap:3px;margin:.2rem 0">
                    ${starsSmall(avg)}
                    <span style="font-size:.68rem;color:#aaa">(${rcnt})</span>
                </div>` : ""}
                <div style="font-size:.7rem;color:#aaa;margin:.15rem 0">
                    Còn ${stock} &nbsp;·&nbsp; Đã bán ${sold}
                </div>
                <div class="list-actions" onclick="event.stopPropagation()">
                    <span class="book-price">${fmt(price)}</span>
                    <button class="btn-cart ${stock === 0 ? "oos" : ""}"
                            onclick="${stock > 0
                ? `window._headerCart.addToCart(${id},'${esc(title)}')`
                : "void(0)"}"
                            ${stock === 0 ? "disabled" : ""}>
                        <i class="bi ${stock > 0 ? "bi-bag-plus" : "bi-x-circle"}"></i>
                        ${stock > 0 ? "Thêm vào giỏ" : "Hết hàng"}
                    </button>
                </div>
            </div>
        </div>`;
    }

    /* ══════════════════════════════════════════
       HORIZONTAL CARD  (dùng trong index.html scroll ngang)
    ══════════════════════════════════════════ */
    function renderHorizontal(b) {
        const id = gf(b, "bookId", "bookID", "BookId", "BookID");
        const stock = gf(b, "numberStock", "NumberStock") ?? 0;
        const src = makeImgSrc(gf(b, "image", "Image") ?? "");
        const title = gf(b, "title", "Title") ?? "";
        const auth = gf(b, "author", "Author") ?? "";
        const price = gf(b, "price", "Price") ?? 0;
        const avg = parseFloat(gf(b, "avgRating", "AvgRating") ?? 0);
        const rcnt = parseInt(gf(b, "reviewCount", "ReviewCount") ?? 0);

        return `
        <div class="h-card" onclick="openBookDetail(${id})">
            <div class="h-card-img">
                ${src
                ? `<img src="${src}" alt="${esc(title)}"
                           onerror="this.src='https://placehold.co/140x190?text=No+Image'">`
                : `<div class="h-card-img-ph"><i class="bi bi-book"></i></div>`}
                ${stock === 0
                ? `<span class="book-badge out" style="font-size:.55rem">Hết</span>`
                : ""}
            </div>
            <div class="h-card-info">
                <div class="h-card-title">${title}</div>
                <div class="h-card-author">${auth}</div>
                ${rcnt > 0 ? `<div style="display:flex;align-items:center;gap:3px;margin:.2rem 0">
                    ${starsSmall(avg)}
                    <span style="font-size:.63rem;color:#aaa">(${rcnt})</span>
                </div>` : ""}
                <div class="h-card-bottom">
                    <span class="h-card-price">${fmt(price)}</span>
                    <button class="h-card-btn ${stock === 0 ? "oos" : ""}"
                            onclick="event.stopPropagation();${stock > 0
                ? `window._headerCart.addToCart(${id},'${esc(title)}')`
                : "void(0)"}"
                            ${stock === 0 ? "disabled" : ""}>
                        <i class="bi bi-bag-plus"></i>
                    </button>
                </div>
            </div>
        </div>`;
    }

    /* ══════════════════════════════════════════
       PUBLIC API
    ══════════════════════════════════════════ */
    window._bookCard = {
        render: renderGrid,
        renderList: renderList,
        renderHorizontal: renderHorizontal
    };

})();