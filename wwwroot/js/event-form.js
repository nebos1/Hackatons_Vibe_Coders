(function () {
    'use strict';

    function updateRecurrence() {
        var selected = document.querySelector('input[name="RecurrenceType"]:checked');
        var fields = document.querySelector('[data-event-recurring-fields]');
        var weekdays = document.querySelector('[data-event-weekdays]');
        var weekdaysHelp = document.querySelector('[data-event-weekdays-help]');
        var occurrenceVisibility = document.querySelector('[data-event-occurrence-visibility]');
        if (!selected || !fields) return;

        var isRecurring = selected.value !== 'None' && selected.value !== '0';
        fields.classList.toggle('is-visible', isRecurring);
        var isWeekly = selected.value === 'Weekly' || selected.value === '2';
        if (weekdays) {
            weekdays.style.display = isWeekly ? 'flex' : 'none';
        }
        if (weekdaysHelp) {
            weekdaysHelp.style.display = isWeekly ? 'block' : 'none';
        }
        if (occurrenceVisibility) {
            occurrenceVisibility.style.display = isRecurring ? 'block' : 'none';
        }
    }

    function updateTicketing() {
        var selected = document.querySelector('input[name="TicketingMode"]:checked');
        var fields = document.querySelector('[data-event-layout-fields]');
        if (!selected || !fields) return;

        var needsLayout = selected.value !== 'GeneralAdmission' && selected.value !== '0';
        fields.classList.toggle('is-visible', needsLayout);
        document.dispatchEvent(new CustomEvent('event-ticketing-mode-changed', {
            detail: { needsLayout: needsLayout }
        }));
    }

    function bindLayoutTicketBuilder() {
        var builder = document.querySelector('[data-layout-ticket-builder]');
        if (!builder) return;

        var layoutSelect = document.querySelector('[name="VenueLayoutId"]');
        var rows = builder.querySelector('[data-layout-ticket-rows]');
        var template = builder.querySelector('[data-layout-ticket-row-template]');
        var total = builder.querySelector('[data-layout-ticket-total]');
        var empty = builder.querySelector('[data-layout-ticket-empty]');
        var initialRows = rows ? rows.children.length : 0;
        var sectionCache = {};

        if (!layoutSelect || !rows || !template) return;

        function needsLayout() {
            var selected = document.querySelector('input[name="TicketingMode"]:checked');
            return !!selected && selected.value !== 'GeneralAdmission' && selected.value !== '0';
        }

        function escapeHtml(value) {
            return String(value == null ? '' : value)
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#039;');
        }

        function replaceToken(html, token, value) {
            return html.split(token).join(value);
        }

        function setVisible(visible) {
            builder.hidden = !visible;
        }

        function setTotal(sections) {
            var seats = sections.reduce(function (sum, section) {
                return sum + (parseInt(section.seatsCount || '0', 10) || 0);
            }, 0);
            if (total) total.textContent = seats + ' места';
            if (empty) empty.hidden = sections.length > 0;
        }

        function render(sections) {
            rows.innerHTML = '';
            sections.forEach(function (section, index) {
                var sectionName = section.sectionName || section.name || 'Сектор';
                var colorHex = section.colorHex || '#2456ff';
                var seatsCount = parseInt(section.seatsCount || '0', 10) || 0;
                var html = template.innerHTML;
                html = replaceToken(html, '__index__', index.toString());
                html = replaceToken(html, '__sectionId__', escapeHtml(section.sectionId || section.id));
                html = replaceToken(html, '__sectionName__', escapeHtml(sectionName));
                html = replaceToken(html, '__colorHex__', escapeHtml(colorHex));
                html = replaceToken(html, '__seatsCount__', escapeHtml(seatsCount));
                rows.insertAdjacentHTML('beforeend', html);
            });
            setTotal(sections);
        }

        async function loadSections(layoutId) {
            if (sectionCache[layoutId]) return sectionCache[layoutId];
            var response = await fetch('/Events/LayoutTicketSections?layoutId=' + encodeURIComponent(layoutId), {
                headers: { 'Accept': 'application/json' },
                credentials: 'same-origin'
            });
            if (!response.ok) throw new Error('HTTP ' + response.status);
            var sections = await response.json();
            sectionCache[layoutId] = Array.isArray(sections) ? sections : [];
            return sectionCache[layoutId];
        }

        async function refresh(forceReload) {
            var layoutId = layoutSelect.value;
            var visible = needsLayout() && !!layoutId;
            setVisible(visible);
            if (!visible) {
                rows.innerHTML = '';
                setTotal([]);
                return;
            }

            if (!forceReload && initialRows > 0) {
                setTotal(Array.from(rows.querySelectorAll('.event-layout-ticket-row')).map(function (row) {
                    var seats = row.querySelector('[name$=".SeatsCount"]');
                    return { seatsCount: seats ? seats.value : 0 };
                }));
                initialRows = 0;
                return;
            }

            try {
                render(await loadSections(layoutId));
            } catch (_) {
                rows.innerHTML = '';
                setTotal([]);
                if (empty) {
                    empty.hidden = false;
                    empty.textContent = 'Не успяхме да заредим секторите на layout-а.';
                }
            }
        }

        layoutSelect.addEventListener('change', function () {
            initialRows = 0;
            refresh(true);
        });
        document.addEventListener('event-ticketing-mode-changed', function () {
            refresh(false);
        });

        refresh(false);
    }

    function bindGenrePicker() {
        document.querySelectorAll('[data-event-genre-picker]').forEach(function (picker) {
            var max = parseInt(picker.getAttribute('data-max-genres') || '3', 10);
            var hidden = document.querySelector('input[name="Genre"]');
            var status = picker.parentElement ? picker.parentElement.querySelector('[data-genre-picker-status]') : null;

            function sync(changed) {
                var checked = Array.prototype.slice.call(picker.querySelectorAll('input[type="checkbox"]:checked'));
                if (checked.length > max && changed) {
                    changed.checked = false;
                    checked = Array.prototype.slice.call(picker.querySelectorAll('input[type="checkbox"]:checked'));
                    if (status) {
                        status.textContent = 'Можеш да избереш до ' + max + ' жанра.';
                    }
                } else if (status) {
                    status.textContent = '';
                }

                picker.querySelectorAll('.event-genre-option').forEach(function (option) {
                    var input = option.querySelector('input[type="checkbox"]');
                    option.classList.toggle('is-selected', !!input && input.checked);
                });

                if (hidden && checked.length > 0) {
                    hidden.value = checked[0].value;
                }
            }

            picker.addEventListener('change', function (event) {
                var input = event.target.closest('input[type="checkbox"]');
                if (!input) return;
                sync(input);
            });

            sync();
        });
    }

    document.querySelectorAll('input[name="RecurrenceType"]').forEach(function (input) {
        input.addEventListener('change', updateRecurrence);
    });

    document.querySelectorAll('input[name="TicketingMode"]').forEach(function (input) {
        input.addEventListener('change', updateTicketing);
    });

    updateRecurrence();
    updateTicketing();
    bindGenrePicker();
    bindLayoutTicketBuilder();
})();
