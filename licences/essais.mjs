// Essais du service de licences, en local avec Miniflare :  node licences/essais.mjs  (depuis le dossier où miniflare est installé, ou NODE_PATH)
import { Miniflare } from 'miniflare';
import crypto from 'node:crypto';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ici = path.dirname(fileURLToPath(import.meta.url));
const SECRET = 'whsec_essai_reel', SECRET_TEST = 'whsec_essai_test', ADMIN = 'motdepasse-admin-essai';
let echecs = 0, n = 0;
const ok = (cond, nom) => { n++; if (!cond) { echecs++; console.log('ÉCHEC :', nom); } else console.log('ok :', nom); };

function signer(corps, secret, t = Math.floor(Date.now() / 1000)) {
  const sig = crypto.createHmac('sha256', secret).update(t + '.' + corps).digest('hex');
  return 't=' + t + ',v1=' + sig;
}

const mf = new Miniflare({
  modules: true, scriptPath: path.join(ici, 'worker.js'), kvNamespaces: ['LICENCES'], compatibilityDate: '2025-06-01',
  bindings: { STRIPE_WEBHOOK_SECRET: SECRET, STRIPE_WEBHOOK_SECRET_TEST: SECRET_TEST, ADMIN_TOKEN: ADMIN },
});
const B = 'https://licences.exemple';
const post = (p, corps, entetes = {}) => mf.dispatchFetch(B + p, { method: 'POST', body: corps, headers: entetes });
const form = (o) => new URLSearchParams(o).toString();
const FORM = { 'Content-Type': 'application/x-www-form-urlencoded' };

function evenement(type, objet, livemode = true) { return JSON.stringify({ id: 'evt_' + crypto.randomUUID(), type, livemode, data: { object: objet } }); }
const ref = 'ref_' + crypto.randomBytes(16).toString('hex');
const session = { id: 'cs_live_a1B2c3D4e5F6g7H8i9J0', object: 'checkout.session', payment_status: 'paid', livemode: true, payment_intent: 'pi_123', client_reference_id: ref,
  customer_details: { email: 'Client@Exemple.fr', name: 'Jean Client' }, amount_total: 999, currency: 'eur', payment_link: 'plink_1' };

// 1. webhook : signature
let r = await post('/stripe', evenement('checkout.session.completed', session), { 'Stripe-Signature': 'v1=faux' });
ok(r.status === 400, 'signature absente refusée');
let corps = evenement('checkout.session.completed', session);
r = await post('/stripe', corps, { 'Stripe-Signature': signer(corps, SECRET, Math.floor(Date.now() / 1000) - 600) });
ok(r.status === 400, 'signature trop ancienne refusée');
r = await post('/stripe', corps, { 'Stripe-Signature': signer(corps, 'whsec_autre') });
ok(r.status === 400, 'mauvais secret refusé');

// 2. page merci avant le webhook : attente
r = await mf.dispatchFetch(B + '/merci?session_id=' + session.id);
let html = await r.text();
ok(r.status === 200 && html.includes('http-equiv="refresh"'), 'page merci en attente avant le paiement');
r = await mf.dispatchFetch(B + '/v1/achats/' + ref);
ok((await r.json()).status === 'pending', 'achat en attente pour l’application');

// 3. webhook valide : création de la clé (deux fois : une seule clé)
r = await post('/stripe', corps, { 'Stripe-Signature': signer(corps, SECRET) });
ok(r.status === 200, 'webhook valide accepté');
r = await post('/stripe', corps, { 'Stripe-Signature': signer(corps, SECRET) });
ok(r.status === 200, 'webhook rejoué accepté');
r = await mf.dispatchFetch(B + '/v1/achats/' + ref);
const a = await r.json();
ok(a.status === 'paid' && /^[A-F0-9-]{36}$/.test(a.license_key), 'l’application récupère la clé');
const CLE = a.license_key;
r = await mf.dispatchFetch(B + '/merci?session_id=' + session.id);
html = await r.text();
ok(html.includes(CLE) && !html.includes('refresh'), 'page merci affiche la clé');
ok(r.headers.get('Cache-Control') === 'no-store' && r.headers.get('X-Robots-Tag') === 'noindex', 'page merci non mise en cache ni indexée');
const kv = await mf.getKVNamespace('LICENCES');
const toutes = (await kv.list({ prefix: 'lic:' })).keys;
ok(toutes.length === 1, 'une seule clé malgré le webhook rejoué');

// 4. activation (format Lemon Squeezy)
const act = async (nom, cle = CLE) => { const x = await post('/v1/licenses/activate', form({ license_key: cle, instance_name: nom }), FORM); return [x.status, await x.json()]; };
let [s1, j1] = await act('PC-AAAA1111');
ok(s1 === 200 && j1.activated === true && j1.instance.id && j1.meta.store_id === 1 && j1.meta.product_id === 1 && j1.meta.customer_email === 'client@exemple.fr', 'activation sur un 1er PC');
let [s1b, j1b] = await act('PC-AAAA1111');
ok(s1b === 200 && j1b.instance.id === j1.instance.id && j1b.license_key.activation_usage === 1, 'réactivation du même PC sans place en plus');
await act('PC-BBBB2222'); await act('PC-CCCC3333');
let [s4, j4] = await act('PC-DDDD4444');
ok(s4 === 400 && /activation limit/.test(j4.error), 'limite de 3 PC');
let [s5, j5] = await act('PC-AAAA1111', 'ABCDEF12-0000-0000-0000-000000000000');
ok(s5 === 404 && /not found/.test(j5.error), 'clé inconnue');
let [s6] = await act('PC-AAAA1111', 'pas une clé');
ok(s6 === 404, 'format de clé invalide');

