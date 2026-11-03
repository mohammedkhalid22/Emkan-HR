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

// ---------- Install as an app (Android / desktop Chrome and Edge) ----------
(function () {
    const button = document.getElementById('installApp');
    if (!button) return;
    let offer = null;

    window.addEventListener('beforeinstallprompt', function (e) {
        e.preventDefault();
        offer = e;
        button.classList.remove('d-none');
    });
    button.addEventListener('click', async function () {
        if (!offer) return;
        offer.prompt();
        await offer.userChoice;
        offer = null;
        button.classList.add('d-none');
    });
    window.addEventListener('appinstalled', function () { button.classList.add('d-none'); });
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

// ---------- Selfie ----------

// A phone photo is several MB: shrink it to 640px JPEG before it goes up
function shrinkPhoto(file, maxSide) {
    return new Promise(function (resolve, reject) {
        const img = new Image();
        img.onload = function () {
            const scale = Math.min(1, maxSide / Math.max(img.width, img.height));
            const canvas = document.createElement('canvas');
            canvas.width = Math.round(img.width * scale);
            canvas.height = Math.round(img.height * scale);
            canvas.getContext('2d').drawImage(img, 0, 0, canvas.width, canvas.height);
            URL.revokeObjectURL(img.src);
            canvas.toBlob(function (blob) {
                blob ? resolve(new File([blob], 'selfie.jpg', { type: 'image/jpeg' })) : reject();
            }, 'image/jpeg', 0.8);
        };
        img.onerror = reject;
        img.src = URL.createObjectURL(file);
    });
}

// Check-in / check-out with a selfie: open the camera first, then let the location step below post.
// Registered before the location handler so it runs first.
document.addEventListener('submit', function (e) {
    const form = e.target.closest('form[data-selfie]');
    if (!form || form.dataset.photo) return;
    e.preventDefault();
    e.stopImmediatePropagation();

    const input = form.querySelector('input[name=selfie]');
    input.onchange = function () {
        const file = input.files[0];
        if (!file) return;
        const done = function (photo) {
            const list = new DataTransfer();
            list.items.add(photo);
            input.files = list.files;
            form.dataset.photo = '1';
            form.requestSubmit();
        };
        // If the browser can't redraw it, send the original (the server still checks type and size)
        shrinkPhoto(file, 640).then(done).catch(function () { done(file); });
    };
    input.click();
});

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
