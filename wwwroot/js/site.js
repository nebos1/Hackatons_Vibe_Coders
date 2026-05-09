document.addEventListener('click', async function (event) {
    var button = event.target.closest('.social-share-button');
    if (!button) return;

    var url = button.getAttribute('data-share-url') || window.location.href;
    var title = button.getAttribute('data-share-title') || document.title;

    try {
        if (navigator.share) {
            await navigator.share({ title: title, url: url });
            return;
        }

        if (navigator.clipboard) {
            await navigator.clipboard.writeText(url);
            var original = button.innerHTML;
            var lang = (document.documentElement.getAttribute('lang') || localStorage.getItem('appLang') || 'bg').toLowerCase();
            button.innerHTML = '<i class="bi bi-check2"></i> ' + (lang === 'en' ? 'Copied' : 'Копирано');
            window.setTimeout(function () { button.innerHTML = original; }, 1600);
        }
    } catch (_) {
        // Sharing can be cancelled by the user; no UI error is needed.
    }
});

(function () {
    function isInteractiveTarget(target) {
        return !!target.closest('a, button, input, select, textarea, label, form, summary, details, [data-no-card-nav]');
    }

    document.addEventListener('click', function (event) {
        var card = event.target.closest('[data-card-href]');
        if (!card || event.defaultPrevented || isInteractiveTarget(event.target)) return;

        var href = card.getAttribute('data-card-href');
        if (href) {
            window.location.href = href;
        }
    });

    document.addEventListener('keydown', function (event) {
        var card = event.target.closest('[data-card-href]');
        if (!card || event.target !== card) return;
        if (event.key !== 'Enter' && event.key !== ' ') return;

        event.preventDefault();
        var href = card.getAttribute('data-card-href');
        if (href) {
            window.location.href = href;
        }
    });
})();

(function () {
    function setupQuickFilterJump() {
        var anchor = document.querySelector('[data-filter-anchor]');
        if (!anchor) return;

        anchor.style.scrollMarginTop = anchor.style.scrollMarginTop || '92px';

        var button = document.createElement('button');
        button.type = 'button';
        button.className = 'quick-filter-jump';
        button.innerHTML = '<i class="bi bi-sliders"></i><span>' + (anchor.getAttribute('data-filter-button-label') || 'Филтри') + '</span>';
        button.setAttribute('aria-label', anchor.getAttribute('data-filter-button-label') || 'Филтри');
        document.body.appendChild(button);

        var ticking = false;

        function update() {
            ticking = false;
            var rect = anchor.getBoundingClientRect();
            var farBelowFilters = rect.bottom < -24 && window.scrollY > 420;
            button.classList.toggle('is-visible', farBelowFilters);
        }

        function requestUpdate() {
            if (ticking) return;
            ticking = true;
            window.requestAnimationFrame(update);
        }

        button.addEventListener('click', function () {
            anchor.scrollIntoView({ behavior: 'smooth', block: 'start' });
        });

        window.addEventListener('scroll', requestUpdate, { passive: true });
        window.addEventListener('resize', requestUpdate);
        update();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', setupQuickFilterJump);
    } else {
        setupQuickFilterJump();
    }
})();

