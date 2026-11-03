// Emkan HR service worker: makes the app installable and keeps the look (CSS, JS, icons) cached.
// Pages are never cached: they hold personal data and must always be fresh. Offline, a page
// request shows offline.html instead of the browser's error.
const CACHE = 'emkan-static-v1';
const OFFLINE = '/offline.html';

self.addEventListener('install', function (event) {
    event.waitUntil(caches.open(CACHE).then(function (cache) {
        return cache.addAll([OFFLINE, '/img/emkan-mark.svg', '/img/icon-192.png']);
    }));
    self.skipWaiting();
});

self.addEventListener('activate', function (event) {
    event.waitUntil(caches.keys().then(function (keys) {
        return Promise.all(keys.filter(function (k) { return k !== CACHE; }).map(function (k) { return caches.delete(k); }));
    }).then(function () { return self.clients.claim(); }));
});

function isStatic(url) {
    return url.origin === self.location.origin && /^\/(css|js|lib|img)\//.test(url.pathname)
        || url.hostname === 'fonts.googleapis.com' || url.hostname === 'fonts.gstatic.com' || url.hostname === 'cdn.jsdelivr.net';
}

self.addEventListener('fetch', function (event) {
    const request = event.request;
    if (request.method !== 'GET') return;

    if (request.mode === 'navigate') {
        event.respondWith(fetch(request).catch(function () { return caches.match(OFFLINE); }));
        return;
    }

    const url = new URL(request.url);
    if (!isStatic(url)) return;

    // Cached copy right away, refreshed in the background
    event.respondWith(caches.open(CACHE).then(function (cache) {
        return cache.match(request).then(function (cached) {
            const fresh = fetch(request).then(function (response) {
                if (response.ok || response.type === 'opaque') cache.put(request, response.clone());
                return response;
            }).catch(function () { return cached; });
            return cached || fresh;
        });
    }));
});
