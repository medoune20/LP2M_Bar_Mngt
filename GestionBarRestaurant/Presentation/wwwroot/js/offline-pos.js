/*
 * Caisse hors-ligne : file d'attente locale (IndexedDB) des ventes validées
 * sans réseau, puis resynchronisation vers l'endpoint existant Vente/Rapide.
 * L'idempotence (champ clientUuid) garantit l'absence de doublon au renvoi.
 * Générique : ne dépend d'aucune variable de la page.
 */
(function () {
    const DB_NAME = 'lp2m-pos';
    const STORE = 'ventes';
    let _changeCb = null;

    function uuid() {
        if (window.crypto && crypto.randomUUID) return crypto.randomUUID();
        return 'xxxxxxxxxxxx4xxxyxxxxxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            const r = Math.random() * 16 | 0;
            return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
        });
    }

    function ouvrir() {
        return new Promise(function (resolve, reject) {
            const req = indexedDB.open(DB_NAME, 1);
            req.onupgradeneeded = function () {
                const db = req.result;
                if (!db.objectStoreNames.contains(STORE)) db.createObjectStore(STORE, { keyPath: 'id' });
            };
            req.onsuccess = function () { resolve(req.result); };
            req.onerror = function () { reject(req.error); };
        });
    }

    function tx(mode, fn) {
        return ouvrir().then(function (db) {
            return new Promise(function (resolve, reject) {
                const t = db.transaction(STORE, mode);
                const store = t.objectStore(STORE);
                const out = fn(store);
                t.oncomplete = function () { resolve(out && out.result !== undefined ? out.result : out); };
                t.onerror = function () { reject(t.error); };
            });
        });
    }

    function compter() {
        return tx('readonly', function (s) { return s.count(); });
    }

    function tous() {
        return ouvrir().then(function (db) {
            return new Promise(function (resolve, reject) {
                const t = db.transaction(STORE, 'readonly');
                const req = t.objectStore(STORE).getAll();
                req.onsuccess = function () { resolve(req.result || []); };
                req.onerror = function () { reject(req.error); };
            });
        });
    }

    function notifier() {
        if (typeof _changeCb === 'function') compter().then(_changeCb).catch(function () { });
    }

    function mettreEnFile(formEl) {
        const data = new FormData(formEl);
        const entries = [];
        data.forEach(function (v, k) { entries.push([k, String(v)]); });
        const item = { id: uuid(), action: formEl.getAttribute('action') || location.pathname, body: entries, date: new Date().toISOString() };
        return tx('readwrite', function (s) { s.add(item); }).then(function () { notifier(); return item; });
    }

    function jetonAntiforgery() {
        const el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : null;
    }

    function envoyer(item) {
        const params = new URLSearchParams();
        const jeton = jetonAntiforgery();
        item.body.forEach(function (pair) {
            if (pair[0] === '__RequestVerificationToken' && jeton) return; // remplacé par le jeton courant
            params.append(pair[0], pair[1]);
        });
        if (jeton) params.append('__RequestVerificationToken', jeton);

        return fetch(item.action, {
            method: 'POST',
            body: params,
            headers: { 'X-Requested-With': 'offline-sync' },
            credentials: 'same-origin',
            redirect: 'follow'
        }).then(function (resp) {
            // Succès = redirection vers le ticket (Vente/Details). L'idempotence
            // protège contre un doublon si la détection échoue.
            const ok = resp && (resp.redirected ? /\/Vente\/Details/i.test(resp.url) : resp.ok);
            return !!ok;
        }).catch(function () { return false; });
    }

    function synchroniser() {
        if (!navigator.onLine) return Promise.resolve({ envoyees: 0, restant: -1 });
        return tous().then(function (items) {
            let envoyees = 0;
            let chaine = Promise.resolve();
            items.forEach(function (item) {
                chaine = chaine.then(function () {
                    return envoyer(item).then(function (ok) {
                        if (ok) { envoyees++; return tx('readwrite', function (s) { s.delete(item.id); }); }
                    });
                });
            });
            return chaine.then(function () { return compter(); }).then(function (restant) {
                notifier();
                return { envoyees: envoyees, restant: restant };
            });
        });
    }

    window.posOffline = {
        mettreEnFile: mettreEnFile,
        synchroniser: synchroniser,
        compter: compter,
        surChangement: function (cb) { _changeCb = cb; notifier(); }
    };

    // Resynchronisation automatique au retour du réseau.
    window.addEventListener('online', function () { synchroniser(); });
    document.addEventListener('DOMContentLoaded', function () { if (navigator.onLine) synchroniser(); });
})();
