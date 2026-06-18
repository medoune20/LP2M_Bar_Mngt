function initQrCodes() {
    if (typeof QRCode === 'undefined') {
        return;
    }

    document.querySelectorAll('.qr-mini').forEach(function (el) {
        if (el.dataset.done === '1') {
            return;
        }

        const value = el.getAttribute('data-qr') || '';
        if (!value) {
            return;
        }

        el.innerHTML = '';
        new QRCode(el, {
            text: value,
            width: 46,
            height: 46,
            correctLevel: QRCode.CorrectLevel.M
        });
        el.dataset.done = '1';
    });
}

document.addEventListener('DOMContentLoaded', initQrCodes);
