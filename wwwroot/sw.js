const CACHE_VERSION = 'evento-v3';
const APP_SHELL = [
    '/',
    '/css/site.css',
    '/js/site.js',
    '/js/i18n.js',
    '/manifest.webmanifest',
    '/img/logo.png',
];
const TICKET_CACHE = 'evento-tickets-v1';

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_VERSION).then((cache) => cache.addAll(APP_SHELL).catch(() => null))
    );
    self.skipWaiting();
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys().then((keys) =>
            Promise.all(keys.filter((k) => k !== CACHE_VERSION && k !== TICKET_CACHE).map((k) => caches.delete(k)))
        )
    );
    self.clients.claim();
});

self.addEventListener('fetch', (event) => {
    const req = event.request;
    if (req.method !== 'GET') return;

    const url = new URL(req.url);
    if (url.origin !== self.location.origin) return;

    // Cache-first for ticket detail pages so users can show their QR offline at the door
    if (url.pathname.startsWith('/Tickets/Details/') ||
        url.pathname.startsWith('/Tickets/MyTickets')) {
        event.respondWith(
            fetch(req)
                .then((res) => {
                    if (res && res.ok) {
                        const clone = res.clone();
                        caches.open(TICKET_CACHE).then((c) => c.put(req, clone));
                    }
                    return res;
                })
                .catch(() => caches.match(req).then((m) => m || new Response('Offline', { status: 503 })))
        );
        return;
    }

    // Stale-while-revalidate for static assets
    if (url.pathname.startsWith('/css/') ||
        url.pathname.startsWith('/js/') ||
        url.pathname.startsWith('/img/') ||
        url.pathname.startsWith('/lib/')) {
        event.respondWith(
            caches.match(req).then((cached) => {
                const fetcher = fetch(req)
                    .then((res) => {
                        if (res && res.ok) {
                            const clone = res.clone();
                            caches.open(CACHE_VERSION).then((c) => c.put(req, clone));
                        }
                        return res;
                    })
                    .catch(() => cached);
                return cached || fetcher;
            })
        );
        return;
    }

    // Network-first for navigation
    if (req.mode === 'navigate') {
        event.respondWith(
            fetch(req)
                .then((res) => {
                    if (res && res.ok && res.status === 200) {
                        const clone = res.clone();
                        caches.open(CACHE_VERSION).then((c) => c.put(req, clone));
                    }
                    return res;
                })
                .catch(() => caches.match(req).then((m) => m || caches.match('/')))
        );
    }
});

self.addEventListener('push', (event) => {
    if (!event.data) return;

    let data = {};
    try {
        data = event.data.json();
    } catch {
        data = { title: 'Evento', body: event.data.text() };
    }

    const title = data.title || 'Evento';
    const options = {
        body: data.body || '',
        icon: data.icon || '/img/logo.svg',
        badge: data.badge || '/img/logo.svg',
        tag: data.tag || 'evento',
        data: { url: data.url || '/' },
        renotify: true,
        // Vibration pattern (Android/Chrome). iOS Safari ignores this but plays system sound.
        vibrate: data.vibrate || [200, 100, 200],
        // Make sure the system notification sound plays.
        silent: false,
        requireInteraction: data.requireInteraction === true,
    };

    const tasks = [self.registration.showNotification(title, options)];
    const hasBadgeCount = Object.prototype.hasOwnProperty.call(data, 'badgeCount') && data.badgeCount !== null;
    const badgeCount = Number(data.badgeCount || 0);
    if (hasBadgeCount && Number.isFinite(badgeCount) && typeof navigator !== 'undefined') {
        try {
            if (badgeCount > 0 && 'setAppBadge' in navigator) {
                tasks.push(navigator.setAppBadge(badgeCount));
            } else if ('clearAppBadge' in navigator) {
                tasks.push(navigator.clearAppBadge());
            }
        } catch {
            // App badging is browser/PWA-mode dependent.
        }
    }

    event.waitUntil(Promise.all(tasks));
});

self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    const url = event.notification.data && event.notification.data.url
        ? event.notification.data.url
        : '/';
    const targetUrl = new URL(url, self.location.origin).href;

    event.waitUntil(
        self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clients) => {
            for (const client of clients) {
                if ('focus' in client && client.url === targetUrl) {
                    return client.focus();
                }
            }

            if (self.clients.openWindow) {
                return self.clients.openWindow(targetUrl);
            }
        })
    );
});
