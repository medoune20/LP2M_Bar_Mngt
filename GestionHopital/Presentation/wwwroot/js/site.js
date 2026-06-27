/* GestionHopital — JavaScript global */

document.addEventListener('DOMContentLoaded', function () {
    // Mobile menu
    const mobileBtn = document.getElementById('mobileMenuBtn');
    const overlay = document.getElementById('mobileOverlay');
    const drawer = document.getElementById('mobileDrawer');
    const closeBtn = document.getElementById('closeDrawer');

    function openDrawer() {
        drawer?.classList.add('show');
        overlay?.classList.add('show');
        document.body.style.overflow = 'hidden';
    }
    function closeDrawer() {
        drawer?.classList.remove('show');
        overlay?.classList.remove('show');
        document.body.style.overflow = '';
    }

    mobileBtn?.addEventListener('click', openDrawer);
    overlay?.addEventListener('click', closeDrawer);
    closeBtn?.addEventListener('click', closeDrawer);

    // Auto-dismiss alerts
    document.querySelectorAll('.alert-dismissible').forEach(function (alert) {
        setTimeout(function () {
            const bsAlert = bootstrap.Alert.getInstance(alert);
            if (bsAlert) bsAlert.close();
            else alert.classList.remove('show');
        }, 5000);
    });

    // Horloge temps réel dans le dashboard
    const clockEl = document.getElementById('gh-clock');
    if (clockEl) {
        function updateClock() {
            const now = new Date();
            clockEl.textContent = now.toLocaleTimeString('fr-FR');
        }
        updateClock();
        setInterval(updateClock, 1000);
    }
});
