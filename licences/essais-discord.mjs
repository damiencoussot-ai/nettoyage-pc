// Essais de la partie Discord et abonnement du service de licences (Miniflare, appels simulés) : node licences/essais-discord.mjs licences/worker.js
import { Miniflare, createFetchMock } from 'miniflare';
import crypto from 'node:crypto';
const W = process.argv[2];
let n = 0, echecs = 0; const ok = (c, nom) => { n++; if (!c) { echecs++; console.log('ÉCHEC :', nom); } else console.log('ok :', nom); };
const { publicKey, privateKey } = crypto.generateKeyPairSync('ed25519');
const pk = Buffer.from(publicKey.export({ format: 'jwk' }).x, 'base64url').toString('hex');
const fetchMock = createFetchMock(); fetchMock.disableNetConnect();
const appels = [];
const d = fetchMock.get('https://discord.com');
// le corps des requêtes arrive parfois en flux : il est lu à part, puis complété dans la liste des appels
const enreg = (m, p, rep, statut = 200) => d.intercept({ method: m, path: p }).reply(statut, (o) => {
  const k = appels.push(m + ' ' + o.path + ' ') - 1;
  if (o.body && typeof o.body.getReader === 'function') new Response(o.body).text().then((t) => { appels[k] += t; }, () => {});
  else appels[k] += String(o.body || '');
  return rep;
}).persist();
const pause = (ms = 100) => new Promise((x) => setTimeout(x, ms));
enreg('POST', '/api/v10/users/@me/channels', { id: '999000000000000001' });
enreg('POST', '/api/v10/channels/999000000000000001/messages', { id: 'm1' });
enreg('PUT', /\/api\/v10\/guilds\/.*\/roles\/.*/, '', 204);
enreg('DELETE', /\/api\/v10\/guilds\/.*\/roles\/.*/, '', 204);
enreg('PUT', '/api/v10/applications/111111111111111111/commands', []);
enreg('PATCH', /\/api\/v10\/webhooks\/111111111111111111\/.*\/messages\/@original/, {});
enreg('PATCH', /\/api\/v10\/channels\/\d+\/messages\/\w+$/, { id: 'avis1' });
enreg('POST', /\/api\/v10\/channels\/\d+\/messages$/, { id: '700000000000000001' });
import http from 'node:http';
const fauxDiscord = { gerer: () => undefined };
const srv = http.createServer((q, s) => { let b = ''; q.on('data', (x) => { b += x; }); q.on('end', () => {
  const chemin = q.url.replace(/^\/api\/v10/, ''); let c = null; try { c = b ? JSON.parse(b) : null; } catch (e) { c = null; }
  const rep = fauxDiscord.gerer(q.method, chemin, c);
  if (rep === undefined) { s.writeHead(404); s.end('{}'); return; }
  if (rep === null) { s.writeHead(204); s.end(); return; }
  s.writeHead(200, { 'Content-Type': 'application/json' }); s.end(JSON.stringify(rep)); }); });
await new Promise((x) => srv.listen(0, '127.0.0.1', x));
const PORT = srv.address().port; fetchMock.enableNetConnect('127.0.0.1:' + PORT);
const SECRET = 'whsec_essai', ADMIN = 'motdepasse-admin-essai';
const mf = new Miniflare({ modules: true, scriptPath: W, kvNamespaces: ['LICENCES'], compatibilityDate: '2025-06-01', fetchMock,
  bindings: { STRIPE_WEBHOOK_SECRET: SECRET, ADMIN_TOKEN: ADMIN, LIENS_PAIEMENT: 'plink_velox', DISCORD_PUBLIC_KEY: pk, DISCORD_APP_ID: '111111111111111111', DISCORD_BOT_TOKEN: 'jeton-bot', LIEN_PAIEMENT: 'https://buy.stripe.com/test_abc', DISCORD_GUILD_ID: '222222222222222222', DISCORD_ROLE_PREMIUM: '333333333333333333', DISCORD_API_ESSAI: 'http://127.0.0.1:' + PORT + '/api/v10' } });
