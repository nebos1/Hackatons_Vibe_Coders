(function () {
    var drawer;
    var content;
    var titleCount;
    var currentUrl;
    var currentPostId;
    var activeTrigger;

    function ensureDrawer() {
        if (drawer) return drawer;

        drawer = document.createElement('div');
        drawer.className = 'post-comments-drawer';
        drawer.hidden = true;
        drawer.innerHTML = [
            '<div class="post-comments-drawer__backdrop" data-comments-close></div>',
            '<section class="post-comments-drawer__panel" role="dialog" aria-modal="true" aria-labelledby="post-comments-drawer-title">',
            '  <header class="post-comments-drawer__header">',
            '    <div>',
            '      <span class="groove-kicker">Коментари</span>',
            '      <h2 id="post-comments-drawer-title">Разговор <span data-comments-title-count></span></h2>',
            '    </div>',
            '    <button class="post-comments-drawer__close" type="button" data-comments-close aria-label="Затвори"><i class="bi bi-x-lg"></i></button>',
            '  </header>',
            '  <div class="post-comments-drawer__content" data-comments-content></div>',
            '</section>'
        ].join('');

        document.body.appendChild(drawer);
        content = drawer.querySelector('[data-comments-content]');
        titleCount = drawer.querySelector('[data-comments-title-count]');
        return drawer;
    }

    function setOpen(open) {
        ensureDrawer();
        drawer.hidden = false;
        document.body.classList.toggle('post-comments-open', open);
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
        content.innerHTML = '<div class="post-comments-loading"><span></span><span></span><span></span></div>';
    }

    function applyTranslations() {
        if (window.EventoI18n && typeof window.EventoI18n.apply === 'function') {
            window.EventoI18n.apply(drawer);
        }
    }

    function updateCardCount(postId, count) {
        document.querySelectorAll('[data-post-card][data-post-id="' + postId + '"] [data-post-comment-count]').forEach(function (el) {
            el.textContent = String(count);
        });
        if (titleCount) {
            titleCount.textContent = '(' + count + ')';
        }
    }

    async function loadComments(url, postId) {
        currentUrl = url;
        currentPostId = postId;
        setLoading();

        var response = await fetch(url, {
            headers: {
                'X-Requested-With': 'XMLHttpRequest',
                'Accept': 'text/html'
            },
            credentials: 'same-origin'
        });

        if (!response.ok) throw new Error('Failed to load comments.');
        content.innerHTML = await response.text();
        applyTranslations();
    }

    function fallbackToDetails(postId) {
        window.location.href = '/flow/p/' + encodeURIComponent(postId) + '#comments';
    }

    document.addEventListener('click', function (event) {
        var openButton = event.target.closest('[data-post-comments-open]');
        if (openButton) {
            event.preventDefault();
            ensureDrawer();
            activeTrigger = openButton;
            var url = openButton.getAttribute('data-comments-url');
            var postId = openButton.getAttribute('data-post-id');
            var count = openButton.querySelector('[data-post-comment-count]');
            if (titleCount) {
                titleCount.textContent = count ? '(' + count.textContent.trim() + ')' : '';
            }

            setOpen(true);
            loadComments(url, postId).catch(function () {
                fallbackToDetails(postId);
            });
            return;
        }

        if (event.target.closest('[data-comments-close]')) {
            event.preventDefault();
            setOpen(false);
        }
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape' && drawer && drawer.classList.contains('is-open')) {
            setOpen(false);
        }
    });

    document.addEventListener('submit', async function (event) {
        var form = event.target.closest('[data-post-comment-form]');
        if (!form) return;

        event.preventDefault();
        if (form.dataset.submitting === 'true') return;
        form.dataset.submitting = 'true';

        var button = event.submitter || form.querySelector('button[type="submit"]');
        if (button) button.disabled = true;

        try {
            var response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                },
                credentials: 'same-origin'
            });

            if (!response.ok) throw new Error('Comment submit failed.');
            var data = await response.json();
            if (data.commentsCount !== undefined && data.postId !== undefined) {
                updateCardCount(data.postId, data.commentsCount);
            }

            var reloadUrl = data.commentsUrl || currentUrl;
            if (reloadUrl) {
                await loadComments(reloadUrl, data.postId || currentPostId);
            }
        } catch (_) {
            if (currentPostId) {
                fallbackToDetails(currentPostId);
            } else {
                HTMLFormElement.prototype.submit.call(form);
            }
        } finally {
            form.dataset.submitting = 'false';
            if (button) button.disabled = false;
        }
    });

    window.addEventListener('pageshow', function () {
        if (activeTrigger && drawer && drawer.classList.contains('is-open')) {
            activeTrigger.focus({ preventScroll: true });
        }
    });
})();
