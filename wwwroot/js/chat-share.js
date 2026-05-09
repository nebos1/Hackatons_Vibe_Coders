(function () {
    var drawer;
    var content;
    var activeTrigger;

    function ensureDrawer() {
        if (drawer) return drawer;

        drawer = document.createElement('div');
        drawer.className = 'chat-share-drawer';
        drawer.hidden = true;
        drawer.innerHTML = [
            '<div class="chat-share-drawer__backdrop" data-chat-share-close></div>',
            '<section class="chat-share-drawer__panel" role="dialog" aria-modal="true" aria-labelledby="chat-share-title">',
            '  <header class="chat-share-drawer__header">',
            '    <div>',
            '      <span class="groove-kicker">Чат</span>',
            '      <h2 id="chat-share-title">Изпрати</h2>',
            '    </div>',
            '    <button class="chat-share-drawer__close" type="button" data-chat-share-close aria-label="Затвори"><i class="bi bi-x-lg"></i></button>',
            '  </header>',
            '  <div class="chat-share-drawer__content" data-chat-share-content></div>',
            '</section>'
        ].join('');

        document.body.appendChild(drawer);
        content = drawer.querySelector('[data-chat-share-content]');
        return drawer;
    }

    function setOpen(open) {
        ensureDrawer();
        drawer.hidden = false;
        document.body.classList.toggle('chat-share-open', open);
        requestAnimationFrame(function () {
            drawer.classList.toggle('is-open', open);
        });

        if (!open) {
            window.setTimeout(function () {
                if (!drawer.classList.contains('is-open')) {
                    drawer.hidden = true;
                    content.innerHTML = '';
                }
            }, 220);
        }
    }

    function setLoading() {
        content.innerHTML = '<div class="chat-share-loading"><span></span><span></span><span></span></div>';
    }

    function setDone(message) {
        content.innerHTML = '<div class="chat-share-done"><i class="bi bi-check2-circle"></i><strong>' + (message || 'Изпратено') + '</strong></div>';
        window.setTimeout(function () { setOpen(false); }, 900);
    }

    function applyTranslations() {
        if (window.EventoI18n && typeof window.EventoI18n.apply === 'function') {
            window.EventoI18n.apply(drawer);
        }
    }

    document.addEventListener('click', function (event) {
        var trigger = event.target.closest('[data-chat-share-open]');
        if (trigger) {
            event.preventDefault();
            activeTrigger = trigger;
            ensureDrawer();
            setOpen(true);
            setLoading();

            fetch(trigger.href, {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'text/html'
                },
                credentials: 'same-origin'
            })
                .then(function (response) {
                    if (!response.ok) throw new Error('Share dialog failed.');
                    return response.text();
                })
                .then(function (html) {
                    content.innerHTML = html;
                    applyTranslations();
                })
                .catch(function () {
                    window.location.href = trigger.href;
                });
            return;
        }

        if (event.target.closest('[data-chat-share-close]')) {
            event.preventDefault();
            setOpen(false);
        }
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape' && drawer && drawer.classList.contains('is-open')) {
            setOpen(false);
        }
    });

    document.addEventListener('submit', function (event) {
        var form = event.target.closest('[data-chat-share-form]');
        if (!form) return;

        event.preventDefault();
        if (form.dataset.submitting === 'true') return;
        form.dataset.submitting = 'true';

        var button = event.submitter || form.querySelector('button[type="submit"]');
        if (button) button.disabled = true;

        fetch(form.action, {
            method: 'POST',
            body: new FormData(form),
            headers: {
                'X-Requested-With': 'XMLHttpRequest',
                'Accept': 'application/json'
            },
            credentials: 'same-origin'
        })
            .then(function (response) {
                if (!response.ok) throw new Error('Share send failed.');
                return response.json();
            })
            .then(function (data) {
                setDone(data.message || 'Изпратено в чат.');
            })
            .catch(function () {
                HTMLFormElement.prototype.submit.call(form);
            })
            .finally(function () {
                form.dataset.submitting = 'false';
                if (button) button.disabled = false;
            });
    });

    window.addEventListener('pageshow', function () {
        if (activeTrigger && drawer && drawer.classList.contains('is-open')) {
            activeTrigger.focus({ preventScroll: true });
        }
    });
})();