const B = 'https://licences.exemple';
function interaction(obj, sigOk = true) {
  const corps = JSON.stringify(obj), ts = String(Math.floor(Date.now() / 1000));
  let sig = crypto.sign(null, Buffer.from(ts + corps), privateKey).toString('hex');
  if (!sigOk) sig = sig.replace(/^./, sig[0] === 'a' ? 'b' : 'a');
  return mf.dispatchFetch(B + '/discord', { method: 'POST', body: corps, headers: { 'X-Signature-Ed25519': sig, 'X-Signature-Timestamp': ts, 'Content-Type': 'application/json' } });
}
const U = '444444444444444444', ADM = '555555555555555555';
const valider = async (c) => (await (await mf.dispatchFetch(B + '/v1/licenses/validate', { method: 'POST', body: new URLSearchParams({ license_key: c }).toString(), headers: { 'Content-Type': 'application/x-www-form-urlencoded' } })).json());
let r = await interaction({ type: 1 }); ok(r.status === 200 && (await r.json()).type === 1, 'PING Discord');
r = await interaction({ type: 1 }, false); ok(r.status === 401, 'signature fausse refusée');
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: U }, permissions: '0' }, data: { name: 'premium' } });
let j = await r.json(); const url = j.data.components[0].components[0].url;
ok(j.data.flags === 64 && /client_reference_id=discord_444444444444444444_[0-9a-f]{16}$/.test(url), '/premium : lien de paiement privé avec référence Discord');
const ref = new URL(url).searchParams.get('client_reference_id');
// paiement Stripe
const session = { id: 'cs_live_Discord1234567890', payment_status: 'paid', livemode: true, payment_intent: 'pi_discord', client_reference_id: ref, customer_details: { email: 'a@b.fr', name: 'Joueur' }, amount_total: 999, currency: 'eur', payment_link: 'plink_velox' };
const corps = JSON.stringify({ id: 'evt_1', type: 'checkout.session.completed', livemode: true, data: { object: session } });
const t = Math.floor(Date.now() / 1000), sigS = crypto.createHmac('sha256', SECRET).update(t + '.' + corps).digest('hex');
r = await mf.dispatchFetch(B + '/stripe', { method: 'POST', body: corps, headers: { 'Stripe-Signature': 't=' + t + ',v1=' + sigS } });
ok(r.status === 200, 'webhook Stripe accepté');
ok(appels.some((a) => a.startsWith('PUT /api/v10/guilds/222222222222222222/members/444444444444444444/roles/333333333333333333')), 'rôle Premium donné');
const msg = appels.find((a) => a.startsWith('POST /api/v10/channels/'));
ok(!!msg, 'clé envoyée en message privé (appel à Discord)');
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: U }, permissions: '0' }, data: { name: 'ma-cle' } });
j = await r.json(); ok(j.data.flags === 64 && j.data.embeds.length === 1 && j.data.embeds[0].description.includes('-'), '/ma-cle : la clé, en privé');
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: '666666666666666666' }, permissions: '0' }, data: { name: 'ma-cle' } });
j = await r.json(); ok(!j.data.embeds, '/ma-cle : rien pour un autre membre');
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: U }, permissions: '0' }, data: { name: 'cle-offerte', options: [{ name: 'membre', value: '777777777777777777' }] } });
j = await r.json(); ok(/réservée/.test(j.data.content), '/cle-offerte refusée sans droits admin');
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: ADM }, permissions: '8' }, token: 'jeton-essai', data: { name: 'cle-offerte', options: [{ name: 'membre', value: '777777777777777777' }, { name: 'duree', value: 30 }, { name: 'raison', value: 'concours' }], resolved: { users: { '777777777777777777': { id: '777777777777777777', username: 'testeur', global_name: 'Testeur77' } } } } });
j = await r.json(); ok(j.type === 5 && j.data.flags === 64, '/cle-offerte : réponse immédiate « en cours » (moins de 3 s)');
await new Promise((x) => setTimeout(x, 300));
ok(appels.some((a) => a.startsWith('PATCH /api/v10/webhooks/111111111111111111/jeton-essai/messages/@original')), '/cle-offerte : résultat affiché ensuite');
// la clé offerte est valide pour l'application
r = await interaction({ type: 2, member: { user: { id: '777777777777777777' }, permissions: '0' }, guild_id: '222222222222222222', data: { name: 'ma-cle' } });
j = await r.json(); const cleOfferte = /```([A-F0-9-]{36})```/.exec(j.data.embeds[0].description)[1];
r = await mf.dispatchFetch(B + '/v1/licenses/activate', { method: 'POST', body: new URLSearchParams({ license_key: cleOfferte, instance_name: 'PC-essai' }).toString(), headers: { 'Content-Type': 'application/x-www-form-urlencoded' } });
j = await r.json(); ok(j.activated === true || j.valid === true || j.license_key, 'clé offerte activable dans Velox : ' + JSON.stringify(j).slice(0, 80));
await pause();
const finAttendue = Date.now() + 30 * 86400000;
ok(j.license_key.status === 'active' && Math.abs(Date.parse(j.license_key.expires_at) - finAttendue) < 60000 && j.meta.customer_name === 'Testeur77', 'clé offerte 30 jours : date de fin et pseudo transmis à Velox');
ok(appels.some((a) => a.startsWith('POST /api/v10/channels/999000000000000001/messages') && a.includes('/avis') && a.includes('clé offerte')), 'clé offerte : le message privé invite à un avis honnête');
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: ADM }, permissions: '8' }, data: { name: 'cle-offerte', options: [{ name: 'membre', value: '777777777777777777' }] } });
j = await r.json(); ok(/Choisis une durée/.test(j.data.content), '/cle-offerte sans durée (anciennes commandes) : refusée avec explication');
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: ADM }, permissions: '8' }, data: { name: 'cle-offerte', options: [{ name: 'membre', value: '777777777777777777' }, { name: 'duree', value: 12 }] } });
j = await r.json(); ok(/Durée inconnue/.test(j.data.content), '/cle-offerte : durée inventée refusée');
// expiration : la date de fin est avancée dans le stockage (on ne peut pas attendre 30 jours)
const KV = await mf.getKVNamespace('LICENCES');
const vieillir = async (c) => { const l = JSON.parse(await KV.get('lic:' + c)); await KV.delete('fin:' + l.fin + ':' + c); l.fin = new Date(Date.now() - 1000).toISOString(); await KV.put('lic:' + c, JSON.stringify(l)); await KV.put('fin:' + l.fin + ':' + c, '1'); };
await vieillir(cleOfferte);
let nAppels = appels.length;
r = await mf.dispatchFetch(B + '/v1/licenses/validate', { method: 'POST', body: new URLSearchParams({ license_key: cleOfferte }).toString(), headers: { 'Content-Type': 'application/x-www-form-urlencoded' } });
j = await r.json(); await pause(); ok(j.valid === false && /expired/.test(j.error) && j.license_key.status === 'expired', 'clé offerte arrivée à sa fin : refusée par Velox (statut « expired »)');
ok(appels.slice(nAppels).some((a) => a.startsWith('DELETE /api/v10/guilds/222222222222222222/members/777777777777777777/roles/333333333333333333')), 'clé offerte finie : rôle Premium retiré');
ok(appels.slice(nAppels).some((a) => a.startsWith('POST /api/v10/channels/999000000000000001/messages') && a.includes('est terminée')), 'clé offerte finie : message privé au membre');
nAppels = appels.length;
r = await mf.dispatchFetch(B + '/v1/licenses/activate', { method: 'POST', body: new URLSearchParams({ license_key: cleOfferte, instance_name: 'PC-autre' }).toString(), headers: { 'Content-Type': 'application/x-www-form-urlencoded' } });
j = await r.json(); ok(j.activated === false && /expired/.test(j.error) && appels.length === nAppels, 'clé finie : activation refusée, sans deuxième message');
ok(!(await KV.get('fin:' + JSON.parse(await KV.get('lic:' + cleOfferte)).fin + ':' + cleOfferte)), 'clé finie : retirée de la liste des fins');
// page admin : recherche par membre, liste des clés offertes, prolongation
const adm = async (o) => (await (await mf.dispatchFetch(B + '/admin', { method: 'POST', body: new URLSearchParams(Object.assign({ jeton: ADMIN }, o)).toString(), headers: { 'Content-Type': 'application/x-www-form-urlencoded' } })).text());
let pageA = await adm({ recherche: '<@777777777777777777>' });
ok(pageA.includes(cleOfferte) && /finie le/.test(pageA) && /concours/.test(pageA), 'admin : recherche par mention Discord, clé offerte finie affichée');
ok((await adm({ recherche: '777777777777777777' })).includes(cleOfferte), 'admin : recherche par identifiant Discord');
pageA = await adm({ action: 'offertes' });
ok(pageA.includes(cleOfferte) && /Voir les clés offertes/.test(pageA), 'admin : liste des clés offertes');
pageA = await adm({ action: 'reactiver', cle: cleOfferte });
ok(/pas réactivée/.test(pageA) && (await valider(cleOfferte)).valid === false, 'admin : « Réactiver » ne relance pas une clé offerte finie');
nAppels = appels.length;
pageA = await adm({ action: 'prolonger', cle: cleOfferte });
let v = await valider(cleOfferte);
ok(/prolongée jusqu/.test(pageA) && v.valid === true && Math.abs(Date.parse(v.license_key.expires_at) - (Date.now() + 30 * 86400000)) < 60000, 'admin : +30 jours relance la clé finie');
ok(appels.slice(nAppels).some((a) => a.startsWith('PUT /api/v10/guilds/222222222222222222/members/777777777777777777/roles/333333333333333333')), 'admin : prolongation = rôle Premium rendu');
await adm({ action: 'avie', cle: cleOfferte });
v = await valider(cleOfferte); ok(v.valid === true && v.license_key.expires_at === null, 'admin : « À vie » enlève la date de fin');
// Cron : une clé de 7 jours finie est arrêtée sans que Velox ne la vérifie
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: ADM }, permissions: '8' }, token: 'jeton-essai2', data: { name: 'cle-offerte', options: [{ name: 'membre', value: '888000000000000008' }, { name: 'duree', value: 7 }] } });
await new Promise((x) => setTimeout(x, 300));
const cle7 = JSON.parse(await KV.get('discord:888000000000000008'))[0];
ok(!!(await KV.list({ prefix: 'fin:' })).keys.find((k) => k.name.endsWith(':' + cle7)), 'clé 7 jours : inscrite dans la liste des fins');
await vieillir(cle7);
const sch = await mf.getWorker();
try { await sch.scheduled({ cron: '0 * * * *' }); } catch (e) { console.log('scheduled', e.message); }
await new Promise((x) => setTimeout(x, 500));
const l7 = JSON.parse(await KV.get('lic:' + cle7));
ok(l7.statut === 'disabled' && l7.raison === 'clé offerte expirée', 'Cron : clé offerte finie désactivée toute seule');
ok((await KV.list({ prefix: 'fin:' })).keys.length === 0, 'Cron : liste des fins vidée');
// remboursement : rôle retiré
const ch = { id: 'ch_1', payment_intent: 'pi_discord', refunded: true, amount: 999, amount_refunded: 999 };
const c2 = JSON.stringify({ id: 'evt_2', type: 'charge.refunded', livemode: true, data: { object: ch } }), sig2 = crypto.createHmac('sha256', SECRET).update(t + '.' + c2).digest('hex');
await mf.dispatchFetch(B + '/stripe', { method: 'POST', body: c2, headers: { 'Stripe-Signature': 't=' + t + ',v1=' + sig2 } });
ok(appels.some((a) => a.startsWith('DELETE /api/v10/guilds/')), 'remboursement : rôle Premium retiré');
r = await mf.dispatchFetch(B + '/admin', { method: 'POST', body: new URLSearchParams({ jeton: ADMIN, action: 'commandes-discord' }).toString(), headers: { 'Content-Type': 'application/x-www-form-urlencoded' } });
const h = await r.text(); await pause(); ok(/7 commandes installées/.test(h), 'installation des commandes depuis /admin');
const cmds = JSON.parse(appels.filter((a) => a.startsWith('PUT /api/v10/applications/111111111111111111/commands')).pop().replace(/^PUT \S+ /, ''));
const cmdOff = cmds.find((c) => c.name === 'cle-offerte'), cmdAvis = cmds.find((c) => c.name === 'avis');
ok(cmdOff.options[1].name === 'duree' && cmdOff.options[1].required && cmdOff.options[1].choices.map((c) => c.value).join() === '7,30,90,365,0', '/cle-offerte : durée obligatoire (7, 30, 90, 365 jours ou à vie)');
ok(cmdAvis && cmdAvis.options[0].choices.length === 5 && cmdAvis.options[1].min_length === 20, '/avis : note de 1 à 5 et commentaire');
ok(cmds.find((c) => c.name === 'premium').description.includes('9,99 €/mois'), '/premium : prix 9,99 €/mois');
console.log(appels.map(a=>a.slice(0,140)).join('\n'));
r = await interaction({ type: 2, guild_id: '888888888888888888', member: { user: { id: ADM }, permissions: '8' }, data: { name: 'cle-offerte', options: [{ name: 'membre', value: '777777777777777777' }] } });
j = await r.json(); ok(/serveur officiel/.test(j.data.content), '/cle-offerte refusée sur un autre serveur');

