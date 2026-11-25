// โหลดข้อมูลตะกร้าจาก LocalStorage
let cart = JSON.parse(localStorage.getItem('restaurant_cart')) || [];

document.addEventListener("DOMContentLoaded", function () {
    updateCartBadge();
    renderCart();

    // ดักจับการกดปุ่ม "เลือก" บนการ์ดอาหาร
    document.querySelectorAll('.btn-add-cart').forEach(button => {
        button.addEventListener('click', function () {
            const id = this.getAttribute('data-id');
            const name = this.getAttribute('data-name');
            const price = parseFloat(this.getAttribute('data-price'));

            // เพิ่มเข้าตะกร้าและอัปเดตตัวเลขสีแดง (ไม่สั่งเปิด Sidebar)
            addToCart(id, name, price);

            // Effect เล็กน้อยเมื่อกดปุ่มเลือก (ปุ่มกะพริบยืนยัน)
            const originalText = this.innerText;
            this.innerText = "✓ เพิ่มแล้ว";
            this.classList.add("bg-success");
            setTimeout(() => {
                this.innerText = originalText;
                this.classList.remove("bg-success");
            }, 600);
        });
    });
});

// ฟังก์ชันเพิ่มอาหารเข้าตะกร้า
function addToCart(id, name, price) {
    const existingItem = cart.find(item => item.id === id);
    if (existingItem) {
        existingItem.quantity += 1;
    } else {
        cart.push({ id: id, name: name, price: price, quantity: 1 });
    }
    saveAndRender();
}

// ฟังก์ชันปรับเพิ่ม-ลดจำนวน (+ / -) ภายใน Sidebar
function changeQuantity(id, delta) {
    const itemIndex = cart.findIndex(item => item.id === id);
    if (itemIndex > -1) {
        cart[itemIndex].quantity += delta;
        if (cart[itemIndex].quantity <= 0) {
            cart.splice(itemIndex, 1);
        }
    }
    saveAndRender();
}

// บันทึกลง LocalStorage พร้อมอัปเดต Badge และรายการใน Sidebar
function saveAndRender() {
    localStorage.setItem('restaurant_cart', JSON.stringify(cart));
    updateCartBadge();
    renderCart();
}

// ฟังก์ชันอัปเดตตัวเลขสีแดงบน Navbar
function updateCartBadge() {
    const badge = document.getElementById('cartBadge');
    if (!badge) return;

    // คำนวณจำนวนชิ้นรวมทั้งหมดในตะกร้า
    const totalCount = cart.reduce((sum, item) => sum + item.quantity, 0);

    if (totalCount > 0) {
        badge.innerText = totalCount > 99 ? '99+' : totalCount;
        badge.style.display = 'inline-block';
    } else {
        badge.style.display = 'none'; // ซ่อนถ้าไม่มีของในตะกร้า
    }
}

// ฟังก์ชันเรนเดอร์รายการสินค้าใน Sidebar (Offcanvas)
function renderCart() {
    const container = document.getElementById('cartItemsContainer');
    if (!container) return;

    if (cart.length === 0) {
        container.innerHTML = `<div class="text-center text-muted my-5">ไม่มีรายการอาหารในตะกร้า</div>`;
        return;
    }

    container.innerHTML = cart.map(item => `
        <div class="d-flex flex-column gap-1 mb-3">
            <div class="d-flex justify-content-between align-items-baseline">
                <span class="cart-item-name text-truncate">${item.name}</span>
                <span class="cart-item-price">${item.price * item.quantity} บาท</span>
            </div>
            <div class="d-flex justify-content-end align-items-center gap-2">
                <button type="button" class="btn-qty" onclick="changeQuantity('${item.id}', -1)">
                    <i class="bi bi-dash-circle-fill"></i>
                </button>
                <span class="qty-number">${item.quantity}</span>
                <button type="button" class="btn-qty" onclick="changeQuantity('${item.id}', 1)">
                    <i class="bi bi-plus-circle-fill"></i>
                </button>
            </div>
        </div>
    `).join('');
}