// 5. validation et retrait
const val = async (id) => { const x = await post('/v1/licenses/validate', form({ license_key: CLE, instance_id: id }), FORM); return [x.status, await x.json()]; };
let [v1, jv1] = await val(j1.instance.id);
ok(v1 === 200 && jv1.valid === true && jv1.license_key.status === 'active', 'validation d’un PC activé');
let [v2, jv2] = await val('inconnu');
ok(v2 === 200 && jv2.valid === false && jv2.license_key.status === 'active', 'validation d’un PC retiré : invalide mais clé active');
r = await post('/v1/licenses/deactivate', form({ license_key: CLE, instance_id: j1.instance.id }), FORM);
ok((await r.json()).deactivated === true, 'retrait d’un PC');
let [s7, j7] = await act('PC-DDDD4444');
ok(s7 === 200 && j7.activated, 'la place libérée sert à un autre PC');

// 6. remboursement : clé désactivée
corps = evenement('charge.refunded', { id: 'ch_1', object: 'charge', payment_intent: 'pi_123', refunded: true, amount: 999, amount_refunded: 999 });
r = await post('/stripe', corps, { 'Stripe-Signature': signer(corps, SECRET) });
let [v3, jv3] = await val(j7.instance.id);
ok(v3 === 200 && jv3.valid === false && jv3.license_key.status === 'disabled', 'clé désactivée après remboursement');
r = await mf.dispatchFetch(B + '/v1/achats/' + ref);
ok((await r.json()).status === 'disabled', 'l’application ne récupère pas une clé remboursée');
let [s8, j8] = await act('PC-EEEE5555');
ok(s8 === 400 && /disabled/.test(j8.error), 'activation refusée après remboursement');

// 7. mode test : clé créée mais refusée tant que ACCEPTER_TEST n'est pas « oui »
const st = { ...session, id: 'cs_test_z9Y8x7W6v5U4t3S2r1Q0', livemode: false, payment_intent: 'pi_test', client_reference_id: 'ref_' + crypto.randomBytes(16).toString('hex') };
corps = evenement('checkout.session.completed', st, false);
r = await post('/stripe', corps, { 'Stripe-Signature': signer(corps, SECRET) });
ok((await r.json()).ignore, 'événement de test refusé sur le secret réel');
r = await post('/stripe', corps, { 'Stripe-Signature': signer(corps, SECRET_TEST) });
r = await mf.dispatchFetch(B + '/v1/achats/' + st.client_reference_id);
const at = await r.json();
ok(at.status === 'paid' && at.test === true, 'achat de test créé avec le secret de test');
let [s9] = await act('PC-TEST0001', at.license_key);
ok(s9 === 404, 'clé de test refusée sans ACCEPTER_TEST');

// 8. paiement non abouti : pas de clé
const sp = { ...session, id: 'cs_live_pendingPending01', payment_status: 'unpaid', payment_intent: 'pi_pending', client_reference_id: 'ref_' + crypto.randomBytes(16).toString('hex') };
corps = evenement('checkout.session.completed', sp);
await post('/stripe', corps, { 'Stripe-Signature': signer(corps, SECRET) });
r = await mf.dispatchFetch(B + '/v1/achats/' + sp.client_reference_id);
ok((await r.json()).status === 'pending', 'paiement non abouti : pas de clé');
corps = evenement('checkout.session.async_payment_succeeded', { ...sp, payment_status: 'paid' });
await post('/stripe', corps, { 'Stripe-Signature': signer(corps, SECRET) });
r = await mf.dispatchFetch(B + '/v1/achats/' + sp.client_reference_id);
ok((await r.json()).status === 'paid', 'paiement différé confirmé : clé créée');

// 9. administration
r = await post('/admin', form({ jeton: 'faux' }), FORM);
ok(r.status === 403, 'admin : mauvais mot de passe refusé');
r = await post('/admin', form({ jeton: ADMIN, recherche: 'client@exemple.fr' }), FORM);
html = await r.text();
ok(html.includes(CLE) && html.includes('disabled'), 'admin : recherche par e-mail');
r = await post('/admin', form({ jeton: ADMIN, recherche: 'client@exemple.fr', action: 'reactiver', cle: CLE }), FORM);
let [v4, jv4] = await val(j7.instance.id);
ok(jv4.valid === true, 'admin : réactivation');
await post('/admin', form({ jeton: ADMIN, action: 'liberer', cle: CLE }), FORM);
let [v5, jv5] = await val(j7.instance.id);
ok(jv5.valid === false, 'admin : PC libérés');
r = await mf.dispatchFetch(B + '/admin?jeton=' + ADMIN);
ok(!(await r.text()).includes(CLE), 'admin : le mot de passe dans l’adresse ne suffit pas');

// 10. injection HTML dans le nom du client
const sx = { ...session, id: 'cs_live_xssXssXssXss01', payment_intent: 'pi_x', client_reference_id: null, customer_details: { email: 'x@y.fr', name: '<script>alert(1)</script>' } };
corps = evenement('checkout.session.completed', sx);
await post('/stripe', corps, { 'Stripe-Signature': signer(corps, SECRET) });
r = await post('/admin', form({ jeton: ADMIN, recherche: 'x@y.fr' }), FORM);
html = await r.text();
ok(!html.includes('<script>alert') && html.includes('&lt;script&gt;'), 'admin : nom du client échappé');
r = await mf.dispatchFetch(B + '/merci?session_id=<b>');
ok(r.status === 400, 'merci : numéro de commande invalide refusé');

await mf.dispose();
console.log('\n' + (n - echecs) + '/' + n + ' essais réussis');
process.exit(echecs ? 1 : 0);