(function () {
    function setupMobileLazyInputs() {
        var isTouchLike = window.matchMedia &&
            window.matchMedia('(hover: none), (pointer: coarse)').matches;

        document.querySelectorAll('[data-mobile-lazy-input]').forEach(function (input) {
            if (!(input instanceof HTMLInputElement || input instanceof HTMLTextAreaElement)) return;
            if (!isTouchLike) {
                input.removeAttribute('readonly');
                return;
            }

            var userIntent = false;
            input.setAttribute('readonly', 'readonly');

            function activate() {
                userIntent = true;
                input.removeAttribute('readonly');
                window.setTimeout(function () {
                    try {
                        input.focus({ preventScroll: true });
                    } catch (_) {
                        input.focus();
                    }
                }, 0);
            }

            input.addEventListener('pointerdown', activate, { once: true });
            input.addEventListener('touchstart', activate, { once: true, passive: true });
            input.addEventListener('focus', function () {
                if (!userIntent && input.hasAttribute('readonly')) {
                    window.setTimeout(function () { input.blur(); }, 0);
                }
            });

            window.addEventListener('pageshow', function () {
                if (document.activeElement === input && !userIntent) {
                    input.blur();
                }
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', setupMobileLazyInputs);
    } else {
        setupMobileLazyInputs();
    }
})();

(function () {
    function normalize(value) {
        return (value || '').toString().toLowerCase().trim();
    }

    function setupMessageSearch() {
        var root = document.querySelector('[data-messages-index]');
        if (!root) return;

        var input = root.querySelector('[data-message-search-input]');
        var clear = root.querySelector('[data-message-search-clear]');
        var empty = root.querySelector('[data-message-search-empty]');
        var tabs = Array.prototype.slice.call(root.querySelectorAll('[data-message-tab]'));
        var panels = Array.prototype.slice.call(root.querySelectorAll('[data-message-tab-panel]'));
        var pageFilterButtons = Array.prototype.slice.call(root.querySelectorAll('[data-message-page-filter]'));
        if (!input) return;

        root.querySelectorAll('[data-message-empty]').forEach(function (panelEmpty) {
            panelEmpty.setAttribute('data-message-search-original-hidden', panelEmpty.hidden ? 'true' : 'false');
        });

        function getActiveTab() {
            var active = root.querySelector('[data-message-tab].is-active');
            return active ? active.getAttribute('data-message-tab') : 'personal';
        }

        function getActivePageFilter() {
            var active = root.querySelector('[data-message-page-filter].is-active');
            return active ? normalize(active.getAttribute('data-message-page-filter')) : '';
        }

        function applyFilter() {
            var query = normalize(input.value);
            var rows = Array.prototype.slice.call(root.querySelectorAll('[data-conversation-row]'));
            var activeTab = getActiveTab();
            var activePage = getActivePageFilter();
            var visibleCount = 0;

            if (clear) {
                clear.hidden = !query;
            }

            rows.forEach(function (row) {
                var haystack = normalize(row.getAttribute('data-message-search') || row.textContent);
                var rowTab = row.getAttribute('data-list-key') || 'personal';
                var rowPage = normalize(row.getAttribute('data-page-name'));
                var isInTab = rowTab === activeTab;
                var isInPage = activeTab !== 'page' || !activePage || rowPage === activePage;
                var isMatch = isInTab && isInPage && (!query || haystack.indexOf(query) !== -1);
                row.hidden = !isMatch;
                if (isMatch) visibleCount += 1;
            });

            root.querySelectorAll('[data-message-empty]').forEach(function (panelEmpty) {
                var list = panelEmpty.closest('[data-message-list]');
                var hasRows = !!(list && list.querySelector('[data-conversation-row]'));
                if (query) {
                    panelEmpty.hidden = true;
                } else if (hasRows) {
                    panelEmpty.hidden = true;
                } else {
                    panelEmpty.hidden = panelEmpty.getAttribute('data-message-search-original-hidden') === 'true';
                }
            });

            if (empty) {
                empty.hidden = !query || visibleCount > 0;
            }
        }

        function setTab(tab) {
            tabs.forEach(function (button) {
                button.classList.toggle('is-active', button.getAttribute('data-message-tab') === tab);
            });
            panels.forEach(function (panel) {
                panel.hidden = panel.getAttribute('data-message-tab-panel') !== tab;
            });
            applyFilter();
        }

        tabs.forEach(function (button) {
            button.addEventListener('click', function () {
                setTab(button.getAttribute('data-message-tab') || 'personal');
            });
        });

        pageFilterButtons.forEach(function (button) {
            button.addEventListener('click', function () {
                pageFilterButtons.forEach(function (b) { b.classList.remove('is-active'); });
                button.classList.add('is-active');
                applyFilter();
            });
        });

        input.addEventListener('input', applyFilter);

        if (clear) {
            clear.addEventListener('click', function () {
                input.value = '';
                applyFilter();
                input.focus();
            });
        }

        var observer = new MutationObserver(function (mutations) {
            if (!normalize(input.value)) return;
            if (mutations.some(function (mutation) { return mutation.addedNodes.length || mutation.removedNodes.length; })) {
                applyFilter();
            }
        });
        observer.observe(root, { childList: true, subtree: true });
        applyFilter();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', setupMessageSearch);
    } else {
        setupMessageSearch();
    }
})();

(function () {
    function showToast(message) {
        if (!message) return;
        var toast = document.createElement('div');
        toast.className = 'groove-toast';
        toast.textContent = message;
        document.body.appendChild(toast);
        requestAnimationFrame(function () { toast.classList.add('is-visible'); });
        window.setTimeout(function () {
            toast.classList.remove('is-visible');
            window.setTimeout(function () { toast.remove(); }, 220);
        }, 1800);
    }

    var pending = sessionStorage.getItem('groove:toast');
    if (pending) {
        sessionStorage.removeItem('groove:toast');
        showToast(pending);
    }

    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!form || !form.getAttribute) return;

        var confirmButton = form.querySelector('[data-confirm-key]');
        if (confirmButton) {
            var key = confirmButton.getAttribute('data-confirm-key');
            var lang = (document.documentElement.getAttribute('lang') || localStorage.getItem('appLang') || 'bg').toLowerCase();
            var messages = {
                'workspace.delete.confirm': {
                    bg: 'Сигурен ли си, че искаш да изтриеш този workspace? Публичните страници под него ще бъдат спрени, но историята за билети и плащания ще остане запазена.',
                    en: 'Are you sure you want to delete this workspace? Public pages under it will be disabled, but ticket and payment history will stay preserved.'
                }
            };
            var entry = messages[key];
            if (entry && !window.confirm(entry[lang] || entry.bg)) {
                event.preventDefault();
                return;
            }
        }

        if (form.matches('[data-comment-like-form]')) return;

        var action = (form.getAttribute('action') || '').toLowerCase();
        var isEn = ((document.documentElement.getAttribute('lang') || localStorage.getItem('appLang') || 'bg').toLowerCase() === 'en');
        if (action.indexOf('/like') >= 0) sessionStorage.setItem('groove:toast', isEn ? 'Liked' : 'Харесано');
        if (action.indexOf('/save') >= 0) sessionStorage.setItem('groove:toast', isEn ? 'Saved' : 'Запазено');
        if (action.indexOf('/follow') >= 0) sessionStorage.setItem('groove:toast', isEn ? 'Following updated' : 'Следването е обновено');
        if (action.indexOf('/shareevent') >= 0) sessionStorage.setItem('groove:toast', isEn ? 'Shared on your profile' : 'Споделено в профила');
        if (action.indexOf('/pinevent') >= 0) sessionStorage.setItem('groove:toast', isEn ? 'Pinned to your profile' : 'Закачено в профила');
    });
})();

(function () {
    document.addEventListener('click', function (event) {
        var target = event.target.closest('.evt-card__action, .evt-card__save, .groove-button, .btn');
        if (!target || target.disabled) return;
        target.classList.remove('ui-pop');
        void target.offsetWidth;
        target.classList.add('ui-pop');
        window.setTimeout(function () { target.classList.remove('ui-pop'); }, 320);
    });
})();

(function () {
    function getCommentContainer(item) {
        return item.closest('.social-comment-replies') || item.closest('.groove-list-stack');
    }

    function sortAndAnimate(container) {
        if (!container) return;
        var selector = container.classList.contains('social-comment-replies')
            ? ':scope > .social-comment-reply-item'
            : ':scope > .social-comment-item';
        var items = Array.prototype.slice.call(container.querySelectorAll(selector));
        if (items.length < 2) return;

        var firstRects = new Map();
        items.forEach(function (item) {
            firstRects.set(item, item.getBoundingClientRect());
        });

        items.sort(function (a, b) {
            var likeDiff = parseInt(b.dataset.commentLikes || '0', 10) - parseInt(a.dataset.commentLikes || '0', 10);
            if (likeDiff !== 0) return likeDiff;
            return parseInt(b.dataset.commentCreated || '0', 10) - parseInt(a.dataset.commentCreated || '0', 10);
        });

        items.forEach(function (item) { container.appendChild(item); });

        if (!Element.prototype.animate || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

        requestAnimationFrame(function () {
            items.forEach(function (item) {
                var first = firstRects.get(item);
                var last = item.getBoundingClientRect();
                if (!first) return;
                var dx = first.left - last.left;
                var dy = first.top - last.top;
                if (!dx && !dy) return;
                item.animate([
                    { transform: 'translate(' + dx + 'px, ' + dy + 'px)' },
                    { transform: 'translate(0, 0)' }
                ], {
                    duration: 260,
                    easing: 'cubic-bezier(.2,.8,.2,1)'
                });
            });
        });
    }

    function updateLikeUi(form, data) {
        var item = form.closest('[data-comment-item]');
        var button = form.querySelector('.comment-like-button');
        if (!item || !button) return;

        var count = button.querySelector('[data-comment-like-count]') || button.querySelector('span');
        var icon = button.querySelector('i');
        item.dataset.commentLikes = String(data.likesCount || 0);
        if (count) count.textContent = String(data.likesCount || 0);

        button.classList.toggle('is-liked', !!data.liked);
        if (icon) {
            icon.classList.toggle('bi-heart-fill', !!data.liked);
            icon.classList.toggle('bi-heart', !data.liked);
        }

        if (data.actionUrl) form.setAttribute('action', data.actionUrl);
        if (data.mode) form.setAttribute('data-comment-like-form', data.mode);

        button.classList.remove('ui-pop');
        void button.offsetWidth;
        button.classList.add('ui-pop');
        window.setTimeout(function () { button.classList.remove('ui-pop'); }, 320);

        sortAndAnimate(getCommentContainer(item));
    }

    document.addEventListener('submit', async function (event) {
        var form = event.target.closest('[data-comment-like-form]');
        if (!form) return;

        event.preventDefault();
        if (form.dataset.submitting === 'true') return;
        form.dataset.submitting = 'true';

        try {
            var response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (!response.ok) throw new Error('Comment like failed.');
            updateLikeUi(form, await response.json());
        } catch (_) {
            form.submit();
        } finally {
            form.dataset.submitting = 'false';
        }
    });
})();

(function () {
    function revealItems(list, count) {
        var items = Array.prototype.slice.call(list.querySelectorAll('[data-progressive-item]'));
        var visible = Math.min(count, items.length);

        items.forEach(function (item, index) {
            item.hidden = index >= visible;
            item.classList.toggle('is-progressive-hidden', index >= visible);
        });

        return visible;
    }

    document.querySelectorAll('[data-progressive-list]').forEach(function (list) {
        var initial = parseInt(list.getAttribute('data-initial-count') || '6', 10);
        var visible = revealItems(list, initial);
        list.setAttribute('data-visible-count', visible.toString());
    });

    document.addEventListener('click', function (event) {
        var button = event.target.closest('[data-progressive-more]');
        if (!button) return;

        var container = button.closest('.evt-load-more');
        var list = null;
        var current = container ? container.previousElementSibling : null;
        while (current && !list) {
            if (current.matches && current.matches('[data-progressive-list]')) {
                list = current;
                break;
            }
            current = current.previousElementSibling;
        }
        if (!list) return;

        event.preventDefault();
        var items = list.querySelectorAll('[data-progressive-item]');
        var currentVisible = parseInt(list.getAttribute('data-visible-count') || '0', 10);
        var step = parseInt(list.getAttribute('data-step-count') || '4', 10);
        var nextVisible = revealItems(list, currentVisible + step);
        list.setAttribute('data-visible-count', nextVisible.toString());

        if (nextVisible >= items.length && container) {
            container.hidden = true;
        }
    });

    document.addEventListener('click', async function (event) {
        var button = event.target.closest('[data-load-more-url]');
        if (!button) return;

        var wrap = button.closest('[data-home-event-load-more]');
        var grid = document.getElementById('event-cards-grid');
        if (!wrap || !grid) return;

        event.preventDefault();

        var page = parseInt(wrap.getAttribute('data-page') || '1', 10);
        var pageSize = parseInt(wrap.getAttribute('data-page-size') || '12', 10);
        var total = parseInt(wrap.getAttribute('data-total') || '0', 10);
        var url = button.getAttribute('data-load-more-url');
        if (!url || button.disabled) return;

        button.disabled = true;
        button.classList.add('is-loading');

        try {
            var response = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok) throw new Error('Failed to load more events.');
            var html = await response.text();
            var template = document.createElement('template');
            template.innerHTML = html.trim();
            var appended = template.content.querySelectorAll('.evt-grid__cell').length;
            if (appended > 0) {
                grid.appendChild(template.content);
            }

            var nextPage = page + 1;
            var loaded = grid.querySelectorAll('.evt-grid__cell').length;
            var loadedLabel = wrap.querySelector('[data-loaded-count]');
            if (loadedLabel) {
                loadedLabel.textContent = Math.min(loaded, total).toString();
            }

            if (appended < pageSize || loaded >= total) {
                wrap.hidden = true;
                return;
            }

            wrap.setAttribute('data-page', nextPage.toString());
            var nextUrl = new URL(url, window.location.origin);
            nextUrl.searchParams.set('page', (nextPage + 1).toString());
            button.setAttribute('data-load-more-url', nextUrl.pathname + nextUrl.search);
        } catch (_) {
            window.location.href = url.replace('partial=True', 'partial=False').replace('partial=true', 'partial=false');
        } finally {
            button.disabled = false;
            button.classList.remove('is-loading');
        }
    });
})();
