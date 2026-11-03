// Fill the shared delete dialog from the clicked button
document.addEventListener('click', function (e) {
    const button = e.target.closest('[data-delete-url]');
    if (!button) return;

    document.getElementById('deleteForm').action = button.dataset.deleteUrl;
    document.getElementById('deleteName').textContent = button.dataset.deleteName;
    bootstrap.Modal.getOrCreateInstance(document.getElementById('deleteModal')).show();
});

// ---------- Notifications bell ----------
(function () {
    const list = document.getElementById('notifList');
    const badge = document.getElementById('notifBadge');
    if (!list || !badge) return;

    function showCount(count) {
        badge.textContent = count > 99 ? '99+' : count;
        badge.classList.toggle('d-none', count === 0);
        document.title = document.title.replace(/^\(\d+\+?\) /, '') ;
        if (count > 0) document.title = '(' + (count > 99 ? '99+' : count) + ') ' + document.title;
    }

    async function refresh() {
        try {
            const res = await fetch(list.dataset.countUrl, { headers: { 'Accept': 'application/json' } });
            if (res.ok) showCount((await res.json()).count);
        } catch { /* offline: try again next time */ }
    }

    document.getElementById('notifButton').addEventListener('show.bs.dropdown', async function () {
        try {
            const res = await fetch(list.dataset.url);
            if (res.ok) list.innerHTML = await res.text();
        } catch {
            list.innerHTML = '<div class="notif-empty">مش قادرين نجيب الإشعارات</div>';
        }
    });

    refresh();
    setInterval(refresh, 60000);
})();

// Slow posts (AI): show a spinner and stop double submits
document.addEventListener('submit', function (e) {
    const form = e.target.closest('form[data-busy]');
    if (!form) return;
    const button = form.querySelector('button[type=submit]');
    button.disabled = true;
    button.innerHTML = '<span class="spinner-border spinner-border-sm"></span> ' + form.dataset.busy;
});

// ---------- Location ----------

function getPosition() {
    return new Promise(function (resolve, reject) {
        if (!navigator.geolocation) return reject({ code: 0 });
        navigator.geolocation.getCurrentPosition(resolve, reject,
            { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 });
    });
}

function locationError(err) {
    if (!window.isSecureContext) return 'المتصفح مش بيدي الموقع غير على رابط https';
    switch (err && err.code) {
        case 1: return 'إنت رفضت إذن الموقع. اسمح للموقع من إعدادات المتصفح وجرّب تاني';
        case 2: return 'مش قادر يحدد مكانك. شغّل الـ GPS وجرّب تاني';
        case 3: return 'تحديد المكان أخد وقت طويل. جرّب تاني';
        default: return 'المتصفح ده مش بيدعم تحديد المكان';
    }
}

// Check-in / check-out: attach the current position before posting
document.addEventListener('submit', function (e) {
    const form = e.target.closest('form[data-geo]');
    if (!form || form.dataset.located) return;
    e.preventDefault();

    const button = form.querySelector('button[type=submit]');
    const label = button.innerHTML;
    button.disabled = true;
    button.innerHTML = '<span class="spinner-border spinner-border-sm"></span> بنحدد مكانك...';

    getPosition()
        .then(function (pos) {
            form.querySelector('[name=latitude]').value = pos.coords.latitude;
            form.querySelector('[name=longitude]').value = pos.coords.longitude;
            form.querySelector('[name=accuracy]').value = Math.round(pos.coords.accuracy);
        })
        .catch(function (err) {
            // Sent without a location; the server refuses it only if the branch requires one
            form.querySelector('[name=locationError]').value = locationError(err);
        })
        .finally(function () {
            form.dataset.located = '1';
            button.innerHTML = label;
            form.submit();
        });
});

// Branch form: fill the location from the device or a pasted Google Maps pair
(function () {
    const lat = document.getElementById('Latitude');
    const lng = document.getElementById('Longitude');
    if (!lat || !lng) return;

    const status = document.getElementById('location-status');
    const mapLink = document.getElementById('open-map');

    function refreshLink() {
        const has = lat.value && lng.value;
        mapLink.classList.toggle('disabled', !has);
        mapLink.href = has ? 'https://www.google.com/maps?q=' + lat.value + ',' + lng.value : '#';
    }

    function set(latitude, longitude) {
        lat.value = Number(latitude).toFixed(6);
        lng.value = Number(longitude).toFixed(6);
        refreshLink();
    }

    document.getElementById('paste-location').addEventListener('input', function () {
        const match = this.value.match(/(-?\d+(?:\.\d+)?)\s*[,،]\s*(-?\d+(?:\.\d+)?)/);
        if (match) {
            set(match[1], match[2]);
            status.className = 'small mt-2 text-success';
            status.textContent = 'اتحط الموقع';
        }
    });

    document.getElementById('use-my-location').addEventListener('click', function () {
        status.className = 'small mt-2 text-muted';
        status.textContent = 'بنحدد مكانك...';
        getPosition()
            .then(function (pos) {
                set(pos.coords.latitude, pos.coords.longitude);
                status.className = 'small mt-2 text-success';
                status.textContent = 'اتحط موقعك الحالي (الدقة حوالي ' + Math.round(pos.coords.accuracy) + ' متر)';
            })
            .catch(function (err) {
                status.className = 'small mt-2 text-danger';
                status.textContent = locationError(err);
            });
    });

    document.getElementById('clear-location').addEventListener('click', function () {
        lat.value = '';
        lng.value = '';
        refreshLink();
        status.textContent = '';
    });

    lat.addEventListener('input', refreshLink);
    lng.addEventListener('input', refreshLink);
    refreshLink();
})();
