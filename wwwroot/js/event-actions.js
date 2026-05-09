(function () {
    function actionPath(form) {
        try {
            return new URL(form.action || form.getAttribute('action') || '', window.location.origin).pathname.toLowerCase();
        } catch (_) {
            return (form.getAttribute('action') || '').toLowerCase();
        }
    }

    function isEventForm(form) {
        if (form.querySelector('[data-event-action]')) return true;
        var path = actionPath(form);
        return /\/events\/(like|unlike|save|unsave|attendance)\//i.test(path);
    }

    function isPostForm(form) {
        if (form.querySelector('[data-post-action]')) return true;
        var path = actionPath(form);
        return /\/posts\/(like|unlike|save|unsave)\//i.test(path);
    }

    function replaceAction(form, controller, fromActions, nextAction) {
        var action = form.getAttribute('action') || form.action || '';
        var lowerActions = fromActions.map(function (a) { return a.toLowerCase(); });
        var wasAbsolute = /^[a-z][a-z0-9+.-]*:/i.test(action);

        try {
            var url = new URL(action, window.location.origin);
            var parts = url.pathname.split('/');
            var changed = false;
            for (var i = 0; i < parts.length; i++) {
                var lower = parts[i].toLowerCase();
                if (lowerActions.indexOf(lower) !== -1) {
                    parts[i] = parts[i] === lower ? nextAction.toLowerCase() : nextAction;
                    changed = true;
                }
            }

            if (changed) {
                url.pathname = parts.join('/');
                form.setAttribute('action', wasAbsolute ? url.href : url.pathname + url.search + url.hash);
                return;
            }
        } catch (_) {
            // Fall back to the legacy controller route replacement below.
        }

        var re = new RegExp('/' + controller + '/(' + fromActions.join('|') + ')(?=/|$)', 'i');
        form.setAttribute('action', action.replace(re, '/' + controller + '/' + nextAction));
    }

    function setButtonBusy(button, busy) {
        if (!button) return;
        button.disabled = busy;
        button.classList.toggle('is-loading', busy);
    }

    function pulse(button) {
        if (!button) return;
        button.classList.remove('ui-pop');
        void button.offsetWidth;
        button.classList.add('ui-pop');
        window.setTimeout(function () { button.classList.remove('ui-pop'); }, 320);
    }

    function setGrooveActive(button, active) {
        if (!button) return;
        button.classList.toggle('groove-button-dark', active);
        button.classList.toggle('groove-button-paper', !active);
    }

    function setIcon(button, filledClass, emptyClass, active) {
        var icon = button ? button.querySelector('i') : null;
        if (!icon) return;
        icon.classList.toggle(filledClass, active);
        icon.classList.toggle(emptyClass, !active);
    }

    function setLabel(button, i18nKey, fallback) {
        var label = button ? button.querySelector('span') : null;
        if (!label) return;
        label.setAttribute('data-i18n', i18nKey);
        label.textContent = fallback;
        if (window.EventoI18n && typeof window.EventoI18n.apply === 'function') {
            window.EventoI18n.apply(button);
        }
    }

    function updateEvent(form, data) {
        var card = form.closest('.event-card, .evt-card');
        var clicked = form.querySelector('[data-event-action]');

        if (typeof data.liked === 'boolean') {
            var likeButton = card
                ? card.querySelector('[data-event-action="like"]')
                : clicked;
            if (likeButton) {
                likeButton.classList.toggle('is-on-liked', data.liked);
                setGrooveActive(likeButton, data.liked);
                if (card) {
                    likeButton.innerHTML = '<i class="bi ' + (data.liked ? 'bi-heart-fill' : 'bi-heart') + '"></i> ' + (data.likesCount ?? '');
                } else {
                    setIcon(likeButton, 'bi-heart-fill', 'bi-heart', data.liked);
                    setLabel(likeButton, data.liked ? 'post.unlike' : 'post.like', data.liked ? 'Премахни харесване' : 'Харесай');
                }
                pulse(likeButton);
            }
            replaceAction(form, 'Events', ['Like', 'Unlike'], data.liked ? 'Unlike' : 'Like');
        }

        if (typeof data.saved === 'boolean') {
            var saveButton = card
                ? card.querySelector('[data-event-action="save"]')
                : clicked;
            if (saveButton) {
                saveButton.classList.toggle('is-saved', data.saved);
                setGrooveActive(saveButton, data.saved);
                if (card) {
                    saveButton.innerHTML = '<i class="bi ' + (data.saved ? 'bi-bookmark-fill' : 'bi-bookmark') + '"></i>';
                } else {
                    setIcon(saveButton, 'bi-bookmark-fill', 'bi-bookmark', data.saved);
                    setLabel(saveButton, data.saved ? 'post.unsave' : 'post.save', data.saved ? 'Премахни запазване' : 'Запази');
                }
                pulse(saveButton);
            }
            replaceAction(form, 'Events', ['Save', 'Unsave'], data.saved ? 'Unsave' : 'Save');
        }

        if ('attendanceStatus' in data) {
            var goingButton = card
                ? card.querySelector('[data-event-action="going"]')
                : document.querySelector('[data-event-action="going"]');
            var interestedButton = card
                ? card.querySelector('[data-event-action="interested"]')
                : document.querySelector('[data-event-action="interested"]');
            var status = data.attendanceStatus || '';
            if (goingButton) {
                goingButton.classList.toggle('is-on-going', status === 'Going');
                setGrooveActive(goingButton, status === 'Going');
                if (card) {
                    goingButton.innerHTML = '<i class="bi bi-check2-circle"></i> ' + (data.goingCount ?? '');
                }
                pulse(goingButton);
            }
            if (interestedButton) {
                interestedButton.classList.toggle('is-on-interested', status === 'Interested');
                setGrooveActive(interestedButton, status === 'Interested');
                if (card) {
                    interestedButton.innerHTML = '<i class="bi bi-star"></i> ' + (data.interestedCount ?? '');
                }
                pulse(interestedButton);
            }
        }
    }

    function updatePost(form, data) {
        var card = form.closest('.post-card, .social-post-card, .social-post-detail');
        var clicked = form.querySelector('[data-post-action]');

        if (typeof data.liked === 'boolean') {
            var likeButton = card
                ? card.querySelector('[data-post-action="like"]')
                : clicked;
            if (likeButton) {
                likeButton.classList.toggle('btn-danger', data.liked);
                likeButton.classList.toggle('btn-outline-danger', !data.liked);
                setGrooveActive(likeButton, data.liked);
                if (card && !card.classList.contains('social-post-detail')) {
                    likeButton.innerHTML = '<i class="bi ' + (data.liked ? 'bi-heart-fill' : 'bi-heart') + '"></i> ' + (data.likesCount ?? '');
                } else {
                    setIcon(likeButton, 'bi-heart-fill', 'bi-heart', data.liked);
                    setLabel(likeButton, data.liked ? 'post.unlike' : 'post.like', data.liked ? 'Премахни харесване' : 'Харесай');
                }
                pulse(likeButton);
            }
            replaceAction(form, 'Posts', ['Like', 'Unlike'], data.liked ? 'Unlike' : 'Like');
        }

        if (typeof data.saved === 'boolean') {
            var saveButton = card
                ? card.querySelector('[data-post-action="save"]')
                : clicked;
            if (saveButton) {
                saveButton.classList.toggle('btn-dark', data.saved);
                saveButton.classList.toggle('btn-outline-dark', !data.saved);
                setGrooveActive(saveButton, data.saved);
                if (card && !card.classList.contains('social-post-detail')) {
                    saveButton.innerHTML = '<i class="bi ' + (data.saved ? 'bi-bookmark-fill' : 'bi-bookmark') + '"></i> ' + (data.savesCount ?? '');
                } else {
                    setIcon(saveButton, 'bi-bookmark-fill', 'bi-bookmark', data.saved);
                    setLabel(saveButton, data.saved ? 'post.unsave' : 'post.save', data.saved ? 'Премахни запазване' : 'Запази');
                }
                pulse(saveButton);
            }
            replaceAction(form, 'Posts', ['Save', 'Unsave'], data.saved ? 'Unsave' : 'Save');
        }
    }

    document.addEventListener('submit', function (event) {
        var form = event.target.closest('form');
        if (!form) return;

        var eventForm = isEventForm(form);
        var postForm = !eventForm && isPostForm(form);
        if (!eventForm && !postForm) return;

        event.preventDefault();
        if (form.dataset.ajaxSubmitting === 'true') return;
        form.dataset.ajaxSubmitting = 'true';

        var button = event.submitter || form.querySelector('button[type="submit"]');
        setButtonBusy(button, true);

        fetch(form.action, {
            method: 'POST',
            body: new FormData(form),
            headers: {
                'X-Requested-With': 'XMLHttpRequest',
                'Accept': 'application/json',
            },
            credentials: 'same-origin',
        })
            .then(function (response) {
                if (!response.ok) throw new Error('HTTP ' + response.status);
                return response.json();
            })
            .then(function (data) {
                if (eventForm) updateEvent(form, data);
                if (postForm) updatePost(form, data);
            })
            .catch(function () {
                HTMLFormElement.prototype.submit.call(form);
            })
            .finally(function () {
                form.dataset.ajaxSubmitting = 'false';
                setButtonBusy(button, false);
            });
    });
})();
