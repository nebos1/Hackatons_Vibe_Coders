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
        'nav.messages':    { bg: 'Съобщения',       en: 'Messages' },
        'auth.login':      { bg: 'Вход',            en: 'Login' },
        'auth.register':   { bg: 'Регистрация',     en: 'Register' },
        'auth.logout':     { bg: 'Изход',           en: 'Logout' },
        'account.overview':{ bg: 'Преглед',         en: 'Overview' },
        'account.title': { bg: 'Акаунт', en: 'Account' },
        'account.since': { bg: 'От', en: 'Since' },
        'account.preferences': { bg: 'Предпочитания', en: 'Preferences' },
        'account.saved.posts': { bg: 'запазени публикации', en: 'Saved posts' },
        'account.saved.events': { bg: 'Запазени събития', en: 'Saved events' },
        'account.your.posts': { bg: 'Твоите публикации', en: 'Your posts' },
        'account.published': { bg: 'Това, което си <span>публикувал</span>.', en: 'What you have <span>published</span>.' },
        'account.no.posts': { bg: 'Все още нямаш публикации.', en: 'You have not posted yet.' },
        'account.saved': { bg: 'Запазено', en: 'Saved' },
        'account.posts.keep': { bg: 'Публикации, които искаш да <span>запазиш</span>.', en: 'Posts you want to <span>keep</span>.' },
        'account.saved.posts.empty': { bg: 'Запазените публикации ще се появят тук.', en: 'Saved posts will appear here.' },
        'account.quick.actions': { bg: 'Бързи действия', en: 'Quick actions' },
        'account.work.tools': { bg: 'Работни инструменти', en: 'Work tools' },
        'account.users': { bg: 'Потребители', en: 'Users' },
        'account.apply.title': { bg: 'Започни да публикуваш събития.', en: 'Start publishing events.' },
        'account.apply.desc': { bg: 'Кандидатствай, когато си готов да продаваш билети и да управляваш събития през Evento.', en: 'Apply when you are ready to sell tickets and manage events through Evento.' },
        'account.application': { bg: 'Кандидатура', en: 'Application' },
        'account.pending.review': { bg: 'Чака преглед.', en: 'Pending review.' },
        'account.edit.application': { bg: 'Редактирай кандидатурата', en: 'Edit application' },
        'account.explore': { bg: 'Разгледай', en: 'Explore' },
        'account.saved.events.empty': { bg: 'Запазените събития ще се появят тук.', en: 'Saved events will appear here.' },
        'account.no.tickets': { bg: 'Все още няма купени билети.', en: 'No purchased tickets yet.' },
        'profile.public':  { bg: 'Публичен профил', en: 'Public profile' },
        'profile.public.page': { bg: 'Публична страница', en: 'Public page' },
        'page.events.empty': { bg: 'Тази публична страница още няма публикувани събития.', en: 'This public page has no published events yet.' },
        'page.posts.empty': { bg: 'Тази публична страница още няма публикации.', en: 'This public page has no posts yet.' },
        'post.create':     { bg: 'Нова публикация', en: 'Create post' },
        'story.create':    { bg: 'Стори',           en: 'Story' },
        'story.empty':     { bg: 'Добави стори',    en: 'Add story' },
        'story.caption':   { bg: 'Кратък текст',    en: 'Caption' },
        'story.post':      { bg: 'Публикувай',      en: 'Post story' },
        'organizer.dashboard': { bg: 'Организаторско табло', en: 'Organizer dashboard' },
        'organizer.public.pages': { bg: 'Публични организаторски страници', en: 'Public organizer pages' },

        // HOME — MARQUEE
        'marquee.live':    { bg: '★ Живи нощи',          en: '★ Live Nights' },
        'marquee.diary':   { bg: '★ Дневник на града',   en: '★ City Diary' },
        'marquee.map':     { bg: '★ Карта на България',  en: '★ Map of Bulgaria' },
        'marquee.tickets': { bg: '★ Свежи билети',        en: '★ Fresh Tickets' },
        'marquee.nearme':  { bg: '★ Около мен',           en: '★ Near Me' },

        // HOME — HERO
        'home.stamp': { bg: 'Evento', en: 'Evento' },
        'home.live':  { bg: 'на живо',  en: 'live' },
        'home.hero.h1': {
            bg: 'Открий <span>събития</span> наблизо.',
            en: 'Discover <span>events</span> nearby.'
        },
        'home.hero.p': {
            bg: 'Концерти, театър, клубни вечери и фестивали в една компактна карта.',
            en: 'Concerts, theatre, club nights and festivals in one compact map.'
        },

        // HOME — SEARCH
        'home.search.btn':         { bg: 'Smart търсене', en: 'Smart Search' },
        'home.search.placeholder': { bg: 'техно тази вечер в София · джаз през уикенда · около мен', en: 'techno tonight in Sofia · jazz this weekend · near me' },
        'home.clear':              { bg: 'Изчисти',        en: 'Clear' },

        // HOME — HERO ACTIONS
        'home.viewmap':  { bg: 'Виж картата',   en: 'View Map' },
        'home.openfeed': { bg: 'Отвори потока', en: 'Open Feed' },
        'home.calendar': { bg: 'Календар',       en: 'Calendar' },
        'home.ai.label': { bg: 'AI Търсене',     en: 'AI Search' },

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

        // HOME — TRENDING
        'home.trending.stamp':       { bg: 'Най-нашумели', en: 'Trending' },
        'home.trending.title':       { bg: 'Събития, които хората следят', en: 'Events people are watching' },
        'home.trending.tab.trend':   { bg: 'Тренд', en: 'Trending' },
        'home.trending.tab.weekend': { bg: 'Този уикенд', en: 'This weekend' },
        'home.trending.tab.nearby':  { bg: 'Наблизо', en: 'Nearby' },
        'home.trending.new':         { bg: 'ново', en: 'new' },
        'home.trending.reactions':   { bg: 'реакции', en: 'reactions' },

        // EVENT CARDS
        'event.status.pending':      { bg: 'Чака одобрение', en: 'Pending' },
        'event.action.going':        { bg: 'Отивам', en: 'Going' },
        'event.action.interested':   { bg: 'Интересува ме', en: 'Interested' },
        'event.action.details':      { bg: 'Детайли', en: 'Details' },
        'genre.Other':               { bg: 'Друго', en: 'Other' },
        'genre.Rock':                { bg: 'Рок', en: 'Rock' },
        'genre.Pop':                 { bg: 'Поп', en: 'Pop' },
        'genre.HipHop':              { bg: 'Хип-хоп', en: 'Hip-hop' },
        'genre.Electronic':          { bg: 'Електронна', en: 'Electronic' },
        'genre.Jazz':                { bg: 'Джаз', en: 'Jazz' },
        'genre.Classical':           { bg: 'Класическа', en: 'Classical' },
        'genre.Folk':                { bg: 'Фолк', en: 'Folk' },
        'genre.Metal':               { bg: 'Метъл', en: 'Metal' },
        'genre.Theater':             { bg: 'Театър', en: 'Theater' },
        'genre.Standup':             { bg: 'Стендъп', en: 'Stand-up' },
        'genre.Festival':            { bg: 'Фестивал', en: 'Festival' },
        'genre.Exhibition':          { bg: 'Изложба', en: 'Exhibition' },
        'genre.Sports':              { bg: 'Спорт', en: 'Sports' },
        'genre.Conference':          { bg: 'Конференция', en: 'Conference' },
        'genre.Workshop':            { bg: 'Работилница', en: 'Workshop' },

        // PUBLIC PROFILE
        'profile.type.organizer':     { bg: 'Организатор', en: 'Organizer' },
        'profile.type.profile':       { bg: 'Профил', en: 'Profile' },
        'profile.website':            { bg: 'Уебсайт', en: 'Website' },
        'profile.followers':          { bg: 'последователи', en: 'followers' },
        'profile.following':          { bg: 'следва', en: 'following' },
        'profile.posts':              { bg: 'публикации', en: 'posts' },
        'profile.events':             { bg: 'събития', en: 'events' },
        'profile.follow':             { bg: 'Последвай', en: 'Follow' },
        'profile.unfollow':           { bg: 'Спри следването', en: 'Unfollow' },
        'profile.message':            { bg: 'Съобщение', en: 'Message' },
        'messages.request.sent':       { bg: 'Заявката чака одобрение', en: 'Request awaiting approval' },
        'messages.request.incoming':   { bg: 'Нова заявка за съобщение', en: 'New message request' },
        'messages.request.title':      { bg: 'Заявка за съобщение', en: 'Message request' },
        'messages.request.desc':       { bg: 'Този човек иска да започне разговор с теб. Одобри заявката, за да можете да си пишете.', en: 'This person wants to start a conversation. Approve the request so you can keep chatting.' },
        'messages.request.waiting':    { bg: 'Заявката е изпратена', en: 'Request sent' },
        'messages.request.waiting.desc': { bg: 'Ще можете да си пишете, когато човекът отсреща я одобри.', en: 'You can chat after the other person approves it.' },
        'messages.request.declined':   { bg: 'Заявката е отказана', en: 'Request declined' },
        'messages.request.declined.desc': { bg: 'Този разговор не е активен.', en: 'This conversation is not active.' },
        'messages.approve':            { bg: 'Одобри', en: 'Approve' },
        'messages.decline':            { bg: 'Откажи', en: 'Decline' },
        'profile.edit':               { bg: 'Редактирай профила', en: 'Edit profile' },
        'profile.events.kicker':      { bg: 'Събития', en: 'Events' },
        'profile.events.title':       { bg: 'Предстоящи и минали <span>събития</span>.', en: 'Upcoming and past <span>events</span>.' },
        'profile.posts.kicker':       { bg: 'Публикации', en: 'Posts' },
        'profile.posts.title':        { bg: 'Последни <span>публикации</span>.', en: 'Latest <span>posts</span>.' },
        'profile.no.posts':           { bg: 'Все още няма публикации.', en: 'No posts yet.' },

        // LOGIN
        'login.stamp':               { bg: 'Вход',          en: 'Login' },
        'login.h1':                  { bg: 'Вход', en: 'Login' },
        'login.p':                   { bg: 'Добре дошъл отново в Evento. Влез, за да следиш събития, билети и любимите си сцени.', en: 'Welcome back to Evento. Sign in to track events, tickets and your favourite venues.' },
        'login.email.label':         { bg: 'Имейл или потребителско име', en: 'Email or username' },
        'login.password.label':      { bg: 'Парола',         en: 'Password' },
        'login.email.placeholder':   { bg: 'you@evento.app', en: 'you@evento.app' },
        'login.password.placeholder':{ bg: 'парола',         en: 'password' },
        'login.remember':            { bg: 'Запомни ме',     en: 'Remember me' },
        'login.btn':                 { bg: 'Вход',           en: 'Sign In' },
        'login.noaccount':           { bg: 'Нямаш профил?',  en: "Don't have an account?" },
        'login.createaccount':       { bg: 'Създай акаунт',  en: 'Create account' },

        // REGISTER
        'register.stamp':                { bg: 'Нов профил',       en: 'New Account' },
        'register.h1':                   { bg: 'Започни своя <span>дневник</span>.', en: 'Start your <span>diary</span>.' },
        'register.p':                    { bg: 'Създай Evento акаунт, за да запазваш вечери, да купуваш билети и да следиш любимите си организатори.', en: 'Create an Evento account to save nights, buy tickets and follow your favourite organizers.' },
        'register.firstname.label':      { bg: 'Име',              en: 'First name' },
        'register.lastname.label':       { bg: 'Фамилия',          en: 'Last name' },
        'register.username.label':       { bg: 'Потребителско име', en: 'Username' },
        'register.email.label':          { bg: 'Имейл',            en: 'Email' },
        'register.password.label':       { bg: 'Парола',           en: 'Password' },
        'register.confirm.label':        { bg: 'Потвърди паролата', en: 'Confirm password' },
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

        // COMMON
        'common.cancel': { bg: 'Отказ', en: 'Cancel' },
        'common.signin': { bg: 'Вход', en: 'Sign in' },
        'common.send': { bg: 'Изпрати', en: 'Send' },
        'common.back': { bg: 'Назад', en: 'Back' },
        'common.clear': { bg: 'Изчисти', en: 'Clear' },
        'common.open': { bg: 'Отвори', en: 'Open' },
        'common.edit': { bg: 'Редакция', en: 'Edit' },
        'common.delete': { bg: 'Изтрий', en: 'Delete' },
        'common.showing': { bg: 'Показани', en: 'Showing' },
        'common.show.more': { bg: 'Покажи още', en: 'Show more' },
        'comments.threads': { bg: 'нишки', en: 'threads' },
        'comments.show.more': { bg: 'Покажи още коментари', en: 'Show more comments' },
        'event.price.free': { bg: 'Безплатно', en: 'Free' },
        'event.price.from': { bg: 'от', en: 'from' },
        'event.tag.today': { bg: 'Днес', en: 'Today' },

        // RECURRING EVENTS
        'event.schedule.kicker': { bg: 'График', en: 'Schedule' },
        'event.schedule.title': { bg: 'Дати на събитието', en: 'Event dates' },
        'event.schedule.single': { bg: 'Еднократно събитие', en: 'Single event' },
        'event.schedule.single.desc': { bg: 'Използва стандартните начална и крайна дата.', en: 'Uses the standard start and end date.' },
        'event.schedule.daily': { bg: 'Повтаря се всеки ден', en: 'Repeats daily' },
        'event.schedule.daily.desc': { bg: 'Всеки ден или през няколко дни до крайна дата.', en: 'Every day or every few days until an end date.' },
        'event.schedule.weekly': { bg: 'Повтаря се всяка седмица', en: 'Repeats weekly' },
        'event.schedule.weekly.desc': { bg: 'Избери един или повече дни от седмицата.', en: 'Choose one or more weekdays.' },
        'event.schedule.edit.note': { bg: 'За една конкретна дата отвори детайлите на събитието и отмени само избраната дата.', en: 'To change one concrete date, open the event details and cancel only the selected date.' },
        'event.dates.kicker': { bg: 'Дати', en: 'Dates' },
        'event.dates.title': { bg: 'Избери конкретна дата', en: 'Choose a specific date' },
        'event.dates.cancel': { bg: 'Отмени тази дата', en: 'Cancel this date' },
        'event.publicpage.choose': { bg: '-- Избери публична страница --', en: '-- Choose public page --' },
        'home.geo.btn.short': { bg: 'Моето място', en: 'My Location' },
        'home.filters.aria': { bg: 'Бързи филтри', en: 'Quick event filters' },
        'home.chip.today': { bg: 'Днес', en: 'Today' },
        'home.chip.weekend': { bg: 'Този уикенд', en: 'This weekend' },
        'home.chip.concerts': { bg: 'Концерти', en: 'Concerts' },
        'home.chip.theatre': { bg: 'Театър', en: 'Theatre' },
        'home.chip.festivals': { bg: 'Фестивали', en: 'Festivals' },
        'home.chip.free': { bg: 'Безплатни', en: 'Free events' },
        'home.chip.family': { bg: 'Семейни', en: 'Family' },
        'chip.concerts': { bg: 'Концерти', en: 'Concerts' },
        'chip.theatre': { bg: 'Театър', en: 'Theatre' },
        'chip.festivals': { bg: 'Фестивали', en: 'Festivals' },
        'chip.free': { bg: 'Безплатни', en: 'Free events' },
        'home.results.kicker': { bg: 'Зона с резултати', en: 'Smart search result area' },
        'home.results.title': { bg: 'Резултати за следващото ти събитие', en: 'Results for your next event' },
        'home.results.date': { bg: 'Дата', en: 'Date' },
        'home.results.distance': { bg: 'Разстояние', en: 'Distance' },
        'home.map.loading': { bg: 'Картата се зарежда...', en: 'Loading map...' },

        // DAYS
        'day.Sunday': { bg: 'Неделя', en: 'Sunday' },
        'day.Monday': { bg: 'Понеделник', en: 'Monday' },
        'day.Tuesday': { bg: 'Вторник', en: 'Tuesday' },
        'day.Wednesday': { bg: 'Сряда', en: 'Wednesday' },
        'day.Thursday': { bg: 'Четвъртък', en: 'Thursday' },
        'day.Friday': { bg: 'Петък', en: 'Friday' },
        'day.Saturday': { bg: 'Събота', en: 'Saturday' },

        // TICKETING / SEATS
        'event.ticketing.kicker': { bg: 'Билети и места', en: 'Ticketing and seating' },
        'event.ticketing.title': { bg: 'Как влиза публиката', en: 'How the audience enters' },
        'event.ticketing.ga': { bg: 'Без места', en: 'General Admission' },
        'event.ticketing.ga.desc': { bg: 'Без избор на място. Старият билетен поток остава активен.', en: 'No seat selection. The existing ticket flow stays active.' },
        'event.ticketing.seated': { bg: 'Използвай схема с места', en: 'Use a seated layout' },
        'event.ticketing.seated.desc': { bg: 'Всяко събитие или дата получава отделна наличност на места.', en: 'Each event or date gets its own seat inventory.' },
        'event.ticketing.standing': { bg: 'Правостоящи зони', en: 'Standing zones' },
        'event.ticketing.standing.desc': { bg: 'Използвай преизползваеми секции за правостоящи или VIP зони.', en: 'Use reusable sections for standing or VIP zones.' },
        'event.ticketing.tables': { bg: 'Маси / VIP', en: 'Tables / VIP' },
        'event.ticketing.tables.desc': { bg: 'Използвай маси или премиум секции с добавка към цената.', en: 'Use tables or premium sections with a price modifier.' },
        'event.ticketing.choose.layout': { bg: '-- Избери преизползваем layout --', en: '-- Choose reusable layout --' },
        'ticket.end': { bg: 'Край', en: 'End' },
        'ticket.seat': { bg: 'Място', en: 'Seat' },
        'ticket.sold.lower': { bg: 'продадени', en: 'sold' },
        'ticket.left': { bg: 'оставащи', en: 'left' },
        'ticket.soldout': { bg: 'Разпродадено', en: 'Sold out' },
        'ticket.getfree': { bg: 'Вземи безплатно', en: 'Get free' },
        'ticket.buy': { bg: 'Купи', en: 'Buy' },
        'ticket.none': { bg: 'Все още няма билети.', en: 'No tickets yet.' },
        'tickets.available': { bg: 'Налични <span>билети</span>.', en: 'Available <span>tickets</span>.' },
        'tickets.add': { bg: 'Добави билет', en: 'Add ticket' },

        // SEAT MAP
        'seat.map.kicker': { bg: 'Карта на местата', en: 'Seat map' },
        'seat.map.help': { bg: 'Избери място преди покупка. Резервацията се пази за кратко.', en: 'Choose a seat before checkout. The reservation is held briefly.' },
        'seat.none': { bg: 'Няма свободни места за избор.', en: 'No selectable seats.' },

        // LAYOUTS
        'layout.my': { bg: 'Моите layout-и', en: 'My Layouts' },
        'layout.kicker': { bg: 'Layout-и', en: 'Layouts' },
        'layout.index.title': { bg: 'Преизползваеми схеми за места', en: 'Reusable seating layouts' },
        'layout.index.desc': { bg: 'Създай схема веднъж и я използвай за много събития, дати, зали, артисти или формати.', en: 'Create a layout once and reuse it across events, dates, venues, artists or formats.' },
        'layout.create': { bg: 'Създай layout', en: 'Create layout' },
        'layout.create.short': { bg: 'Нов layout', en: 'New layout' },
        'layout.empty.title': { bg: 'Все още няма layout-и.', en: 'No layouts yet.' },
        'layout.empty.desc': { bg: 'Започни с прост layout или остави събитията General Admission.', en: 'Start with a simple layout or keep events as General Admission.' },
        'layout.sections': { bg: 'Секции', en: 'Sections' },
        'layout.seats': { bg: 'Места', en: 'Seats' },
        'layout.locked.note': { bg: 'Има продадени места. Големи промени ще създадат нова версия.', en: 'Sold seats exist. Major changes create a new version.' },
        'layout.preview': { bg: 'Преглед', en: 'Preview' },
        'layout.duplicate': { bg: 'Дублирай', en: 'Duplicate' },
        'layout.editor.kicker': { bg: 'Редактор на зала', en: 'Layout editor' },
        'layout.editor.seatmap': { bg: 'Карта на залата', en: 'Seat map' },
        'layout.editor.desc': { bg: 'Създай преизползваема карта със етажи, секции, редове, правостоящи зони, VIP места и маси.', en: 'Build a reusable venue map with floors, sections, rows, standing zones, VIP areas and tables.' },
        'layout.version.warning': { bg: 'Този layout вече има продадени места. При запис ще се създаде нова версия.', en: 'This layout already has sold seats. Saving creates a new version.' },
        'layout.section': { bg: 'Секция', en: 'Section' },
        'layout.generate.rows': { bg: 'Генерирай редове', en: 'Generate rows' },
        'layout.selected': { bg: 'Избрано', en: 'Selected' },
        'layout.section.name': { bg: 'Име на секция', en: 'Section name' },
        'layout.price.modifier': { bg: 'Добавка към цена', en: 'Price modifier' },
        'layout.remove.section': { bg: 'Премахни секцията', en: 'Remove section' },
        'layout.save': { bg: 'Запази layout', en: 'Save layout' },

        // STATUSES / TYPES
        'status.occurrence.Scheduled': { bg: 'Планирана', en: 'Scheduled' },
        'status.occurrence.Cancelled': { bg: 'Отменена', en: 'Cancelled' },
        'status.occurrence.SoldOut': { bg: 'Разпродадена', en: 'Sold out' },
        'status.layout.Draft': { bg: 'Чернова', en: 'Draft' },
        'status.layout.Active': { bg: 'Активен', en: 'Active' },
        'status.layout.Archived': { bg: 'Архивиран', en: 'Archived' },
        'section.type.Seated': { bg: 'Седящи места', en: 'Seated' },
        'section.type.Standing': { bg: 'Правостоящи', en: 'Standing' },
        'section.type.VIP': { bg: 'VIP', en: 'VIP' },
        'section.type.Table': { bg: 'Маси', en: 'Tables' },

        // CONVERSATION
        'comments.kicker': { bg: 'Разговор', en: 'Conversation' },
        'comments.title': { bg: 'Коментари', en: 'Comments' },
        'comments.placeholder': { bg: 'Напиши коментар...', en: 'Write a comment...' },
        'comments.reply': { bg: 'Отговори', en: 'Reply' },
        'comments.reply.placeholder': { bg: 'Напиши отговор...', en: 'Write a reply...' },
        'comments.empty': { bg: 'Все още няма коментари.', en: 'No comments yet.' },
        'event.location': { bg: 'Локация', en: 'Location' },
        'event.social': { bg: 'Социално', en: 'Social' },
        'event.attendance': { bg: 'Посещения', en: 'Attendance' },
        'event.status.waiting.approval': { bg: 'Това събитие чака одобрение.', en: 'This event is waiting for approval.' },
        'organizer.profiles.desc': { bg: 'Създавай отделни страници за брандове, зали, регулярни вечери или колективи. Избираш страница при публикуване на събитие.', en: 'Create separate pages for brands, venues, recurring nights or collectives. Pick one when publishing an event.' },
        'social.likes': { bg: 'харесвания', en: 'likes' },
        'social.comments': { bg: 'коментара', en: 'comments' },
        'social.saves': { bg: 'запазвания', en: 'saves' },
        'social.share': { bg: 'Сподели', en: 'Share' },
        'post.like': { bg: 'Харесай', en: 'Like' },
        'post.unlike': { bg: 'Премахни харесване', en: 'Unlike' },
        'post.save': { bg: 'Запази', en: 'Save' },
        'post.unsave': { bg: 'Премахни запазване', en: 'Unsave' },
        'messages.seen': { bg: 'видяно', en: 'seen' },
        'messages.placeholder': { bg: 'Напиши съобщение...', en: 'Write a message...' },
        'messages.index.title': { bg: 'Лични <span>съобщения</span>.', en: 'Direct <span>messages</span>.' },
        'messages.index.desc': { bg: 'Пиши си с хора и организатори. Засега съобщенията се обновяват при нормално зареждане на страницата.', en: 'Chat with people and organizers. Messages refresh with normal page requests for now.' },
        'messages.empty': { bg: 'Все още няма разговори. Отвори профил и започни съобщение.', en: 'No conversations yet. Open a profile and start a message.' },
        'messages.none': { bg: 'Все още няма съобщения', en: 'No messages yet' },
        'feed.kicker': { bg: 'Поток', en: 'Feed' },
        'feed.title': { bg: 'Последно от сцената.', en: 'Latest from the scene.' },
        'feed.signin.stories': { bg: 'Влез, за да следваш хора и да виждаш активни сторита.', en: 'Sign in to follow people and see active stories.' },
        'feed.following.kicker': { bg: 'Следиш', en: 'Following' },
        'feed.following.title': { bg: 'Хората, които следиш.', en: 'People you follow.' },
        'feed.posts.kicker': { bg: 'Публикации', en: 'Posts' },
        'feed.posts.title': { bg: 'Новини от организатори и свежи постове.', en: 'Organizer updates and fresh posts.' },
        'feed.all': { bg: 'Всички', en: 'All' },
        'feed.more': { bg: 'Още', en: 'More' },
        'feed.suggested': { bg: 'Предложени профили', en: 'Suggested profiles' },
        'feed.search.placeholder': { bg: 'Търси хора, @username или публична страница...', en: 'Search people, @username or public page...' },
        'feed.search.action': { bg: 'Търси', en: 'Search' },
        'feed.search.results': { bg: 'Резултати', en: 'Results' },
        'feed.search.empty': { bg: 'Няма намерени профили или публични страници.', en: 'No profiles or public pages found.' },
        'story.active': { bg: 'Активни сторита', en: 'Active stories' },
        'story.close': { bg: 'Затвори стори', en: 'Close story' },
        'story.viewer': { bg: 'Преглед на стори', en: 'Story viewer' },
        'organizer.pages.kicker': { bg: 'Организаторски страници', en: 'Organizer pages' },
        'organizer.pages.title': { bg: 'Сменяй между <span>публични идентичности</span>.', en: 'Switch between <span>public identities</span>.' },
        'organizer.pages.public': { bg: 'Публична страница', en: 'Public page' },
        'organizer.pages.singular': { bg: 'организаторска страница', en: 'organizer page' },
        'organizer.pages.form.desc': { bg: 'Това е публичната идентичност, която хората виждат в събитията. Можеш да имаш няколко страници и да избираш една при публикуване.', en: 'This is the public identity people see on events. You can keep several pages and choose one when publishing.' },
        'organizer.pages.all': { bg: 'Всички страници', en: 'All pages' },
        'organizer.pages.name.placeholder': { bg: 'Име на клуб, колектив, промоутър...', en: 'Club name, collective, promoter...' },
        'organizer.pages.city.placeholder': { bg: 'София, Пловдив...', en: 'Sofia, Plovdiv...' },
        'organizer.pages.tagline.placeholder': { bg: 'Ъндърграунд техно вечери, live jazz сесии...', en: 'Underground techno nights, live jazz sessions...' },
        'organizer.pages.use.default': { bg: 'Използвай като основна страница', en: 'Use as default page' },
        'organizer.pages.active.selectable': { bg: 'Активна и избираема', en: 'Active and selectable' },
        'organizer.pages.required': { bg: 'Задължително', en: 'Required' },
        'organizer.pages.required.desc': { bg: 'Нужна е поне една активна публична страница, преди организатор да създава събития.', en: 'At least one active public page is required before an organizer can create events.' },
        'organizer.pages.save': { bg: 'Запази страницата', en: 'Save page' },
        'organizer.pages.new': { bg: 'Нова страница', en: 'New page' },
        'organizer.pages.empty.title': { bg: 'Създай първата си публична страница.', en: 'Create your first public page.' },
        'organizer.pages.empty.desc': { bg: 'Трябва да имаш поне една страница преди да създаваш събития.', en: 'You need at least one page before creating events.' },
        'organizer.pages.create': { bg: 'Създай страница', en: 'Create page' },
        'organizer.pages.default': { bg: 'Основна', en: 'Default' },
        'organizer.pages.make.default': { bg: 'Направи основна', en: 'Make default' },
        'organizer.pages.archive': { bg: 'Архивирай', en: 'Archive' },
        'organizer.pages.delete': { bg: 'Изтрий', en: 'Delete' },
    };

    Object.assign(KEYED, {
        'nav.organizer.panel': { bg: 'Организаторски панел', en: 'Organizer Panel' },
        'nav.discover': { bg: 'Открий', en: 'Discover' },
        'workspace.title': { bg: 'Бизнес workspace', en: 'Business workspace' },
        'workspace.h1': { bg: 'Юридически и платежни профили', en: 'Legal and payment entities' },
        'workspace.desc': { bg: 'Всяка публична организаторска страница принадлежи към workspace. Продажбите на билети се отчитат и изплащат към workspace-а, свързан със страницата.', en: 'Each public organizer page belongs to a workspace. Ticket sales are reported and paid to the workspace connected to that page.' },
        'workspace.new': { bg: 'Нов workspace', en: 'New workspace' },
        'workspace.all': { bg: 'Всички workspaces', en: 'All workspaces' },
        'workspace.default': { bg: 'Основен', en: 'Default' },
        'workspace.empty.h': { bg: 'Все още няма workspaces.', en: 'No workspaces yet.' },
        'workspace.empty.p': { bg: 'Създай workspace, преди да правиш публични организаторски страници и да продаваш билети.', en: 'Create a workspace before creating public organizer pages and selling tickets.' },
        'workspace.form.h1': { bg: 'Детайли за workspace', en: 'Workspace details' },
        'workspace.form.desc': { bg: 'Тук се пази само юридическа идентичност и безопасен платежен статус. Не въвеждай банкови карти или банкови сметки.', en: 'Store legal identity and safe payment status only. Do not enter bank card or bank account details here.' },
        'workspace.save': { bg: 'Запази workspace', en: 'Save workspace' },
        'workspace.delete': { bg: 'Изтрий', en: 'Delete' },
        'workspace.delete.last': { bg: 'Създай друг workspace, преди да изтриеш този.', en: 'Create another workspace before deleting this one.' },
        'workspace.charges': { bg: 'Приема плащания', en: 'Charges enabled' },
        'workspace.payouts': { bg: 'Изплащанията са активни', en: 'Payouts enabled' },
        'workspace.safety': { bg: 'Безопасност на плащанията', en: 'Payment safety' },
        'workspace.safety.desc': { bg: 'Тук се пазят само Stripe account id и статуси. Чувствителните банкови данни остават извън Evento.', en: 'Only Stripe account id and status flags are stored here. Sensitive banking details stay outside Evento.' },
        'workspace.display.name': { bg: 'Име за показване', en: 'Display name' },
        'workspace.legal.name': { bg: 'Юридическо име', en: 'Legal name' },
        'workspace.company.number': { bg: 'ЕИК / фирмен номер', en: 'Company number' },
        'workspace.billing.email': { bg: 'Имейл за фактуриране', en: 'Billing email' },
        'workspace.phone.number': { bg: 'Телефон', en: 'Phone number' },
        'workspace.city': { bg: 'Град', en: 'City' },
        'workspace.country': { bg: 'Държава', en: 'Country' },
        'workspace.address': { bg: 'Адрес', en: 'Address' },
        'workspace.payment.provider': { bg: 'Платежен доставчик', en: 'Payment provider' },
        'workspace.stripe.account': { bg: 'Stripe account ID', en: 'Stripe account ID' },
        'workspace.stripe.status': { bg: 'Stripe onboarding статус', en: 'Stripe onboarding status' },
        'workspace.active.context': { bg: 'Активен publishing context', en: 'Active publishing context' },
        'workspace.label': { bg: 'Workspace', en: 'Workspace' },
        'workspace.publish.context': { bg: 'Publishing context', en: 'Publishing context' },
        'workspace.publish.as': { bg: 'Публикуваш като', en: 'Publishing as' },
        'workspace.payments.to': { bg: 'Плащанията отиват към', en: 'Payments go to' },
        'workspace.feed.visible': { bg: 'Видимо в Discover', en: 'Visible in Discover' },
        'workspace.switch.workspace': { bg: 'Workspace', en: 'Workspace' },
        'workspace.switch.page': { bg: 'Page', en: 'Page' },
        'workspace.switch.save': { bg: 'Switch', en: 'Switch' },
        'ticket.quantity': { bg: 'Брой билети', en: 'Ticket quantity' },
        'workspace.page.context': { bg: 'Business workspace', en: 'Business workspace' },
        'workspace.page.context.desc': { bg: 'This public page belongs to the selected legal/payment workspace. Ticket payouts for this page go to that workspace.', en: 'This public page belongs to the selected legal/payment workspace. Ticket payouts for this page go to that workspace.' },
        'workspace.page.payout.note': { bg: 'Confirm that sales from this organizer page should be paid to the selected workspace.', en: 'Confirm that sales from this organizer page should be paid to the selected workspace.' },
        'workspace.show.owner': { bg: 'Show owner profile publicly', en: 'Show owner profile publicly' },
        'workspace.show.legal': { bg: 'Show legal business name publicly', en: 'Show legal business name publicly' },
        'identity.commenting.as': { bg: 'Коментираш като', en: 'Commenting as' },
        'identity.replying.as': { bg: 'Отговаряш като', en: 'Replying as' },
        'identity.posting.as': { bg: 'Публикуваш като', en: 'Posting as' },
        'identity.user': { bg: 'Потребител', en: 'User' },
        'identity.page': { bg: 'Организаторска страница', en: 'Organizer Page' },
        'identity.admin': { bg: 'Админ', en: 'Admin' },
        'identity.system': { bg: 'Система', en: 'System' },
        'home.discover.events': { bg: 'Открий събития', en: 'Discover Events' },
        'home.become.organizer': { bg: 'Стани организатор', en: 'Become Organizer' },
        'home.login.cta': { bg: 'Влез, за да получаваш по-добри препоръки и да запазваш събития.', en: 'Sign in to get better recommendations and save events.' },
        'home.login.register': { bg: 'Вход / Регистрация', en: 'Login / Register' },
        'home.personal.city': { bg: 'Показваме ти повече събития около', en: 'Showing more events around' },
        'home.quick.filters': { bg: 'Бързи филтри', en: 'Quick filters' },
        'home.quick.filters.title': { bg: 'Намери точната вечер по-бързо.', en: 'Find the right night faster.' },
        'home.tonight': { bg: 'Днес', en: 'Today' },
        'home.weekend': { bg: 'Този уикенд', en: 'This Weekend' },
        'home.free': { bg: 'Безплатни', en: 'Free' },
        'home.near.me': { bg: 'Наблизо', en: 'Near Me' },
        'home.popular': { bg: 'Популярни', en: 'Popular' },
        'home.new': { bg: 'Нови', en: 'New' },
        'home.tonight.title': { bg: 'Какво се случва днес.', en: 'What is happening today.' },
        'home.weekend.title': { bg: 'План за събота и неделя.', en: 'Plans for Saturday and Sunday.' },
        'home.tonight.empty': { bg: 'Няма събития за днес.', en: 'No events today.' },
        'home.weekend.empty': { bg: 'Няма събития за този уикенд.', en: 'No events this weekend.' },
        'home.all.events': { bg: 'Разгледай всички събития', en: 'Browse all events' },
        'home.recently.viewed': { bg: 'Последно разглеждани', en: 'Recently Viewed' },
        'home.continue.exploring': { bg: 'Продължи разглеждането.', en: 'Continue Exploring.' },
        'home.popular.organizers': { bg: 'Популярни организатори', en: 'Popular Organizers' },
        'home.popular.organizers.title': { bg: 'Официални страници, които движат сцената.', en: 'Official pages moving the scene.' },
        'home.upcoming.events': { bg: 'предстоящи събития', en: 'upcoming events' },
        'home.popular.cities': { bg: 'Популярни градове', en: 'Popular Cities' },
        'home.popular.cities.title': { bg: 'Избери град и виж какво предстои.', en: 'Choose a city and see what is next.' },
        'home.how.title': { bg: 'Как работи', en: 'How it works' },
        'home.how.discover': { bg: 'Открий събитие', en: 'Discover an event' },
        'home.how.save.buy': { bg: 'Запази или купи билет', en: 'Save or buy a ticket' },
        'home.how.follow': { bg: 'Последвай организатор и остани в течение', en: 'Follow organizers and stay in the loop' },
        'home.organizer.cta.kicker': { bg: 'Организираш събития?', en: 'Organizing events?' },
        'home.organizer.cta.title': { bg: 'Създай публична страница, публикувай събития и управлявай билети от едно място.', en: 'Create a public page, publish events, and manage tickets in one place.' },

        // HOME SEARCH TABS
        'search.tab.all':     { bg: 'Всички',       en: 'All' },
        'search.tab.tonight': { bg: 'Днес',   en: 'Today' },
        'search.tab.weekend': { bg: 'Уикенд',        en: 'Weekend' },
        'search.tab.week':    { bg: 'Тази седмица',  en: 'This week' },

        // HOME FILTER CHIPS (extra)
        'home.chip.all':  { bg: 'Всички',     en: 'All' },
        'home.chip.kids': { bg: 'За деца',    en: 'For children' },
        'home.chip.live': { bg: 'Live музика', en: 'Live music' },

        // ORGANIZER TOUR
        'org.tour.step1.h':  { bg: 'Добре дошъл в Organizer Panel!', en: 'Welcome to the Organizer Panel!' },
        'org.tour.step1.p':  { bg: 'Тук управляваш публичните си идентичности, събития, билети и приходи — всичко от едно място.', en: 'Manage your public identities, events, tickets and revenue — all in one place.' },
        'org.tour.step2.h':  { bg: 'Публична страница', en: 'Public Page' },
        'org.tour.step2.p':  { bg: 'Преди да публикуваш събитие, трябва да имаш поне една публична страница. Тя е лицето ти — хората я виждат при всяко събитие.', en: 'Before publishing an event you need at least one public page. It is your public face — people see it on every event.' },
        'org.tour.step3.h':  { bg: 'Публикувай събитие', en: 'Publish an Event' },
        'org.tour.step3.p':  { bg: 'Натисни "Ново събитие" и добави снимка, дати, жанр и локация. Събитието влиза за одобрение от admin, след което е видимо за всички.', en: 'Hit "New Event" and add a photo, dates, genre and location. The event goes for admin approval, then it becomes public.' },
        'org.tour.step4.h':  { bg: 'Билети и приходи', en: 'Tickets & Revenue' },
        'org.tour.step4.p':  { bg: 'Добавяй различни типове билети — безплатни, платени или VIP. Продажбата се случва директно от страницата на събитието.', en: 'Add different ticket types — free, paid or VIP. Sales happen directly from the event page.' },
        'org.tour.step5.h':  { bg: 'Статистики и VIP Boost', en: 'Stats & VIP Boost' },
        'org.tour.step5.p':  { bg: 'Следи продажбите, гледанията и ангажираността. Нов организатор получава 1 VIP boost — постави го на събитието, което искаш да промотираш най-много.', en: 'Track sales, views and engagement. New organizers get 1 VIP boost — use it on the event you want to promote most.' },
        'org.tour.skip':     { bg: 'Пропусни', en: 'Skip' },
        'org.tour.next':     { bg: 'Напред', en: 'Next' },
        'org.tour.finish':   { bg: 'Готово!', en: 'Done!' },
        'org.tour.help':     { bg: '? Помощ', en: '? Help' }
    });

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
        'Добави основните детайли за събитието, за да го публикуваш в каталога на Evento.': 'Add the main event details to publish it in the Evento catalog.',
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

        // HOME FILTER CHIPS + LABELS
        'Всички': 'All',
        'За деца': 'For children',
        'Live музика': 'Live music',
        'Днес': 'Today',
        'Тази вечер': 'Today',
        'Тази седмица': 'This week',
        'Уикенд': 'Weekend',
        'Календар': 'Calendar',
        'AI Търсене': 'AI Search',

        // BOTTOM NAV
        'Профил': 'Profile',
        'Поток': 'Feed',
        'Открий': 'Discover',
        'Съобщения': 'Messages',
        'Панел': 'Panel',

        // PROFILE MEMORIES
        'Спомени': 'Memories',
        'Преди': 'Before',
        'година': 'year',
        'години': 'years',
        'този месец': 'this month',
        'going': 'going',
        'saved': 'saved',

        // TICKETS PAGE
        'Локация': 'Location',
        'Начало': 'Start',
        'Детайли': 'Details',
        'Валиден': 'Valid',
        'Купен на': 'Bought on',
        'Разгледай събития': 'Explore Events',
        'Намери събитие': 'Find an Event',

        // AI PLAN
        'Избери': 'Choose',
        'Дата': 'Date',
        'Настроение / повод': 'Vibe / occasion',
        'Подреди ми вечерта': 'Plan my evening',
        'Всички събития за деня': 'All events for the day',
        'Няма публикувани събития за избрания ден. Опитай с друга дата.': 'No events for the selected day. Try another date.',
        'Evento AI предлага': 'Evento AI suggests',

        // RECOMMENDED
        'Подбор': 'Selection',
        'Вечери, избрани за теб.': 'Evenings chosen for you.',
        'Моите предпочитания': 'My Preferences',
        'Настрой препоръките': 'Set Recommendations',
        'Предстоящи предложения': 'Upcoming suggestions',
        'Промени предпочитанията': 'Change Preferences',

        // WRAPPED
        'Все още няма какво да обвием.': 'Nothing to wrap yet.',
        'Здравей,': 'Hello,',
        'Скролни': 'Scroll',
        'Това беше твоята година.': 'That was your year.',
        'Споделяй, запомняй, продължавай.': 'Share, remember, continue.',
        'Брой събития': 'Events count',
        'Топ жанр': 'Top genre',
        'Любим град': 'Favourite city',
        'На сцената': 'On stage',
        'Топ организатор': 'Top organizer',
        'Най-натоварен месец': 'Busiest month',
        'Топ моменти': 'Top moments',
        'Твоите фаворити': 'Your favourites',
        'Нови организатори': 'New organizers',
        'Это е твоята година.' : 'This is your year.',
    };

    // ── Helpers ────────────────────────────────────────────────────────────────

    Object.assign(KEYED, {
        'messages.personal.panel': { bg: 'Лични', en: 'Personal' },
        'messages.personal.title': { bg: 'Лични съобщения', en: 'Personal messages' },
        'messages.pages.panel': { bg: 'Страници', en: 'Page inbox' },
        'messages.pages.title': { bg: 'Съобщения към публичните ти страници', en: 'Messages to your public pages' },
        'messages.pages.empty': { bg: 'Никой още не е писал на страниците ти.', en: 'No one has written to your pages yet.' },
        'messages.requests.panel': { bg: 'Заявки', en: 'Requests' },
        'messages.requests.title': { bg: 'Чакат одобрение', en: 'Needs approval' },
        'messages.scope.page.inbox': { bg: 'Входящи на страница', en: 'Page inbox' },
        'messages.scope.to.page': { bg: 'Към страница', en: 'To page' },
        'messages.page.thread': { bg: 'Разговор със страница', en: 'Page conversation' },
        'messages.replying.for.page': { bg: 'Отговаряш за', en: 'Replying for' },
        'messages.request.first.title': { bg: 'Изпрати първото съобщение', en: 'Send the first message' },
        'messages.request.first.desc': { bg: 'Това ще създаде заявка. Другият човек вижда първото съобщение и одобрява чата, преди да продължите.', en: 'This will create a request. The other person can read it once and approve the chat before you continue.' },
        'messages.request.empty.title': { bg: 'Чака първо съобщение', en: 'Waiting for a first message' },
        'messages.request.empty.desc': { bg: 'Заявката ще се появи, когато бъде изпратено първото съобщение.', en: 'This request will appear once the first message is sent.' },
        'messages.request.draft': { bg: 'Напиши първото съобщение', en: 'Write first message' },
        'messages.sent': { bg: 'Изпратено', en: 'Sent' },
        'messages.delivered': { bg: 'Доставено', en: 'Delivered' },
        'messages.send.request': { bg: 'Изпрати заявка', en: 'Send request' },
        'messages.identity.unavailable': { bg: 'Тази идентичност вече не е достъпна за акаунта ти.', en: 'This page identity is no longer available for your account.' },
        'share.chat': { bg: 'Изпрати в чат', en: 'Send in chat' },
        'share.card.event': { bg: 'Събитие', en: 'Event' },
        'share.card.post': { bg: 'Пост', en: 'Post' },
        'share.chat.title': { bg: 'Избери разговор', en: 'Choose a conversation' },
        'share.chat.preview': { bg: 'Споделяш', en: 'Sharing' },
        'share.chat.empty': { bg: 'Все още няма активни чатове. Отвори профил и започни разговор.', en: 'No active chats yet. Open a profile first and start a conversation.' },
        'share.chat.note': { bg: 'Добави бележка', en: 'Add a note' },
        'share.chat.note.placeholder': { bg: 'Кажи нещо за него...', en: 'Say something about it...' },
        'layout.venue.name': { bg: 'Зала / място', en: 'Venue' },
        'layout.name': { bg: 'Име на layout', en: 'Layout name' },
        'layout.floor.add': { bg: 'Етаж', en: 'Floor' },
        'layout.floor.name': { bg: 'Име на етаж', en: 'Floor name' },
        'layout.standing': { bg: 'Правостоящи', en: 'Standing' },
        'layout.table': { bg: 'Маса', en: 'Table' },
        'layout.generate.table': { bg: 'Места около маса', en: 'Table seats' },
        'layout.duplicate.item': { bg: 'Дублирай', en: 'Duplicate' },
        'layout.delete.item': { bg: 'Изтрий', en: 'Delete' },
        'layout.section.type': { bg: 'Тип', en: 'Type' },
        'layout.section.shape': { bg: 'Форма', en: 'Shape' },
        'layout.rotation': { bg: 'Завъртане', en: 'Rotation' },
        'layout.seat.type': { bg: 'Тип място', en: 'Seat type' },
        'layout.seat.status': { bg: 'Статус', en: 'Status' },
        'layout.quick.generate': { bg: 'Бързо генериране', en: 'Quick generate' },
    });

    Object.assign(KEYED, {
        'card.manage': { bg: 'Управление', en: 'Manage' },
        'event.similar': { bg: 'Сходни събития', en: 'Similar events' },
        'home.events.discover': { bg: 'Открий своята вечер', en: 'Discover your night' },
        'home.events.discover.sub': { bg: 'Подбрани събития, фестивали и партита около теб.', en: 'Curated events, festivals and parties near you.' },
        'home.trending.sub': { bg: 'Най-нашумелите събития според реакциите.', en: 'The most talked-about events by reactions.' },
        'home.trending.empty': { bg: 'Все още няма достатъчно реакции.', en: 'Not enough reactions yet.' },
        'messages.message.page': { bg: 'Пиши на страницата', en: 'Message page' },
        'nav.aiplan': { bg: 'AI план за деня', en: 'AI day plan' },
        'nav.wrapped': { bg: 'Year Wrapped', en: 'Year Wrapped' },
        'share.btn': { bg: 'Сподели', en: 'Share' },
        'share.copy': { bg: 'Копирай линк', en: 'Copy link' },
        'sort.recent': { bg: 'Най-нови', en: 'Recently added' },
        'sort.soon': { bg: 'Започват скоро', en: 'Starting soon' },
        'sort.popular': { bg: 'Най-популярни', en: 'Most popular' },
        'wrapped.hello': { bg: 'Здравей,', en: 'Hello,' }
    });

    Object.assign(KEYED, {
        // PROFILE STATS
        'stat.this.month':  { bg: 'този месец',         en: 'this month' },
        'stats.attended':   { bg: 'Посетени събития',  en: 'Events Attended' },
        'stats.interested': { bg: 'Интересува ме',      en: 'Interested' },
        'stats.likes':      { bg: 'Дадени харесвания',  en: 'Likes Given' },
        'stats.followers':  { bg: 'Последователи',       en: 'Followers' },
        'stats.favgenre':   { bg: 'Любим жанр',          en: 'Favourite Genre' },
        'stats.cities':     { bg: 'Града посетени',      en: 'Cities Visited' },

        // PROFILE MEMORIES
        'memories.kicker': { bg: 'Спомени',                          en: 'Memories' },
        'memories.desc':   { bg: 'Преди време беше на тези събития', en: 'You attended these events before' },

        // TICKETS PAGE
        'tickets.page.stamp':  { bg: 'Билети',              en: 'Tickets' },
        'tickets.page.title':  { bg: 'Твоите <span>запазени</span> места.',   en: 'Your <span>reserved</span> seats.' },
        'tickets.page.desc':   { bg: 'Всички купени билети са тук, заедно с QR кода и бърз достъп до PDF версията.', en: 'All purchased tickets are here, along with the QR code and quick PDF access.' },
        'tickets.explore':     { bg: 'Разгледай събития',   en: 'Explore Events' },
        'tickets.empty.title': { bg: 'Още нямаш <span>купени билети</span>.', en: 'No <span>purchased tickets</span> yet.' },
        'tickets.empty.desc':  { bg: 'Когато вземеш билет за събитие, той ще се появи тук с всички детайли за вход.', en: 'When you get a ticket for an event, it will appear here with all entry details.' },
        'tickets.find':        { bg: 'Намери събитие',       en: 'Find an Event' },
        'tickets.dt.location': { bg: 'Локация',              en: 'Location' },
        'tickets.dt.start':    { bg: 'Начало',               en: 'Start' },
        'tickets.dt.price':    { bg: 'Цена',                 en: 'Price' },
        'tickets.details.btn': { bg: 'Детайли',              en: 'Details' },
        'tickets.used':        { bg: 'Използван',            en: 'Used' },
        'tickets.valid':       { bg: 'Валиден',              en: 'Valid' },

        // AI PLAN
        'aiplan.kicker':       { bg: 'AI компаньон',           en: 'AI Companion' },
        'aiplan.h1':           { bg: 'Какъв ще бъде твоят ден?', en: 'What will your day be like?' },
        'aiplan.p':            { bg: 'Кажи ни град и настроение — Evento AI ще ти подреди вечер от реалните събития около теб.', en: 'Tell us your city and vibe — Evento AI will curate an evening from real events near you.' },
        'aiplan.city':         { bg: 'Град',                   en: 'City' },
        'aiplan.city.choose':  { bg: 'Избери',                 en: 'Choose' },
        'aiplan.date':         { bg: 'Дата',                   en: 'Date' },
        'aiplan.vibe':         { bg: 'Настроение / повод',     en: 'Vibe / occasion' },
        'aiplan.vibe.placeholder': { bg: 'напр. първа среща · с приятели · спокойна вечер · техно до късно', en: 'e.g. first date · with friends · quiet evening · techno until late' },
        'aiplan.submit':       { bg: 'Подреди ми вечерта',     en: 'Plan my evening' },
        'aiplan.result.kicker':{ bg: 'Evento AI предлага',     en: 'Evento AI suggests' },
        'aiplan.all.events':   { bg: 'Всички събития за деня', en: 'All events for the day' },
        'aiplan.no.events':    { bg: 'Няма публикувани събития за избрания ден. Опитай с друга дата.', en: 'No events for the selected day. Try another date.' },

        // RECOMMENDED
        'rec.stamp':           { bg: 'Подбор',              en: 'Selection' },
        'rec.h1':              { bg: 'Вечери, избрани <span>за теб</span>.', en: 'Evenings chosen <span>for you</span>.' },
        'rec.p':               { bg: 'Тук събираме предстоящите събития, които най-добре пасват на твоите предпочитания. Ако още не си ги настроил, ще покажем всички одобрени предложения.', en: 'Here we gather upcoming events that best match your preferences. If you have not set them yet, we will show all approved events.' },
        'rec.myprefs':         { bg: 'Моите предпочитания', en: 'My Preferences' },
        'rec.settings':        { bg: 'Настрой препоръките', en: 'Set Recommendations' },
        'rec.no.prefs':        { bg: 'Нямаш запазени предпочитания.', en: 'You have no saved preferences.' },
        'rec.no.prefs.desc':   { bg: 'Показваме всички предстоящи одобрени събития. Добави любим жанр, град или радиус, за да получаваш по-точни предложения.', en: 'Showing all upcoming approved events. Add a favourite genre, city or radius for more precise suggestions.' },
        'rec.upcoming.kicker': { bg: 'Предстоящи предложения', en: 'Upcoming suggestions' },
        'rec.empty.title':     { bg: 'Още няма <span>точно попадение</span>.', en: 'No <span>perfect match</span> yet.' },
        'rec.empty.desc':      { bg: 'Няма предстоящи събития, които да отговарят на сегашните ти настройки. Можеш да ги промениш или да разгледаш всички събития.', en: 'No upcoming events match your current settings. You can change them or browse all events.' },
        'rec.change.prefs':    { bg: 'Промени предпочитанията', en: 'Change Preferences' },

        // WRAPPED
        'wrapped.empty.h1':       { bg: 'Все още няма какво да обвием.',     en: 'Nothing to wrap yet.' },
        'wrapped.find.event':     { bg: 'Намери събитие',                    en: 'Find an Event' },
        'wrapped.year.in.evento': { bg: 'Това е твоята година във Evento.',  en: 'This is your year in Evento.' },
        'wrapped.your.year':      { bg: 'Това беше твоята година.',          en: 'That was your year.' },
        'wrapped.share.memo':     { bg: 'Споделяй, запомняй, продължавай.', en: 'Share, remember, continue.' },
        'wrapped.save':           { bg: 'Запази',                            en: 'Save' },
        'wrapped.scroll':         { bg: 'Скролни',                           en: 'Scroll' },
        'wrapped.kicker.count':   { bg: 'Брой събития',                     en: 'Events count' },
        'wrapped.kicker.genre':   { bg: 'Топ жанр',                         en: 'Top genre' },
        'wrapped.kicker.city':    { bg: 'Любим град',                       en: 'Favourite city' },
        'wrapped.kicker.scene':   { bg: 'На сцената',                       en: 'On stage' },
        'wrapped.kicker.organizer':{ bg: 'Топ организатор',                 en: 'Top organizer' },
        'wrapped.kicker.month':   { bg: 'Най-натоварен месец',              en: 'Busiest month' },
        'wrapped.kicker.moments': { bg: 'Топ моменти',                      en: 'Top moments' },
        'wrapped.kicker.favs':    { bg: 'Твоите фаворити',                  en: 'Your favourites' },
        'wrapped.kicker.likes':   { bg: 'Харесвания',                       en: 'Likes' },
        'wrapped.kicker.comments':{ bg: 'Коментари',                        en: 'Comments' },
        'wrapped.kicker.orgs':    { bg: 'Нови организатори',                en: 'New organizers' },
        'wrapped.kicker.ai':      { bg: 'Evento AI ти казва',               en: 'Evento AI tells you' },
    });

    function getLang() {
        var meta = document.querySelector('meta[name="x-app-lang"]');
        return meta ? meta.getAttribute('content') : 'bg';
    }

    var SKIP_TAGS = { SCRIPT: 1, STYLE: 1, NOSCRIPT: 1, TEXTAREA: 1, CODE: 1, PRE: 1, INPUT: 1, SELECT: 1 };

    function shouldSkipElement(el) {
        return !el || SKIP_TAGS[el.tagName] || !!(el.closest && el.closest('[data-no-i18n], .gm-style'));
    }

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
        if (shouldSkipElement(el)) return;
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
            if (shouldSkipElement(el)) return;
            var t = dict[el.getAttribute('placeholder')];
            if (t) el.setAttribute('placeholder', t);
        });
        root.querySelectorAll('[aria-label]').forEach(function (el) {
            if (shouldSkipElement(el)) return;
            var t = dict[el.getAttribute('aria-label')];
            if (t) el.setAttribute('aria-label', t);
        });
    }

    function translateKeyedElement(el, lang) {
        if (!el || shouldSkipElement(el)) return;

        var key = el.getAttribute('data-i18n');
        var entry = key ? KEYED[key] : null;
        if (entry && entry[lang] !== undefined) {
            el.textContent = entry[lang];
        }

        key = el.getAttribute('data-i18n-html');
        entry = key ? KEYED[key] : null;
        if (entry && entry[lang] !== undefined) {
            el.innerHTML = entry[lang];
        }

        key = el.getAttribute('data-i18n-placeholder');
        entry = key ? KEYED[key] : null;
        if (entry && entry[lang] !== undefined) {
            el.setAttribute('placeholder', entry[lang]);
        }

        key = el.getAttribute('data-i18n-title');
        entry = key ? KEYED[key] : null;
        if (entry && entry[lang] !== undefined) {
            el.setAttribute('title', entry[lang]);
        }

        key = el.getAttribute('data-i18n-aria-label');
        entry = key ? KEYED[key] : null;
        if (entry && entry[lang] !== undefined) {
            el.setAttribute('aria-label', entry[lang]);
        }
    }

    function translateKeyed(lang, root) {
        root = root || document;
        if (root.nodeType === 1) {
            translateKeyedElement(root, lang);
        }

        root.querySelectorAll('[data-i18n], [data-i18n-html], [data-i18n-placeholder], [data-i18n-title], [data-i18n-aria-label]')
            .forEach(function (el) {
                translateKeyedElement(el, lang);
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

    function showTranslateBtns(root) {
        (root || document).querySelectorAll('.groove-translate-btn').forEach(function (b) {
            b.style.display = 'inline-block';
        });
    }

    function collapseComments() {
        document.querySelectorAll('.groove-list-stack').forEach(function (stack) {
            var items = Array.from(stack.children).filter(function (el) {
                return el.tagName === 'ARTICLE' && el.hasAttribute('data-comment-item');
            });
            if (items.length <= 1) return;
            for (var i = 1; i < items.length; i++) {
                items[i].style.display = 'none';
            }
            var remaining = items.length - 1;
            var btn = document.createElement('button');
            btn.className = 'groove-button groove-button-paper mt-3 d-block w-100';
            btn.setAttribute('type', 'button');
            btn.innerHTML = '<i class="bi bi-chat-left-text"></i> ' + (
                lang === 'en'
                    ? 'Show ' + remaining + ' more comment' + (remaining !== 1 ? 's' : '')
                    : 'Покажи още ' + remaining + ' ' + (remaining === 1 ? 'коментар' : 'коментара')
            );
            btn.addEventListener('click', function () {
                for (var j = 1; j < items.length; j++) { items[j].style.display = ''; }
                btn.remove();
            });
            var footer = stack.nextElementSibling;
            stack.parentNode.insertBefore(btn, footer || null);
        });
    }

    function autoTranslateQueue() {
        if (typeof window.gtTranslate !== 'function') return;
        var btns = Array.from(document.querySelectorAll('.groove-translate-btn'))
            .filter(function (b) { return b.dataset.state !== '1' && b.offsetParent !== null; })
            .slice(0, 10);
        if (!btns.length) return;
        var i = 0;
        function fire() {
            if (i >= btns.length) return;
            var btn = btns[i++];
            if (btn.dataset.state !== '1' && btn.offsetParent !== null && typeof window.gtTranslate === 'function') {
                window.gtTranslate(btn);
            }
            setTimeout(fire, 650);
        }
        fire();
    }

    function run() {
        applyTranslations(lang);
        collapseComments();
        if (lang !== 'bg') {
            showTranslateBtns();
            setTimeout(autoTranslateQueue, 0);
        }

        if (window.MutationObserver) {
            var observer = new MutationObserver(function (mutations) {
                mutations.forEach(function (m) {
                    m.addedNodes.forEach(function (node) {
                        if (node.nodeType !== 1) return;
                        if (shouldSkipElement(node)) return;
                        translateKeyed(lang, node);
                        if (lang !== 'bg') {
                            walkAndTranslate(node, EN);
                            translateAttributes(node, EN);
                            showTranslateBtns(node);
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
