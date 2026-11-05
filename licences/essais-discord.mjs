// Essais de la partie Discord du service de licences (Miniflare, appels à Discord simulés) : node licences/essais-discord.mjs licences/worker.js
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
const SECRET = 'whsec_essai', ADMIN = 'motdepasse-admin-essai';
const mf = new Miniflare({ modules: true, scriptPath: W, kvNamespaces: ['LICENCES'], compatibilityDate: '2025-06-01', fetchMock,
  bindings: { STRIPE_WEBHOOK_SECRET: SECRET, ADMIN_TOKEN: ADMIN, DISCORD_PUBLIC_KEY: pk, DISCORD_APP_ID: '111111111111111111', DISCORD_BOT_TOKEN: 'jeton-bot', LIEN_PAIEMENT: 'https://buy.stripe.com/test_abc', DISCORD_GUILD_ID: '222222222222222222', DISCORD_ROLE_PREMIUM: '333333333333333333' } });
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
ok(msg && /[A-F0-9]{8}-[A-F0-9]{4}-/.test(msg), 'clé envoyée en message privé');
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: U }, permissions: '0' }, data: { name: 'ma-cle' } });
j = await r.json(); ok(j.data.flags === 64 && j.data.embeds.length === 1 && j.data.embeds[0].description.includes('-'), '/ma-cle : la clé, en privé');
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: '666666666666666666' }, permissions: '0' }, data: { name: 'ma-cle' } });
j = await r.json(); ok(!j.data.embeds, '/ma-cle : rien pour un autre membre');
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: U }, permissions: '0' }, data: { name: 'cle-offerte', options: [{ name: 'membre', value: '777777777777777777' }] } });
j = await r.json(); ok(/réservée/.test(j.data.content), '/cle-offerte refusée sans droits admin');
r = await interaction({ type: 2, guild_id: '222222222222222222', member: { user: { id: ADM }, permissions: '8' }, data: { name: 'cle-offerte', options: [{ name: 'membre', value: '777777777777777777' }, { name: 'raison', value: 'concours' }] } });
j = await r.json(); ok(/Clé offerte/.test(j.data.content) && /envoyée en message privé/.test(j.data.content), '/cle-offerte par un admin');
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
await mf.dispose();
console.log(echecs === 0 ? 'TOUT OK (' + n + ')' : echecs + ' échec(s) sur ' + n);
