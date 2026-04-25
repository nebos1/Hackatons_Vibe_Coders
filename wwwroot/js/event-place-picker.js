(function () {
    'use strict';

    var BG_BOUNDS = { south: 41.2, west: 22.0, north: 44.3, east: 28.8 };
    var DEFAULT_CENTER = { lat: 42.6977, lng: 23.3219 };

    function whenMapsReady(cb) {
        if (window.google && window.google.maps) { cb(); return; }
        window.GROOVEON_MAPS = window.GROOVEON_MAPS || { ready: false, callbacks: [] };
        if (window.GROOVEON_MAPS.ready) { cb(); return; }
        window.GROOVEON_MAPS.callbacks.push(cb);
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

        if (parts.length === 0 && place.formatted_address) return place.formatted_address;
        return parts.join(', ');
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
        if (input.value === value) return;
        input.value = value == null ? '' : value;
        if (autoset) input.dataset.autoset = '1';
        input.dispatchEvent(new Event('input', { bubbles: true }));
        input.dispatchEvent(new Event('change', { bubbles: true }));
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
                        map: map, position: latLng, draggable: true,
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

            // Address autocomplete (Places API)
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

                addressInput.setAttribute('placeholder',
                    addressInput.getAttribute('placeholder') || 'Start typing a venue or address...');
            }

            [addressInput, cityInput].forEach(function (el) {
                if (!el) return;
                el.addEventListener('input', function () { el.dataset.autoset = ''; });
            });

            // Suppress form submit on Enter inside the autocomplete dropdown
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
