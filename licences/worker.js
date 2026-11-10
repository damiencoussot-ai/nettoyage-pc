// Velox - service de licences (Cloudflare Worker, gratuit).
//
// Rôle : quand Stripe confirme un paiement de « Velox Premium », créer une clé unique, la montrer
// au client juste après le paiement, la donner à l'application qui a lancé l'achat, et répondre aux
// vérifications de l'application (même format que l'API des licences de Lemon Squeezy : activate,
// validate, deactivate). Un remboursement ou une contestation désactive la clé.
//
// Branchements à créer dans Cloudflare (Workers > ce Worker > Settings) :
//   KV namespace  LICENCES                 (stockage des clés)
//   Secret        STRIPE_WEBHOOK_SECRET    (« whsec_… » du webhook Stripe en mode réel)
//   Secret        STRIPE_WEBHOOK_SECRET_TEST (facultatif : « whsec_… » du webhook en mode test)
//   Secret        ADMIN_TOKEN              (mot de passe de la page /admin, choisi par l'éditeur)
//   Variable      ACCEPTER_TEST = oui      (facultatif, pendant les essais : accepte les clés du mode test)
//   Variable      LIENS_PAIEMENT           (OBLIGATOIRE : identifiants plink_… des liens de paiement Velox, séparés par des virgules ; vide = aucun paiement accepté)
//
// Adresses :
//   POST /stripe                    webhook Stripe (événements checkout.session.*, customer.subscription.updated|deleted,
//                                   charge.refunded, charge.dispute.created)
//   GET  /merci?session_id=cs_…     page affichée après le paiement (lien « après paiement » du lien Stripe)
//   GET  /v1/achats/<référence>     l'application récupère la clé de l'achat qu'elle a lancé
//   POST /v1/licenses/activate|validate|deactivate   vérifications de l'application
//   GET/POST /admin                 page de l'éditeur : chercher une clé par e-mail, désactiver, libérer les PC
//   POST /discord                   commandes Discord (/premium, /ma-cle, /telecharger, /cle-offerte)
//
// Discord (facultatif) : vendre Premium sur le serveur Discord et y donner les clés.
//   Variable      DISCORD_APP_ID           (« Application ID » de l'application Discord)
//   Variable      DISCORD_PUBLIC_KEY       (« Public Key » de l'application Discord)
//   Secret        DISCORD_BOT_TOKEN        (jeton du bot : messages privés et rôle Premium)
//   Variable      LIEN_PAIEMENT            (lien de paiement Stripe « https://buy.stripe.com/… », abonnement 4,99 €/mois)
//   Variable      LIEN_GESTION             (facultatif : portail client Stripe « https://billing.stripe.com/p/login/… » pour résilier)
//   Variable      DISCORD_GUILD_ID         (facultatif : identifiant du serveur, pour donner le rôle)
//   Variable      DISCORD_ROLE_PREMIUM     (facultatif : identifiant du rôle « Premium »)
//   Dans le portail Discord : « Interactions Endpoint URL » = https://<ce worker>/discord
//   Puis, une fois : page /admin, bouton « Installer les commandes Discord ».

const LIMITE_PC = 3;
const PRODUIT = 'Velox Premium';
const PRIX = '4,99 € par mois';
const CONTACT = 'nettoyagepcai@gmail.com';
const SITE = 'https://damiencoussot-ai.github.io/nettoyage-pc/';
const ENTETES_PAGE = {
  'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store', 'Referrer-Policy': 'no-referrer',
  'X-Robots-Tag': 'noindex', 'X-Content-Type-Options': 'nosniff', 'X-Frame-Options': 'DENY',
  'Content-Security-Policy': "default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src data:; form-action 'self'; base-uri 'none'",
};

export default {
  async fetch(requete, env, ctx) {
    const url = new URL(requete.url);
    const chemin = url.pathname.replace(/\/+$/, '') || '/';
    try {
      if (chemin === '/stripe' && requete.method === 'POST') return await webhook(requete, env);
      if (chemin === '/merci' && requete.method === 'GET') return await merci(url, env);
      if (chemin.startsWith('/v1/achats/') && requete.method === 'GET') return await achat(decodeURIComponent(chemin.slice(11)), env);
      if (chemin.startsWith('/v1/licenses/') && requete.method === 'POST') return await licences(chemin.slice(13), requete, env);
      if (chemin === '/admin') return await admin(requete, env);
      if (chemin === '/discord' && requete.method === 'POST') return await discord(requete, env, ctx);
      if (chemin === '/') return new Response('Service de licences de Velox.\n', { headers: { 'Content-Type': 'text/plain; charset=utf-8' } });
      return json({ error: 'not found' }, 404);
    } catch (e) {
      console.log('erreur', e && e.stack || e);
      return json({ error: 'internal error' }, 500);
    }
  },
  // déclencheur Cron de Cloudflare (toutes les heures) : annonce une nouvelle version de Velox sur Discord
  async scheduled(evenement, env, ctx) {
    ctx.waitUntil(annoncerVersion(env, false).then((t) => console.log('annonce', t)).catch((e) => console.log('annonce', e)));
  },
};

