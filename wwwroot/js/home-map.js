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
        try { return new Date(iso).toLocaleString(); } catch (_) { return iso; }
    }

    function normalize(s) { return (s == null ? '' : String(s)).trim().toLowerCase(); }

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
        return '<div class="event-popup" style="width:240px;">' +
            (m.imageUrl ? '<img src="' + escapeHtml(m.imageUrl) + '" alt="" style="width:100%;height:110px;object-fit:cover;border-radius:6px;margin-bottom:6px;" />' : '') +
            '<div style="font-weight:700;margin-bottom:4px;font-size:0.95rem;">' + escapeHtml(m.title) + '</div>' +
            '<div style="font-size:0.78rem;color:#6c757d;margin-bottom:2px;">' +
                '<i class="bi bi-geo-alt"></i> ' + escapeHtml(m.address) + ', ' + escapeHtml(m.city) + '</div>' +
            '<div style="font-size:0.78rem;color:#6c757d;margin-bottom:4px;">' +
                '<i class="bi bi-clock"></i> ' + escapeHtml(formatDate(m.startTime)) + '</div>' +
            '<div style="font-size:0.78rem;margin:4px 0;">' +
                '<span class="badge" style="background:' + genreColor(m.genre) + ';color:#fff;">' + escapeHtml(m.genre) + '</span>' +
                (m.isApproximate ? ' <span class="badge bg-light text-muted border">approx.</span>' : '') +
            '</div>' +
            '<a class="btn btn-sm btn-primary mt-1 w-100" href="/Events/Details/' + m.eventId + '">Open details</a>' +
            '</div>';
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

        var dataEl = document.getElementById('home-events-data');
        var markersData = [];
        if (dataEl) {
            try {
                var parsed = JSON.parse(dataEl.textContent);
                markersData = parsed && parsed.markers ? parsed.markers : [];
            } catch (_) { markersData = []; }
        }

        var form = document.getElementById('home-smart-search-form');
        var input = document.getElementById('home-smart-search-input');
        var clearBtn = document.getElementById('home-smart-clear');
        var statusEl = document.getElementById('home-search-status');
        var antiforgeryEl = document.querySelector('input[name="__RequestVerificationToken"]');
        var antiforgery = antiforgeryEl ? antiforgeryEl.value : null;

        var pendingFilter = null;
        var bridge = { applyFilter: function (f) { pendingFilter = f; return null; } };

        whenMapsReady(function () { setupMap(mapEl); });

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

        function setupMap(mapEl) {
            var map = new google.maps.Map(mapEl, {
                center: BG_CENTER,
                zoom: BG_ZOOM,
                mapTypeControl: false,
                streetViewControl: false,
                fullscreenControl: true,
                gestureHandling: 'greedy',
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

            var infoWindow = new google.maps.InfoWindow({ maxWidth: 260 });
            var liveMarkers = {};

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
                        highlightCard(m.eventId, true);
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
                if (badge) badge.textContent = visible + ' event' + (visible === 1 ? '' : 's');
                var emptyEl = document.getElementById('home-no-results');
                if (emptyEl) emptyEl.style.display = (hasFilter && visible === 0) ? '' : 'none';
            }

            bridge.applyFilter = applyFilter;
            applyFilter(pendingFilter);
            pendingFilter = null;

            document.addEventListener('click', function (e) {
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
