(function () {
    'use strict';

    var SELECTOR = 'textarea[data-mentions], input[type="text"][data-mentions]';

    function debounce(fn, wait) {
        var t;
        return function () {
            var ctx = this, args = arguments;
            clearTimeout(t);
            t = setTimeout(function () { fn.apply(ctx, args); }, wait);
        };
    }

    function getMentionContext(field) {
        var caret = field.selectionStart;
        if (caret === null || caret === undefined) return null;
        var value = field.value || '';
        var head = value.substring(0, caret);
        var atIndex = head.lastIndexOf('@');
        if (atIndex < 0) return null;
        // Must be at start or preceded by whitespace / punctuation
        if (atIndex > 0) {
            var prev = head.charAt(atIndex - 1);
            if (/[A-Za-z0-9_]/.test(prev)) return null;
        }
        var fragment = head.substring(atIndex + 1);
        if (!/^[A-Za-z0-9._]*$/.test(fragment)) return null;
        if (fragment.length > 30) return null;
        return { atIndex: atIndex, fragment: fragment, caret: caret };
    }

    function buildPopup() {
        var pop = document.createElement('div');
        pop.className = 'evt-mention-pop';
        pop.setAttribute('role', 'listbox');
        pop.style.display = 'none';
        document.body.appendChild(pop);
        return pop;
    }

    function positionPopup(pop, field) {
        var rect = field.getBoundingClientRect();
        pop.style.left = (window.scrollX + rect.left) + 'px';
        pop.style.top = (window.scrollY + rect.bottom + 4) + 'px';
        pop.style.minWidth = Math.max(220, rect.width / 2) + 'px';
    }

    function renderResults(pop, items, onPick) {
        pop.innerHTML = '';
        if (!items || items.length === 0) {
            pop.style.display = 'none';
            return;
        }
        items.forEach(function (item, idx) {
            var row = document.createElement('button');
            row.type = 'button';
            row.className = 'evt-mention-pop__item' + (idx === 0 ? ' is-active' : '');
            row.dataset.index = String(idx);

            var name = document.createElement('span');
            name.className = 'evt-mention-pop__name';
            name.textContent = '@' + item.userName;

            var sub = document.createElement('span');
            sub.className = 'evt-mention-pop__sub';
            sub.textContent = (item.displayName || '').trim();

            row.appendChild(name);
            if (sub.textContent) row.appendChild(sub);
            row.addEventListener('mousedown', function (e) {
                e.preventDefault();
                onPick(item);
            });
            pop.appendChild(row);
        });
        pop.style.display = 'flex';
    }

    function moveActive(pop, delta) {
        var items = pop.querySelectorAll('.evt-mention-pop__item');
        if (items.length === 0) return;
        var current = pop.querySelector('.evt-mention-pop__item.is-active');
        var idx = current ? parseInt(current.dataset.index || '0', 10) : 0;
        idx = (idx + delta + items.length) % items.length;
        items.forEach(function (el) { el.classList.remove('is-active'); });
        items[idx].classList.add('is-active');
        items[idx].scrollIntoView({ block: 'nearest' });
    }

    function pickActive(pop) {
        var current = pop.querySelector('.evt-mention-pop__item.is-active');
        if (current) current.dispatchEvent(new MouseEvent('mousedown'));
    }

    function attach(field) {
        if (field.dataset.mentionsBound) return;
        field.dataset.mentionsBound = '1';

        var pop = buildPopup();
        var ctx = null;
        var lastFragment = null;

        function close() {
            pop.style.display = 'none';
            ctx = null;
            lastFragment = null;
        }

        function applyPick(item) {
            if (!ctx) return;
            var value = field.value;
            var before = value.substring(0, ctx.atIndex);
            var after = value.substring(ctx.caret);
            var insert = '@' + item.userName + ' ';
            field.value = before + insert + after;
            var pos = (before + insert).length;
            field.setSelectionRange(pos, pos);
            field.focus();
            close();
        }

        var fetchAndShow = debounce(function (fragment) {
            fetch('/api/mentions/search?q=' + encodeURIComponent(fragment), {
                credentials: 'same-origin',
                headers: { 'Accept': 'application/json' }
            })
                .then(function (r) { return r.ok ? r.json() : { items: [] }; })
                .then(function (data) {
                    if (lastFragment !== fragment) return; // outdated
                    renderResults(pop, data.items || [], applyPick);
                    if ((data.items || []).length > 0) positionPopup(pop, field);
                })
                .catch(function () { /* ignore */ });
        }, 150);

        field.addEventListener('input', function () {
            ctx = getMentionContext(field);
            if (!ctx) { close(); return; }
            lastFragment = ctx.fragment;
            if (ctx.fragment.length === 0) {
                pop.style.display = 'none';
                return;
            }
            fetchAndShow(ctx.fragment);
        });

        field.addEventListener('keydown', function (e) {
            if (pop.style.display === 'none') return;
            if (e.key === 'ArrowDown') { e.preventDefault(); moveActive(pop, 1); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); moveActive(pop, -1); }
            else if (e.key === 'Enter' || e.key === 'Tab') {
                if (pop.querySelector('.evt-mention-pop__item')) {
                    e.preventDefault();
                    pickActive(pop);
                }
            } else if (e.key === 'Escape') {
                close();
            }
        });

        field.addEventListener('blur', function () {
            // small timeout so a click on the popup can fire
            setTimeout(close, 120);
        });
    }

    function attachAll(root) {
        (root || document).querySelectorAll(SELECTOR).forEach(attach);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { attachAll(document); });
    } else {
        attachAll(document);
    }

    // Re-scan when DOM mutates (e.g. dynamically added composers)
    var observer = new MutationObserver(function (mutations) {
        mutations.forEach(function (m) {
            m.addedNodes.forEach(function (n) {
                if (n.nodeType !== 1) return;
                if (n.matches && n.matches(SELECTOR)) {
                    attach(n);
                } else if (n.querySelectorAll) {
                    attachAll(n);
                }
            });
        });
    });
    observer.observe(document.body, { childList: true, subtree: true });
})();
