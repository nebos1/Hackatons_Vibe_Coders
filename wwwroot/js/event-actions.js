(function () {
    var ACTION_PATTERNS = [
        '/Events/Like/',
        '/Events/Unlike/',
        '/Events/Save/',
        '/Events/Unsave/',
        '/Events/Attendance/',
    ];

    function matchesEventAction(url) {
        return ACTION_PATTERNS.some(function (p) { return url.indexOf(p) !== -1; });
    }

    document.addEventListener('submit', function (e) {
        var form = e.target.closest('form');
        if (!form) return;
        var action = form.getAttribute('action') || form.action || '';
        if (!matchesEventAction(action)) return;
        var card = form.closest('.event-card, .evt-card');
        if (!card) return;

        e.preventDefault();
        var btn = form.querySelector('button[type="submit"]');
        if (btn) btn.disabled = true;

        fetch(action, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' },
            credentials: 'same-origin',
        })
            .then(function (resp) {
                if (!resp.ok) throw new Error('HTTP ' + resp.status);
                return resp.json();
            })
            .then(function (data) { applyState(card, form, data); })
            .catch(function () { form.submit(); })
            .finally(function () { if (btn) btn.disabled = false; });
    });

    function applyState(card, form, data) {
        if (typeof data.liked === 'boolean') {
            var likeBtn = card.querySelector('.evt-card__action[title="Like"]');
            if (likeBtn) {
                likeBtn.classList.toggle('is-on-liked', data.liked);
                likeBtn.innerHTML = '<i class="bi ' + (data.liked ? 'bi-heart-fill' : 'bi-heart') + '"></i> ' + data.likesCount;
            }
            var likeForm = likeBtn ? likeBtn.closest('form') : form;
            if (likeForm) {
                var nextAction = data.liked ? 'Unlike' : 'Like';
                likeForm.action = likeForm.action.replace(/\/Events\/(Like|Unlike)\//, '/Events/' + nextAction + '/');
            }
        }

        if (typeof data.saved === 'boolean') {
            var saveBtn = card.querySelector('.evt-card__save');
            if (saveBtn) {
                saveBtn.classList.toggle('is-saved', data.saved);
                saveBtn.innerHTML = '<i class="bi ' + (data.saved ? 'bi-bookmark-fill' : 'bi-bookmark') + '"></i>';
            }
            var saveForm = saveBtn ? saveBtn.closest('form') : form;
            if (saveForm) {
                var nextAction = data.saved ? 'Unsave' : 'Save';
                saveForm.action = saveForm.action.replace(/\/Events\/(Save|Unsave)\//, '/Events/' + nextAction + '/');
            }
        }

        if ('attendanceStatus' in data) {
            var goingBtn = card.querySelector('.evt-card__action[title="Going"]');
            var interestedBtn = card.querySelector('.evt-card__action[title="Interested"]');
            var status = data.attendanceStatus;
            if (goingBtn) {
                goingBtn.classList.toggle('is-on-going', status === 'Going');
                goingBtn.innerHTML = '<i class="bi bi-check2-circle"></i> ' + data.goingCount;
            }
            if (interestedBtn) {
                interestedBtn.classList.toggle('is-on-interested', status === 'Interested');
                interestedBtn.innerHTML = '<i class="bi bi-star"></i> ' + data.interestedCount;
            }
        }

        if (typeof data.heat === 'number') {
            updateHeat(card, data.heat, data.isHot === true);
        }
    }

    function updateHeat(card, heat, isHot) {
        var meta = card.querySelector('.evt-card__meta');
        var rating = card.querySelector('.evt-card__rating');
        if (heat > 0) {
            if (rating) {
                rating.innerHTML = '<i class="bi bi-fire"></i> ' + heat;
            } else if (meta) {
                rating = document.createElement('span');
                rating.className = 'evt-card__rating';
                rating.title = 'Heat';
                rating.innerHTML = '<i class="bi bi-fire"></i> ' + heat;
                meta.appendChild(rating);
            }
        } else if (rating) {
            rating.remove();
        }

        var firstChip = card.querySelector('.evt-card__media .evt-card__chip');
        if (!firstChip) return;
        if (firstChip.classList.contains('evt-card__chip--vip')
            || firstChip.classList.contains('evt-card__chip--pending')) {
            return;
        }
        if (isHot) {
            if (!firstChip.classList.contains('evt-card__chip--hot')) {
                firstChip.dataset.originalHtml = firstChip.innerHTML;
                firstChip.dataset.originalI18n = firstChip.getAttribute('data-i18n') || '';
                firstChip.removeAttribute('data-i18n');
                firstChip.classList.add('evt-card__chip--hot');
                firstChip.innerHTML = '<i class="bi bi-fire"></i> Hot';
            }
        } else {
            if (firstChip.classList.contains('evt-card__chip--hot') && firstChip.dataset.originalHtml) {
                firstChip.classList.remove('evt-card__chip--hot');
                firstChip.innerHTML = firstChip.dataset.originalHtml;
                if (firstChip.dataset.originalI18n) {
                    firstChip.setAttribute('data-i18n', firstChip.dataset.originalI18n);
                }
            }
        }
    }
})();