// abonnement : création, résiliation, réactivation
const sess2 = { id: 'cs_live_Abonnement123456789', mode: 'subscription', subscription: 'sub_ABC', payment_status: 'paid', livemode: true, payment_intent: null, client_reference_id: 'discord_' + U + '_0123456789abcdef', customer_details: { email: 'abo@b.fr', name: 'Abonné' }, amount_total: 999, currency: 'eur', payment_link: 'plink_velox' };
const signe = (o) => { const c = JSON.stringify(o), tt = Math.floor(Date.now() / 1000); return [c, 't=' + tt + ',v1=' + crypto.createHmac('sha256', SECRET).update(tt + '.' + c).digest('hex')]; };
let [cA, sA] = signe({ id: 'evt_a', type: 'checkout.session.completed', livemode: true, data: { object: sess2 } });
await mf.dispatchFetch(B + '/stripe', { method: 'POST', body: cA, headers: { 'Stripe-Signature': sA } });
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: U }, permissions: '0' }, data: { name: 'ma-cle' } });
j = await r.json(); const cleAbo = /```([A-F0-9-]{36})```/.exec(j.data.embeds[j.data.embeds.length - 1].description)[1];
ok((await valider(cleAbo)).valid === true, 'abonnement : clé valide après le paiement');
[cA, sA] = signe({ id: 'evt_b', type: 'customer.subscription.updated', livemode: true, data: { object: { id: 'sub_ABC', status: 'past_due' } } });
await mf.dispatchFetch(B + '/stripe', { method: 'POST', body: cA, headers: { 'Stripe-Signature': sA } });
ok((await valider(cleAbo)).valid === true, 'abonnement en retard de paiement : clé encore valide');
const avant = appels.length;
[cA, sA] = signe({ id: 'evt_c', type: 'customer.subscription.deleted', livemode: true, data: { object: { id: 'sub_ABC', status: 'canceled' } } });
await mf.dispatchFetch(B + '/stripe', { method: 'POST', body: cA, headers: { 'Stripe-Signature': sA } });
ok((await valider(cleAbo)).valid === false, 'abonnement résilié : clé désactivée');
ok(appels.slice(avant).some((a) => a.startsWith('DELETE /api/v10/guilds/')), 'abonnement résilié : rôle Premium retiré');
[cA, sA] = signe({ id: 'evt_d', type: 'customer.subscription.updated', livemode: true, data: { object: { id: 'sub_ABC', status: 'active' } } });
await mf.dispatchFetch(B + '/stripe', { method: 'POST', body: cA, headers: { 'Stripe-Signature': sA } });
ok((await valider(cleAbo)).valid === true, 'abonnement repris : clé réactivée');
// un abonné qui avait aussi une clé offerte garde son rôle quand la clé offerte finit
await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: ADM }, permissions: '8' }, token: 'jeton-essai3', data: { name: 'cle-offerte', options: [{ name: 'membre', value: U }, { name: 'duree', value: 7 }] } });
await pause(300);
const cleU = JSON.parse(await KV.get('discord:' + U)).pop();
await vieillir(cleU);
const nU = appels.length;
ok((await valider(cleU)).valid === false && (await valider(cleAbo)).valid === true, 'abonné + clé offerte finie : seule la clé offerte s\'arrête');
await pause();
ok(!appels.slice(nU).some((a) => a.startsWith('DELETE /api/v10/guilds/222222222222222222/members/' + U + '/roles/')), 'abonné + clé offerte finie : rôle Premium gardé');

