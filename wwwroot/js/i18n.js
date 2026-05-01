(function () {
    'use strict';

    // ── Keyed translations (data-i18n / data-i18n-html / data-i18n-placeholder) ─
    var KEYED = {
        // NAV
        'nav.home':        { bg: 'Начало',        en: 'Home' },
        'nav.feed':        { bg: 'Поток',          en: 'Feed' },
        'nav.events':      { bg: 'Събития',        en: 'Events' },
        'nav.recommended': { bg: 'Препоръчани',    en: 'Recommended' },
        'nav.mytickets':   { bg: 'Моите билети',   en: 'My Tickets' },
        'nav.validate':    { bg: 'Валидиране',      en: 'Validate' },
        'nav.organizer':   { bg: 'Организатор',    en: 'Organizer' },
        'nav.admin':       { bg: 'Админ',           en: 'Admin' },

        // HOME — MARQUEE
        'marquee.live':    { bg: '★ Живи нощи',          en: '★ Live Nights' },
        'marquee.diary':   { bg: '★ Дневник',             en: '★ Diary' },
        'marquee.map':     { bg: '★ Карта на България',  en: '★ Map of Bulgaria' },
        'marquee.tickets': { bg: '★ Свежи билети',        en: '★ Fresh Tickets' },
        'marquee.nearme':  { bg: '★ Около мен',           en: '★ Near Me' },

        // HOME — HERO
        'home.stamp': { bg: 'GrooveOn', en: 'GrooveOn' },
        'home.live':  { bg: 'на живо',  en: 'live' },
        'home.hero.h1': {
            bg: 'Дневник за най-<span>шумните</span> нощи в България.',
            en: 'The diary of the <span>loudest</span> nights in Bulgaria.'
        },
        'home.hero.p': {
            bg: 'GrooveOn е част скрапбук, част карта, част бюро за събития. Организаторите движат сцената, слушателите намират точната вечер, а всеки град получава своите карфици.',
            en: 'GrooveOn is part scrapbook, part map, part event bureau. Organizers drive the scene, listeners find the right night, and every city gets its own pins.'
        },

        // HOME — SEARCH
        'home.search.btn':         { bg: 'Smart търсене', en: 'Smart Search' },
        'home.search.placeholder': { bg: 'техно тази вечер в София · джаз през уикенда · около мен', en: 'techno tonight in Sofia · jazz this weekend · near me' },
        'home.clear':              { bg: 'Изчисти',        en: 'Clear' },

        // HOME — HERO ACTIONS
        'home.viewmap':  { bg: 'Виж картата',   en: 'View Map' },
        'home.openfeed': { bg: 'Отвори потока', en: 'Open Feed' },

        // HOME — STATS
        'home.stats.nights': { bg: 'записани нощи', en: 'recorded nights' },
        'home.stats.cities': { bg: 'града',          en: 'cities' },
        'home.stats.pins':   { bg: 'карфици',        en: 'pins' },

        // HOME — MAP SECTION
        'home.map.stamp':  { bg: 'Картата',   en: 'The Map' },
        'home.map.pins':   { bg: 'карфици,',  en: 'pins,' },
        'home.map.cities': { bg: 'града',     en: 'cities' },
        'home.map.p': {
            bg: 'Когато организаторите добавят събития с координати, те се появяват тук на момента. Кликни върху карфица, за да видиш детайлите.',
            en: 'When organizers add events with coordinates, they appear here instantly. Click on a pin to see the details.'
        },
        'home.geo.btn':  { bg: 'Покажи моето местоположение', en: 'Show my location' },
        'home.map.live': { bg: 'жива карта',  en: 'live map' },
        'home.map.empty':{ bg: 'празна карта', en: 'empty map' },
        'home.noresults':{ bg: 'Няма събития, отговарящи на търсенето.', en: 'No events match your search.' },

        // HOME — EVENTS SECTION
        'home.events.stamp':    { bg: 'Бюро за събития',                  en: 'Event Bureau' },
        'home.events.title':    { bg: 'Събития',                           en: 'Events' },
        'home.events.empty':    { bg: 'Все още няма публикувани събития',  en: 'No events published yet' },
        'home.events.create':   { bg: 'Създай събитие',                    en: 'Create event' },
        'home.events.emptytext':{ bg: 'Все още няма публикувани събития. Когато организаторите добавят такива, те ще се появят тук.', en: 'No events yet. When organizers add events, they will appear here.' },
        'home.events.apply':    { bg: 'Кандидатствай за организатор',      en: 'Apply as Organizer' },

        // HOME — FILTER
        'home.filter.search': { bg: 'Търси по заглавие...', en: 'Search by title...' },
        'home.filter.btn':    { bg: 'Филтър',               en: 'Filter' },
        'filter.allcities':   { bg: 'Всички градове',       en: 'All cities' },
        'filter.allgenres':   { bg: 'Всички жанрове',       en: 'All genres' },

        // LOGIN
        'login.stamp':               { bg: 'Вход',          en: 'Login' },
        'login.h1':                  { bg: 'Влез <span>вътре</span>.', en: 'Sign <span>in</span>.' },
        'login.p':                   { bg: 'Добре дошъл отново в GrooveOn. Влез, за да следиш събития, билети и любимите си сцени.', en: 'Welcome back to GrooveOn. Sign in to track events, tickets and your favourite venues.' },
        'login.email.placeholder':   { bg: 'you@grooveon.app', en: 'you@grooveon.app' },
        'login.password.placeholder':{ bg: 'парола',         en: 'password' },
        'login.remember':            { bg: 'Запомни ме',     en: 'Remember me' },
        'login.btn':                 { bg: 'Вход',           en: 'Sign In' },
        'login.noaccount':           { bg: 'Нямаш профил?',  en: "Don't have an account?" },
        'login.createaccount':       { bg: 'Създай акаунт',  en: 'Create account' },

        // REGISTER
        'register.stamp':                { bg: 'Нов профил',       en: 'New Account' },
        'register.h1':                   { bg: 'Започни своя <span>дневник</span>.', en: 'Start your <span>diary</span>.' },
        'register.p':                    { bg: 'Създай GrooveOn акаунт, за да запазваш вечери, да купуваш билети и да следиш любимите си организатори.', en: 'Create a GrooveOn account to save nights, buy tickets and follow your favourite organizers.' },
        'register.firstname.placeholder':{ bg: 'Ивана',            en: 'Jane' },
        'register.lastname.placeholder': { bg: 'Петрова',          en: 'Doe' },
        'register.password.placeholder': { bg: 'поне 5 символа',   en: 'at least 5 characters' },
        'register.confirm.placeholder':  { bg: 'повтори паролата', en: 'repeat password' },
        'register.btn':                  { bg: 'Създай акаунт',    en: 'Create Account' },
        'register.hasaccount':           { bg: 'Вече имаш профил?',en: 'Already have an account?' },
        'register.loginlink':            { bg: 'Вход',              en: 'Sign In' },

        // ORGANIZER DASHBOARD
        'org.setup.h1':          { bg: 'Нека подредим твоя <span>организаторски профил</span>.', en: "Let's set up your <span>organizer profile</span>." },
        'org.setup.p':           { bg: 'Добави ime на организация, описание и контакти, за да започнеш да публикуваш събития и билети.', en: 'Add an organization name, description and contacts to start publishing events and tickets.' },
        'org.setup.btn':         { bg: 'Попълни профила',      en: 'Complete Profile' },
        'org.website':           { bg: 'Уебсайт',              en: 'Website' },
        'org.newevent':          { bg: 'Ново събитие',         en: 'New Event' },
        'org.newpost':           { bg: 'Нова публикация',      en: 'New Post' },
        'org.editprofile':       { bg: 'Редактирай профила',   en: 'Edit Profile' },
        'org.approved':          { bg: 'Одобрен профил',       en: 'Approved Profile' },
        'org.pending':           { bg: 'Чака одобрение',       en: 'Awaiting Approval' },
        'org.stat.revenue':      { bg: 'Приходи (общо)',       en: 'Revenue (total)' },
        'org.stat.avgticket':    { bg: 'Среден билет:',        en: 'Avg. ticket:' },
        'org.stat.sold':         { bg: 'Продадени билети',     en: 'Tickets Sold' },
        'org.stat.scanned':      { bg: 'Сканирани:',           en: 'Scanned:' },
        'org.stat.last30':       { bg: 'Последни 30 дни',      en: 'Last 30 Days' },
        'org.stat.pcs':          { bg: 'бр.',                  en: 'pcs.' },
        'org.stat.events':       { bg: 'Събития',              en: 'Events' },
        'org.stat.upcoming':     { bg: 'Предстоящи:',          en: 'Upcoming:' },
        'org.stat.past':         { bg: 'минали:',              en: 'past:' },
        'org.stat.tickettypes':  { bg: 'Видове билети',        en: 'Ticket Types' },
        'org.stat.activeevents': { bg: 'Активни събития:',     en: 'Active events:' },
        'org.stat.engagement':   { bg: 'Ангажираност',         en: 'Engagement' },
        'org.stat.comments':     { bg: 'коментара',            en: 'comments' },
        'org.stat.posts':        { bg: 'публикации',           en: 'posts' },
        'org.kicker.stats':      { bg: 'Статистики',           en: 'Statistics' },
        'org.sales.h2':          { bg: 'Продажби през <span>последните 30 дни</span>.', en: 'Sales over the <span>last 30 days</span>.' },
        'org.top5sold.h':        { bg: 'Топ 5 събития (продадени билети)', en: 'Top 5 Events (tickets sold)' },
        'org.nosales':           { bg: 'Все още няма продажби.',  en: 'No sales yet.' },
        'org.top5rev.h':         { bg: 'Топ 5 събития (приход)', en: 'Top 5 Events (revenue)' },
        'org.norevenue':         { bg: 'Все още няма приходи.',  en: 'No revenue yet.' },
        'org.genre.h':           { bg: 'Разпределение по жанр',  en: 'Genre Breakdown' },
        'org.nopublished':       { bg: 'Няма публикувани събития.', en: 'No published events.' },
        'org.cities.h':          { bg: 'Събития по град',        en: 'Events by City' },
        'org.myprofile':         { bg: 'Моят профил',            en: 'My Profile' },
        'org.kicker.sales':      { bg: 'Продажби',               en: 'Sales' },
        'org.tickets.h2':        { bg: 'Билети по <span>събития</span>.', en: 'Tickets by <span>event</span>.' },
        'org.th.event':          { bg: 'Събитие',                en: 'Event' },
        'org.th.start':          { bg: 'Начало',                 en: 'Start' },
        'org.th.status':         { bg: 'Статус',                 en: 'Status' },
        'org.th.sold':           { bg: 'Продадени',              en: 'Sold' },
        'org.badge.active':      { bg: 'Активни билети',         en: 'Active Tickets' },
        'org.badge.none':        { bg: 'Без билети',             en: 'No Tickets' },
        'org.btn.edit':          { bg: 'Редакция',               en: 'Edit' },
        'org.btn.tickets':       { bg: 'Билети',                 en: 'Tickets' },
        'org.kicker.catalog':    { bg: 'Каталог',                en: 'Catalog' },
        'org.recent.events.h2': { bg: 'Последни <span>събития</span>.', en: 'Recent <span>events</span>.' },
        'org.kicker.diary':      { bg: 'Дневник',                en: 'Diary' },
        'org.recent.posts.h2':  { bg: 'Последни <span>публикации</span>.', en: 'Recent <span>posts</span>.' },
    };

    // ── Full text-replacement dictionary (BG → EN) for unlabelled nodes ────────
    var EN = {
        // AUTH
        'Изход': 'Logout',
        'Регистрация': 'Register',
        'Вход': 'Login',

        // COMMON ACTIONS
        'Създай': 'Create',
        'Редактирай': 'Edit',
        'Изтрий': 'Delete',
        'Отказ': 'Cancel',
        'Запази': 'Save',
        'Запази промените': 'Save Changes',
        'Назад': 'Back',
        'Затвори': 'Close',
        'Потвърди': 'Confirm',
        'Одобри': 'Approve',
        'Откажи': 'Reject',
        'Деактивирай': 'Deactivate',
        'Активирай': 'Activate',
        'Купи билет': 'Buy Ticket',
        'Купи': 'Buy',
        'Виж всички': 'See All',
        'Виж повече': 'See More',
        'Виж детайли': 'View Details',
        'Добави': 'Add',
        'Изпрати': 'Submit',
        'Провери': 'Check',
        'Зареди още': 'Load More',
        'Обнови': 'Refresh',
        'Генерирай с AI': 'Generate with AI',
        'Свали PDF': 'Download PDF',
        'Управление': 'Manage',
        'Изчисти': 'Clear',
        'Филтър': 'Filter',

        // EVENTS
        'Събитие': 'Event',
        'Ново събитие': 'New Event',
        'Редакция на събитие': 'Edit Event',
        'Изтриване на събитие': 'Delete Event',
        'Създай нова вечер.': 'Create a new night.',
        'Добави основните детайли за събитието, за да го публикуваш в каталога на GrooveOn.': 'Add the main event details to publish it in the GrooveOn catalog.',
        'Промени детайлите, датите и визуалното представяне на публикацията.': 'Change the event details, dates, and visuals.',
        'Към началото': 'Back to Home',
        'Към детайлите': 'Back to Details',
        'Маркирай локацията': 'Mark the Location',
        '-- Избери град --': '-- Select city --',
        'Избери предложение и градът + координатите ще се попълнят автоматично.': 'Select a suggestion and city + coordinates will be filled in automatically.',
        'JPG, PNG, WebP или GIF, до 5 MB.': 'JPG, PNG, WebP or GIF, up to 5 MB.',
        'Качи снимка': 'Upload Photo',
        'Одобрено': 'Approved',
        'Харесай': 'Like',
        'Харесвания': 'Likes',
        'Коментари': 'Comments',
        'Добави коментар': 'Add a comment',
        'Изпрати коментар': 'Post Comment',
        'Предстои': 'Upcoming',
        'Приключило': 'Ended',
        'Неодобрено': 'Pending Approval',
        'Виж детайли': 'View Details',
        'Купи билет': 'Buy Ticket',

        // FORM LABELS
        'Заглавие': 'Title',
        'Описание': 'Description',
        'Жанр': 'Genre',
        'Град': 'City',
        'Адрес': 'Address',
        'Снимка': 'Photo',
        'URL на снимка': 'Image URL',
        'Цена': 'Price',
        'Безплатен билет': 'Free Ticket',
        'Общо количество': 'Total Quantity',
        'Оставащо количество': 'Remaining Quantity',
        'Активен': 'Active',
        'Съдържание': 'Content',
        'Уебсайт': 'Website',
        'Телефон': 'Phone',
        'Биография': 'Bio',
        'Организация': 'Organization',
        'Фирмен номер': 'Company Number',
        'Дата на начало': 'Start Date',
        'Дата на край': 'End Date',

        // TICKETS
        'Билет': 'Ticket',
        'Билети': 'Tickets',
        'Нов тип билет': 'New Ticket Type',
        'Създай нов тип билет.': 'Create a new ticket type.',
        'Назад към билетите': 'Back to Tickets',
        'Управление на билети': 'Manage Tickets',
        'Валидиране на билет': 'Ticket Validation',
        'Използван': 'Used',
        'Наличен': 'Available',
        'Продадени': 'Sold',
        'Оставащи': 'Remaining',
        'Съвет': 'Tip',
        'Можеш или да въведеш `0`, или просто да отметнеш `Безплатен билет`.': 'You can enter `0` or simply check `Free Ticket`.',
        'Ако не зададеш оставащи бройки, системата ще използва общото количество.': 'If remaining quantity is not set, the system will use the total quantity.',

        // POSTS
        'Публикация': 'Post',
        'Публикации': 'Posts',
        'Нова публикация': 'New Post',
        'Редакция на публикация': 'Edit Post',
        'Изтриване на публикация': 'Delete Post',
        'Последни публикации': 'Latest Posts',

        // ORGANIZER
        'Табло': 'Dashboard',
        'Дейност': 'Activity',
        'Редактирай профила': 'Edit Profile',
        'Стани организатор': 'Become an Organizer',
        'Очаква одобрение': 'Awaiting Approval',
        'Кандидатствай': 'Apply',
        'Скорошни събития': 'Recent Events',

        // ADMIN
        'Администрация': 'Administration',
        'Потребители': 'Users',
        'Организатори': 'Organizers',
        'Чакащи организатори': 'Pending Organizers',
        'Чакащи събития': 'Pending Events',
        'Роля': 'Role',
        'Промени роля': 'Change Role',
        'Транзакции': 'Transactions',

        // STATUS / VALIDATION
        'Достъпът до местоположението е отказан.': 'Location access denied.',
        'Определяне на местоположението...': 'Determining location...',
        'Браузърът не поддържа геолокация.': 'Browser does not support geolocation.',

        // ACCOUNT
        'Моят профил': 'My Profile',
        'Редактирай профил': 'Edit Profile',
        'Предпочитания': 'Preferences',
        'Предпочитан жанр': 'Preferred Genre',
        'Предпочитан град': 'Preferred City',

        // FORM LABELS (from Display attributes on Identity models)
        'Имейл или потребителско ime': 'Email or Username',
        'Парола': 'Password',
        'Запомни ме': 'Remember me',
        'Потвърди паролата': 'Confirm Password',
        'Фамилия': 'Last Name',
        'Потребителско ime': 'Username',
        'Имейл': 'Email',
        'Ime': 'First Name',

        // ORGANIZER PROFILE
        'Стани организатор': 'Become an Organizer',
        'Очаква одобрение': 'Awaiting Approval',
        'Кандидатствай': 'Apply',
        'Скорошни събития': 'Recent Events',
        'Дейност': 'Activity',
        'Редактирай профила': 'Edit Profile',
    };

    // ── Helpers ────────────────────────────────────────────────────────────────

    function getLang() {
        var meta = document.querySelector('meta[name="x-app-lang"]');
        return meta ? meta.getAttribute('content') : 'bg';
    }

    var SKIP_TAGS = { SCRIPT: 1, STYLE: 1, NOSCRIPT: 1, TEXTAREA: 1, CODE: 1, PRE: 1, INPUT: 1, SELECT: 1 };

    function translateTextNode(node, dict) {
        var original = node.textContent;
        var trimmed = original.trim();
        if (!trimmed) return;
        var translated = dict[trimmed];
        if (translated) {
            node.textContent = original.replace(trimmed, translated);
        }
    }

    function walkAndTranslate(el, dict) {
        if (!el || SKIP_TAGS[el.tagName]) return;
        var child = el.firstChild;
        while (child) {
            if (child.nodeType === 3) {
                translateTextNode(child, dict);
            } else if (child.nodeType === 1) {
                walkAndTranslate(child, dict);
            }
            child = child.nextSibling;
        }
    }

    function translateAttributes(root, dict) {
        root.querySelectorAll('[placeholder]').forEach(function (el) {
            var t = dict[el.getAttribute('placeholder')];
            if (t) el.setAttribute('placeholder', t);
        });
        root.querySelectorAll('[aria-label]').forEach(function (el) {
            var t = dict[el.getAttribute('aria-label')];
            if (t) el.setAttribute('aria-label', t);
        });
    }

    function translateKeyed(lang) {
        // data-i18n → textContent
        document.querySelectorAll('[data-i18n]').forEach(function (el) {
            var key = el.getAttribute('data-i18n');
            var entry = KEYED[key];
            if (entry && entry[lang] !== undefined) {
                el.textContent = entry[lang];
            }
        });

        // data-i18n-html → innerHTML (for elements with inline tags like <span>)
        document.querySelectorAll('[data-i18n-html]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-html');
            var entry = KEYED[key];
            if (entry && entry[lang] !== undefined) {
                el.innerHTML = entry[lang];
            }
        });

        // data-i18n-placeholder → placeholder attribute
        document.querySelectorAll('[data-i18n-placeholder]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-placeholder');
            var entry = KEYED[key];
            if (entry && entry[lang] !== undefined) {
                el.setAttribute('placeholder', entry[lang]);
            }
        });
    }

    function applyTranslations(lang) {
        // Always apply keyed translations (works for both bg and en)
        translateKeyed(lang);

        // Only run text-node replacement when switching to English
        if (lang !== 'bg') {
            walkAndTranslate(document.body, EN);
            translateAttributes(document.body, EN);
        }
    }

    // ── Boot ───────────────────────────────────────────────────────────────────

    var lang = getLang();

    function run() {
        applyTranslations(lang);

        if (window.MutationObserver) {
            var observer = new MutationObserver(function (mutations) {
                mutations.forEach(function (m) {
                    m.addedNodes.forEach(function (node) {
                        if (node.nodeType !== 1) return;
                        // Translate keyed elements in newly added subtree
                        node.querySelectorAll('[data-i18n]').forEach(function (el) {
                            var entry = KEYED[el.getAttribute('data-i18n')];
                            if (entry && entry[lang] !== undefined) el.textContent = entry[lang];
                        });
                        node.querySelectorAll('[data-i18n-html]').forEach(function (el) {
                            var entry = KEYED[el.getAttribute('data-i18n-html')];
                            if (entry && entry[lang] !== undefined) el.innerHTML = entry[lang];
                        });
                        node.querySelectorAll('[data-i18n-placeholder]').forEach(function (el) {
                            var entry = KEYED[el.getAttribute('data-i18n-placeholder')];
                            if (entry && entry[lang] !== undefined) el.setAttribute('placeholder', entry[lang]);
                        });
                        if (lang !== 'bg') {
                            walkAndTranslate(node, EN);
                            translateAttributes(node, EN);
                        }
                    });
                });
            });
            observer.observe(document.body, { childList: true, subtree: true });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', run);
    } else {
        run();
    }
})();
