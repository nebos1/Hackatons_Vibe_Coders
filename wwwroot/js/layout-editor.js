(function () {
    'use strict';

    var root = document.querySelector('[data-layout-editor]');
    if (!root) return;

    var readOnly = root.getAttribute('data-readonly') === 'true';
    var stage = root.querySelector('[data-layout-stage]');
    var floorsHost = root.querySelector('[data-layout-floors]');
    var floorNameInput = root.querySelector('[data-layout-floor-name]');
    var jsonInput = document.querySelector('[data-layout-json]');
    var ai = {
        description: document.querySelector('[data-ai-description]'),
        image: document.querySelector('[data-ai-image]'),
        generate: document.querySelector('[data-ai-generate]'),
        clearImage: document.querySelector('[data-ai-clear-image]'),
        tutorial: document.querySelector('[data-layout-tutorial]'),
        tutorialPanel: document.querySelector('[data-layout-tutorial-panel]'),
        tutorialClose: document.querySelector('[data-layout-tutorial-close]'),
        status: document.querySelector('[data-ai-status]')
    };
    var sectionTypeLabels = {
        Seated: 'Седящи',
        Standing: 'Правостоящи',
        VIP: 'VIP',
        Table: 'Маси'
    };

    var els = {
        selectedTitle: root.querySelector('[data-layout-selected-title]'),
        sectionPanel: root.querySelector('[data-layout-section-panel]'),
        seatPanel: root.querySelector('[data-layout-seat-panel]'),
        addFloor: root.querySelector('[data-layout-add-floor]'),
        addSection: root.querySelector('[data-layout-add-section]'),
        addStanding: root.querySelector('[data-layout-add-standing]'),
        addTable: root.querySelector('[data-layout-add-table]'),
        generateRows: root.querySelector('[data-layout-generate-rows]'),
        generateTable: root.querySelector('[data-layout-generate-table]'),
        undo: root.querySelector('[data-layout-undo]'),
        redo: root.querySelector('[data-layout-redo]'),
        duplicate: root.querySelector('[data-layout-duplicate]'),
        del: root.querySelector('[data-layout-delete]'),
        rowsCount: root.querySelector('[data-rows-count]'),
        rowsSize: root.querySelector('[data-rows-size]'),
        tableSize: root.querySelector('[data-table-size]'),
        tableCapacity: root.querySelector('[data-table-capacity]'),
        section: {
            name: root.querySelector('[data-section-name]'),
            type: root.querySelector('[data-section-type]'),
            shape: root.querySelector('[data-section-shape]'),
            price: root.querySelector('[data-section-price]'),
            x: root.querySelector('[data-section-x]'),
            y: root.querySelector('[data-section-y]'),
            width: root.querySelector('[data-section-width]'),
            height: root.querySelector('[data-section-height]'),
            rotation: root.querySelector('[data-section-rotation]')
        },
        seat: {
            label: root.querySelector('[data-seat-label]'),
            row: root.querySelector('[data-seat-row]'),
            number: root.querySelector('[data-seat-number]'),
            type: root.querySelector('[data-seat-type]'),
            status: root.querySelector('[data-seat-status]'),
            x: root.querySelector('[data-seat-x]'),
            y: root.querySelector('[data-seat-y]'),
            radius: root.querySelector('[data-seat-radius]'),
            capacity: root.querySelector('[data-seat-capacity]'),
            unlimited: root.querySelector('[data-seat-unlimited]')
        }
    };

    var state = normalize(parseState(jsonInput ? jsonInput.value : null));
    var activeFloorId = state.floors[0].clientId;
    var selected = { type: 'section', id: state.sections[0] ? state.sections[0].clientId : null };
    var undoStack = [];
    var redoStack = [];
    var maxHistory = 60;
    var referenceObjectUrl = null;

    function parseState(value) {
        try {
            var parsed = value ? JSON.parse(value) : null;
            if (parsed && Array.isArray(parsed.sections)) return parsed;
        } catch (_) { }
        return {};
    }

    function uid(prefix) {
        return prefix + '-' + Math.random().toString(16).slice(2) + Date.now().toString(16);
    }

    function clamp(value, min, max) {
        return Math.max(min, Math.min(max, value));
    }

    function number(value, fallback) {
        var n = parseFloat(value);
        return Number.isFinite(n) ? n : fallback;
    }

    function intFrom(text, patterns, fallback) {
        for (var i = 0; i < patterns.length; i += 1) {
            var match = text.match(patterns[i]);
            if (match) {
                var value = parseInt(match[1], 10);
                if (Number.isFinite(value)) return value;
            }
        }
        return fallback;
    }

    function includesAny(text, words) {
        return words.some(function (word) { return text.indexOf(word) >= 0; });
    }

    function normalize(raw) {
        var normalized = {
            canvasWidth: number(raw.canvasWidth, 1200),
            canvasHeight: number(raw.canvasHeight, 760),
            floors: Array.isArray(raw.floors) ? raw.floors : [],
            sections: Array.isArray(raw.sections) ? raw.sections : []
        };

        if (!normalized.floors.length) {
            var floorNames = normalized.sections.map(function (s) { return s.floorName || 'Етаж 1'; });
            floorNames = floorNames.filter(function (name, index) { return floorNames.indexOf(name) === index; });
            normalized.floors = (floorNames.length ? floorNames : ['Етаж 1']).map(function (name, index) {
                return { clientId: 'floor-' + (index + 1), name: name };
            });
        }

        normalized.floors.forEach(function (floor, index) {
            floor.clientId = floor.clientId || 'floor-' + (index + 1);
            floor.name = floor.name || ('Етаж ' + (index + 1));
        });

        normalized.sections.forEach(function (section, index) {
            section.clientId = section.clientId || uid('section');
            var matchingFloor = normalized.floors.find(function (f) {
                return f.clientId === section.floorId || f.name === section.floorName;
            }) || normalized.floors[0];
            section.floorId = matchingFloor.clientId;
            section.floorName = matchingFloor.name;
            section.name = section.name || ('Секция ' + (index + 1));
            section.type = section.type || 'Seated';
            section.shape = section.shape || (section.type === 'Table' ? 'Circle' : 'Rectangle');
            section.capacity = Math.max(0, parseInt(section.capacity || '0', 10));
            section.priceModifier = number(section.priceModifier, 0);
            section.x = number(section.x, 80 + index * 30);
            section.y = number(section.y, 80 + index * 30);
            section.width = Math.max(80, number(section.width, 260));
            section.height = Math.max(70, number(section.height, 160));
            section.rotation = number(section.rotation, 0);
            section.seats = Array.isArray(section.seats) ? section.seats : [];
            section.seats.forEach(function (seat, seatIndex) {
                seat.clientId = seat.clientId || uid('seat');
                seat.row = seat.row || 'A';
                seat.number = seat.number || String(seatIndex + 1);
                seat.label = seat.label || (seat.row + seat.number);
                seat.x = number(seat.x, 24 + (seatIndex % 10) * 34);
                seat.y = number(seat.y, 52 + Math.floor(seatIndex / 10) * 34);
                seat.radius = Math.max(9, number(seat.radius, seat.seatType === 'Table' ? 28 : 15));
                seat.rotation = number(seat.rotation, 0);
                seat.capacity = Math.max(1, parseInt(seat.capacity || '1', 10));
                seat.isCapacityUnlimited = seat.isCapacityUnlimited === true;
                seat.seatType = seat.seatType || (section.type === 'Table' ? 'Table' : 'Standard');
                seat.status = seat.status || 'Active';
            });
        });

        return normalized;
    }

    function activeFloor() {
        return state.floors.find(function (f) { return f.clientId === activeFloorId; }) || state.floors[0];
    }

    function selectedSection() {
        if (selected.type === 'section') {
            return state.sections.find(function (s) { return s.clientId === selected.id; }) || null;
        }
        if (selected.type === 'seat') {
            return state.sections.find(function (s) {
                return s.seats.some(function (seat) { return seat.clientId === selected.id; });
            }) || null;
        }
        return null;
    }

    function selectedSeat() {
        if (selected.type !== 'seat') return null;
        var section = selectedSection();
        return section ? section.seats.find(function (seat) { return seat.clientId === selected.id; }) || null : null;
    }

    function sync() {
        state.sections.forEach(function (section) {
            var floor = state.floors.find(function (f) { return f.clientId === section.floorId; }) || state.floors[0];
            section.floorName = floor.name;
            section.capacity = section.seats.length
                ? section.seats.reduce(function (total, seat) { return total + (seat.isCapacityUnlimited ? 0 : Math.max(1, parseInt(seat.capacity || '1', 10))); }, 0)
                : Math.max(0, parseInt(section.capacity || '0', 10));
        });
        if (jsonInput) jsonInput.value = JSON.stringify(state);
    }

    function renderFloors() {
        floorsHost.innerHTML = '';
        state.floors.forEach(function (floor) {
            var button = document.createElement('button');
            button.type = 'button';
            button.className = floor.clientId === activeFloorId ? 'is-active' : '';
            button.textContent = floor.name;
            button.addEventListener('click', function () {
                activeFloorId = floor.clientId;
                var first = state.sections.find(function (s) { return s.floorId === activeFloorId; });
                selected = first ? { type: 'section', id: first.clientId } : { type: null, id: null };
                render();
            });
            floorsHost.appendChild(button);
        });
        if (floorNameInput) floorNameInput.value = activeFloor().name;
    }

    function render() {
        if (!stage) return;
        renderFloors();
        stage.style.width = state.canvasWidth + 'px';
        stage.style.height = state.canvasHeight + 'px';
        stage.querySelectorAll('.layout-pro-section').forEach(function (n) { n.remove(); });

        state.sections
            .filter(function (section) { return section.floorId === activeFloorId; })
            .forEach(function (section) {
                var node = document.createElement('div');
                node.className = 'layout-pro-section shape-' + section.shape.toLowerCase() + (selected.type === 'section' && selected.id === section.clientId ? ' is-selected' : '');
                node.dataset.sectionId = section.clientId;
                node.dataset.sectionType = section.type;
                applyBox(node, section);

                var head = document.createElement('button');
                head.type = 'button';
                head.className = 'layout-pro-section__head';
                head.innerHTML = '<strong></strong><small></small>';
                head.querySelector('strong').textContent = section.name;
                head.querySelector('small').textContent = (sectionTypeLabels[section.type] || section.type) + ' - ' + section.capacity;
                head.addEventListener('pointerdown', function (event) {
                    select('section', section.clientId, true);
                    if (!readOnly) startDrag(event, section, node, null);
                });
                head.addEventListener('click', function () { select('section', section.clientId); });
                node.appendChild(head);

                section.seats.forEach(function (seat) {
                    var seatNode = document.createElement('button');
                    seatNode.type = 'button';
                    seatNode.className = 'layout-pro-seat' + (seat.seatType === 'Table' ? ' is-table' : '') + (seat.status === 'Blocked' ? ' is-blocked' : '') + (selected.type === 'seat' && selected.id === seat.clientId ? ' is-selected' : '');
                    seatNode.dataset.seatId = seat.clientId;
                    seatNode.style.left = seat.x + 'px';
                    seatNode.style.top = seat.y + 'px';
                    seatNode.style.width = (seat.radius * 2) + 'px';
                    seatNode.style.height = (seat.radius * 2) + 'px';
                    seatNode.style.transform = 'rotate(' + seat.rotation + 'deg)';
                    seatNode.innerHTML = '<span></span>' + (seat.seatType === 'Table' && (seat.isCapacityUnlimited || seat.capacity > 1) ? '<small></small>' : '');
                    seatNode.querySelector('span').textContent = seat.label || (seat.row + seat.number);
                    if (seatNode.querySelector('small')) seatNode.querySelector('small').textContent = seat.isCapacityUnlimited ? '∞' : seat.capacity;
                    seatNode.addEventListener('pointerdown', function (event) {
                        select('seat', seat.clientId, true);
                        if (!readOnly) startDrag(event, seat, seatNode, section);
                        event.stopPropagation();
                    });
                    seatNode.addEventListener('click', function (event) {
                        select('seat', seat.clientId);
                        event.stopPropagation();
                    });
                    node.appendChild(seatNode);
                });

                if (!readOnly) {
                    var resize = document.createElement('span');
                    resize.className = 'layout-pro-section__resize';
                    resize.addEventListener('pointerdown', function (event) {
                        select('section', section.clientId, true);
                        startResize(event, section, node);
                        event.stopPropagation();
                    });
                    node.appendChild(resize);
                }

                stage.appendChild(node);
            });

        updatePanel();
        sync();
    }

    function applyBox(node, section) {
        node.style.left = section.x + 'px';
        node.style.top = section.y + 'px';
        node.style.width = section.width + 'px';
        node.style.height = section.height + 'px';
        node.style.transform = 'rotate(' + section.rotation + 'deg)';
    }

    function select(type, id, deferRender) {
        selected = { type: type, id: id };
        if (deferRender) {
            updatePanel();
            sync();
            return;
        }
        render();
    }

    function updatePanel() {
        var section = selectedSection();
        var seat = selectedSeat();
        if (els.selectedTitle) {
            els.selectedTitle.textContent = seat ? (seat.label || seat.row + seat.number) : section ? section.name : 'Нищо не е избрано';
        }
        els.sectionPanel.hidden = !section;
        els.seatPanel.hidden = !seat;

        if (section) {
            setValue(els.section.name, section.name);
            setValue(els.section.type, section.type);
            setValue(els.section.shape, section.shape);
            setValue(els.section.price, section.priceModifier);
            setValue(els.section.x, Math.round(section.x));
            setValue(els.section.y, Math.round(section.y));
            setValue(els.section.width, Math.round(section.width));
            setValue(els.section.height, Math.round(section.height));
            setValue(els.section.rotation, section.rotation);
        }
        if (seat) {
            setValue(els.seat.label, seat.label || seat.row + seat.number);
            setValue(els.seat.row, seat.row);
            setValue(els.seat.number, seat.number);
            setValue(els.seat.type, seat.seatType);
            setValue(els.seat.status, seat.status);
            setValue(els.seat.x, Math.round(seat.x));
            setValue(els.seat.y, Math.round(seat.y));
            setValue(els.seat.radius, Math.round(seat.radius));
            setValue(els.seat.capacity, seat.capacity);
            setChecked(els.seat.unlimited, seat.isCapacityUnlimited);
            if (els.seat.capacity) els.seat.capacity.disabled = readOnly || seat.isCapacityUnlimited;
        }
    }

    function setValue(el, value) {
        if (el && document.activeElement !== el) el.value = value;
    }

    function setChecked(el, value) {
        if (el && document.activeElement !== el) el.checked = !!value;
    }

    function snapshot() {
        return JSON.stringify({ state: state, activeFloorId: activeFloorId, selected: selected });
    }

    function restore(snapshotValue) {
        var parsed = JSON.parse(snapshotValue);
        state = normalize(parsed.state || {});
        activeFloorId = parsed.activeFloorId || state.floors[0].clientId;
        selected = parsed.selected || { type: null, id: null };
        render();
    }

    function remember() {
        undoStack.push(snapshot());
        if (undoStack.length > maxHistory) undoStack.shift();
        redoStack = [];
        updateHistoryButtons();
    }

    function updateHistoryButtons() {
        if (els.undo) els.undo.disabled = readOnly || undoStack.length === 0;
        if (els.redo) els.redo.disabled = readOnly || redoStack.length === 0;
    }

    function undo() {
        if (!undoStack.length) return;
        redoStack.push(snapshot());
        restore(undoStack.pop());
        updateHistoryButtons();
    }

    function redo() {
        if (!redoStack.length) return;
        undoStack.push(snapshot());
        restore(redoStack.pop());
        updateHistoryButtons();
    }

    function startDrag(event, item, node, parentSection) {
        remember();
        node.classList.add('is-dragging');
        node.classList.add('is-selected');
        var start = {
            pointerId: event.pointerId,
            clientX: event.clientX,
            clientY: event.clientY,
            x: item.x || 0,
            y: item.y || 0,
            maxX: parentSection ? parentSection.width - item.radius * 2 : state.canvasWidth - item.width,
            maxY: parentSection ? parentSection.height - item.radius * 2 : state.canvasHeight - item.height
        };
        node.setPointerCapture(event.pointerId);
        node.addEventListener('pointermove', move);
        node.addEventListener('pointerup', end);
        node.addEventListener('pointercancel', end);

        function move(e) {
            item.x = clamp(start.x + e.clientX - start.clientX, 0, Math.max(0, start.maxX));
            item.y = clamp(start.y + e.clientY - start.clientY, 0, Math.max(0, start.maxY));
            if (parentSection) {
                node.style.left = item.x + 'px';
                node.style.top = item.y + 'px';
            } else {
                applyBox(node, item);
            }
            sync();
            updatePanel();
        }

        function end() {
            node.classList.remove('is-dragging');
            node.removeEventListener('pointermove', move);
            node.removeEventListener('pointerup', end);
            node.removeEventListener('pointercancel', end);
            render();
        }
    }

    function startResize(event, section, node) {
        remember();
        node.classList.add('is-resizing');
        var start = {
            clientX: event.clientX,
            clientY: event.clientY,
            width: section.width,
            height: section.height
        };
        node.setPointerCapture(event.pointerId);
        node.addEventListener('pointermove', move);
        node.addEventListener('pointerup', end);
        node.addEventListener('pointercancel', end);

        function move(e) {
            section.width = clamp(start.width + e.clientX - start.clientX, 80, state.canvasWidth - section.x);
            section.height = clamp(start.height + e.clientY - start.clientY, 70, state.canvasHeight - section.y);
            section.seats.forEach(function (seat) {
                seat.x = clamp(seat.x, 0, section.width - seat.radius * 2);
                seat.y = clamp(seat.y, 0, section.height - seat.radius * 2);
            });
            applyBox(node, section);
            sync();
            updatePanel();
        }

        function end() {
            node.classList.remove('is-resizing');
            node.removeEventListener('pointermove', move);
            node.removeEventListener('pointerup', end);
            node.removeEventListener('pointercancel', end);
            render();
        }
    }

    function addFloor() {
        remember();
        var floor = { clientId: uid('floor'), name: 'Floor ' + (state.floors.length + 1) };
        state.floors.push(floor);
        activeFloorId = floor.clientId;
        selected = { type: null, id: null };
        render();
    }

    function addSection(type, shape) {
        remember();
        var floor = activeFloor();
        var count = state.sections.filter(function (s) { return s.floorId === floor.clientId; }).length;
        var section = {
            clientId: uid('section'),
            floorId: floor.clientId,
            floorName: floor.name,
            name: type === 'Standing' ? 'Standing zone' : type === 'Table' ? 'Table zone' : 'Section ' + (count + 1),
            type: type || 'Seated',
            shape: shape || 'Rectangle',
            capacity: type === 'Standing' ? 80 : 0,
            priceModifier: 0,
            x: 100 + count * 28,
            y: 100 + count * 28,
            width: type === 'Table' ? 230 : 360,
            height: type === 'Table' ? 230 : 190,
            rotation: 0,
            seats: []
        };
        state.sections.push(section);
        if (type === 'Table') addTableUnit(section);
        select('section', section.clientId);
    }

    function addTableUnit(section) {
        var next = section.seats.length + 1;
        var cap = parseInt(els.tableCapacity.value || '4', 10);
        section.seats.push({
            clientId: uid('seat'),
            row: 'T',
            number: String(next),
            label: 'T' + next,
            x: Math.max(20, section.width / 2 - 34),
            y: Math.max(42, section.height / 2 - 34),
            radius: 34,
            rotation: 0,
            capacity: cap,
            isCapacityUnlimited: false,
            seatType: 'Table',
            status: 'Active'
        });
    }

    function setAiStatus(message, kind) {
        if (!ai.status) return;
        ai.status.textContent = message || '';
        ai.status.dataset.kind = kind || '';
    }

    function csrfToken() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function applyAiLayout(layout) {
        remember();
        state = normalize(layout || {});
        activeFloorId = state.floors[0].clientId;
        selected = state.sections[0]
            ? { type: 'section', id: state.sections[0].clientId }
            : { type: null, id: null };
        render();
        setAiStatus('Готово. AI направи начална схема, можеш да я местиш и редактираш.', 'ok');
    }

    async function generateAiLayout() {
        if (readOnly) return;

        var description = ((ai.description && ai.description.value) || '').trim();
        var file = ai.image && ai.image.files && ai.image.files[0] ? ai.image.files[0] : null;
        var originalText = ai.generate ? ai.generate.innerHTML : '';

        if (ai.generate) {
            ai.generate.disabled = true;
            ai.generate.innerHTML = '<i class="bi bi-stars"></i> Генерирам...';
        }
        setAiStatus('AI подрежда залата...', 'loading');

        var fallbackMessage = 'AI не е наличен в момента. Направих локална стартова схема.';

        try {
            if (description || file) {
                var form = new FormData();
                form.append('description', description);
                if (file) form.append('image', file);

                var headers = {};
                var token = csrfToken();
                if (token) headers.RequestVerificationToken = token;

                var response = await fetch('/Layouts/AiGenerate', {
                    method: 'POST',
                    body: form,
                    headers: headers
                });

                if (response.ok) {
                    var payload = await response.json();
                    if (payload && payload.layout && Array.isArray(payload.layout.sections)) {
                        applyAiLayout(payload.layout);
                        return;
                    }
                } else {
                    try {
                        var errorPayload = await response.json();
                        if (errorPayload && errorPayload.message) {
                            fallbackMessage = errorPayload.message + ' Направих локална стартова схема.';
                        }
                    } catch (_) { }
                }
            }
        } catch (error) {
            console.warn('AI layout generation failed', error);
        } finally {
            if (ai.generate) {
                ai.generate.disabled = false;
                ai.generate.innerHTML = originalText;
            }
        }

        setAiStatus(fallbackMessage, 'fallback');
        generateLocalAiLayout();
    }

    function generateLocalAiLayout() {
        if (readOnly) return;
        var text = ((ai.description && ai.description.value) || '').toLowerCase();
        remember();

        var floorCount = clamp(intFrom(text, [/(\d+)\s*(?:етажа|етаж|нива|ниво|floors?|levels?)/], 1), 1, 5);
        var rows = clamp(intFrom(text, [/(\d+)\s*(?:реда|редове|rows?)/, /(?:реда|редове|rows?)[^\d]{0,18}(\d+)/], 6), 1, 40);
        var perRow = clamp(intFrom(text, [/по\s*(\d+)\s*(?:места|седалки|chairs?|seats?)/, /(\d+)\s*(?:места|седалки|chairs?|seats?)\s*(?:на|във|per)\s*(?:ред|row)/], 10), 1, 60);
        var tableCount = clamp(intFrom(text, [/(\d+)\s*(?:маси|маса|tables?)/], includesAny(text, ['маса', 'маси', 'table']) ? 8 : 0), 0, 80);
        var tableCapacity = clamp(intFrom(text, [/по\s*(\d+)\s*(?:души|човека|хора|people|guests?)/, /(?:капацитет|capacity)[^\d]{0,12}(\d+)/], 4), 1, 100);
        var unlimited = includesAny(text, ['без лимит', 'без ограничение', 'unlimited', 'no limit']);
        var wantsStanding = includesAny(text, ['правостоя', 'standing', 'dancefloor', 'дансинг']);
        var wantsVip = includesAny(text, ['vip', 'вип']);

        if (!text.trim()) {
            rows = 8;
            perRow = 12;
            tableCount = 6;
            tableCapacity = 4;
        }

        var newState = {
            canvasWidth: 1200,
            canvasHeight: 820,
            floors: [],
            sections: []
        };

        for (var floorIndex = 0; floorIndex < floorCount; floorIndex += 1) {
            var floor = {
                clientId: 'floor-' + (floorIndex + 1),
                name: floorIndex === 0 ? 'Партер' : 'Етаж ' + (floorIndex + 1)
            };
            newState.floors.push(floor);

            var scaledRows = Math.max(1, Math.round(rows / floorCount));
            var sectionWidth = clamp(perRow * 44 + 92, 360, 920);
            var sectionHeight = clamp(scaledRows * 42 + 90, 220, 560);
            var seated = {
                clientId: uid('section'),
                floorId: floor.clientId,
                floorName: floor.name,
                name: floorIndex === 0 ? 'Основна секция' : 'Балкон ' + floorIndex,
                type: 'Seated',
                shape: 'Rounded',
                capacity: scaledRows * perRow,
                priceModifier: floorIndex === 0 ? 0 : 10,
                x: 80,
                y: 88,
                width: sectionWidth,
                height: sectionHeight,
                rotation: 0,
                seats: []
            };

            for (var r = 0; r < scaledRows; r += 1) {
                var row = String.fromCharCode(65 + (r % 26));
                for (var c = 0; c < perRow; c += 1) {
                    seated.seats.push({
                        clientId: uid('seat'),
                        row: row,
                        number: String(c + 1),
                        label: row + (c + 1),
                        x: 34 + c * Math.max(30, (sectionWidth - 84) / Math.max(1, perRow - 1)),
                        y: 64 + r * Math.max(30, (sectionHeight - 104) / Math.max(1, scaledRows - 1)),
                        radius: 15,
                        rotation: 0,
                        capacity: 1,
                        isCapacityUnlimited: false,
                        seatType: floorIndex === 0 && wantsVip && r < 2 ? 'VIP' : 'Standard',
                        status: 'Active'
                    });
                }
            }
            newState.sections.push(seated);

            if (tableCount > 0 && floorIndex === 0) {
                var tableSection = {
                    clientId: uid('section'),
                    floorId: floor.clientId,
                    floorName: floor.name,
                    name: 'Маси',
                    type: 'Table',
                    shape: 'Rounded',
                    capacity: unlimited ? 0 : tableCount * tableCapacity,
                    priceModifier: wantsVip ? 20 : 0,
                    x: 80,
                    y: sectionHeight + 130,
                    width: clamp(Math.ceil(Math.sqrt(tableCount)) * 96 + 80, 340, 920),
                    height: clamp(Math.ceil(tableCount / Math.ceil(Math.sqrt(tableCount))) * 86 + 88, 220, 520),
                    rotation: 0,
                    seats: []
                };
                var columns = Math.max(1, Math.ceil(Math.sqrt(tableCount)));
                for (var t = 0; t < tableCount; t += 1) {
                    tableSection.seats.push({
                        clientId: uid('seat'),
                        row: 'T',
                        number: String(t + 1),
                        label: 'Маса ' + (t + 1),
                        x: 44 + (t % columns) * 96,
                        y: 62 + Math.floor(t / columns) * 86,
                        radius: 30,
                        rotation: 0,
                        capacity: tableCapacity,
                        isCapacityUnlimited: unlimited,
                        seatType: 'Table',
                        status: 'Active'
                    });
                }
                newState.sections.push(tableSection);
            }

            if (wantsStanding && floorIndex === 0) {
                newState.sections.push({
                    clientId: uid('section'),
                    floorId: floor.clientId,
                    floorName: floor.name,
                    name: 'Правостояща зона',
                    type: 'Standing',
                    shape: 'Rounded',
                    capacity: 120,
                    priceModifier: 0,
                    x: sectionWidth + 120,
                    y: 100,
                    width: 260,
                    height: 300,
                    rotation: 0,
                    seats: []
                });
            }
        }

        state = normalize(newState);
        activeFloorId = state.floors[0].clientId;
        selected = state.sections[0]
            ? { type: 'section', id: state.sections[0].clientId }
            : { type: null, id: null };
        render();
    }

    function setReferenceImage(url) {
        if (!stage) return;
        var existing = stage.querySelector('.layout-pro-reference');
        if (!url) {
            if (existing) existing.remove();
            stage.classList.remove('has-reference');
            return;
        }

        if (!existing) {
            existing = document.createElement('img');
            existing.className = 'layout-pro-reference';
            existing.alt = 'Референтна снимка на залата';
            stage.insertBefore(existing, stage.firstChild);
        }
        existing.src = url;
        stage.classList.add('has-reference');
    }

    function generateRows() {
        var section = selectedSection();
        if (!section) return;
        remember();
        var rows = Math.max(1, parseInt(els.rowsCount.value || '5', 10));
        var perRow = Math.max(1, parseInt(els.rowsSize.value || '10', 10));
        var padX = 28;
        var padTop = 58;
        var gapX = perRow === 1 ? 0 : (section.width - padX * 2 - 28) / (perRow - 1);
        var gapY = rows === 1 ? 0 : (section.height - padTop - 30) / (rows - 1);
        section.seats = [];
        for (var r = 0; r < rows; r++) {
            var row = String.fromCharCode(65 + (r % 26));
            for (var c = 0; c < perRow; c++) {
                section.seats.push({
                    clientId: uid('seat'),
                    row: row,
                    number: String(c + 1),
                    label: row + (c + 1),
                    x: padX + c * gapX,
                    y: padTop + r * gapY,
                    radius: 15,
                    rotation: 0,
                    capacity: 1,
                    isCapacityUnlimited: false,
                    seatType: section.type === 'VIP' ? 'VIP' : 'Standard',
                    status: 'Active'
                });
            }
        }
        select('section', section.clientId);
    }

    function generateTableSeats() {
        var section = selectedSection();
        if (!section) return;
        remember();
        section.type = 'Table';
        section.shape = 'Circle';
        section.seats = [];
        var size = Math.max(2, parseInt(els.tableSize.value || '6', 10));
        var cx = section.width / 2;
        var cy = section.height / 2;
        var radius = Math.min(section.width, section.height) / 2 - 38;
        for (var i = 0; i < size; i++) {
            var a = -Math.PI / 2 + (Math.PI * 2 * i / size);
            section.seats.push({
                clientId: uid('seat'),
                row: 'T',
                number: String(i + 1),
                label: 'T' + (i + 1),
                x: cx + Math.cos(a) * radius - 15,
                y: cy + Math.sin(a) * radius - 15,
                radius: 15,
                rotation: Math.round(a * 180 / Math.PI + 90),
                capacity: 1,
                isCapacityUnlimited: false,
                seatType: 'Standard',
                status: 'Active'
            });
        }
        select('section', section.clientId);
    }

    function duplicateSelected() {
        var section = selectedSection();
        var seat = selectedSeat();
        if (!section) return;
        remember();
        if (seat && section) {
            var copy = Object.assign({}, seat, {
                clientId: uid('seat'),
                number: String(section.seats.length + 1),
                label: seat.row + (section.seats.length + 1),
                x: clamp(seat.x + 24, 0, section.width - seat.radius * 2),
                y: clamp(seat.y + 24, 0, section.height - seat.radius * 2)
            });
            section.seats.push(copy);
            select('seat', copy.clientId);
            return;
        }
        var clone = JSON.parse(JSON.stringify(section));
        clone.clientId = uid('section');
        clone.name = section.name + ' copy';
        clone.x = clamp(section.x + 32, 0, state.canvasWidth - section.width);
        clone.y = clamp(section.y + 32, 0, state.canvasHeight - section.height);
        clone.seats.forEach(function (s) { s.clientId = uid('seat'); });
        state.sections.push(clone);
        select('section', clone.clientId);
    }

    function deleteSelected() {
        var section = selectedSection();
        var seat = selectedSeat();
        if (!section) return;
        remember();
        if (seat && section) {
            section.seats = section.seats.filter(function (s) { return s.clientId !== seat.clientId; });
            selected = { type: 'section', id: section.clientId };
            render();
            return;
        }
        state.sections = state.sections.filter(function (s) { return s.clientId !== section.clientId; });
        var first = state.sections.find(function (s) { return s.floorId === activeFloorId; });
        selected = first ? { type: 'section', id: first.clientId } : { type: null, id: null };
        render();
    }

    function bindInput(el, getter) {
        if (!el) return;
        el.addEventListener('input', getter);
        el.addEventListener('change', getter);
    }

    function updateSection(prop, value) {
        var section = selectedSection();
        if (!section) return;
        remember();
        section[prop] = value;
        render();
    }

    function updateSeat(prop, value) {
        var seat = selectedSeat();
        if (!seat) return;
        remember();
        seat[prop] = value;
        if (prop === 'row' || prop === 'number') seat.label = seat.row + seat.number;
        render();
    }

    if (els.addFloor) els.addFloor.addEventListener('click', addFloor);
    if (els.addSection) els.addSection.addEventListener('click', function () { addSection('Seated', 'Rectangle'); });
    if (els.addStanding) els.addStanding.addEventListener('click', function () { addSection('Standing', 'Rounded'); });
    if (els.addTable) els.addTable.addEventListener('click', function () { addSection('Table', 'Circle'); });
    if (els.generateRows) els.generateRows.addEventListener('click', generateRows);
    if (els.generateTable) els.generateTable.addEventListener('click', generateTableSeats);
    if (els.undo) els.undo.addEventListener('click', undo);
    if (els.redo) els.redo.addEventListener('click', redo);
    if (els.duplicate) els.duplicate.addEventListener('click', duplicateSelected);
    if (els.del) els.del.addEventListener('click', deleteSelected);
    if (ai.generate) ai.generate.addEventListener('click', generateAiLayout);
    if (ai.image) {
        ai.image.addEventListener('change', function () {
            var file = ai.image.files && ai.image.files[0];
            if (!file) return;
            if (referenceObjectUrl) URL.revokeObjectURL(referenceObjectUrl);
            referenceObjectUrl = URL.createObjectURL(file);
            setReferenceImage(referenceObjectUrl);
        });
    }
    if (ai.clearImage) {
        ai.clearImage.addEventListener('click', function () {
            if (referenceObjectUrl) URL.revokeObjectURL(referenceObjectUrl);
            referenceObjectUrl = null;
            if (ai.image) ai.image.value = '';
            setReferenceImage(null);
        });
    }
    if (ai.tutorial && ai.tutorialPanel) {
        ai.tutorial.addEventListener('click', function () {
            ai.tutorialPanel.hidden = false;
        });
    }
    if (ai.tutorialClose && ai.tutorialPanel) {
        ai.tutorialClose.addEventListener('click', function () {
            ai.tutorialPanel.hidden = true;
        });
        ai.tutorialPanel.addEventListener('click', function (event) {
            if (event.target === ai.tutorialPanel) ai.tutorialPanel.hidden = true;
        });
    }
    root.addEventListener('keydown', function (event) {
        if (!event.ctrlKey && !event.metaKey) return;
        if (event.key.toLowerCase() === 'z') {
            event.preventDefault();
            if (event.shiftKey) redo();
            else undo();
        }
        if (event.key.toLowerCase() === 'y') {
            event.preventDefault();
            redo();
        }
    });

    if (floorNameInput) {
        floorNameInput.addEventListener('focus', remember, { once: false });
        floorNameInput.addEventListener('input', function () {
            var floor = activeFloor();
            floor.name = floorNameInput.value || floor.name;
            state.sections.filter(function (s) { return s.floorId === floor.clientId; }).forEach(function (s) { s.floorName = floor.name; });
            render();
        });
    }

    bindInput(els.section.name, function () { updateSection('name', els.section.name.value); });
    bindInput(els.section.type, function () { updateSection('type', els.section.type.value); });
    bindInput(els.section.shape, function () { updateSection('shape', els.section.shape.value); });
    bindInput(els.section.price, function () { updateSection('priceModifier', number(els.section.price.value, 0)); });
    bindInput(els.section.x, function () { updateSection('x', number(els.section.x.value, 0)); });
    bindInput(els.section.y, function () { updateSection('y', number(els.section.y.value, 0)); });
    bindInput(els.section.width, function () { updateSection('width', Math.max(80, number(els.section.width.value, 80))); });
    bindInput(els.section.height, function () { updateSection('height', Math.max(70, number(els.section.height.value, 70))); });
    bindInput(els.section.rotation, function () { updateSection('rotation', number(els.section.rotation.value, 0)); });

    bindInput(els.seat.label, function () { updateSeat('label', els.seat.label.value); });
    bindInput(els.seat.row, function () { updateSeat('row', els.seat.row.value || 'A'); });
    bindInput(els.seat.number, function () { updateSeat('number', els.seat.number.value || '1'); });
    bindInput(els.seat.type, function () { updateSeat('seatType', els.seat.type.value); });
    bindInput(els.seat.status, function () { updateSeat('status', els.seat.status.value); });
    bindInput(els.seat.x, function () { updateSeat('x', number(els.seat.x.value, 0)); });
    bindInput(els.seat.y, function () { updateSeat('y', number(els.seat.y.value, 0)); });
    bindInput(els.seat.radius, function () { updateSeat('radius', Math.max(9, number(els.seat.radius.value, 15))); });
    bindInput(els.seat.capacity, function () { updateSeat('capacity', Math.max(1, parseInt(els.seat.capacity.value || '1', 10))); });
    bindInput(els.seat.unlimited, function () { updateSeat('isCapacityUnlimited', els.seat.unlimited.checked); });

    sync();
    render();
    updateHistoryButtons();
})();
