(function () {
    'use strict';

    var BG_BOUNDS = { south: 41.2, west: 22.0, north: 44.3, east: 28.8 };
    var DEFAULT_CENTER = { lat: 42.6977, lng: 23.3219 };
    var CITY_COORDS = {
        Sofia: { lat: 42.6977, lng: 23.3219 },
        Plovdiv: { lat: 42.1354, lng: 24.7453 },
        Varna: { lat: 43.2141, lng: 27.9147 },
        Burgas: { lat: 42.5048, lng: 27.4626 },
        Ruse: { lat: 43.8564, lng: 25.9658 },
        'Stara Zagora': { lat: 42.4258, lng: 25.6342 },
        Pleven: { lat: 43.4170, lng: 24.6166 },
        Sliven: { lat: 42.6824, lng: 26.3293 },
        Dobrich: { lat: 43.5675, lng: 27.8275 },
        Shumen: { lat: 43.2706, lng: 26.9229 },
        Pernik: { lat: 42.6055, lng: 23.0307 },
        Haskovo: { lat: 41.9344, lng: 25.5556 },
        Yambol: { lat: 42.4842, lng: 26.5036 },
        Pazardzhik: { lat: 42.1928, lng: 24.3378 },
        Blagoevgrad: { lat: 42.0119, lng: 23.0897 },
        'Veliko Tarnovo': { lat: 43.0757, lng: 25.6172 },
        Vratsa: { lat: 43.2102, lng: 23.5527 },
        Gabrovo: { lat: 42.8740, lng: 25.3187 },
        Asenovgrad: { lat: 42.0167, lng: 24.8667 },
        Vidin: { lat: 43.9961, lng: 22.8775 },
        Kazanlak: { lat: 42.6175, lng: 25.3942 },
        Kyustendil: { lat: 42.2842, lng: 22.6911 },
        Montana: { lat: 43.4123, lng: 23.2256 },
        Targovishte: { lat: 43.2503, lng: 26.5722 },
        Razgrad: { lat: 43.5333, lng: 26.5167 },
        Silistra: { lat: 44.1167, lng: 27.2667 }
    };
    var BG_TO_LATIN = {
        'а': 'a', 'б': 'b', 'в': 'v', 'г': 'g', 'д': 'd', 'е': 'e', 'ж': 'zh',
        'з': 'z', 'и': 'i', 'й': 'y', 'к': 'k', 'л': 'l', 'м': 'm', 'н': 'n',
        'о': 'o', 'п': 'p', 'р': 'r', 'с': 's', 'т': 't', 'у': 'u', 'ф': 'f',
        'х': 'h', 'ц': 'ts', 'ч': 'ch', 'ш': 'sh', 'щ': 'sht', 'ъ': 'a',
        'ь': 'y', 'ю': 'yu', 'я': 'ya'
    };
    var CITY_CANONICAL = {};

    Object.keys(CITY_COORDS).forEach(function (name) {
        CITY_CANONICAL[normalizeCityKey(name)] = name;
    });

    function whenMapsReady(cb) {
        if (window.google && window.google.maps) { cb(); return; }
        window.GROOVEON_MAPS = window.GROOVEON_MAPS || { ready: false, callbacks: [] };
        if (window.GROOVEON_MAPS.ready) { cb(); return; }
        window.GROOVEON_MAPS.callbacks.push(cb);
    }

    function normalizeCityKey(value) {
        return (value || '')
            .toString()
            .trim()
            .toLowerCase()
            .replace(/[.,]+/g, ' ')
            .replace(/\s+/g, ' ');
    }

    function transliterateBgToLatin(value) {
        var normalized = normalizeCityKey(value);
        var out = '';

        for (var i = 0; i < normalized.length; i++) {
            out += BG_TO_LATIN[normalized.charAt(i)] || normalized.charAt(i);
        }

        return out;
    }

    function canonicalizeCityName(value) {
        var normalized = normalizeCityKey(value);
        if (!normalized) return '';

        return CITY_CANONICAL[normalized]
            || CITY_CANONICAL[transliterateBgToLatin(normalized)]
            || '';
    }

    function getCityFieldText(input) {
        if (!input) return '';

        if (input.tagName === 'SELECT') {
            var opt = input.options[input.selectedIndex];
            return opt ? (opt.text || opt.value || '') : (input.value || '');
        }

        return input.value || '';
    }

    function dispatchValueEvents(input) {
        input.dispatchEvent(new Event('input', { bubbles: true }));
        input.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function findCityOption(selectEl, value) {
        if (!selectEl || selectEl.tagName !== 'SELECT') return null;

        var normalized = normalizeCityKey(value);
        var canonical = canonicalizeCityName(value);

        for (var i = 0; i < selectEl.options.length; i++) {
            var opt = selectEl.options[i];
            if (normalizeCityKey(opt.value) === normalized || normalizeCityKey(opt.text) === normalized) {
                return opt;
            }
        }

        if (!canonical) return null;

        var canonicalKey = normalizeCityKey(canonical);
        for (var j = 0; j < selectEl.options.length; j++) {
            var option = selectEl.options[j];
            if (normalizeCityKey(option.value) === canonicalKey || normalizeCityKey(option.text) === canonicalKey) {
                return option;
            }
        }

        return null;
    }

    function pickComponent(components, type, useShortName) {
        if (!components) return '';
        for (var i = 0; i < components.length; i++) {
            if (components[i].types && components[i].types.indexOf(type) !== -1) {
                return useShortName ? components[i].short_name : components[i].long_name;
            }
        }
        return '';
    }

    function buildAddress(place) {
        var c = place.address_components || [];
        var route = pickComponent(c, 'route');
        var streetNumber = pickComponent(c, 'street_number');
        var sublocality = pickComponent(c, 'sublocality') || pickComponent(c, 'sublocality_level_1');
        var premise = pickComponent(c, 'premise');
        var name = place.name && place.name !== route ? place.name : '';

        var parts = [];
        if (name && !route) parts.push(name);
        if (route) parts.push(streetNumber ? (route + ' ' + streetNumber) : route);
        else if (premise) parts.push(premise);
        if (sublocality) parts.push(sublocality);

        var built = parts.join(', ').trim();
        if (place.formatted_address) {
            var formatted = place.formatted_address.trim();
            if (!built || built.length < 4) return formatted;
        }

        return built;
    }

    function pickCity(place) {
        var c = place.address_components || [];
        return pickComponent(c, 'locality')
            || pickComponent(c, 'postal_town')
            || pickComponent(c, 'administrative_area_level_2')
            || pickComponent(c, 'administrative_area_level_1');
    }

    function setVal(input, value, autoset) {
        if (!input) return;

        var nextValue = value == null ? '' : value;
        if (autoset) input.dataset.autoset = '1';

        if (input.id === 'City') {
            if (input.tagName === 'SELECT') {
                var match = findCityOption(input, nextValue);
                if (!match) {
                    if (!nextValue && input.value) {
                        input.value = '';
                        dispatchValueEvents(input);
                    }
                    return;
                }

                if (input.value === match.value) return;
                input.value = match.value;
                dispatchValueEvents(input);
                return;
            }

            nextValue = canonicalizeCityName(nextValue) || nextValue;
        }

        if (input.value === nextValue) return;
        input.value = nextValue;
        dispatchValueEvents(input);
    }

    function init() {
        var addressInput = document.getElementById('Address');
        var cityInput = document.getElementById('City');
        var latInput = document.getElementById('Latitude');
        var lngInput = document.getElementById('Longitude');
        var mapEl = document.getElementById('event-location-map');
        var statusEl = document.getElementById('event-location-status');

        if (!addressInput && !mapEl) return;

        whenMapsReady(setup);

        function setup() {
            var bgBounds = new google.maps.LatLngBounds(
                { lat: BG_BOUNDS.south, lng: BG_BOUNDS.west },
                { lat: BG_BOUNDS.north, lng: BG_BOUNDS.east }
            );

            var map = null, marker = null, geocoder = null;

            if (mapEl) {
                var initialLat = parseFloat(latInput && latInput.value);
                var initialLng = parseFloat(lngInput && lngInput.value);
                var hasInitial = isFinite(initialLat) && isFinite(initialLng);

                map = new google.maps.Map(mapEl, {
                    center: hasInitial ? { lat: initialLat, lng: initialLng } : DEFAULT_CENTER,
                    zoom: hasInitial ? 17 : 7,
                    mapTypeControl: false,
                    streetViewControl: false,
                    fullscreenControl: true,
                    gestureHandling: 'greedy'
                });

                if (hasInitial) {
                    marker = new google.maps.Marker({
                        map: map,
                        position: { lat: initialLat, lng: initialLng },
                        draggable: true,
                        animation: google.maps.Animation.DROP
                    });
                    bindMarkerDrag();
                } else {
                    map.fitBounds(bgBounds);
                }

                map.addListener('click', function (e) {
                    placeMarker(e.latLng);
                    setVal(latInput, e.latLng.lat().toFixed(6));
                    setVal(lngInput, e.latLng.lng().toFixed(6));
                    reverseGeocode(e.latLng);
                });
            }

            geocoder = new google.maps.Geocoder();

            function bindMarkerDrag() {
                if (!marker) return;
                marker.addListener('dragend', function () {
                    var pos = marker.getPosition();
                    setVal(latInput, pos.lat().toFixed(6));
                    setVal(lngInput, pos.lng().toFixed(6));
                    reverseGeocode(pos);
                });
            }

            function placeMarker(latLng) {
                if (!map) return;
                if (!marker) {
                    marker = new google.maps.Marker({
                        map: map,
                        position: latLng,
                        draggable: true,
                        animation: google.maps.Animation.DROP
                    });
                    bindMarkerDrag();
                } else {
                    marker.setPosition(latLng);
                }
            }

            function reverseGeocode(latLng) {
                if (!geocoder) return;
                if (statusEl) statusEl.textContent = 'Resolving address...';

                geocoder.geocode({ location: latLng, language: 'bg', region: 'BG' }, function (results, status) {
                    if (status !== 'OK' || !results || !results.length) {
                        if (statusEl) statusEl.textContent = '';
                        return;
                    }

                    var place = results[0];
                    var city = pickCity(place);
                    var addr = buildAddress(place);

                    if (cityInput && (cityInput.dataset.autoset === '1' || !cityInput.value)) {
                        setVal(cityInput, city, true);
                    }

                    if (addressInput && (addressInput.dataset.autoset === '1' || !addressInput.value)) {
                        setVal(addressInput, addr, true);
                    }

                    if (statusEl) statusEl.textContent = 'Pinned: ' + (place.formatted_address || addr);
                });
            }

            if (addressInput && google.maps.places && google.maps.places.Autocomplete) {
                var ac = new google.maps.places.Autocomplete(addressInput, {
                    fields: ['geometry', 'address_components', 'formatted_address', 'name'],
                    componentRestrictions: { country: ['bg'] },
                    bounds: bgBounds,
                    strictBounds: false
                });

                ac.addListener('place_changed', function () {
                    var place = ac.getPlace();
                    if (!place || !place.geometry || !place.geometry.location) {
                        if (statusEl) statusEl.textContent = 'Pick a suggestion from the dropdown.';
                        return;
                    }

                    var loc = place.geometry.location;
                    setVal(latInput, loc.lat().toFixed(6));
                    setVal(lngInput, loc.lng().toFixed(6));

                    var city = pickCity(place);
                    if (city) setVal(cityInput, city, true);

                    var addr = buildAddress(place);
                    if (addr) setVal(addressInput, addr, true);

                    if (map) {
                        placeMarker(loc);
                        map.panTo(loc);
                        map.setZoom(17);
                    }

                    if (statusEl) statusEl.textContent = 'Pinned: ' + (place.formatted_address || addr);
                });

                addressInput.setAttribute(
                    'placeholder',
                    addressInput.getAttribute('placeholder') || 'Start typing a venue or address...'
                );
            }

            var acService = google.maps.places && google.maps.places.AutocompleteService
                ? new google.maps.places.AutocompleteService()
                : null;
            var suggestionsEl = document.getElementById('city-address-suggestions');
            var suggestionsTimer = 0;

            function parseLatLng(value) {
                if (!value) return null;

                var parts = value.split(',');
                var lat = parseFloat(parts[0]);
                var lng = parseFloat(parts[1]);
                if (!isFinite(lat) || !isFinite(lng)) return null;

                return { lat: lat, lng: lng };
            }

            function parseLatLngFromOption(el) {
                if (!el) return null;

                if (el.tagName === 'SELECT') {
                    var opt = el.options[el.selectedIndex];
                    if (opt) {
                        var optionCoords = parseLatLng(opt.getAttribute('data-latlng'));
                        if (optionCoords) return optionCoords;
                    }
                }

                var canonical = canonicalizeCityName(getCityFieldText(el));
                return canonical && CITY_COORDS[canonical] ? CITY_COORDS[canonical] : null;
            }

            function scheduleCityAddressSuggestions() {
                if (!suggestionsEl) return;
                window.clearTimeout(suggestionsTimer);
                suggestionsTimer = window.setTimeout(showCityAddressSuggestions, 120);
            }

            function showCityAddressSuggestions() {
                if (!acService || !suggestionsEl || !cityInput || !addressInput) return;

                var cityName = getCityFieldText(cityInput).trim();
                var coords = parseLatLngFromOption(cityInput);
                if (!cityName || !coords) {
                    suggestionsEl.style.display = 'none';
                    return;
                }

                var searchText = addressInput.value && addressInput.value.trim();

                var lat = coords.lat;
                var lng = coords.lng;
                var sw = new google.maps.LatLng(lat - 0.08, lng - 0.12);
                var ne = new google.maps.LatLng(lat + 0.08, lng + 0.12);
                var bounds = new google.maps.LatLngBounds(sw, ne);

                acService.getPlacePredictions({
                    input: searchText ? (cityName + ' ' + searchText) : (cityName + ' '),
                    bounds: bounds,
                    componentRestrictions: { country: 'bg' },
                    types: ['address']
                }, function (preds, status) {
                    if (status !== google.maps.places.PlacesServiceStatus.OK || !preds || !preds.length) {
                        suggestionsEl.style.display = 'none';
                        return;
                    }

                    var items = preds.slice(0, 3);
                    suggestionsEl.innerHTML = '';
                    items.forEach(function (p) {
                        var a = document.createElement('a');
                        a.className = 'list-group-item list-group-item-action';
                        a.href = '#';
                        a.textContent = p.description;
                        a.dataset.placeid = p.place_id;
                        a.addEventListener('click', function (ev) {
                            ev.preventDefault();
                            geocoder.geocode({ placeId: p.place_id, language: 'bg' }, function (results, gst) {
                                if (gst !== 'OK' || !results || !results.length) return;

                                var place = results[0];
                                var loc = place.geometry.location;
                                setVal(latInput, loc.lat().toFixed(6));
                                setVal(lngInput, loc.lng().toFixed(6));

                                var city = pickCity(place);
                                if (city) setVal(cityInput, city, true);

                                var addr = buildAddress(place);
                                if (addr) setVal(addressInput, addr, true);

                                if (map) {
                                    placeMarker(loc);
                                    map.panTo(loc);
                                    map.setZoom(17);
                                }

                                suggestionsEl.style.display = 'none';
                                if (statusEl) statusEl.textContent = 'Pinned: ' + (place.formatted_address || addr);
                            });
                        });
                        suggestionsEl.appendChild(a);
                    });

                    suggestionsEl.style.display = 'block';
                });
            }

            if (addressInput) {
                addressInput.addEventListener('focus', function () {
                    scheduleCityAddressSuggestions();
                });

                addressInput.addEventListener('input', function () {
                    if (!suggestionsEl) return;

                    var text = addressInput.value.trim();
                    if (text.length <= 1) {
                        scheduleCityAddressSuggestions();
                        return;
                    }

                    suggestionsEl.style.display = 'none';
                });

                document.addEventListener('click', function (e) {
                    if (suggestionsEl && !suggestionsEl.contains(e.target) && e.target !== addressInput) {
                        suggestionsEl.style.display = 'none';
                    }
                });
            }

            [addressInput, cityInput].forEach(function (el) {
                if (!el) return;
                el.addEventListener('input', function () { el.dataset.autoset = ''; });
            });

            var myLocationBtn = document.getElementById('btn-use-my-location');
            if (myLocationBtn) {
                myLocationBtn.addEventListener('click', function () {
                    if (!navigator.geolocation) {
                        if (statusEl) statusEl.textContent = 'Браузърът не поддържа геолокация.';
                        return;
                    }

                    var icon = myLocationBtn.querySelector('i');
                    myLocationBtn.disabled = true;
                    if (icon) { icon.className = 'bi bi-arrow-repeat'; }
                    if (statusEl) statusEl.textContent = 'Определяне на местоположението...';

                    navigator.geolocation.getCurrentPosition(
                        function (pos) {
                            var lat = pos.coords.latitude;
                            var lng = pos.coords.longitude;
                            var latLng = new google.maps.LatLng(lat, lng);

                            setVal(latInput, lat.toFixed(6));
                            setVal(lngInput, lng.toFixed(6));

                            if (map) {
                                placeMarker(latLng);
                                map.panTo(latLng);
                                map.setZoom(17);
                            }

                            if (addressInput) addressInput.dataset.autoset = '1';
                            if (cityInput) cityInput.dataset.autoset = '1';
                            reverseGeocode(latLng);

                            myLocationBtn.disabled = false;
                            if (icon) { icon.className = 'bi bi-geo-alt-fill'; }
                        },
                        function () {
                            myLocationBtn.disabled = false;
                            if (icon) { icon.className = 'bi bi-geo-alt-fill'; }
                            if (statusEl) statusEl.textContent = 'Достъпът до местоположението е отказан.';
                        },
                        { enableHighAccuracy: true, timeout: 10000 }
                    );
                });
            }

            if (addressInput) {
                addressInput.addEventListener('keydown', function (e) {
                    if (e.key === 'Enter') {
                        var pacOpen = document.querySelector('.pac-container:not([style*="display: none"])');
                        if (pacOpen && pacOpen.querySelector('.pac-item-selected, .pac-item:hover')) {
                            e.preventDefault();
                        }
                    }
                });
            }
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