// organisation du serveur (bouton de /admin) : serveur simulé comme celui de Damien
const GU = '222222222222222222';
let id0 = 900000000000000000n; const nid = () => String(++id0);
const serveur = { salons: [
  { id: nid(), type: 4, name: 'Salons textuels', permission_overwrites: [] }, { id: nid(), type: 4, name: 'Salons vocaux', permission_overwrites: [] },
  { id: nid(), type: 0, name: 'annonce', permission_overwrites: [] }, { id: nid(), type: 0, name: 'mises-à-jour', permission_overwrites: [] },
  { id: nid(), type: 0, name: 'général', permission_overwrites: [] }, { id: nid(), type: 0, name: 'acheter-premium', permission_overwrites: [] },
  { id: nid(), type: 0, name: 'support', permission_overwrites: [] }, { id: nid(), type: 0, name: 'tes-captures', permission_overwrites: [] },
  { id: nid(), type: 2, name: 'Général', permission_overwrites: [] }],
  roles: [{ id: GU, name: '@everyone', color: 0, hoist: false }, { id: '333333333333333333', name: 'Premium', color: 0, hoist: false }], messages: 0, systeme: null };
const appelsOrg = [];
const route = (m, chemin, c) => {
  let x;
  if (m === 'GET' && chemin === '/guilds/' + GU + '/channels') return serveur.salons;
  if (m === 'GET' && chemin === '/guilds/' + GU + '/roles') return serveur.roles;
  if (m === 'POST' && chemin === '/guilds/' + GU + '/roles') { x = Object.assign({ id: nid() }, c); serveur.roles.push(x); return x; }
  if (m === 'PATCH' && (x = /^\/guilds\/\d+\/roles\/(\d+)$/.exec(chemin))) return Object.assign(serveur.roles.find((r) => r.id === x[1]), c);
  if (m === 'POST' && chemin === '/guilds/' + GU + '/channels') { x = Object.assign({ id: nid(), permission_overwrites: [] }, c); serveur.salons.push(x); return x; }
  if (m === 'PATCH' && chemin === '/guilds/' + GU + '/channels') { c.forEach((p) => { serveur.salons.find((s) => s.id === p.id).position = p.position; }); return null; }
  if (m === 'PATCH' && chemin === '/guilds/' + GU) { serveur.systeme = c.system_channel_id; return {}; }
  if (m === 'POST' && /^\/channels\/\d+\/messages$/.test(chemin)) { serveur.messages++; return { id: nid() }; }
  if (m === 'PATCH' && /^\/channels\/\d+\/messages\/\d+$/.test(chemin)) return { id: chemin.split('/').pop() };
  if (m === 'PATCH' && (x = /^\/channels\/(\d+)$/.exec(chemin))) return Object.assign(serveur.salons.find((s) => s.id === x[1]), c);
  return undefined;
};
fauxDiscord.gerer = (m, ch, c) => { appelsOrg.push(m + ' ' + ch); return route(m, ch, c); };
const organiser = async () => (await (await mf.dispatchFetch(B + '/admin', { method: 'POST', body: new URLSearchParams({ jeton: ADMIN, action: 'organiser-discord' }).toString(), headers: { 'Content-Type': 'application/x-www-form-urlencoded' } })).text());
let page1 = await organiser();
ok(/Terminé/.test(page1), 'organiser : terminé en un clic (' + appelsOrg.length + ' appels, budget 46)');
const nomS = (n) => serveur.salons.find((x) => x.name === n);
ok(nomS('📢・annonces') && nomS('📢・annonces').id === serveur.salons[2].id, 'organiser : « annonce » renommé et rangé (pas recréé)');
ok(serveur.salons.filter((x) => x.type === 0).length === 13 && serveur.salons.filter((x) => x.type === 4).length === 7, 'organiser : 13 salons texte, 5 catégories en plus des 2 anciennes (rien supprimé)');
const cat = nomS('👋 BIENVENUE'); ok(nomS('📜・règles').parent_id === cat.id && nomS('💬・général').parent_id === nomS('⚡ VELOX').id, 'organiser : salons dans les bonnes catégories');
ok(nomS('📜・règles').permission_overwrites.some((o) => o.id === GU && o.deny !== '0'), 'organiser : règles en lecture seule');
const sp = nomS('👑・salon-premium').permission_overwrites;
ok(sp.some((o) => o.id === GU && (BigInt(o.deny) & 0x400n)) && sp.some((o) => o.id === '333333333333333333' && (BigInt(o.allow) & 0x400n)), 'organiser : salon Premium visible seulement avec le rôle');
ok(serveur.systeme === nomS('👋・bienvenue').id, 'organiser : arrivées annoncées dans #bienvenue');
ok(serveur.roles.length === 6 && serveur.roles.find((r) => r.id === '333333333333333333').hoist === true, 'organiser : 4 rôles à choisir + Premium doré mis en avant');
ok(serveur.messages === 5, 'organiser : 5 messages du bot publiés');
const nb1 = serveur.salons.length, msg1 = serveur.messages; appelsOrg.length = 0;
const page2 = await organiser();
ok(serveur.salons.length === nb1 && serveur.messages === msg1 && /Terminé/.test(page2), 'organiser relancé : rien en double, messages mis à jour (' + appelsOrg.length + ' appels)');
// bouton de rôle
const roleGamer = serveur.roles.find((r) => r.name === '🎮 Gamer').id;
r = await interaction({ type: 3, guild_id: GU, member: { user: { id: U }, roles: [], permissions: '0' }, data: { custom_id: 'role:gamer' } });
j = await r.json(); ok(j.data.flags === 64 && /ajouté/.test(j.data.content) && appels.some((a) => a.startsWith('PUT /api/v10/guilds/' + GU + '/members/' + U + '/roles/' + roleGamer)), 'bouton rôle : ajouté');
r = await interaction({ type: 3, guild_id: GU, member: { user: { id: U }, roles: [roleGamer], permissions: '0' }, data: { custom_id: 'role:gamer' } });
j = await r.json(); ok(/retiré/.test(j.data.content), 'bouton rôle : reclic = retiré');
r = await interaction({ type: 3, guild_id: GU, member: { user: { id: U }, roles: [], permissions: '0' }, data: { custom_id: 'premium' } });
j = await r.json(); ok(/client_reference_id=discord_/.test(j.data.components[0].components[0].url), 'bouton S\'abonner = /premium');
r = await interaction({ type: 3, guild_id: GU, member: { user: { id: ADM }, roles: [], permissions: '8' }, data: { custom_id: 'cle-offerte' } });
j = await r.json(); ok(/inconnue/.test(j.data.content), 'bouton inventé « cle-offerte » refusé');
ok(nomS('💎・acheter-premium').topic.includes('9,99 €/mois'), 'organiser : prix 9,99 €/mois dans le salon Premium');

