(function () {
    'use strict';

    var root = document.querySelector('[data-layout-editor]');
    if (!root) return;

    var readOnly = root.getAttribute('data-readonly') === 'true';
    var stage = root.querySelector('[data-layout-stage]');
    var jsonInput = document.querySelector('[data-layout-json]');
    var addButton = root.querySelector('[data-layout-add-section]');
    var generateButton = root.querySelector('[data-layout-generate]');
    var deleteButton = root.querySelector('[data-layout-delete]');
    var nameInput = root.querySelector('[data-layout-prop-name]');
    var typeInput = root.querySelector('[data-layout-prop-type]');
    var priceInput = root.querySelector('[data-layout-prop-price]');
    var selectedId = null;

    var state = parseState(jsonInput ? jsonInput.value : null);
    var TEXT = {
        bg: {
            section: 'Секция',
            newSection: 'Нова секция',
            rows: 'Редове',
            seatsPerRow: 'Места на ред'
        },
        en: {
            section: 'Section',
            newSection: 'New section',
            rows: 'Rows',
            seatsPerRow: 'Seats per row'
        }
    };

    function currentLang() {
        var meta = document.querySelector('meta[name="x-app-lang"]');
        return meta && meta.getAttribute('content') === 'en' ? 'en' : 'bg';
    }

    function t(key) {
        var lang = currentLang();
        return (TEXT[lang] && TEXT[lang][key]) || TEXT.bg[key] || key;
    }

    function parseState(value) {
        try {
            var parsed = value ? JSON.parse(value) : null;
            if (parsed && Array.isArray(parsed.sections)) return parsed;
        } catch (_) { }

        return { sections: [] };
    }

    function sync() {
        if (jsonInput) {
            jsonInput.value = JSON.stringify(state);
        }
    }

    function uid() {
        return 'section-' + Math.random().toString(16).slice(2);
    }

    function selectedSection() {
        return state.sections.find(function (section) {
            return section.clientId === selectedId;
        }) || null;
    }

    function render() {
        if (!stage) return;

        stage.innerHTML = '';
        state.sections.forEach(function (section) {
            if (!section.clientId) section.clientId = uid();

            var node = document.createElement('button');
            node.type = 'button';
            node.className = 'layout-editor-section' + (section.clientId === selectedId ? ' is-selected' : '');
            node.style.left = (section.x || 0) + 'px';
            node.style.top = (section.y || 0) + 'px';
            node.style.width = (section.width || 220) + 'px';
            node.style.height = (section.height || 140) + 'px';
            node.dataset.sectionId = section.clientId;

            var title = document.createElement('span');
            title.className = 'layout-editor-section__title';
            title.textContent = section.name || t('section');
            node.appendChild(title);

            var seats = document.createElement('span');
            seats.className = 'layout-editor-seats';
            (section.seats || []).forEach(function (seat) {
                var seatNode = document.createElement('span');
                seatNode.className = 'layout-editor-seat';
                seatNode.textContent = seat.row + seat.number;
                seatNode.style.left = (seat.x || 0) + 'px';
                seatNode.style.top = (seat.y || 0) + 'px';
                seats.appendChild(seatNode);
            });
            node.appendChild(seats);

            node.addEventListener('click', function () {
                select(section.clientId);
            });

            if (!readOnly) {
                enableDrag(node, section);
            }

            stage.appendChild(node);
        });

        updateProperties();
        sync();
    }

    function select(id) {
        selectedId = id;
        render();
    }

    function updateProperties() {
        var section = selectedSection();
        if (nameInput) nameInput.value = section ? section.name || '' : '';
        if (typeInput) typeInput.value = section ? section.type || 'Seated' : 'Seated';
        if (priceInput) priceInput.value = section ? section.priceModifier || 0 : '';
    }

    function enableDrag(node, section) {
        var start = null;
        node.addEventListener('pointerdown', function (event) {
            if (event.target.classList.contains('layout-editor-seat')) return;
            start = {
                x: event.clientX,
                y: event.clientY,
                sectionX: section.x || 0,
                sectionY: section.y || 0
            };
            node.setPointerCapture(event.pointerId);
        });

        node.addEventListener('pointermove', function (event) {
            if (!start) return;
            section.x = Math.max(0, start.sectionX + event.clientX - start.x);
            section.y = Math.max(0, start.sectionY + event.clientY - start.y);
            node.style.left = section.x + 'px';
            node.style.top = section.y + 'px';
            sync();
        });

        node.addEventListener('pointerup', function () {
            start = null;
        });
    }

    function addSection() {
        var section = {
            clientId: uid(),
            name: t('newSection'),
            type: 'Seated',
            capacity: 0,
            priceModifier: 0,
            x: 80 + state.sections.length * 28,
            y: 80 + state.sections.length * 28,
            width: 260,
            height: 160,
            seats: []
        };

        state.sections.push(section);
        selectedId = section.clientId;
        render();
    }

    function generateRows() {
        var section = selectedSection();
        if (!section) return;

        var rows = Math.max(1, parseInt(window.prompt(t('rows'), '4') || '4', 10));
        var seatsPerRow = Math.max(1, parseInt(window.prompt(t('seatsPerRow'), '10') || '10', 10));
        var gapX = Math.max(24, (section.width - 36) / Math.max(1, seatsPerRow - 1));
        var gapY = Math.max(24, (section.height - 54) / Math.max(1, rows - 1));

        section.seats = [];
        for (var r = 0; r < rows; r++) {
            var rowLabel = String.fromCharCode(65 + r);
            for (var s = 0; s < seatsPerRow; s++) {
                section.seats.push({
                    row: rowLabel,
                    number: String(s + 1),
                    x: 18 + s * gapX,
                    y: 42 + r * gapY,
                    seatType: section.type === 'VIP' ? 'VIP' : 'Standard',
                    status: 'Active'
                });
            }
        }

        section.capacity = section.seats.length;
        render();
    }

    if (addButton) addButton.addEventListener('click', addSection);
    if (generateButton) generateButton.addEventListener('click', generateRows);
    if (deleteButton) {
        deleteButton.addEventListener('click', function () {
            if (!selectedId) return;
            state.sections = state.sections.filter(function (section) {
                return section.clientId !== selectedId;
            });
            selectedId = state.sections[0] ? state.sections[0].clientId : null;
            render();
        });
    }

    if (nameInput) {
        nameInput.addEventListener('input', function () {
            var section = selectedSection();
            if (!section) return;
            section.name = nameInput.value;
            render();
        });
    }

    if (typeInput) {
        typeInput.addEventListener('change', function () {
            var section = selectedSection();
            if (!section) return;
            section.type = typeInput.value;
            render();
        });
    }

    if (priceInput) {
        priceInput.addEventListener('input', function () {
            var section = selectedSection();
            if (!section) return;
            section.priceModifier = parseFloat(priceInput.value || '0') || 0;
            sync();
        });
    }

    if (state.sections.length > 0) {
        selectedId = state.sections[0].clientId || (state.sections[0].clientId = uid());
    }

    render();
})();
