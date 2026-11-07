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
const enreg = (m, p, rep, statut = 200) => d.intercept({ method: m, path: p }).reply(statut, (o) => { appels.push(m + ' ' + o.path + ' ' + String(o.body || '')); return rep; }).persist();
enreg('POST', '/api/v10/users/@me/channels', { id: '999000000000000001' });
enreg('POST', '/api/v10/channels/999000000000000001/messages', { id: 'm1' });
enreg('PUT', /\/api\/v10\/guilds\/.*\/roles\/.*/, '', 204);
enreg('DELETE', /\/api\/v10\/guilds\/.*\/roles\/.*/, '', 204);
enreg('PUT', '/api/v10/applications/111111111111111111/commands', []);
enreg('PATCH', /\/api\/v10\/webhooks\/111111111111111111\/.*\/messages\/@original/, {});
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
  bindings: { STRIPE_WEBHOOK_SECRET: SECRET, ADMIN_TOKEN: ADMIN, DISCORD_PUBLIC_KEY: pk, DISCORD_APP_ID: '111111111111111111', DISCORD_BOT_TOKEN: 'jeton-bot', LIEN_PAIEMENT: 'https://buy.stripe.com/test_abc', DISCORD_GUILD_ID: '222222222222222222', DISCORD_ROLE_PREMIUM: '333333333333333333', DISCORD_API_ESSAI: 'http://127.0.0.1:' + PORT + '/api/v10' } });
const B = 'https://licences.exemple';
function interaction(obj, sigOk = true) {
  const corps = JSON.stringify(obj), ts = String(Math.floor(Date.now() / 1000));
  let sig = crypto.sign(null, Buffer.from(ts + corps), privateKey).toString('hex');
  if (!sigOk) sig = sig.replace(/^./, sig[0] === 'a' ? 'b' : 'a');
  return mf.dispatchFetch(B + '/discord', { method: 'POST', body: corps, headers: { 'X-Signature-Ed25519': sig, 'X-Signature-Timestamp': ts, 'Content-Type': 'application/json' } });
}
const U = '444444444444444444', ADM = '555555555555555555';
let r = await interaction({ type: 1 }); ok(r.status === 200 && (await r.json()).type === 1, 'PING Discord');
r = await interaction({ type: 1 }, false); ok(r.status === 401, 'signature fausse refusée');
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: U }, permissions: '0' }, data: { name: 'premium' } });
let j = await r.json(); const url = j.data.components[0].components[0].url;
ok(j.data.flags === 64 && /client_reference_id=discord_444444444444444444_[0-9a-f]{16}$/.test(url), '/premium : lien de paiement privé avec référence Discord');
const ref = new URL(url).searchParams.get('client_reference_id');
// paiement Stripe
const session = { id: 'cs_live_Discord1234567890', payment_status: 'paid', livemode: true, payment_intent: 'pi_discord', client_reference_id: ref, customer_details: { email: 'a@b.fr', name: 'Joueur' }, amount_total: 999, currency: 'eur' };
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
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: ADM }, permissions: '8' }, token: 'jeton-essai', data: { name: 'cle-offerte', options: [{ name: 'membre', value: '777777777777777777' }, { name: 'raison', value: 'concours' }] } });
j = await r.json(); ok(j.type === 5 && j.data.flags === 64, '/cle-offerte : réponse immédiate « en cours » (moins de 3 s)');
await new Promise((x) => setTimeout(x, 300));
ok(appels.some((a) => a.startsWith('PATCH /api/v10/webhooks/111111111111111111/jeton-essai/messages/@original')), '/cle-offerte : résultat affiché ensuite');
// la clé offerte est valide pour l'application
r = await interaction({ type: 2, member: { user: { id: '777777777777777777' }, permissions: '0' }, guild_id: '222222222222222222', data: { name: 'ma-cle' } });
j = await r.json(); const cleOfferte = /```([A-F0-9-]{36})```/.exec(j.data.embeds[0].description)[1];
r = await mf.dispatchFetch(B + '/v1/licenses/activate', { method: 'POST', body: new URLSearchParams({ license_key: cleOfferte, instance_name: 'PC-essai' }).toString(), headers: { 'Content-Type': 'application/x-www-form-urlencoded' } });
j = await r.json(); ok(j.activated === true || j.valid === true || j.license_key, 'clé offerte activable dans Velox : ' + JSON.stringify(j).slice(0, 80));
// remboursement : rôle retiré
const ch = { id: 'ch_1', payment_intent: 'pi_discord', refunded: true, amount: 999, amount_refunded: 999 };
const c2 = JSON.stringify({ id: 'evt_2', type: 'charge.refunded', livemode: true, data: { object: ch } }), sig2 = crypto.createHmac('sha256', SECRET).update(t + '.' + c2).digest('hex');
await mf.dispatchFetch(B + '/stripe', { method: 'POST', body: c2, headers: { 'Stripe-Signature': 't=' + t + ',v1=' + sig2 } });
ok(appels.some((a) => a.startsWith('DELETE /api/v10/guilds/')), 'remboursement : rôle Premium retiré');
r = await mf.dispatchFetch(B + '/admin', { method: 'POST', body: new URLSearchParams({ jeton: ADMIN, action: 'commandes-discord' }).toString(), headers: { 'Content-Type': 'application/x-www-form-urlencoded' } });
const h = await r.text(); ok(/4 commandes installées/.test(h), 'installation des commandes depuis /admin');
console.log(appels.map(a=>a.slice(0,140)).join('\n'));
r = await interaction({ type: 2, guild_id: '888888888888888888', member: { user: { id: ADM }, permissions: '8' }, data: { name: 'cle-offerte', options: [{ name: 'membre', value: '777777777777777777' }] } });
j = await r.json(); ok(/serveur officiel/.test(j.data.content), '/cle-offerte refusée sur un autre serveur');

// abonnement : création, résiliation, réactivation
const sess2 = { id: 'cs_live_Abonnement123456789', mode: 'subscription', subscription: 'sub_ABC', payment_status: 'paid', livemode: true, payment_intent: null, client_reference_id: 'discord_' + U + '_0123456789abcdef', customer_details: { email: 'abo@b.fr', name: 'Abonné' }, amount_total: 499, currency: 'eur' };
const signe = (o) => { const c = JSON.stringify(o), tt = Math.floor(Date.now() / 1000); return [c, 't=' + tt + ',v1=' + crypto.createHmac('sha256', SECRET).update(tt + '.' + c).digest('hex')]; };
let [cA, sA] = signe({ id: 'evt_a', type: 'checkout.session.completed', livemode: true, data: { object: sess2 } });
await mf.dispatchFetch(B + '/stripe', { method: 'POST', body: cA, headers: { 'Stripe-Signature': sA } });
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: U }, permissions: '0' }, data: { name: 'ma-cle' } });
j = await r.json(); const cleAbo = /```([A-F0-9-]{36})```/.exec(j.data.embeds[j.data.embeds.length - 1].description)[1];
const valider = async (c) => (await (await mf.dispatchFetch(B + '/v1/licenses/validate', { method: 'POST', body: new URLSearchParams({ license_key: c }).toString(), headers: { 'Content-Type': 'application/x-www-form-urlencoded' } })).json());
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
ok(serveur.salons.filter((x) => x.type === 0).length === 12 && serveur.salons.filter((x) => x.type === 4).length === 7, 'organiser : 12 salons texte, 5 catégories en plus des 2 anciennes (rien supprimé)');
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
await mf.dispose(); srv.close();
console.log(echecs === 0 ? 'TOUT OK (' + n + ')' : echecs + ' échec(s) sur ' + n);