// /avis (le salon « avis » existe maintenant)
const salonAvis = nomS('⭐・avis').id;
const avis = (uid, note, texte, jeton = 'jeton-avis') => interaction({ type: 2, guild_id: GU, token: jeton, member: { user: { id: uid, username: 'pseudo' + uid.slice(-2), global_name: 'Testeur77' }, permissions: '0' }, data: { name: 'avis', options: [{ name: 'note', value: note }, { name: 'commentaire', value: texte }] } });
r = await avis('666666666666666666', 5, 'Super logiciel, vraiment rapide et clair.');
j = await r.json(); ok(j.data.flags === 64 && /réservés aux membres/.test(j.data.content), '/avis : refusé sans clé Premium');
r = await avis('777777777777777777', 4, 'trop court');
j = await r.json(); ok(/trop court/.test(j.data.content), '/avis : commentaire trop court refusé');
r = await avis('777777777777777777', 9, 'Une note impossible mais un texte assez long.');
j = await r.json(); ok(/de 1 à 5/.test(j.data.content), '/avis : note hors de 1 à 5 refusée');
nAppels = appels.length;
r = await avis('777777777777777777', 4, 'Le Studio son est top, voir https://pub.exemple et discord.gg/autre ; il manque un mode sombre plus contrasté.');
j = await r.json(); ok(j.type === 5 && j.data.flags === 64, '/avis : réponse immédiate « en cours »');
await new Promise((x) => setTimeout(x, 300));
const pub = appels.slice(nAppels).find((a) => a.startsWith('POST /api/v10/channels/' + salonAvis + '/messages'));
ok(!!pub && pub.includes('★★★★☆') && pub.includes('Testeur · a reçu une clé Premium offerte') && pub.includes('Testeur77'), '/avis : publié dans #avis avec la note et la mention « clé offerte »');
ok(pub && !pub.includes('pub.exemple') && !pub.includes('discord.gg') && pub.includes('[lien retiré]'), '/avis : liens retirés');
ok(pub && pub.includes('"parse":[]'), '/avis : aucune mention ne notifie personne');
const av = JSON.parse(await KV.get('avis:777777777777777777'));
ok(av.note === 4 && av.canal === salonAvis && av.origine === 'offerte', '/avis : enregistré (note, salon, origine)');
nAppels = appels.length;
await avis('777777777777777777', 5, 'Après une semaine : rien à redire, je m\'abonne.');
await new Promise((x) => setTimeout(x, 300));
ok(appels.slice(nAppels).some((a) => a.startsWith('PATCH /api/v10/channels/' + salonAvis + '/messages/')) && !appels.slice(nAppels).some((a) => a.startsWith('POST /api/v10/channels/' + salonAvis)), '/avis refait : le message est modifié, pas de doublon');
ok(appels.slice(nAppels).some((a) => a.includes('mis à jour')), '/avis refait : « mis à jour »');
ok((await adm({ recherche: '777777777777777777' })).includes('avis 5/5'), 'admin : l\'avis du membre est affiché avec sa clé');
r = await interaction({ type: 2, guild_id: '999999999999999999', member: { user: { id: '777777777777777777' }, permissions: '0' }, data: { name: 'avis', options: [{ name: 'note', value: 5 }, { name: 'commentaire', value: 'Sur un autre serveur, ça ne doit pas passer.' }] } });
j = await r.json(); ok(/serveur officiel/.test(j.data.content), '/avis : refusé sur un autre serveur');

