(function () {
    var hubConnection = null;
    var signalRLoading = null;

    function loadSignalR() {
        if (window.signalR) {
            return Promise.resolve();
        }
        if (signalRLoading) {
            return signalRLoading;
        }

        signalRLoading = new Promise(function (resolve, reject) {
            var script = document.createElement('script');
            script.src = 'https://cdn.jsdelivr.net/npm/@microsoft/signalr@7.0.5/dist/browser/signalr.min.js';
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });

        return signalRLoading;
    }

    function getActiveConversationToken() {
        var shell = document.querySelector('[data-current-conversation-token]');
        return shell ? shell.getAttribute('data-current-conversation-token') : '';
    }

    function formatBadge(count) {
        if (count > 99) return '99+';
        return String(Math.max(0, count || 0));
    }

    function updateUnreadBadges(count) {
        count = Math.max(0, count || 0);
        document.querySelectorAll('[data-unread-badge]').forEach(function (badge) {
            badge.textContent = formatBadge(count);
            badge.title = count + ' непрочетени';
            badge.hidden = !count || count <= 0;
        });

        if ('setAppBadge' in navigator && 'clearAppBadge' in navigator) {
            try {
                var badgeAction = count > 0 ? navigator.setAppBadge(count) : navigator.clearAppBadge();
                if (badgeAction && typeof badgeAction.catch === 'function') {
                    badgeAction.catch(function () { });
                }
            } catch {
                // Badging support varies between browsers and PWA modes.
            }
        }
    }

    function syncBadgeFromMarkup() {
        var badge = document.querySelector('[data-unread-badge]:not([hidden])');
        var raw = badge ? (badge.textContent || '').replace(/\D/g, '') : '';
        updateUnreadBadges(raw ? parseInt(raw, 10) : 0);
    }

    function ensureConversationBadge(row, count) {
        var badge = row.querySelector('[data-conversation-badge]');
        if (count > 0 && !badge) {
            badge = document.createElement('b');
            badge.setAttribute('data-conversation-badge', '');
            row.appendChild(badge);
        }
        if (badge) {
            badge.textContent = formatBadge(count);
            badge.hidden = count <= 0;
        }
    }

    function buildConversationRow(update) {
        var row = document.createElement('a');
        row.href = update.url || '/Messages';
        row.className = 'social-conversation-row' + (update.listKey === 'requests' ? ' is-request' : '');
        row.setAttribute('data-conversation-row', '');
        row.setAttribute('data-token', update.token);
        row.setAttribute('data-list-key', update.listKey || 'personal');
        row.setAttribute('data-unseen', update.unseenCount || 0);

        if (update.imageUrl) {
            var img = document.createElement('img');
            img.src = update.imageUrl;
            img.alt = update.name || '';
            row.appendChild(img);
        } else {
            var fallback = document.createElement('span');
            fallback.className = 'social-conversation-row__fallback';
            fallback.textContent = update.initial || '?';
            row.appendChild(fallback);
        }

        var copy = document.createElement('div');
        var name = document.createElement('strong');
        name.setAttribute('data-conversation-name', '');
        name.textContent = update.name || 'Conversation';
        var last = document.createElement('small');
        last.setAttribute('data-conversation-last', '');
        last.textContent = update.lastMessage || '';
        copy.appendChild(name);
        copy.appendChild(last);
        row.appendChild(copy);

        var time = document.createElement('time');
        time.setAttribute('data-conversation-time', '');
        time.textContent = update.updatedAt || '';
        row.appendChild(time);

        ensureConversationBadge(row, update.unseenCount || 0);
        return row;
    }

    function updateConversationList(update) {
        if (!update || !update.token) return;

        var activeToken = getActiveConversationToken();
        if (activeToken && activeToken.toLowerCase() === String(update.token).toLowerCase()) {
            var activeUnseenCount = update.unseenCount || 0;
            update.unseenCount = 0;
            if (typeof update.totalUnreadCount === 'number') {
                update.totalUnreadCount = Math.max(0, update.totalUnreadCount - activeUnseenCount);
            }
        }

        if (typeof update.totalUnreadCount === 'number') {
            updateUnreadBadges(update.totalUnreadCount);
        }

        var row = document.querySelector('[data-conversation-row][data-token="' + update.token + '"]');
        var list = document.querySelector('[data-message-list="' + (update.listKey || 'personal') + '"]');
        if (!row && list) {
            row = buildConversationRow(update);
            list.prepend(row);
            row.classList.add('is-live-new');
            var empty = list.querySelector('[data-message-empty]');
            if (empty) empty.hidden = true;
            return;
        }

        if (!row) return;

        row.setAttribute('data-unseen', update.unseenCount || 0);
        row.href = update.url || row.href;
        var name = row.querySelector('[data-conversation-name]');
        var last = row.querySelector('[data-conversation-last]');
        var time = row.querySelector('[data-conversation-time]');
        if (name && update.name) name.textContent = update.name;
        if (last) last.textContent = update.lastMessage || '';
        if (time) time.textContent = update.updatedAt || '';
        ensureConversationBadge(row, update.unseenCount || 0);
        row.classList.add('is-live-new');

        if (row.parentElement) {
            row.parentElement.prepend(row);
        }
        window.setTimeout(function () { row.classList.remove('is-live-new'); }, 900);
    }

    function connectRealtime() {
        loadSignalR()
            .then(function () {
                if (!window.signalR || hubConnection) return;
                hubConnection = new signalR.HubConnectionBuilder()
                    .withUrl('/hubs/chat')
                    .withAutomaticReconnect()
                    .build();

                hubConnection.on('ConversationUpdated', updateConversationList);
                hubConnection.start().catch(function () { });
            })
            .catch(function () { });
    }

    function urlBase64ToUint8Array(base64String) {
        var padding = '='.repeat((4 - base64String.length % 4) % 4);
        var base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        var rawData = window.atob(base64);
        var outputArray = new Uint8Array(rawData.length);
        for (var i = 0; i < rawData.length; i += 1) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray;
    }

    function getCsrfToken() {
        var meta = document.querySelector('meta[name="request-verification-token"]');
        return meta ? meta.getAttribute('content') : '';
    }

    function isInstalledPwa() {
        return window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
    }

    function subscribeForPush(registration, publicKey) {
        if (!registration || !registration.pushManager || !publicKey) return Promise.resolve();
        return registration.pushManager.getSubscription()
            .then(function (existing) {
                return existing || registration.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: urlBase64ToUint8Array(publicKey)
                });
            })
            .then(function (subscription) {
                var json = subscription.toJSON();
                return fetch('/Push/Subscribe', {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': getCsrfToken()
                    },
                    body: JSON.stringify({
                        endpoint: json.endpoint,
                        keys: {
                            p256dh: json.keys && json.keys.p256dh,
                            auth: json.keys && json.keys.auth
                        }
                    })
                });
            })
            .catch(function () { });
    }

    function showPushPrompt(registration, publicKey) {
        if (document.getElementById('evt-notification-prompt')) return;
        if (localStorage.getItem('evt_push_prompt_dismissed')) return;

        var prompt = document.createElement('div');
        prompt.id = 'evt-notification-prompt';
        prompt.className = 'evt-notification-prompt';
        prompt.innerHTML =
            '<div><strong>Известия за съобщения</strong><span>Разреши системни известия на телефона за нови съобщения.</span></div>' +
            '<button type="button" data-push-allow>Разреши</button>' +
            '<button type="button" data-push-dismiss aria-label="Не сега"><i class="bi bi-x-lg"></i></button>';
        document.body.appendChild(prompt);

        prompt.querySelector('[data-push-allow]').addEventListener('click', function () {
            window.Notification.requestPermission().then(function (permission) {
                if (permission === 'granted') {
                    subscribeForPush(registration, publicKey);
                }
                prompt.remove();
            });
        });
        prompt.querySelector('[data-push-dismiss]').addEventListener('click', function () {
            localStorage.setItem('evt_push_prompt_dismissed', String(Date.now()));
            prompt.remove();
        });
    }

    function setupPushNotifications() {
        if (!('serviceWorker' in navigator) || !('Notification' in window) || !('PushManager' in window)) {
            return;
        }

        navigator.serviceWorker.register('/sw.js')
            .then(function () { return navigator.serviceWorker.ready; })
            .then(function (registration) {
                return fetch('/Push/PublicKey', { credentials: 'same-origin', cache: 'no-store' })
                    .then(function (r) { return r.ok ? r.json() : null; })
                    .then(function (config) {
                        if (!config || !config.publicKey) return;
                        if (window.Notification.permission === 'granted') {
                            subscribeForPush(registration, config.publicKey);
                        } else if (window.Notification.permission === 'default' && isInstalledPwa()) {
                            showPushPrompt(registration, config.publicKey);
                        }
                    });
            })
            .catch(function () { });
    }

    syncBadgeFromMarkup();
    connectRealtime();
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', setupPushNotifications);
    } else {
        setupPushNotifications();
    }
})();
