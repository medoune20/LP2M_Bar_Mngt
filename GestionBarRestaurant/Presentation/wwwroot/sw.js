/*
 * Service worker LP2M APPS — stratégie prudente :
 *  - navigations (pages) : réseau d'abord, repli sur le cache puis page hors-ligne ;
 *  - assets statiques same-origin : cache d'abord, mise à jour en arrière-plan ;
 *  - requêtes non-GET (ventes, API) : jamais interceptées.
 * Les utilisateurs en ligne reçoivent toujours du contenu frais.
 */
const VERSION = 'lp2m-v1';
const SCOPE = self.registration.scope;

const PRECACHE = [
    SCOPE,
    SCOPE + 'css/site.css',
    SCOPE + 'js/site.js',
    SCOPE + 'js/offline-pos.js'
];

self.addEventListener('install', function (e) {
    self.skipWaiting();
    e.waitUntil(caches.open(VERSION).then(function (c) {
        return Promise.all(PRECACHE.map(function (u) {
            return c.add(new Request(u, { cache: 'reload' })).catch(function () { });
        }));
    }));
});

self.addEventListener('activate', function (e) {
    e.waitUntil(
        caches.keys().then(function (cles) {
            return Promise.all(cles.filter(function (k) { return k !== VERSION; }).map(function (k) { return caches.delete(k); }));
        }).then(function () { return self.clients.claim(); })
    );
});

function pageHorsLigne() {
    return new Response(
        '<!doctype html><html lang="fr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">' +
        '<title>Hors-ligne</title><style>body{font-family:Segoe UI,system-ui,sans-serif;display:flex;min-height:100vh;align-items:center;justify-content:center;background:#faf9f8;color:#323130;margin:0}' +
        '.b{text-align:center;padding:2rem}</style></head><body><div class="b"><h1>Hors-ligne</h1>' +
        '<p>La connexion est indisponible. La caisse rapide reste utilisable et vos ventes seront synchronisées au retour du réseau.</p>' +
        '<button onclick="location.reload()" style="padding:.6rem 1.2rem;border:0;border-radius:6px;background:#165DFF;color:#fff;font-weight:600">Réessayer</button>' +
        '</div></body></html>',
        { headers: { 'Content-Type': 'text/html; charset=utf-8' } }
    );
}

self.addEventListener('fetch', function (e) {
    const req = e.request;
    if (req.method !== 'GET') return; // ne jamais intercepter les POST (ventes/API)

    const url = new URL(req.url);
    const memeOrigine = url.origin === self.location.origin;

    if (req.mode === 'navigate') {
        e.respondWith(
            fetch(req).then(function (resp) {
                if (memeOrigine && resp && resp.ok) {
                    const copie = resp.clone();
                    caches.open(VERSION).then(function (c) { c.put(req, copie); });
                }
                return resp;
            }).catch(function () {
                return caches.match(req).then(function (m) { return m || pageHorsLigne(); });
            })
        );
        return;
    }

    if (memeOrigine && /\.(css|js|png|jpg|jpeg|webp|svg|woff2?|ico|json)$/i.test(url.pathname)) {
        e.respondWith(
            caches.match(req).then(function (m) {
                const reseau = fetch(req).then(function (resp) {
                    if (resp && resp.ok) {
                        const copie = resp.clone();
                        caches.open(VERSION).then(function (c) { c.put(req, copie); });
                    }
                    return resp;
                }).catch(function () { return m; });
                return m || reseau;
            })
        );
    }
});