// /score : code de joueur (même calcul que Velox, refait ici indépendamment)
const fnv = (s) => { let h = 2166136261; for (const o of Buffer.from('velox|' + s, 'utf8')) { h ^= o; h = Math.imul(h, 16777619) >>> 0; } return h.toString(16).padStart(8, '0').slice(0, 6); };
const corpsJ = '12-3450-9-22-7-15', codeJ = 'VLX1-' + corpsJ + '-' + fnv(corpsJ);
r = await interaction({ type: 2, guild_id: GU, member: { user: { id: U, username: 'flyZ20' }, permissions: '0' }, data: { name: 'score', options: [{ name: 'code', value: codeJ }] } });
j = await r.json(); ok(!j.data.flags && /Or · niveau 12/.test(j.data.embeds[0].title) && j.data.embeds[0].fields[1].value === '9 / 22', '/score : carte publique, rang Or niveau 12');
r = await interaction({ type: 2, guild_id: GU, member: { user: { id: U }, permissions: '0' }, data: { name: 'score', options: [{ name: 'code', value: 'VLX1-99-3450-9-22-7-15-' + fnv(corpsJ) }] } });
j = await r.json(); ok(j.data.flags === 64 && /invalide/.test(j.data.content), '/score : code trafiqué refusé');
r = await interaction({ type: 2, guild_id: GU, member: { user: { id: U }, permissions: '0' }, data: { name: 'essai' } });
j = await r.json(); ok(j.data.flags === 64 && /7 jours/.test(j.data.embeds[0].title) && j.data.components[0].components[0].custom_id === 'telecharger', '/essai : explique l\'essai de 7 jours, bouton de téléchargement direct');
r = await interaction({ type: 3, guild_id: GU, member: { user: { id: U }, roles: [] }, data: { custom_id: 'essai' } });
j = await r.json(); ok(/7 jours/.test(j.data.embeds[0].title), 'bouton Essai gratuit');
// /telecharger sans GitHub (réseau coupé) : message clair, pas de lien vers le site
r = await interaction({ type: 2, guild_id: GU, member: { user: { id: U }, permissions: '0' }, data: { name: 'telecharger' } });
j = await r.json(); ok(j.data.flags === 64 && /GitHub ne répond pas/.test(j.data.content) && !JSON.stringify(j).includes('github.io'), '/telecharger : GitHub injoignable, message clair');
// annonce de version (GitHub simulé)
const gh = fetchMock.get('https://api.github.com');
gh.intercept({ method: 'GET', path: '/repos/damiencoussot-ai/nettoyage-pc/releases/latest' }).reply(200, { tag_name: 'v5.9.7', html_url: 'https://github.com/damiencoussot-ai/nettoyage-pc/releases/tag/v5.9.7', body: 'Velox 5.9.7\n\n- Nouveau\n\nSHA256 de Velox-Setup-5.9.7.exe : voir', published_at: '2026-11-07T12:00:00Z', assets: [{ name: 'NettoyagePC-Setup-5.9.7.exe', size: 42813979, browser_download_url: 'https://github.com/damiencoussot-ai/nettoyage-pc/releases/download/v5.9.7/NettoyagePC-Setup-5.9.7.exe' }, { name: 'Velox-Setup-5.9.7.exe', size: 42813979, digest: 'sha256:' + 'ab'.repeat(32), browser_download_url: 'https://github.com/damiencoussot-ai/nettoyage-pc/releases/download/v5.9.7/Velox-Setup-5.9.7.exe' }] }).persist();
// /telecharger : lien direct vers l'installateur de la dernière version, sans passer par le site
r = await interaction({ type: 2, guild_id: GU, member: { user: { id: U }, permissions: '0' }, data: { name: 'telecharger' } });
j = await r.json();
const btn = j.data.components[0].components[0];
ok(j.data.flags === 64 && btn.url === 'https://github.com/damiencoussot-ai/nettoyage-pc/releases/download/v5.9.7/Velox-Setup-5.9.7.exe' && /Velox 5\.9\.7/.test(btn.label), '/telecharger : bouton = installateur Velox-Setup-5.9.7.exe en direct');
ok(/40,8 Mo/.test(j.data.embeds[0].description) && j.data.embeds[0].description.includes('ab'.repeat(32)) && !JSON.stringify(j).includes('github.io'), '/telecharger : taille, empreinte SHA256, aucun lien vers le site');
r = await interaction({ type: 3, guild_id: GU, member: { user: { id: U }, roles: [] }, data: { custom_id: 'telecharger' } });
j = await r.json(); ok(j.data.components[0].components[0].url.endsWith('/Velox-Setup-5.9.7.exe'), 'bouton « Télécharger Velox » des messages du bot = même lien direct');
const KVt = await mf.getKVNamespace('LICENCES');
ok(JSON.parse(await KVt.get('telechargement')).version === '5.9.7', '/telecharger : version gardée en cache (10 min)');
enreg('POST', /\/api\/v10\/channels\/\d+\/messages$/, { id: 'annonce1' });
const annoncer = async () => (await (await mf.dispatchFetch(B + '/admin', { method: 'POST', body: new URLSearchParams({ jeton: ADMIN, action: 'annoncer-version' }).toString(), headers: { 'Content-Type': 'application/x-www-form-urlencoded' } })).text());
const nAv = appels.length;
ok(/5\.9\.7 annoncée/.test(await annoncer()), 'annonce : version 5.9.7 postée dans #mises-à-jour');
const posted = appels.slice(nAv).find((a) => a.startsWith('POST /api/v10/channels/' + serveur.salons.find((x) => x.name === '🆕・mises-à-jour').id));
ok(!!posted, 'annonce : dans le bon salon');
await mf.dispatchFetch(B + '/'); // le Cron ne réannonce pas la même version :
const sched = await mf.getWorker(); const nAv2 = appels.length;
try { await sched.scheduled({ cron: '0 * * * *' }); } catch (e) { console.log('scheduled', e.message); }
await new Promise((x) => setTimeout(x, 300));
ok(appels.length === nAv2, 'Cron : pas de deuxième annonce pour la même version');
await mf.dispose(); srv.close();
console.log(echecs === 0 ? 'TOUT OK (' + n + ')' : echecs + ' échec(s) sur ' + n);
