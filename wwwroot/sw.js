const CACHE_VERSION = 'evento-v1';
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