// ------------------------------------------------------------ outils
const enc = (t) => new TextEncoder().encode(t);
const hex = (buf) => [...new Uint8Array(buf)].map((b) => b.toString(16).padStart(2, '0')).join('');
function egal(a, b) {
  a = String(a); b = String(b);
  if (a.length !== b.length) return false;
  let d = 0;
  for (let i = 0; i < a.length; i++) d |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return d === 0;
}
function json(objet, statut = 200) {
  return new Response(JSON.stringify(objet), { status: statut, headers: { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' } });
}
const echapper = (t) => String(t == null ? '' : t).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
async function lire(env, cle) { const v = await env.LICENCES.get(cle); return v ? JSON.parse(v) : null; }
async function ecrire(env, lic) { await env.LICENCES.put('lic:' + lic.cle, JSON.stringify(lic)); }
const formatCle = (c) => /^[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}$/.test(c || '');
const formatRef = (r) => /^[A-Za-z0-9_-]{24,200}$/.test(r || '');

// ------------------------------------------------------------ webhook Stripe
async function signatureValide(corps, entete, secret) {
  if (!secret || !entete) return false;
  let t = ''; const v1 = [];
  for (const morceau of entete.split(',')) {
    const i = morceau.indexOf('=');
    if (i < 0) continue;
    const k = morceau.slice(0, i).trim(), v = morceau.slice(i + 1).trim();
    if (k === 't') t = v; else if (k === 'v1') v1.push(v);
  }
  if (!/^\d+$/.test(t) || v1.length === 0) return false;
  if (Math.abs(Date.now() / 1000 - Number(t)) > 300) return false;   // signature de plus de 5 minutes : rejouée ?
  const k = await crypto.subtle.importKey('raw', enc(secret), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
  const attendu = hex(await crypto.subtle.sign('HMAC', k, enc(t + '.' + corps)));
  return v1.some((s) => egal(s, attendu));
}

async function webhook(requete, env) {
  const corps = await requete.text();
  const entete = requete.headers.get('Stripe-Signature') || '';
  const reel = await signatureValide(corps, entete, env.STRIPE_WEBHOOK_SECRET);
  const test = !reel && await signatureValide(corps, entete, env.STRIPE_WEBHOOK_SECRET_TEST);
  if (!reel && !test) return json({ error: 'signature invalide' }, 400);
  const ev = JSON.parse(corps);
  const o = ev.data && ev.data.object || {};
  if (reel && ev.livemode === false) return json({ ignore: 'événement de test sur le webhook réel' });
  switch (ev.type) {
    case 'checkout.session.completed':
    case 'checkout.session.async_payment_succeeded':
      if (o.payment_status === 'paid' && lienAccepte(o, env)) await creerLicence(env, o);
      break;
    case 'customer.subscription.updated':
    case 'customer.subscription.deleted':
      await suivreAbonnement(env, o, ev.type);
      break;
    case 'charge.refunded':
      if (o.refunded === true || (o.amount_refunded >= o.amount && o.amount > 0)) await desactiverParPaiement(env, o.payment_intent, 'remboursée');
      break;
    case 'charge.dispute.created':
      await desactiverParPaiement(env, o.payment_intent, 'contestée');
      break;
  }
  return json({ recu: true });
}

function lienAccepte(session, env) {
  const liste = String(env.LIENS_PAIEMENT || '').split(',').map((s) => s.trim()).filter(Boolean);
  // aucun lien configuré : aucun paiement n'est accepté (sinon un paiement pour un autre produit du même compte Stripe
  // créerait une licence Velox)
  if (liste.length === 0) { console.log('Paiement ignoré : la variable LIENS_PAIEMENT est vide.'); return false; }
  return liste.includes(session.payment_link || '');
}

async function creerLicence(env, s) {
  const deja = await env.LICENCES.get('ses:' + s.id);
  if (deja) return deja;
  const cle = crypto.randomUUID().toUpperCase();
  const client = s.customer_details || {};
  const lic = {
    cle, statut: 'active', raison: '', email: String(client.email || '').toLowerCase(), nom: client.name || '',
    session: s.id, paiement: s.payment_intent || '', ref: formatRef(s.client_reference_id) ? s.client_reference_id : '',
    montant: s.amount_total, devise: s.currency || '', test: s.livemode === false, creee: new Date().toISOString(), activations: [],
    abonnement: s.mode === 'subscription' && s.subscription ? String(s.subscription) : '',
  };
  await ecrire(env, lic);
  await env.LICENCES.put('ses:' + s.id, cle);
  if (lic.abonnement) await env.LICENCES.put('sub:' + lic.abonnement, cle);
  if (lic.paiement) await env.LICENCES.put('pi:' + lic.paiement, cle);
  if (lic.ref) await env.LICENCES.put('ref:' + lic.ref, cle, { expirationTtl: 90 * 86400 });
  if (lic.email) await env.LICENCES.put('mail:' + lic.email + ':' + cle, '1');
  // achat lancé depuis Discord (/premium) : la clé arrive aussi en message privé, avec le rôle Premium
  const idDiscord = /^discord_(\d{15,22})_/.exec(lic.ref || '');
  if (idDiscord && (!lic.test || env.ACCEPTER_TEST === 'oui')) await donnerSurDiscord(env, idDiscord[1], lic, 'achat');
  return cle;
}

/** Abonnement : la clé reste active tant qu'il est payé ; résilié ou impayé, elle se désactive (et le rôle Discord est retiré).
 *  « past_due » (paiement en retard, Stripe réessaie) garde la clé active quelques jours. */
async function suivreAbonnement(env, sub, type) {
  const cle = sub && sub.id ? await env.LICENCES.get('sub:' + sub.id) : null;
  if (!cle) return;
  const lic = await lire(env, 'lic:' + cle);
  if (!lic) return;
  const fini = type === 'customer.subscription.deleted' || ['canceled', 'unpaid', 'incomplete_expired'].includes(sub.status);
  const actif = !fini && ['active', 'trialing', 'past_due'].includes(sub.status);
  if (fini && lic.statut === 'active') {
    lic.statut = 'disabled'; lic.raison = 'abonnement terminé'; lic.modifiee = new Date().toISOString();
    await ecrire(env, lic);
    if (lic.discord) await roleDiscord(env, lic.discord, false);
  } else if (actif && lic.statut === 'disabled' && lic.raison === 'abonnement terminé') {
    lic.statut = 'active'; lic.raison = ''; lic.modifiee = new Date().toISOString();
    await ecrire(env, lic);
    if (lic.discord) await roleDiscord(env, lic.discord, true);
  }
}

async function desactiverParPaiement(env, paiement, raison) {
  if (!paiement) return;
  const cle = await env.LICENCES.get('pi:' + paiement);
  if (!cle) return;
  const lic = await lire(env, 'lic:' + cle);
  if (!lic) return;
  lic.statut = 'disabled'; lic.raison = raison; lic.modifiee = new Date().toISOString();
  await ecrire(env, lic);
  if (lic.discord) await roleDiscord(env, lic.discord, false);   // remboursement : le rôle Premium est retiré
}

// ------------------------------------------------------------ après le paiement
async function merci(url, env) {
  const session = url.searchParams.get('session_id') || '';
  const essai = Math.min(60, Math.max(0, parseInt(url.searchParams.get('essai') || '0', 10) || 0));
  if (!/^cs_(test|live)_[A-Za-z0-9]{10,200}$/.test(session)) return page('Lien incomplet', '<p>Ce lien ne contient pas de numéro de commande. Si vous venez de payer, ouvrez Velox : onglet Premium, ou écrivez à ' + CONTACT + '.</p>', 400);
  const cle = await env.LICENCES.get('ses:' + session);
  const lic = cle ? await lire(env, 'lic:' + cle) : null;
  if (!lic) {
    if (essai >= 45) return page('Paiement reçu', '<p>Votre paiement est enregistré, mais la clé n\'est pas encore prête. Rechargez cette page dans quelques minutes.</p><p>Si vous avez acheté depuis Velox, Premium s\'activera tout seul dans l\'application. Sinon, écrivez à <b>' + CONTACT + '</b> avec l\'adresse e-mail utilisée pour l\'achat : nous vous enverrons votre clé.</p>');
    const suite = '/merci?session_id=' + encodeURIComponent(session) + '&essai=' + (essai + 1);
    return page('Préparation de votre clé…', '<p class="attente">Paiement reçu. Votre clé est en cours de création, cela prend quelques secondes.</p><p class="petit">La page se met à jour toute seule.</p>', 200, '<meta http-equiv="refresh" content="3;url=' + echapper(suite) + '">');
  }
  if (lic.statut !== 'active') return page('Clé désactivée', '<p>Cette commande a été remboursée ou contestée : la clé n\'est plus active. Pour toute question : ' + CONTACT + '.</p>');
  const corps = '<p>Merci pour votre achat ! Voici votre clé ' + PRODUIT + ' :</p>' +
    '<div class="cle" id="cle">' + echapper(lic.cle) + '</div>' +
    '<p><button type="button" id="copier">Copier la clé</button> <span id="copiee" class="petit"></span></p>' +
    '<ul><li><b>Si vous avez acheté depuis Velox</b>, Premium s\'active tout seul : revenez simplement dans l\'application.</li>' +
    '<li><b>Sinon</b>, ouvrez Velox, onglet <b>Premium</b>, bouton <b>« J\'ai une clé »</b>, et collez la clé.</li>' +
    '<li>La clé fonctionne sur ' + LIMITE_PC + ' PC à la fois. <b>Gardez-la</b> (notez-la ou faites une capture d\'écran) pour vos autres PC.</li></ul>' +
    '<p class="petit">Le reçu et la facture vous sont envoyés par e-mail par Link, le service de paiement de Stripe. Satisfait ou remboursé 14 jours : écrivez à ' + CONTACT + '. ' +
    (lic.test ? '<b>Clé de test : elle ne fonctionne qu\'avec la boutique en mode test.</b>' : '') + '</p>' +
    '<script>document.getElementById("copier").onclick=function(){var t=document.getElementById("cle").textContent,m=document.getElementById("copiee");' +
    'function ok(){m.textContent="Copiée."}function non(){var r=document.createRange();r.selectNodeContents(document.getElementById("cle"));var s=getSelection();s.removeAllRanges();s.addRange(r);m.textContent="Sélectionnée : faites Ctrl+C."}' +
    'if(navigator.clipboard){navigator.clipboard.writeText(t).then(ok,non)}else non()};</script>';
  return page('Votre clé Premium', corps);
}

function page(titre, corps, statut = 200, entete = '') {
  const html = '<!doctype html><html lang="fr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">' + entete +
    '<title>' + echapper(titre) + ' · Velox</title><style>' +
    ':root{--fond:#060A12;--carte:#0A1220;--bord:#18323F;--texte:#DDEBF5;--discret:#8AA0B4;--accent:#3DF5FF;color-scheme:dark}' +
    '@media (prefers-color-scheme:light){:root{--fond:#EEF3F7;--carte:#fff;--bord:#CBD9E3;--texte:#0E1C26;--discret:#4C6170;--accent:#007A9E;color-scheme:light}}' +
    'body{margin:0;background:var(--fond);color:var(--texte);font:16px/1.6 "Segoe UI",system-ui,sans-serif}' +
    'main{max-width:640px;margin:0 auto;padding:40px max(16px,env(safe-area-inset-left,0px)) 56px}' +
    '.marque{font:600 1.1rem "Bahnschrift","Segoe UI",sans-serif;color:var(--texte);text-decoration:none}' +
    'h1{font:600 1.8rem/1.2 "Bahnschrift","Segoe UI",sans-serif;margin:24px 0 16px}' +
    '.cle{font:600 1.25rem "Cascadia Mono",Consolas,monospace;letter-spacing:.04em;background:var(--carte);border:1px solid var(--accent);padding:16px;margin:12px 0;overflow-wrap:anywhere;user-select:all}' +
    'button{font:600 1rem "Segoe UI",sans-serif;padding:10px 18px;border:0;border-radius:3px;background:var(--accent);color:#04121A;cursor:pointer}' +
    '.petit{color:var(--discret);font-size:.9rem}ul{padding-left:20px}li{margin:6px 0}.attente::before{content:"◌ ";color:var(--accent)}a{color:var(--accent)}' +
    '</style></head><body><main><a class="marque" href="' + SITE + '">Velox</a><h1>' + echapper(titre) + '</h1>' + corps + '</main></body></html>';
  return new Response(html, { status: statut, headers: ENTETES_PAGE });
}

// ------------------------------------------------------------ l'application récupère sa clé
async function achat(ref, env) {
  if (!formatRef(ref)) return json({ status: 'invalid' }, 400);
  const cle = await env.LICENCES.get('ref:' + ref);
  if (!cle) return json({ status: 'pending' });
  const lic = await lire(env, 'lic:' + cle);
  if (!lic || lic.statut !== 'active') return json({ status: 'disabled' });
  return json({ status: 'paid', license_key: lic.cle, test: lic.test });
}

// ------------------------------------------------------------ vérifications (format de l'API Lemon Squeezy)
function meta(lic, env) {
  return { store_id: Number(env.STORE_ID || 1), product_id: Number(env.PRODUCT_ID || 1), product_name: PRODUIT, order_id: lic.session, customer_name: lic.nom, customer_email: lic.email };
}
function infoCle(lic) {
  return { status: lic.statut === 'active' ? 'active' : 'disabled', key: lic.cle, activation_limit: LIMITE_PC, activation_usage: lic.activations.length, created_at: lic.creee, expires_at: null };
}

async function licences(action, requete, env) {
  let champs;
  try { champs = new URLSearchParams(await requete.text()); } catch { return json({ error: 'requête illisible' }, 400); }
  const cle = String(champs.get('license_key') || '').trim().toUpperCase();
  const absente = { activate: 'activated', validate: 'valid', deactivate: 'deactivated' }[action];
  if (!absente) return json({ error: 'not found' }, 404);
  const lic = formatCle(cle) ? await lire(env, 'lic:' + cle) : null;
  if (!lic || (lic.test && env.ACCEPTER_TEST !== 'oui')) return json({ [absente]: false, error: 'license_key not found.' }, 404);
  const m = meta(lic, env);

  if (action === 'activate') {
    const nom = String(champs.get('instance_name') || '').slice(0, 64);
    if (!nom) return json({ activated: false, error: 'instance_name is required.' }, 400);
    if (lic.statut !== 'active') return json({ activated: false, error: 'This license key is disabled.', license_key: infoCle(lic), meta: m }, 400);
    let inst = lic.activations.find((a) => a.name === nom);    // même PC : on réutilise son activation
    if (!inst) {
      if (lic.activations.length >= LIMITE_PC) return json({ activated: false, error: 'This license key has reached the activation limit.', license_key: infoCle(lic), meta: m }, 400);
      inst = { id: crypto.randomUUID(), name: nom, created_at: new Date().toISOString() };
      lic.activations.push(inst);
      await ecrire(env, lic);
    }
    return json({ activated: true, error: null, license_key: infoCle(lic), instance: inst, meta: m });
  }

  const id = String(champs.get('instance_id') || '');
  const inst = lic.activations.find((a) => a.id === id) || null;
  if (action === 'validate') {
    const valide = lic.statut === 'active' && (id === '' || inst !== null);
    return json({ valid: valide, error: valide ? null : (lic.statut !== 'active' ? 'This license key is disabled.' : 'instance not found.'), license_key: infoCle(lic), instance: inst, meta: m });
  }
  // deactivate
  if (!inst) return json({ deactivated: false, error: 'instance not found.', license_key: infoCle(lic), meta: m }, 404);
  lic.activations = lic.activations.filter((a) => a.id !== id);
  await ecrire(env, lic);
  return json({ deactivated: true, error: null, license_key: infoCle(lic), meta: m });
}

// ------------------------------------------------------------ page de l'éditeur
// ------------------------------------------------------------ téléchargements (compteurs publics de GitHub)
// Chaque installateur publié a un compteur. Les mises à jour automatiques de Velox comptent aussi (un PC qui se met à jour = 1).
async function statsTelechargements() {
  try {
    const r = await fetch('https://api.github.com/repos/' + DEPOT + '/releases?per_page=100', { headers: { 'User-Agent': 'velox-licences', Accept: 'application/vnd.github+json' } });
    if (!r.ok) return '';
    const versions = (await r.json()).map((v) => ({ tag: v.tag_name, date: (v.published_at || '').slice(0, 10), n: (v.assets || []).filter((a) => /\.exe$/i.test(a.name)).reduce((s, a) => s + (a.download_count || 0), 0) }));
    const total = versions.reduce((s, v) => s + v.n, 0);
    const lignes = versions.slice(0, 8).map((v) => '<tr><td>' + echapper(v.tag) + '</td><td>' + echapper(v.date) + '</td><td style="text-align:right"><b>' + v.n + '</b></td></tr>').join('');
    return '<h2 style="margin-top:8px">Téléchargements</h2><p><b style="font-size:28px">' + total + '</b> au total (' + versions.length + ' versions)</p>' +
      '<table style="border-collapse:collapse;min-width:320px"><tr><th style="text-align:left">Version</th><th style="text-align:left">Publiée</th><th style="text-align:right">Téléchargements</th></tr>' + lignes + '</table>' +
      '<p class="petit">Compteurs de GitHub : installateur téléchargé depuis le site, winget ou la mise à jour automatique de Velox (tes propres installations comptent aussi).</p>';
  } catch (e) { return ''; }
}

async function admin(requete, env) {
  if (!env.ADMIN_TOKEN || env.ADMIN_TOKEN.length < 12) return page('Administration', '<p>La page d\'administration n\'est pas activée (secret ADMIN_TOKEN absent ou trop court).</p>', 403);
  let jeton = '', recherche = '', action = '', cible = '';
  if (requete.method === 'POST') {
    const f = new URLSearchParams(await requete.text());
    jeton = f.get('jeton') || ''; recherche = (f.get('recherche') || '').trim().toLowerCase(); action = f.get('action') || ''; cible = (f.get('cle') || '').toUpperCase();
  }
  const formJeton = '<form method="post"><p><label>Mot de passe de la page<br><input type="password" name="jeton" autocomplete="current-password" style="font-size:1rem;padding:8px;width:100%"></label></p><p><button>Entrer</button></p></form>';
  if (!jeton) return page('Administration', formJeton);
  if (!egal(jeton, env.ADMIN_TOKEN)) return page('Administration', '<p>Mot de passe incorrect.</p>' + formJeton, 403);

  let message = '';
  if (action && formatCle(cible)) {
    const lic = await lire(env, 'lic:' + cible);
    if (lic) {
      if (action === 'desactiver') { lic.statut = 'disabled'; lic.raison = 'désactivée par l\'éditeur'; }
      if (action === 'reactiver') { lic.statut = 'active'; lic.raison = ''; }
      if (action === 'liberer') lic.activations = [];
      lic.modifiee = new Date().toISOString();
      await ecrire(env, lic);
      message = '<p><b>Fait :</b> ' + echapper(action) + ' pour ' + echapper(cible) + '.</p>';
      if (!recherche) recherche = cible.toLowerCase();
    }
  }
  const champ = (n, v) => '<input type="hidden" name="' + n + '" value="' + echapper(v) + '">';
  let resultats = '';
  if (recherche) {
    const cles = [];
    if (formatCle(recherche.toUpperCase())) cles.push(recherche.toUpperCase());
    else {
      const liste = await env.LICENCES.list({ prefix: 'mail:' + recherche + ':', limit: 100 });
      for (const k of liste.keys) cles.push(k.name.split(':').pop());
    }
    for (const c of cles) {
      const lic = await lire(env, 'lic:' + c);
      if (!lic) continue;
      const bouton = (a, t) => '<form method="post" style="display:inline">' + champ('jeton', jeton) + champ('recherche', recherche) + champ('action', a) + champ('cle', lic.cle) + '<button style="margin:4px 6px 0 0">' + t + '</button></form>';
      resultats += '<div style="border:1px solid var(--bord);background:var(--carte);padding:12px;margin:10px 0"><div class="cle" style="font-size:1rem;margin:0 0 8px">' + echapper(lic.cle) + '</div>' +
        '<div class="petit">' + echapper(lic.nom) + ' · ' + echapper(lic.email) + ' · ' + echapper(lic.creee.slice(0, 10)) + (lic.test ? ' · TEST' : '') + '<br>État : ' + echapper(lic.statut) + (lic.raison ? ' (' + echapper(lic.raison) + ')' : '') +
        ' · PC : ' + lic.activations.length + '/' + LIMITE_PC + (lic.activations.length ? ' (' + lic.activations.map((a) => echapper(a.name)).join(', ') + ')' : '') + '</div>' +
        (lic.statut === 'active' ? bouton('desactiver', 'Désactiver') : bouton('reactiver', 'Réactiver')) + bouton('liberer', 'Libérer les PC') + '</div>';
    }
    if (!resultats) resultats = '<p>Aucune clé trouvée pour « ' + echapper(recherche) + ' ».</p>';
  }
  const formRecherche = '<form method="post">' + champ('jeton', jeton) + '<p><label>E-mail de l\'acheteur ou clé<br><input name="recherche" value="' + echapper(recherche) + '" style="font-size:1rem;padding:8px;width:100%"></label></p><p><button>Chercher</button></p></form>';
  // Discord : installer (ou mettre à jour) les commandes du bot
  if (action === 'commandes-discord') message = '<p><b>Discord :</b> ' + echapper(await installerCommandes(env)) + '</p>';
  if (action === 'annoncer-version') message = '<p><b>Annonce :</b> ' + echapper(await annoncerVersion(env, true)) + '</p>';
  if (action === 'organiser-discord') message = '<p><b>Serveur Discord :</b></p><pre style="white-space:pre-wrap">' + echapper(await organiserServeur(env)) + '</pre>';
  const formDiscord = env.DISCORD_APP_ID ? '<form method="post" style="margin-top:24px">' + champ('jeton', jeton) + champ('action', 'commandes-discord') + '<button>Installer les commandes Discord</button></form>' +
    (env.DISCORD_GUILD_ID ? '<form method="post" style="margin-top:12px">' + champ('jeton', jeton) + champ('action', 'organiser-discord') + '<button>Organiser le serveur Discord</button>' +
      '<p class="petit">Range les salons en catégories, crée les règles, la bienvenue, les rôles à choisir et les messages Premium. Rien n\'est supprimé ; tu peux recliquer sans risque (ça met à jour au lieu de dupliquer).</p></form>' +
      '<form method="post" style="margin-top:12px">' + champ('jeton', jeton) + champ('action', 'annoncer-version') + '<button>Annoncer la dernière version maintenant</button>' +
      '<p class="petit">Normalement automatique (toutes les heures, avec le déclencheur Cron) : à utiliser pour réannoncer à la main.</p></form>' : '') : '';
  return page('Administration', message + (await statsTelechargements()) + formRecherche + resultats + formDiscord);
}

// ------------------------------------------------------------ Discord
// Le bot ne lit aucun message : Discord envoie seulement les commandes « / » à cette adresse, signées (Ed25519).
// Les clés ne sont montrées qu'à la personne concernée (réponse privée ou message privé), jamais dans un salon.
const API_DISCORD = 'https://discord.com/api/v10';
const EPHEMERE = 64, ADMINISTRATEUR = 8n;
const hexOctets = (h) => new Uint8Array((h.match(/../g) || []).map((x) => parseInt(x, 16)));

async function signatureDiscord(requete, corps, env) {
  const sig = requete.headers.get('X-Signature-Ed25519') || '', ts = requete.headers.get('X-Signature-Timestamp') || '';
  if (!env.DISCORD_PUBLIC_KEY || !/^[0-9a-f]{128}$/i.test(sig) || !/^\d+$/.test(ts)) return false;
  if (Math.abs(Date.now() / 1000 - Number(ts)) > 300) return false;
  for (const nom of ['Ed25519', 'NODE-ED25519']) {
    try {
      const algo = nom === 'Ed25519' ? { name: 'Ed25519' } : { name: nom, namedCurve: nom };
      const k = await crypto.subtle.importKey('raw', hexOctets(env.DISCORD_PUBLIC_KEY), algo, false, ['verify']);
      return await crypto.subtle.verify(algo, k, hexOctets(sig), enc(ts + corps));
    } catch (e) { /* algorithme suivant */ }
  }
  return false;
}

function reponse(contenu, options = {}) {
  return json({ type: 4, data: Object.assign({ flags: EPHEMERE, allowed_mentions: { parse: [] } }, typeof contenu === 'string' ? { content: contenu } : contenu, options) });
}

const MAGENTA = 0xff2bd6;
function carteCle(lic, titre) {
  return {
    title: titre, color: MAGENTA,
    description: 'Ta clé **' + PRODUIT + '** :\n```' + lic.cle + '```\nDans Velox : onglet **Premium**, bouton **« J\'ai une clé »**, puis colle-la. Elle marche sur ' + LIMITE_PC + ' PC.',
    footer: { text: 'Garde-la pour toi : elle ne sera jamais demandée par le support.' },
  };
}

async function discord(requete, env, ctx) {
  const corps = await requete.text();
  if (!await signatureDiscord(requete, corps, env)) return new Response('signature invalide', { status: 401 });
  const i = JSON.parse(corps);
  if (i.type === 1) return json({ type: 1 });                        // vérification de l'adresse par Discord
  if (i.type !== 2 && i.type !== 3) return reponse('Action inconnue.');
  const membre = i.member || {}, utilisateur = membre.user || i.user || {};
  const id = String(utilisateur.id || '');
  if (!/^\d{15,22}$/.test(id)) return reponse('Utilisateur inconnu.');
  // type 2 : commande « / » ; type 3 : bouton d'un message du bot (seulement les boutons connus)
  const bouton = i.type === 3 ? String((i.data && i.data.custom_id) || '') : '';
  const nom = i.type === 2 ? ((i.data && i.data.name) || '') : (['premium', 'ma-cle', 'telecharger', 'essai'].includes(bouton) ? bouton : '');
  if (bouton.startsWith('role:')) return await basculerRole(env, i, id, bouton.slice(5));

  if (nom === 'premium') {
    if (!env.LIEN_PAIEMENT) return reponse('La boutique n\'est pas encore ouverte. Reste connecté au salon des annonces !');
    // référence de l'achat : l'identifiant Discord (pour envoyer la clé ici) + un nombre au hasard
    const ref = 'discord_' + id + '_' + hex(crypto.getRandomValues(new Uint8Array(8)));
    const lien = env.LIEN_PAIEMENT + (env.LIEN_PAIEMENT.includes('?') ? '&' : '?') + 'client_reference_id=' + ref;
    return reponse({
      embeds: [{
        title: PRODUIT + ' · ' + PRIX, color: MAGENTA,
        description: '19 outils de pro débloqués d\'un coup : gardien en temps réel, mode jeu automatique, preuve en FPS, moniteur en direct, sauvegarde automatique…\n\n' +
          '• Paiement sécurisé par **Stripe**, **sans engagement** : tu arrêtes quand tu veux\n• Ta clé arrive ici **en message privé** juste après le paiement\n• Valable sur ' + LIMITE_PC + ' PC tant que l\'abonnement est actif\n• Rétractation possible pendant 14 jours',
      }],
      components: [{ type: 1, components: [{ type: 2, style: 5, label: 'S\'abonner · 4,99 €/mois', url: lien }, { type: 2, style: 5, label: 'Voir le site', url: SITE }] }],
    });
  }

  if (nom === 'ma-cle') {
    const liste = JSON.parse(await env.LICENCES.get('discord:' + id) || '[]');
    if (liste.length === 0) return reponse('Aucune clé liée à ton compte Discord. Si tu as acheté sur le site, ta clé est dans l\'e-mail de Stripe ; sinon écris à ' + CONTACT + '.');
    const embeds = [];
    for (const c of liste.slice(-5)) {
      const lic = await lire(env, 'lic:' + c);
      if (lic) embeds.push(carteCle(lic, lic.statut === 'active' ? 'Ta clé Premium' : 'Clé désactivée (' + (lic.raison || lic.statut) + ')'));
    }
    const gestion = env.LIEN_GESTION ? [{ type: 1, components: [{ type: 2, style: 5, label: 'Gérer ou résilier mon abonnement', url: env.LIEN_GESTION }] }] : [];
    return reponse({ embeds, components: gestion });
  }

  if (nom === 'telecharger') {
    return reponse({ content: 'Télécharge **Velox** (gratuit, Windows 10 et 11) :', components: [{ type: 1, components: [{ type: 2, style: 5, label: 'Télécharger Velox', url: SITE }] }] });
  }

  if (nom === 'essai') {
    return reponse({
      embeds: [{
        title: '🎁 Essai gratuit de Velox Premium · 7 jours', color: OR,
        description: 'L\'essai est **intégré à Velox**, sans carte bancaire et sans inscription :\n\n1. Télécharge et ouvre **Velox**\n2. Onglet **Premium**\n3. Clique sur **« Essayer gratuitement 7 jours »**\n\nTous les outils Premium sont débloqués tout de suite. Ça te plaît ? Garde-les avec `/premium` (4,99 €/mois, sans engagement).',
      }],
      components: [{ type: 1, components: [{ type: 2, style: 5, label: 'Télécharger Velox', url: SITE }, { type: 2, style: 3, label: 'S\'abonner', custom_id: 'premium' }] }],
    });
  }

  if (nom === 'score') {
    const code = String(((i.data.options || []).find((o) => o.name === 'code') || {}).value || '').trim();
    const s = lireCodeJoueur(code);
    if (!s) return reponse('Code invalide. Dans Velox : **QG Gamer → « Partager sur Discord »** copie ton code, puis tape `/score` ici et colle-le (Ctrl+V).');
    const rang = rangJeu(s.niveau), pseudo = utilisateur.global_name || utilisateur.username || 'Joueur';
    // réponse publique : tout le salon voit la carte
    return json({ type: 4, data: { allowed_mentions: { parse: [] }, embeds: [{
      author: { name: pseudo + ' · profil Gamer Velox' },
      title: rang.emoji + ' Rang ' + rang.nom + ' · niveau ' + s.niveau, color: rang.couleur,
      description: barre(s.avancement) + '\n',
      fields: [
        { name: '⚡ XP', value: s.xp.toLocaleString('fr-FR'), inline: true },
        { name: '🏆 Succès', value: s.succes + ' / ' + s.total, inline: true },
        { name: '🔥 Meilleure série', value: s.serie + ' jour' + (s.serie > 1 ? 's' : ''), inline: true },
        { name: '🎯 Défis réussis', value: String(s.defis), inline: true },
        { name: '📈 Prochain rang', value: rang.suivant ? rang.suivant + ' au niveau ' + rang.niveauSuivant : 'Rang maximum !', inline: true },
      ],
      footer: { text: 'Toi aussi : QG Gamer → « Partager sur Discord », puis /score' } }] } });
  }

  if (nom === 'cle-offerte') {
    // réservé aux administrateurs du serveur (vérifié ici, pas seulement par Discord)
    let droits = 0n;
    try { droits = BigInt(membre.permissions || '0'); } catch (e) { droits = 0n; }
    // seulement sur TON serveur (DISCORD_GUILD_ID) : même si le bot était ajouté ailleurs, personne d'autre ne peut créer de clés
    if (!env.DISCORD_GUILD_ID || i.guild_id !== env.DISCORD_GUILD_ID) return reponse('Commande réservée au serveur officiel de Velox.');
    if ((droits & ADMINISTRATEUR) !== ADMINISTRATEUR) return reponse('Commande réservée aux administrateurs du serveur.');
    const opts = (i.data.options || []);
    const cible = String((opts.find((o) => o.name === 'membre') || {}).value || '');
    const raison = String((opts.find((o) => o.name === 'raison') || {}).value || 'clé offerte').slice(0, 100);
    if (!/^\d{15,22}$/.test(cible)) return reponse('Membre inconnu.');
    // Discord attend une réponse en moins de 3 secondes : on répond tout de suite « en cours… », puis on crée la clé,
    // on donne le rôle, on envoie le message privé, et on remplace la réponse par le résultat.
    const travail = (async () => {
      const lic = {
        cle: crypto.randomUUID().toUpperCase(), statut: 'active', raison: '', email: '', nom: 'Discord ' + cible, session: '', paiement: '', ref: '',
        montant: 0, devise: 'eur', test: false, offerte: raison, offertePar: id, creee: new Date().toISOString(), activations: [],
      };
      await ecrire(env, lic);
      const envoyee = await donnerSurDiscord(env, cible, lic, 'offerte');
      const texte = 'Clé offerte à <@' + cible + '> (' + raison + ').' + (envoyee ? ' Elle lui a été envoyée en message privé.' : ' Ses messages privés sont fermés : voici la clé à lui transmettre toi-même :\n```' + lic.cle + '```');
      await modifierReponse(env, i.token, texte);
    })().catch((e) => console.log('cle-offerte', e));
    if (ctx && ctx.waitUntil) { ctx.waitUntil(travail); return json({ type: 5, data: { flags: EPHEMERE } }); }
    await travail;
    return json({ type: 5, data: { flags: EPHEMERE } });
  }
  return reponse('Commande inconnue.');
}

/** Remplace la réponse « en cours… » d'une commande (jeton de l'interaction, valable 15 minutes ; pas besoin du jeton du bot). */
async function modifierReponse(env, jeton, texte) {
  try {
    await fetch(API_DISCORD + '/webhooks/' + env.DISCORD_APP_ID + '/' + jeton + '/messages/@original', {
      method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ content: texte, allowed_mentions: { parse: [] } }),
    });
  } catch (e) { console.log('discord réponse', e); }
}

async function appelDiscord(env, methode, chemin, corps) {
  if (!env.DISCORD_BOT_TOKEN) return null;
  try {
    const r = await fetch(API_DISCORD + chemin, {
      method: methode, headers: { Authorization: 'Bot ' + env.DISCORD_BOT_TOKEN, 'Content-Type': 'application/json', 'X-Audit-Log-Reason': 'Velox Premium' },
      body: corps === undefined ? undefined : JSON.stringify(corps),
    });
    if (!r.ok) { console.log('discord', methode, chemin, r.status); return null; }
    return r.status === 204 ? {} : await r.json();
  } catch (e) { console.log('discord', e); return null; }
}

async function roleDiscord(env, id, donner) {
  if (!env.DISCORD_GUILD_ID || !env.DISCORD_ROLE_PREMIUM) return;
  await appelDiscord(env, donner ? 'PUT' : 'DELETE', '/guilds/' + env.DISCORD_GUILD_ID + '/members/' + id + '/roles/' + env.DISCORD_ROLE_PREMIUM);
}

/** Lie la clé au compte Discord, donne le rôle Premium et envoie la clé en message privé (vrai si le message est parti). */
async function donnerSurDiscord(env, id, lic, origine) {
  lic.discord = id; await ecrire(env, lic);
  const liste = JSON.parse(await env.LICENCES.get('discord:' + id) || '[]');
  if (!liste.includes(lic.cle)) { liste.push(lic.cle); await env.LICENCES.put('discord:' + id, JSON.stringify(liste)); }
  await roleDiscord(env, id, true);
  const canal = await appelDiscord(env, 'POST', '/users/@me/channels', { recipient_id: id });
  if (!canal || !canal.id) return false;
  const titre = origine === 'offerte' ? 'Tu as reçu une clé Velox Premium !' : 'Merci pour ton achat !';
  return !!await appelDiscord(env, 'POST', '/channels/' + canal.id + '/messages', { embeds: [carteCle(lic, titre)], allowed_mentions: { parse: [] } });
}

// ------------------------------------------------------------ /score : code de joueur copié depuis Velox
const RANGS_JEU = [
  { nom: 'Bronze', niveau: 1, couleur: 0xe8a060, emoji: '🥉' }, { nom: 'Argent', niveau: 5, couleur: 0xecf2fa, emoji: '🥈' },
  { nom: 'Or', niveau: 10, couleur: 0xffde66, emoji: '🥇' }, { nom: 'Platine', niveau: 15, couleur: 0x96fff0, emoji: '💠' },
  { nom: 'Diamant', niveau: 20, couleur: 0x8cd2ff, emoji: '💎' }, { nom: 'Légende', niveau: 30, couleur: 0xff2bd6, emoji: '👑' },
];
function rangJeu(niveau) {
  let k = 0; RANGS_JEU.forEach((r, j) => { if (niveau >= r.niveau) k = j; });
  const s = RANGS_JEU[k + 1];
  return Object.assign({}, RANGS_JEU[k], { suivant: s ? s.nom : '', niveauSuivant: s ? s.niveau : 0 });
}
/** Même contrôle que Velox (Succes.Controle) : FNV-1a 32 bits de « velox|corps », 6 premiers chiffres hexadécimaux. */
function controleJoueur(corps) {
  let h = 2166136261;
  for (const o of enc('velox|' + corps)) { h ^= o; h = Math.imul(h, 16777619) >>> 0; }
  return h.toString(16).padStart(8, '0').slice(0, 6);
}
function lireCodeJoueur(code) {
  const m = /^VLX1-(\d{1,4})-(\d{1,8})-(\d{1,3})-(\d{1,3})-(\d{1,4})-(\d{1,6})-([0-9a-f]{6})$/i.exec(code || '');
  if (!m) return null;
  const corps = m.slice(1, 7).join('-');
  if (controleJoueur(corps) !== m[7].toLowerCase()) return null;
  const [niveau, xp, succes, total, serie, defis] = m.slice(1, 7).map(Number);
  if (niveau < 1 || succes > total || total > 200) return null;
  const r = rangJeu(niveau);
  const avancement = r.niveauSuivant ? Math.min(1, (niveau - r.niveau) / (r.niveauSuivant - r.niveau)) : 1;
  return { niveau, xp, succes, total, serie, defis, avancement };
}
const barre = (x) => '`' + '█'.repeat(Math.round(x * 16)) + '░'.repeat(16 - Math.round(x * 16)) + '` ' + Math.round(x * 100) + ' % vers le rang suivant';

// ------------------------------------------------------------ annonce automatique des nouvelles versions (GitHub)
// Appelée toutes les heures par le déclencheur Cron de Cloudflare (et par le bouton de /admin). Une version n'est annoncée qu'une fois.
const DEPOT = 'damiencoussot-ai/nettoyage-pc';
async function annoncerVersion(env, forcer) {
  const etat = JSON.parse(await env.LICENCES.get('serveur') || '{}');
  const salon = (etat.salons || {}).maj;
  if (!salon) return 'Salon #mises-à-jour inconnu : clique d\'abord sur « Organiser le serveur Discord ».';
  let rel;
  try {
    const r = await fetch('https://api.github.com/repos/' + DEPOT + '/releases/latest', { headers: { 'User-Agent': 'velox-licences', Accept: 'application/vnd.github+json' } });
    if (!r.ok) return 'GitHub ne répond pas (' + r.status + ').';
    rel = await r.json();
  } catch (e) { return 'GitHub injoignable.'; }
  const tag = String(rel.tag_name || '');
  if (!tag) return 'Aucune version publiée.';
  if (!forcer && await env.LICENCES.get('annonce:derniere') === tag) return 'La version ' + tag + ' est déjà annoncée.';
  const version = tag.replace(/^v/i, '');
  // l'installateur doit être celui de CETTE version (sinon, erreur de publication : on n'annonce pas)
  const exe = (rel.assets || []).find((a) => a.name === 'Velox-Setup-' + version + '.exe') || (rel.assets || []).find((a) => a.name === 'NettoyagePC-Setup-' + version + '.exe');
  if (!exe) return 'La version ' + version + ' ne contient pas son installateur (Velox-Setup-' + version + '.exe) : annonce suspendue. Vérifie les fichiers joints sur GitHub.';
  let notes = String(rel.body || '').replace(/\r/g, '').replace(/^SHA256.*$/gim, '').trim();
  if (notes.length > 3500) notes = notes.slice(0, 3500).replace(/\n[^\n]*$/, '') + '\n…';
  const role = (etat.roles || {}).annonces;
  const msg = {
    content: role ? '<@&' + role + '>' : '', allowed_mentions: { parse: [], roles: role ? [role] : [] },
    embeds: [{ title: '🆕 Velox ' + version + ' est disponible !', url: rel.html_url || SITE, color: MAGENTA, description: notes || 'Nouvelle version de Velox.',
      footer: { text: 'Velox se met à jour tout seul, ou télécharge-le ci-dessous.' }, timestamp: rel.published_at || new Date().toISOString() }],
    components: [{ type: 1, components: [{ type: 2, style: 5, label: 'Télécharger Velox ' + version, url: exe ? exe.browser_download_url : SITE }].concat(rel.html_url ? [{ type: 2, style: 5, label: 'Voir sur GitHub', url: rel.html_url }] : []) }],
  };
  const m = await appelDiscord(env, 'POST', '/channels/' + salon + '/messages', msg);
  if (!m) return 'Discord a refusé le message (le bot a-t-il le droit d\'écrire dans #mises-à-jour ?).';
  await env.LICENCES.put('annonce:derniere', tag);
  return 'Version ' + version + ' annoncée dans #mises-à-jour.';
}

async function installerCommandes(env) {
  if (!env.DISCORD_APP_ID || !env.DISCORD_BOT_TOKEN) return 'DISCORD_APP_ID ou DISCORD_BOT_TOKEN manquant.';
  const commandes = [
    { name: 'premium', description: 'S\'abonner à Velox Premium (4,99 €/mois, sans engagement) : clé en message privé', type: 1 },
    { name: 'ma-cle', description: 'Revoir ta clé Velox Premium (visible seulement par toi)', type: 1 },
    { name: 'telecharger', description: 'Télécharger Velox (gratuit)', type: 1 },
    { name: 'essai', description: 'Essayer Velox Premium gratuitement pendant 7 jours', type: 1 },
    { name: 'score', description: 'Montrer ton rang de joueur Velox (code copié depuis le QG Gamer)', type: 1, options: [{ type: 3, name: 'code', description: 'Ton code VLX1-… (Velox : QG Gamer → Partager sur Discord)', required: true, max_length: 60 }] },
    { name: 'cle-offerte', description: 'Admin : offrir une clé Velox Premium à un membre', type: 1, default_member_permissions: '8', dm_permission: false,
      options: [{ type: 6, name: 'membre', description: 'Le membre qui reçoit la clé', required: true }, { type: 3, name: 'raison', description: 'Concours, partenariat…', required: false, max_length: 100 }] },
  ];
  const r = await appelDiscord(env, 'PUT', '/applications/' + env.DISCORD_APP_ID + '/commands', commandes);
  return r ? commandes.length + ' commandes installées (/premium, /ma-cle, /telecharger, /essai, /score, /cle-offerte).' : 'échec : vérifie DISCORD_APP_ID et DISCORD_BOT_TOKEN.';
}

// ------------------------------------------------------------ organisation du serveur Discord (bouton de /admin)
// Range les salons en catégories, crée ce qui manque, met les droits (lecture seule, salon Premium) et publie les
// messages du bot. Rien n'est supprimé. Peut être relancé : ce qui existe déjà est retrouvé (par son nom) et mis à jour.
const VOIR = 0x400n, ECRIRE = 0x800n, FILS = 0x4000000000n | 0x800000000n, CONNECTER = 0x100000n, PARLER = 0x200000n;
const OR = 0xffc83d, VIOLET = 0x7c5cff;
const ROLES_CHOIX = [
  { cle: 'gamer', nom: '🎮 Gamer', couleur: MAGENTA, texte: 'tu joues sur PC : salon gaming, sessions entre joueurs, astuces FPS' },
  { cle: 'travail', nom: '💼 Travail', couleur: 0x4f8cff, texte: 'PC de travail : astuces de vitesse et de sauvegarde' },
  { cle: 'famille', nom: '🏠 Famille', couleur: 0x2ecc71, texte: 'PC de la maison : protection et entretien simple' },
  { cle: 'annonces', nom: '🔔 Annonces', couleur: 0xf1c40f, texte: 'être prévenu à chaque nouvelle version de Velox', mention: true },
];
const PLAN_SERVEUR = [
  { cle: 'cat-bienvenue', nom: '👋 BIENVENUE', salons: [
    { cle: 'regles', nom: '📜・règles', alias: ['regles', 'reglement', 'rules'], lecture: true, sujet: 'Les règles du serveur Velox' },
    { cle: 'bienvenue', nom: '👋・bienvenue', alias: ['bienvenue', 'welcome', 'accueil'], lecture: true, sujet: 'Bienvenue ! Par où commencer' },
    { cle: 'annonces', nom: '📢・annonces', alias: ['annonce', 'annonces'], lecture: true, sujet: 'Les nouveautés de Velox' },
    { cle: 'maj', nom: '🆕・mises-à-jour', alias: ['mises-a-jour', 'mise-a-jour', 'maj'], lecture: true, sujet: 'Chaque nouvelle version de Velox' }] },
  { cle: 'cat-velox', nom: '⚡ VELOX', salons: [
    { cle: 'general', nom: '💬・général', alias: ['general', 'discussion'], sujet: 'Discute de tout avec la communauté' },
    { cle: 'gaming', nom: '🎮・gaming', alias: ['gaming', 'jeux'], sujet: 'Tes jeux, tes FPS, tes réglages' },
    { cle: 'captures', nom: '📸・tes-captures', alias: ['tes-captures', 'captures'], sujet: 'Montre ton score Velox, ta carte de joueur, ton setup' },
    { cle: 'avis', nom: '⭐・avis', alias: ['avis', 'reviews'], sujet: 'Ton avis sur Velox : ce que tu aimes, ce qui manque' },
    { cle: 'roles', nom: '🎭・rôles', alias: ['roles'], lecture: true, sujet: 'Choisis tes rôles en un clic' }] },
  { cle: 'cat-premium', nom: '💎 PREMIUM', salons: [
    { cle: 'acheter', nom: '💎・acheter-premium', alias: ['acheter-premium', 'premium-boutique', 'boutique'], lecture: true, sujet: 'Velox Premium : 4,99 €/mois sans engagement' },
    { cle: 'salon-premium', nom: '👑・salon-premium', alias: ['salon-premium'], premium: true, sujet: 'Réservé aux membres Premium' }] },
  { cle: 'cat-aide', nom: '🛟 AIDE', salons: [
    { cle: 'support', nom: '🛟・support', alias: ['support', 'aide'], sujet: 'Un souci avec Velox ? Explique-le ici (ne donne jamais ta clé)' },
    { cle: 'idees', nom: '💡・idées', alias: ['idees', 'suggestions'], sujet: 'Tes idées pour les prochaines versions' }] },
  { cle: 'cat-vocal', nom: '🔊 VOCAL', salons: [
    { cle: 'v-general', nom: '🔊 Général', type: 2, alias: ['general'] },
    { cle: 'v-gaming', nom: '🎮 Gaming', type: 2, alias: ['gaming'] },
    { cle: 'v-gaming2', nom: '🎮 Gaming 2', type: 2, alias: ['gaming2', 'gaming-2'] },
    { cle: 'v-premium', nom: '💎 Premium', type: 2, alias: ['premium'], premium: true }] },
];
const normal = (s) => String(s || '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-z0-9-]/g, '').replace(/^-+|-+$/g, '');

async function organiserServeur(env) {
  const G = env.DISCORD_GUILD_ID;
  if (!G || !env.DISCORD_BOT_TOKEN) return 'DISCORD_GUILD_ID ou DISCORD_BOT_TOKEN manquant.';
  let appels = 0; const journal = [], erreurs = [];
  // adresse de Discord (les essais automatiques la remplacent par un faux Discord local ; seulement en http://127.0.0.1)
  const base = /^http:\/\/127\.0\.0\.1:\d+\/api\/v10$/.test(env.DISCORD_API_ESSAI || '') ? env.DISCORD_API_ESSAI : API_DISCORD;
  // appel à Discord avec un budget (Cloudflare gratuit : 50 appels par clic) et une attente si Discord demande de ralentir
  const api = async (m, chemin, corps) => {
    for (let essai = 0; essai < 2; essai++) {
      if (appels >= 46) throw new Error('budget');
      appels++;
      const r = await fetch(base + chemin, { method: m, headers: { Authorization: 'Bot ' + env.DISCORD_BOT_TOKEN, 'Content-Type': 'application/json', 'X-Audit-Log-Reason': 'Organisation Velox' }, body: corps === undefined ? undefined : JSON.stringify(corps) });
      if (r.status === 429 && essai === 0) { let s = 2; try { s = Math.min(5, Number((await r.json()).retry_after) || 2); } catch (e) { /* 2 s */ } await new Promise((x) => setTimeout(x, s * 1000)); continue; }
      if (!r.ok) { erreurs.push(m + ' ' + chemin.replace(/\d{15,22}/g, '…') + ' → ' + r.status); return null; }
      return r.status === 204 ? {} : await r.json();
    }
    return null;
  };
  const etat = JSON.parse(await env.LICENCES.get('serveur') || '{}');
  etat.roles = etat.roles || {}; etat.salons = etat.salons || {}; etat.messages = etat.messages || {};
  try {
    const salons = await api('GET', '/guilds/' + G + '/channels'), roles = await api('GET', '/guilds/' + G + '/roles');
    if (!salons || !roles) return 'Le bot ne peut pas lire le serveur. Dans Discord : Paramètres du serveur → Rôles → rôle « Velox » → coche « Administrateur », puis reclique.\n' + erreurs.join('\n');
    // 1. rôles à choisir + rôle Premium mis en avant (doré, affiché à part)
    for (const ro of ROLES_CHOIX) {
      let r = roles.find((x) => x.id === etat.roles[ro.cle]) || roles.find((x) => normal(x.name) === normal(ro.nom));
      if (!r) { r = await api('POST', '/guilds/' + G + '/roles', { name: ro.nom, color: ro.couleur, mentionable: !!ro.mention }); if (r) journal.push('rôle créé : ' + ro.nom); }
      if (r) etat.roles[ro.cle] = r.id;
    }
    const prem = roles.find((x) => x.id === env.DISCORD_ROLE_PREMIUM);
    if (prem && (prem.color !== OR || !prem.hoist)) { if (await api('PATCH', '/guilds/' + G + '/roles/' + prem.id, { color: OR, hoist: true })) journal.push('rôle Premium : doré et affiché à part'); }
    // 2. catégories et salons
    const pris = new Set();
    const trouver = (type, cleEtat, alias) => {
      let s = salons.find((x) => x.id === etat.salons[cleEtat] && !pris.has(x.id));
      if (!s) s = salons.find((x) => x.type === type && !pris.has(x.id) && alias.includes(normal(x.name)));
      if (s) pris.add(s.id);
      return s;
    };
    const positions = [];
    for (let ci = 0; ci < PLAN_SERVEUR.length; ci++) {
      const c = PLAN_SERVEUR[ci];
      let cat = trouver(4, c.cle, [normal(c.nom)]);
      if (!cat) { cat = await api('POST', '/guilds/' + G + '/channels', { name: c.nom, type: 4 }); if (cat) { journal.push('catégorie créée : ' + c.nom); salons.push(cat); pris.add(cat.id); } }
      if (!cat) continue;
      etat.salons[c.cle] = cat.id; positions.push({ id: cat.id, position: ci });
      for (let si = 0; si < c.salons.length; si++) {
        const p = c.salons[si], type = p.type || 0;
        // droits : lecture seule pour tout le monde, ou visible seulement avec le rôle Premium
        let droits = null;
        if (p.lecture) droits = [{ id: G, type: 0, deny: String(ECRIRE | FILS), allow: '0' }];
        if (p.premium && env.DISCORD_ROLE_PREMIUM) {
          const base = type === 2 ? VOIR | CONNECTER : VOIR;
          droits = [{ id: G, type: 0, deny: String(base), allow: '0' }, { id: env.DISCORD_ROLE_PREMIUM, type: 0, allow: String(type === 2 ? base | PARLER : base | ECRIRE), deny: '0' }];
        }
        let s = trouver(type, p.cle, p.alias.concat([normal(p.nom)]));
        if (s) {
          const modif = {};
          if (s.name !== p.nom) modif.name = p.nom;
          if (s.parent_id !== cat.id) modif.parent_id = cat.id;
          if (type === 0 && p.sujet && s.topic !== p.sujet) modif.topic = p.sujet;
          if (droits) {
            const gardes = (s.permission_overwrites || []).filter((o) => !droits.some((d) => d.id === o.id));
            const voulu = gardes.concat(droits);
            const pareil = (s.permission_overwrites || []).length === voulu.length && droits.every((d) => (s.permission_overwrites || []).some((o) => o.id === d.id && String(o.allow) === d.allow && String(o.deny) === d.deny));
            if (!pareil) modif.permission_overwrites = voulu;
          }
          if (Object.keys(modif).length) { if (await api('PATCH', '/channels/' + s.id, modif)) journal.push('salon rangé : ' + p.nom); }
        } else {
          const corps = { name: p.nom, type, parent_id: cat.id };
          if (type === 0 && p.sujet) corps.topic = p.sujet;
          if (droits) corps.permission_overwrites = droits;
          s = await api('POST', '/guilds/' + G + '/channels', corps);
          if (s) { journal.push('salon créé : ' + p.nom); salons.push(s); pris.add(s.id); }
        }
        if (s) { etat.salons[p.cle] = s.id; positions.push({ id: s.id, position: si }); }
      }
    }
    // les anciennes catégories (vides) passent en bas ; elles ne sont pas supprimées
    salons.filter((x) => x.type === 4 && !pris.has(x.id)).forEach((x, k) => positions.push({ id: x.id, position: 20 + k }));
    if (positions.length) await api('PATCH', '/guilds/' + G + '/channels', positions);
    // 3. message automatique de Discord à chaque arrivée : dans #bienvenue
    if (etat.salons.bienvenue) await api('PATCH', '/guilds/' + G, { system_channel_id: etat.salons.bienvenue });
    await env.LICENCES.put('serveur', JSON.stringify(etat));
    // 4. messages du bot (modifiés s'ils existent déjà)
    for (const [cle, salon, contenu] of messagesServeur(env, etat)) {
      if (!etat.salons[salon]) continue;
      const ancien = etat.messages[cle];
      let m = null;
      if (ancien && ancien.canal === etat.salons[salon]) m = await api('PATCH', '/channels/' + ancien.canal + '/messages/' + ancien.id, contenu);
      if (!m) { m = await api('POST', '/channels/' + etat.salons[salon] + '/messages', contenu); if (m) journal.push('message publié : ' + cle); }
      if (m && m.id) etat.messages[cle] = { canal: etat.salons[salon], id: m.id };
    }
  } catch (e) {
    await env.LICENCES.put('serveur', JSON.stringify(etat));
    if (e.message === 'budget') return journal.concat(['', 'Pas tout à fait fini (limite d\'appels de Cloudflare) : reclique sur « Organiser le serveur Discord » pour terminer.']).concat(erreurs).join('\n');
    return 'Erreur : ' + e.message;
  }
  await env.LICENCES.put('serveur', JSON.stringify(etat));
  return (journal.length ? journal.join('\n') : 'Tout était déjà en place (messages mis à jour).') + '\n\nTerminé.' +
    (erreurs.length ? '\nProblèmes (donne la permission « Administrateur » au rôle du bot si besoin) :\n' + erreurs.join('\n') : '') +
    '\nLes anciennes catégories vides (« Salons textuels », « Salons vocaux ») sont descendues en bas : tu peux les supprimer toi-même.';
}

function messagesServeur(env, etat) {
  const G = env.DISCORD_GUILD_ID, s = etat.salons, lien = (c) => 'https://discord.com/channels/' + G + '/' + c;
  const m = (c) => (s[c] ? '<#' + s[c] + '>' : '#' + c);
  const sansMention = { parse: [] };
  return [
    ['regles', 'regles', { allowed_mentions: sansMention, embeds: [{
      title: '📜 Règles du serveur Velox', color: MAGENTA,
      description: '**1. Respect.** Pas d\'insultes, de harcèlement ni de propos haineux.\n' +
        '**2. Pas de spam ni de pub** pour d\'autres serveurs ou produits sans accord.\n' +
        '**3. Rien de choquant ou d\'illégal** (cracks, liens douteux, contenu NSFW).\n' +
        '**4. Ta clé Premium est secrète.** Ne la poste jamais : le staff ne te la demandera **jamais**.\n' +
        '**5. Un souci avec Velox ?** Va dans ' + m('support') + ', avec une capture si possible.\n' +
        '**6. Pseudo et avatar corrects.**\n\nLe staff peut retirer un message ou un membre qui ne respecte pas ces règles.',
      footer: { text: 'En restant sur le serveur, tu acceptes ces règles.' } }] }],
    ['bienvenue', 'bienvenue', { allowed_mentions: sansMention, embeds: [{
      title: '👋 Bienvenue sur le serveur officiel de Velox !', color: VIOLET,
      description: '**Velox** nettoie, accélère et protège ton PC Windows : gratuit, sans pub, et rien n\'est supprimé sans ton accord.\n\n' +
        '**Par où commencer ?**\n📜 Lis les ' + m('regles') + '\n🎭 Choisis tes rôles dans ' + m('roles') + '\n💬 Présente-toi dans ' + m('general') + '\n⭐ Donne ton avis dans ' + m('avis') + '\n🏆 Montre ton rang de joueur : `/score` dans ' + m('captures') + '\n💎 Débloque tout avec Premium dans ' + m('acheter') + '\n🛟 Une question ? ' + m('support'),
      footer: { text: 'Velox · Windows 10 et 11' } }],
      components: [{ type: 1, components: [
        { type: 2, style: 5, label: 'Télécharger Velox', emoji: { name: '⬇️' }, url: SITE },
        { type: 2, style: 1, label: 'Velox Premium', emoji: { name: '💎' }, custom_id: 'premium' },
      ].concat(s.roles ? [{ type: 2, style: 5, label: 'Choisir mes rôles', emoji: { name: '🎭' }, url: lien(s.roles) }] : []) }] }],
    ['roles', 'roles', { allowed_mentions: sansMention, embeds: [{
      title: '🎭 Choisis tes rôles', color: MAGENTA,
      description: 'Clique sur un bouton pour **ajouter** le rôle, reclique pour **l\'enlever**.\n\n' + ROLES_CHOIX.map((r) => '**' + r.nom + '** : ' + r.texte).join('\n') +
        '\n\n**💎 Premium** est donné automatiquement quand tu t\'abonnes.' }],
      components: [{ type: 1, components: ROLES_CHOIX.map((r) => ({ type: 2, style: 2, label: r.nom.replace(/^\S+\s/, ''), emoji: { name: r.nom.split(' ')[0] }, custom_id: 'role:' + r.cle })) }] }],
    ['acheter', 'acheter', { allowed_mentions: sansMention, embeds: [{
      title: '💎 Velox Premium · 4,99 €/mois', color: OR,
      description: '**19 outils de pro débloqués d\'un coup :**\n' +
        '🛡️ Gardien en temps réel (alerte quand un programme abuse)\n🎮 Mode jeu automatique dès que tu lances un jeu\n📈 Preuve en FPS avant / après\n' +
        '📊 Moniteur en direct (processeur, carte graphique, températures)\n💾 Sauvegarde automatique de tes dossiers\n🔄 Mise à jour automatique de tes applications\n…et bien plus.\n\n' +
        '**Comment ça marche ?**\n1. Clique sur **S\'abonner** (ou tape `/premium`)\n2. Paie en toute sécurité avec **Stripe**\n3. Ta clé arrive **en message privé** + le rôle 💎 Premium\n4. Dans Velox : onglet **Premium** → **J\'ai une clé**',
      footer: { text: 'Sans engagement · rétractation 14 jours · clé valable sur ' + LIMITE_PC + ' PC' } }],
      components: [{ type: 1, components: [
        { type: 2, style: 3, label: 'S\'abonner', emoji: { name: '💎' }, custom_id: 'premium' },
        { type: 2, style: 1, label: 'Essai gratuit 7 jours', emoji: { name: '🎁' }, custom_id: 'essai' },
        { type: 2, style: 2, label: 'Ma clé', emoji: { name: '🔑' }, custom_id: 'ma-cle' },
        { type: 2, style: 5, label: 'Voir le site', url: SITE }] }] }],
    ['salon-premium', 'salon-premium', { allowed_mentions: sansMention, embeds: [{
      title: '👑 Bienvenue dans le salon Premium', color: OR,
      description: 'Merci de soutenir Velox ! Ici : **aide prioritaire**, **avant-premières** des prochaines versions et un vocal réservé.\n\nTa clé à tout moment : `/ma-cle` (visible seulement par toi).' }] }],
  ];
}

async function basculerRole(env, i, id, cle) {
  if (!env.DISCORD_GUILD_ID || i.guild_id !== env.DISCORD_GUILD_ID) return reponse('Ce bouton marche seulement sur le serveur officiel de Velox.');
  const etat = JSON.parse(await env.LICENCES.get('serveur') || '{}');
  const role = (etat.roles || {})[cle], info = ROLES_CHOIX.find((r) => r.cle === cle);
  if (!role || !info) return reponse('Ce rôle n\'existe plus : préviens le staff.');
  const a = ((i.member || {}).roles || []).includes(role);
  const ok = await appelDiscord(env, a ? 'DELETE' : 'PUT', '/guilds/' + env.DISCORD_GUILD_ID + '/members/' + id + '/roles/' + role);
  if (!ok) return reponse('Impossible de changer ton rôle pour le moment. Réessaie dans un instant.');
  return reponse(a ? 'Rôle **' + info.nom + '** retiré.' : 'Rôle **' + info.nom + '** ajouté !');
}
