(function () {
    'use strict';

    var CITY_FALLBACK = {
        'софия': [42.6977, 23.3219], 'sofia': [42.6977, 23.3219],
        'пловдив': [42.1354, 24.7453], 'plovdiv': [42.1354, 24.7453],
        'варна': [43.2141, 27.9147], 'varna': [43.2141, 27.9147],
        'бургас': [42.5048, 27.4626], 'burgas': [42.5048, 27.4626],
        'русе': [43.8356, 25.9657], 'ruse': [43.8356, 25.9657],
        'стара загора': [42.4258, 25.6345], 'stara zagora': [42.4258, 25.6345],
        'плевен': [43.4170, 24.6067], 'pleven': [43.4170, 24.6067],
        'велико търново': [43.0757, 25.6172], 'veliko tarnovo': [43.0757, 25.6172],
        'благоевград': [42.0209, 23.0943], 'blagoevgrad': [42.0209, 23.0943],
        'шумен': [43.2712, 26.9361], 'shumen': [43.2712, 26.9361],
        'добрич': [43.5726, 27.8273], 'dobrich': [43.5726, 27.8273],
        'сливен': [42.6817, 26.3229], 'sliven': [42.6817, 26.3229],
        'перник': [42.6052, 23.0378], 'pernik': [42.6052, 23.0378],
        'хасково': [41.9344, 25.5556], 'haskovo': [41.9344, 25.5556],
        'ямбол': [42.4842, 26.5035], 'yambol': [42.4842, 26.5035]
    };

    var BG_BOUNDS = { south: 41.2, west: 22.0, north: 44.3, east: 28.8 };
    var BG_CENTER = { lat: 42.7339, lng: 25.4858 };
    var BG_ZOOM = 7;

    var GENRE_COLORS = {
        techno: '#0ea5e9', house: '#a855f7', hiphop: '#f59e0b',
        pop: '#ec4899', rock: '#dc2626', jazz: '#7c3aed',
        classical: '#14b8a6', other: '#0f766e'
    };

    function escapeHtml(value) {
        return String(value == null ? '' : value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function formatDate(iso) {
        try {
            var lang = currentLang() === 'en' ? 'en-GB' : 'bg-BG';
            return new Date(iso).toLocaleString(lang, {
                day: '2-digit',
                month: 'short',
                hour: '2-digit',
                minute: '2-digit'
            });
        } catch (_) { return iso; }
    }

    function currentLang() {
        var meta = document.querySelector('meta[name="x-app-lang"]');
        return meta && meta.getAttribute('content') === 'en' ? 'en' : 'bg';
    }

    function normalize(s) { return (s == null ? '' : String(s)).trim().toLowerCase(); }

    function readHomeEventsData() {
        var dataEl = document.getElementById('home-events-data');
        if (!dataEl) return [];
        try {
            var parsed = JSON.parse(dataEl.textContent || '{}');
            return parsed && parsed.markers ? parsed.markers : [];
        } catch (_) {
            return [];
        }
    }

    function lookupCityCoords(city) {
        if (!city) return null;
        return CITY_FALLBACK[normalize(city)] || null;
    }

    function genreColor(genre) {
        return GENRE_COLORS[normalize(genre)] || GENRE_COLORS.other;
    }

    function eventMatchesFilter(ev, filter) {
        if (!filter) return true;

        if (filter.city) {
            var fcity = normalize(filter.city);
            var fc = lookupCityCoords(filter.city);
            var ec = lookupCityCoords(ev.city);
            var byName = normalize(ev.city) === fcity ||
                normalize(ev.city).indexOf(fcity) !== -1 ||
                fcity.indexOf(normalize(ev.city)) !== -1;
            var byCoords = !!(fc && ec && fc[0] === ec[0] && fc[1] === ec[1]);
            if (!byName && !byCoords) return false;
        }

        if (filter.genre && normalize(ev.genre) !== normalize(filter.genre)) return false;

        var terms = (filter.keywords && filter.keywords.length) ? filter.keywords.slice() : [];
        if (filter.keyword) terms.push(filter.keyword);
        terms = terms.map(normalize)
            .filter(function (t) { return t && t.length >= 2; })
            .filter(function (t) {
                if (filter.city && t === normalize(filter.city)) return false;
                if (filter.genre && t === normalize(filter.genre)) return false;
                return true;
            });
        if (terms.length === 0) return true;

        var haystack = [ev.title, ev.description, ev.venueName, ev.city, ev.address, ev.genre]
            .map(normalize).join(' ');
        for (var i = 0; i < terms.length; i++) {
            if (haystack.indexOf(terms[i]) === -1) return false;
        }
        return true;
    }

    function popupHtml(m) {
        var lang = currentLang();
        var detailsText = lang === 'en' ? 'Open event' : 'Виж събитието';
        var approxText = lang === 'en' ? 'approx.' : 'прибл.';
        var organizerText = lang === 'en' ? 'Organizer' : 'Организатор';
        var location = [m.address, m.city].filter(Boolean).join(', ');

        return '<article class="evt-map-card">' +
            (m.imageUrl
                ? '<a class="evt-map-card__media" href="/Events/Details/' + encodeURIComponent(m.eventId) + '">' +
                    '<img src="' + escapeHtml(m.imageUrl) + '" alt="' + escapeHtml(m.title) + '" />' +
                  '</a>'
                : '') +
            '<div class="evt-map-card__body">' +
                '<div class="evt-map-card__chips">' +
                    '<span class="evt-map-card__chip" style="background:' + genreColor(m.genre) + ';">' + escapeHtml(m.genre || '') + '</span>' +
                    (m.isApproximate ? '<span class="evt-map-card__chip evt-map-card__chip--muted">' + approxText + '</span>' : '') +
                '</div>' +
                '<a class="evt-map-card__title" href="/Events/Details/' + encodeURIComponent(m.eventId) + '">' + escapeHtml(m.title) + '</a>' +
                '<div class="evt-map-card__meta"><i class="bi bi-geo-alt"></i><span>' + escapeHtml(location || m.city || '') + '</span></div>' +
                '<div class="evt-map-card__meta"><i class="bi bi-clock"></i><span>' + escapeHtml(formatDate(m.startTime)) + '</span></div>' +
                (m.organizerName ? '<div class="evt-map-card__meta"><i class="bi bi-person-badge"></i><span>' + organizerText + ': ' + escapeHtml(m.organizerName) + '</span></div>' : '') +
                '<a class="evt-map-card__cta" href="/Events/Details/' + encodeURIComponent(m.eventId) + '">' +
                    '<span>' + detailsText + '</span><i class="bi bi-arrow-right"></i>' +
                '</a>' +
            '</div>' +
        '</article>';
    }

    function pinSvg(color) {
        return 'data:image/svg+xml;charset=UTF-8,' + encodeURIComponent(
            '<svg xmlns="http://www.w3.org/2000/svg" width="32" height="44" viewBox="0 0 32 44">' +
            '<path d="M16 0C7.16 0 0 7.16 0 16c0 11 16 28 16 28s16-17 16-28C32 7.16 24.84 0 16 0z" fill="' + color + '"/>' +
            '<circle cx="16" cy="16" r="6" fill="#fff"/>' +
            '</svg>'
        );
    }

    function setStatus(el, level, text) {
        if (!el) return;
        if (!text) { el.className = 'small text-muted'; el.textContent = ''; return; }
        var cls = 'small ';
        switch (level) {
            case 'success': cls += 'text-success'; break;
            case 'warning': cls += 'text-warning'; break;
            case 'danger': cls += 'text-danger'; break;
            case 'info': cls += 'text-primary'; break;
            default: cls += 'text-muted';
        }
        el.className = cls;
        el.textContent = text;
    }

    function whenMapsReady(cb) {
        if (window.google && window.google.maps) { cb(); return; }
        window.GROOVEON_MAPS = window.GROOVEON_MAPS || { ready: false, callbacks: [] };
        if (window.GROOVEON_MAPS.ready) { cb(); return; }
        window.GROOVEON_MAPS.callbacks.push(cb);
    }

    function init() {
        var mapEl = document.getElementById('events-map');
        if (!mapEl) return;

        var markersData = readHomeEventsData();

        var form = document.getElementById('home-smart-search-form');
        var input = document.getElementById('home-smart-search-input');
        var clearBtn = document.getElementById('home-smart-clear');
        var statusEl = document.getElementById('home-search-status');
        var antiforgeryEl = document.querySelector('input[name="__RequestVerificationToken"]');
        var antiforgery = antiforgeryEl ? antiforgeryEl.value : null;

        var pendingFilter = null;
        var bridge = { applyFilter: function (f) { pendingFilter = f; return null; } };
        var mapStarted = false;

        function setMapLoadMessage(text) {
            var loadingText = mapEl.querySelector('.map-loading-state span');
            if (loadingText) loadingText.textContent = text;
        }

        window.gm_authFailure = function () {
            setMapLoadMessage('Google Maps rejected this key. Check Maps JavaScript API, billing, and referrer restrictions.');
        };

        function startMap() {
            if (mapStarted || !(window.google && window.google.maps)) return;
            mapStarted = true;
            markersData = readHomeEventsData();
            setupMap(mapEl);
        }

        window.GrooveHomeStartMap = startMap;
        whenMapsReady(startMap);
        var mapPollCount = 0;
        var mapPoll = window.setInterval(function () {
            if (window.google && window.google.maps) {
                window.clearInterval(mapPoll);
                startMap();
                return;
            }
            mapPollCount++;
            if (mapPollCount > 80) {
                window.clearInterval(mapPoll);
            }
        }, 100);

        // Fallback: try again after all resources finish loading (catches edge cases with cached Maps API)
        window.addEventListener('load', function () {
            if (!mapStarted && window.google && window.google.maps) startMap();
        });

        // bfcache: when user presses Back/Forward the page is restored from memory —
        // hide any stale loading overlay and trigger a map resize so tiles re-render.
        window.addEventListener('pageshow', function (event) {
            if (!event.persisted) return;
            var loadingEl = mapEl.querySelector('.map-loading-state');
            if (loadingEl) loadingEl.style.display = 'none';
            window.setTimeout(function () {
                if (window.GrooveHomeMapRefresh) window.GrooveHomeMapRefresh();
            }, 80);
        });

        window.setTimeout(function () {
            if (mapStarted || (window.google && window.google.maps)) return;
            setMapLoadMessage('Map is still loading. Check network, API restrictions, or the browser console.');
        }, 2500);

        function localFallback(query) {
            var q = normalize(query);
            var foundCity = null;
            for (var key in CITY_FALLBACK) {
                if (Object.prototype.hasOwnProperty.call(CITY_FALLBACK, key) && q.indexOf(key) !== -1) {
                    foundCity = key; break;
                }
            }
            var coords = foundCity ? CITY_FALLBACK[foundCity] : null;
            return {
                rawQuery: query, city: foundCity, genre: null, keyword: query,
                keywords: query.split(/\s+/).filter(function (t) { return t.length >= 2; }),
                latitude: coords ? coords[0] : null, longitude: coords ? coords[1] : null,
                nearMe: false, aiUsed: false, aiStatus: 'LocalFallback'
            };
        }

        function describeFilter(f) {
            var parts = [];
            if (f.aiUsed) parts.push('AI:');
            if (f.city) parts.push('city=' + f.city);
            if (f.genre) parts.push('genre=' + f.genre);
            if (f.dateIntent) parts.push('when=' + f.dateIntent);
            if (f.keyword && !f.city && !f.genre) parts.push('keyword=' + f.keyword);
            if (f.nearMe) parts.push('near me');
            return parts.length > 1 ? parts.join(' ') : '';
        }

        function doSmartSearch(query) {
            query = (query || '').trim();
            if (!query) { bridge.applyFilter(null); setStatus(statusEl, null, ''); return; }
            setStatus(statusEl, 'info', 'Searching...');
            var headers = { 'Content-Type': 'application/json' };
            if (antiforgery) headers['RequestVerificationToken'] = antiforgery;

            fetch('/api/search/smart', {
                method: 'POST', headers: headers,
                body: JSON.stringify({ query: query }), credentials: 'same-origin'
            }).then(function (r) {
                if (!r.ok) throw new Error('HTTP ' + r.status);
                return r.json();
            }).then(function (data) {
                var filter = {
                    rawQuery: data.rawQuery || query,
                    city: data.city || null, genre: data.genre || null,
                    keyword: data.keyword || null, keywords: data.keywords || [],
                    dateIntent: data.dateIntent || null,
                    latitude: data.latitude == null ? null : data.latitude,
                    longitude: data.longitude == null ? null : data.longitude,
                    nearMe: !!data.nearMe, aiUsed: !!data.aiUsed
                };
                var res = bridge.applyFilter(filter);
                var summary = describeFilter(filter);
                if (res && res.visible === 0) {
                    setStatus(statusEl, 'warning',
                        'No events found for "' + query + '"' + (summary ? ' (' + summary + ')' : '') + '.');
                } else if (res) {
                    setStatus(statusEl, filter.aiUsed ? 'success' : 'info',
                        (summary || 'Showing matches for "' + query + '"') +
                        ' — ' + res.visible + ' result' + (res.visible === 1 ? '' : 's'));
                } else {
                    setStatus(statusEl, 'info', 'Map is loading — try again in a second.');
                }
            }).catch(function (err) {
                var fallback = localFallback(query);
                var res = bridge.applyFilter(fallback);
                setStatus(statusEl, 'warning',
                    'Smart search unavailable, showing local matches' +
                    (res && res.visible === 0 ? ' (none found)' : (res ? ' (' + res.visible + ')' : '')) + '.');
                if (window.console) console.warn('Smart search failed', err);
            });
        }

        if (form) {
            form.addEventListener('submit', function (e) {
                e.preventDefault();
                doSmartSearch(input ? input.value : '');
                var mapSection = document.getElementById('event-map');
                if (mapSection) {
                    mapSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }
            });
        }
        if (clearBtn) {
            clearBtn.addEventListener('click', function (e) {
                e.preventDefault();
                if (input) input.value = '';
                bridge.applyFilter(null);
                setStatus(statusEl, null, '');
            });
        }

        var activeHomeRequest = null;

        function setHomeLoading(isLoading) {
            ['home-filter-tabs', 'home-events-surface'].forEach(function (id) {
                var el = document.getElementById(id);
                if (!el) return;
                el.classList.toggle('is-loading', !!isLoading);
                if (id === 'home-events-surface') {
                    el.setAttribute('aria-busy', isLoading ? 'true' : 'false');
                }
            });
        }

        function syncStatFromDocument(doc, id) {
            var next = doc.getElementById(id);
            var current = document.getElementById(id);
            if (next && current) current.textContent = next.textContent;
        }

        function replaceHomeDataScript(doc) {
            var nextData = doc.getElementById('home-events-data');
            if (!nextData) return;

            var currentData = document.getElementById('home-events-data');
            if (currentData) {
                currentData.textContent = nextData.textContent || '';
                return;
            }

            var clone = document.createElement('script');
            clone.id = 'home-events-data';
            clone.type = 'application/json';
            clone.textContent = nextData.textContent || '';
            document.body.appendChild(clone);
        }

        function swapHomeResults(html) {
            var doc = new DOMParser().parseFromString(html, 'text/html');
            var nextTabs = doc.getElementById('home-filter-tabs');
            var nextSurface = doc.getElementById('home-events-surface');
            var tabs = document.getElementById('home-filter-tabs');
            var surface = document.getElementById('home-events-surface');
            if (!nextTabs || !nextSurface || !tabs || !surface) return false;

            replaceHomeDataScript(doc);
            tabs.replaceWith(nextTabs);
            surface.replaceWith(nextSurface);
            syncStatFromDocument(doc, 'home-total-events-count');
            syncStatFromDocument(doc, 'home-city-count');

            if (typeof window.GrooveHomeUpdateEvents === 'function') {
                window.GrooveHomeUpdateEvents();
            }

            return true;
        }

        function loadHomeResults(url, pushState) {
            var tabs = document.getElementById('home-filter-tabs');
            var surface = document.getElementById('home-events-surface');
            if (!tabs || !surface || !window.fetch || !window.DOMParser) {
                window.location.href = url.href;
                return;
            }

            var savedScroll = window.scrollY || window.pageYOffset || 0;
            if (activeHomeRequest && activeHomeRequest.abort) activeHomeRequest.abort();
            activeHomeRequest = window.AbortController ? new AbortController() : null;

            setHomeLoading(true);
            fetch(url.href, {
                method: 'GET',
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'fetch' },
                signal: activeHomeRequest ? activeHomeRequest.signal : undefined
            }).then(function (response) {
                if (!response.ok) throw new Error('HTTP ' + response.status);
                return response.text();
            }).then(function (html) {
                if (!swapHomeResults(html)) throw new Error('Missing home results fragment');
                if (pushState) {
                    window.history.pushState({ homeResults: true }, '', url.href);
                }
                window.scrollTo(0, savedScroll);
            }).catch(function (err) {
                if (err && err.name === 'AbortError') return;
                if (window.console) console.warn('Home filter refresh failed', err);
                window.location.href = url.href;
            }).finally(function () {
                setHomeLoading(false);
                activeHomeRequest = null;
            });
        }

        function urlFromFilterForm(form) {
            var rawAction = form.getAttribute('action') || window.location.pathname;
            var url = new URL(rawAction, window.location.href);
            url.search = '';
            new FormData(form).forEach(function (value, key) {
                if (key === '__RequestVerificationToken') return;
                var text = value == null ? '' : String(value).trim();
                if (text) url.searchParams.set(key, text);
            });
            return url;
        }

        document.addEventListener('click', function (e) {
            if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
            var link = e.target.closest('#home-filter-tabs a.evt-search-tab-link, #home-events-surface .evt-chips a.evt-chip');
            if (!link) return;

            var url = new URL(link.href, window.location.href);
            if (url.origin !== window.location.origin) return;
            var sortSelect = document.querySelector('#home-events-filter select[name="sort"]');
            if (sortSelect && sortSelect.value && sortSelect.value !== 'recent' && !url.searchParams.has('sort')) {
                url.searchParams.set('sort', sortSelect.value);
            }
            e.preventDefault();
            loadHomeResults(url, true);
        });

        document.addEventListener('submit', function (e) {
            var formEl = e.target.closest && e.target.closest('#home-events-filter');
            if (!formEl) return;
            e.preventDefault();
            loadHomeResults(urlFromFilterForm(formEl), true);
        });

        document.addEventListener('change', function (e) {
            var select = e.target.closest && e.target.closest('#home-events-filter select[name="sort"]');
            if (!select || !select.form) return;
            if (select.form.requestSubmit) select.form.requestSubmit();
            else select.form.dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));
        });

        window.addEventListener('popstate', function () {
            loadHomeResults(new URL(window.location.href), false);
        });

        function setupMap(mapEl) {
            var map = new google.maps.Map(mapEl, {
                center: BG_CENTER,
                zoom: BG_ZOOM,
                mapTypeControl: false,
                streetViewControl: false,
                fullscreenControl: true,
                gestureHandling: 'cooperative',
                styles: [
                    { featureType: 'poi.business', stylers: [{ visibility: 'off' }] },
                    { featureType: 'transit', elementType: 'labels.icon', stylers: [{ visibility: 'off' }] }
                ]
            });
            var bgBounds = new google.maps.LatLngBounds(
                { lat: BG_BOUNDS.south, lng: BG_BOUNDS.west },
                { lat: BG_BOUNDS.north, lng: BG_BOUNDS.east }
            );
            map.fitBounds(bgBounds);

            // Hide loading overlay as soon as Maps has rendered its canvas
            var loadingOverlay = mapEl.querySelector('.map-loading-state');
            if (loadingOverlay) loadingOverlay.style.display = 'none';

            // Fix tiles not covering full container on initial load
            google.maps.event.addListenerOnce(map, 'tilesloaded', function () {
                var center = map.getCenter();
                google.maps.event.trigger(map, 'resize');
                if (center) map.setCenter(center);
            });
            setTimeout(function () {
                var center = map.getCenter();
                google.maps.event.trigger(map, 'resize');
                if (center) map.setCenter(center);
            }, 400);

            var infoWindow = new google.maps.InfoWindow({ maxWidth: 320, disableAutoPan: false });
            var liveMarkers = {};
            window.GrooveHomeMapRefresh = function () {
                if (!map || !(window.google && window.google.maps)) return;
                var center = map.getCenter();
                google.maps.event.trigger(map, 'resize');
                if (center) map.setCenter(center);
            };
            window.GrooveHomeUpdateEvents = function () {
                if (!map || !(window.google && window.google.maps)) return null;
                markersData = readHomeEventsData();
                var result = applyFilter(null);
                var center = map.getCenter();
                google.maps.event.trigger(map, 'resize');
                if (center) map.setCenter(center);
                return result;
            };

            // Preview panel helpers
            var previewPanel = document.getElementById('map-event-preview');
            var previewImg = document.getElementById('map-preview-img');
            var previewTitle = document.getElementById('map-preview-title');
            var previewAddress = document.getElementById('map-preview-address');
            var previewTime = document.getElementById('map-preview-time');
            var previewGenre = document.getElementById('map-preview-genre');
            var previewLink = document.getElementById('map-preview-link');
            var previewClose = document.getElementById('map-preview-close');

            var detailUrl = function(id) { return '/Events/Details/' + id; };

            function showPreview(m) {
                if (!previewPanel) return;
                if (previewImg) {
                    if (m.imageUrl) { previewImg.src = m.imageUrl; previewImg.style.display = ''; }
                    else { previewImg.style.display = 'none'; }
                }
                if (previewTitle) {
                    previewTitle.textContent = m.title || '';
                    previewTitle.style.cursor = 'pointer';
                    previewTitle.onclick = function () { window.location.href = detailUrl(m.eventId); };
                }
                if (previewAddress) previewAddress.textContent = (m.address || '') + (m.city ? ', ' + m.city : '');
                if (previewTime) previewTime.textContent = formatDate(m.startTime);
                if (previewGenre) {
                    previewGenre.textContent = m.genre || '';
                    previewGenre.style.background = genreColor(m.genre);
                    previewGenre.style.color = '#fff';
                }
                if (previewLink) previewLink.href = detailUrl(m.eventId);
                previewPanel.style.display = '';
                previewPanel.classList.add('is-open');
            }

            function closePreview() {
                if (!previewPanel) return;
                previewPanel.classList.remove('is-open');
                setTimeout(function () { previewPanel.style.display = 'none'; }, 220);
                infoWindow.close();
                document.querySelectorAll('.event-card.is-active').forEach(function (n) { n.classList.remove('is-active'); });
            }

            if (previewClose) previewClose.addEventListener('click', closePreview);

            function highlightCard(eventId, scroll) {
                document.querySelectorAll('.event-card.is-active').forEach(function (n) {
                    n.classList.remove('is-active');
                });
                var card = document.querySelector('.event-card[data-event-id="' + eventId + '"]');
                if (card) {
                    card.classList.add('is-active');
                    if (scroll) card.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
            }

            function clearMarkers() {
                Object.keys(liveMarkers).forEach(function (k) { liveMarkers[k].setMap(null); });
                liveMarkers = {};
            }

            function renderMarkers(filter) {
                clearMarkers();
                var bounds = new google.maps.LatLngBounds();
                var any = false;

                markersData.forEach(function (m) {
                    if (typeof m.lat !== 'number' || typeof m.lng !== 'number') return;
                    if (!eventMatchesFilter(m, filter)) return;

                    var pos = { lat: m.lat, lng: m.lng };
                    var color = genreColor(m.genre);
                    var marker = new google.maps.Marker({
                        map: map,
                        position: pos,
                        title: m.title,
                        icon: {
                            url: pinSvg(color),
                            size: new google.maps.Size(32, 44),
                            scaledSize: new google.maps.Size(32, 44),
                            anchor: new google.maps.Point(16, 44)
                        },
                        animation: google.maps.Animation.DROP
                    });

                    marker.addListener('click', function () {
                        infoWindow.setContent(popupHtml(m));
                        infoWindow.open({ map: map, anchor: marker });
                        if (previewPanel) {
                            previewPanel.classList.remove('is-open');
                            previewPanel.style.display = 'none';
                        }
                        highlightCard(m.eventId, false);
                    });

                    liveMarkers[m.eventId] = marker;
                    bounds.extend(pos);
                    any = true;
                });

                return { bounds: bounds, any: any };
            }

            function applyCardVisibility(filter) {
                var grid = document.getElementById('event-cards-grid');
                if (!grid) return 0;
                var visible = 0;
                Array.prototype.forEach.call(grid.children, function (col) {
                    var card = col.querySelector('.event-card');
                    if (!card) return;
                    var ev = {
                        id: card.getAttribute('data-event-id'),
                        title: col.getAttribute('data-event-title') || '',
                        city: col.getAttribute('data-event-city') || '',
                        address: col.getAttribute('data-event-address') || '',
                        genre: col.getAttribute('data-event-genre') || ''
                    };
                    var match = eventMatchesFilter(ev, filter);
                    col.style.display = match ? '' : 'none';
                    if (match) visible++;
                });
                return visible;
            }

            function fitMap(filter, result) {
                if (result.any) {
                    var keys = Object.keys(liveMarkers);
                    if (keys.length === 1) {
                        map.panTo(liveMarkers[keys[0]].getPosition());
                        map.setZoom(15);
                    } else {
                        map.fitBounds(result.bounds, 60);
                    }
                    return;
                }
                if (filter && typeof filter.latitude === 'number' && typeof filter.longitude === 'number') {
                    map.panTo({ lat: filter.latitude, lng: filter.longitude });
                    map.setZoom(12);
                    return;
                }
                if (filter && filter.city) {
                    var c = lookupCityCoords(filter.city);
                    if (c) { map.panTo({ lat: c[0], lng: c[1] }); map.setZoom(12); return; }
                }
                map.fitBounds(bgBounds);
            }

            function applyFilter(filter) {
                var rendered = renderMarkers(filter);
                var visible = applyCardVisibility(filter);
                updateCount(visible, !!filter);
                fitMap(filter, rendered);
                return { visible: visible };
            }

            function updateCount(visible, hasFilter) {
                var badge = document.getElementById('home-event-count');
                if (badge) badge.textContent = String(visible);
                var emptyEl = document.getElementById('home-no-results');
                if (emptyEl) emptyEl.style.display = (hasFilter && visible === 0) ? '' : 'none';
            }

            bridge.applyFilter = applyFilter;
            applyFilter(pendingFilter);
            pendingFilter = null;

            document.addEventListener('click', function (e) {
                if (e.target.closest('a[href]')) return;
                var trigger = e.target.closest('[data-show-on-map]');
                if (!trigger) return;
                e.preventDefault();
                var id = trigger.getAttribute('data-show-on-map');
                var marker = liveMarkers[id] || liveMarkers[parseInt(id, 10)];
                if (!marker) return;
                map.panTo(marker.getPosition());
                map.setZoom(16);
                google.maps.event.trigger(marker, 'click');
            });

            document.addEventListener('keydown', function (e) {
                if (e.key !== 'Enter' && e.key !== ' ') return;
                if (e.target.closest('a[href]')) return;
                var trigger = e.target.closest('[data-show-on-map]');
                if (!trigger) return;
                e.preventDefault();
                trigger.click();
            });

            // Geolocation
            var geoBtn = document.getElementById('use-my-location');
            var geoStatus = document.getElementById('geo-status');
            var youAreHere = null;
            if (geoBtn && navigator.geolocation) {
                geoBtn.addEventListener('click', function () {
                    if (geoStatus) geoStatus.textContent = 'Locating...';
                    navigator.geolocation.getCurrentPosition(function (pos) {
                        var p = { lat: pos.coords.latitude, lng: pos.coords.longitude };
                        if (youAreHere) youAreHere.setMap(null);
                        youAreHere = new google.maps.Marker({
                            map: map, position: p,
                            icon: {
                                path: google.maps.SymbolPath.CIRCLE,
                                scale: 8, fillColor: '#0d6efd', fillOpacity: 0.6,
                                strokeColor: '#0d6efd', strokeWeight: 2
                            },
                            title: 'You are here'
                        });
                        map.panTo(p); map.setZoom(13);
                        if (geoStatus) geoStatus.textContent = 'Showing your location.';
                    }, function () {
                        if (geoStatus) geoStatus.textContent = 'Location access denied or unavailable.';
                    }, { enableHighAccuracy: false, timeout: 8000, maximumAge: 60000 });
                });
            } else if (geoBtn) {
                geoBtn.disabled = true;
            }
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
