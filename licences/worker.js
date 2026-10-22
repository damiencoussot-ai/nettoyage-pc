// Nettoyage PC - service de licences (Cloudflare Worker, gratuit).
//
// Rôle : quand Stripe confirme un paiement de « Nettoyage PC Premium », créer une clé unique, la montrer
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
//   Variable      LIENS_PAIEMENT           (facultatif : identifiants plink_… acceptés, séparés par des virgules)
//
// Adresses :
//   POST /stripe                    webhook Stripe (événements checkout.session.*, charge.refunded, charge.dispute.created)
//   GET  /merci?session_id=cs_…     page affichée après le paiement (lien « après paiement » du lien Stripe)
//   GET  /v1/achats/<référence>     l'application récupère la clé de l'achat qu'elle a lancé
//   POST /v1/licenses/activate|validate|deactivate   vérifications de l'application
//   GET/POST /admin                 page de l'éditeur : chercher une clé par e-mail, désactiver, libérer les PC

const LIMITE_PC = 3;
const PRODUIT = 'Nettoyage PC Premium';
const CONTACT = 'nettoyagepcai@gmail.com';
const SITE = 'https://damiencoussot-ai.github.io/nettoyage-pc/';
const ENTETES_PAGE = {
  'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store', 'Referrer-Policy': 'no-referrer',
  'X-Robots-Tag': 'noindex', 'X-Content-Type-Options': 'nosniff', 'X-Frame-Options': 'DENY',
  'Content-Security-Policy': "default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src data:; form-action 'self'; base-uri 'none'",
};

export default {
  async fetch(requete, env) {
    const url = new URL(requete.url);
    const chemin = url.pathname.replace(/\/+$/, '') || '/';
    try {
      if (chemin === '/stripe' && requete.method === 'POST') return await webhook(requete, env);
      if (chemin === '/merci' && requete.method === 'GET') return await merci(url, env);
      if (chemin.startsWith('/v1/achats/') && requete.method === 'GET') return await achat(decodeURIComponent(chemin.slice(11)), env);
      if (chemin.startsWith('/v1/licenses/') && requete.method === 'POST') return await licences(chemin.slice(13), requete, env);
      if (chemin === '/admin') return await admin(requete, env);
      if (chemin === '/') return new Response('Service de licences de Nettoyage PC.\n', { headers: { 'Content-Type': 'text/plain; charset=utf-8' } });
      return json({ error: 'not found' }, 404);
    } catch (e) {
      console.log('erreur', e && e.stack || e);
      return json({ error: 'internal error' }, 500);
    }
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
  return liste.length === 0 || liste.includes(session.payment_link || '');
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
  };
  await ecrire(env, lic);
  await env.LICENCES.put('ses:' + s.id, cle);
  if (lic.paiement) await env.LICENCES.put('pi:' + lic.paiement, cle);
  if (lic.ref) await env.LICENCES.put('ref:' + lic.ref, cle, { expirationTtl: 90 * 86400 });
  if (lic.email) await env.LICENCES.put('mail:' + lic.email + ':' + cle, '1');
  return cle;
}

async function desactiverParPaiement(env, paiement, raison) {
  if (!paiement) return;
  const cle = await env.LICENCES.get('pi:' + paiement);
  if (!cle) return;
  const lic = await lire(env, 'lic:' + cle);
  if (!lic) return;
  lic.statut = 'disabled'; lic.raison = raison; lic.modifiee = new Date().toISOString();
  await ecrire(env, lic);
}

// ------------------------------------------------------------ après le paiement
async function merci(url, env) {
  const session = url.searchParams.get('session_id') || '';
  const essai = Math.min(60, Math.max(0, parseInt(url.searchParams.get('essai') || '0', 10) || 0));
  if (!/^cs_(test|live)_[A-Za-z0-9]{10,200}$/.test(session)) return page('Lien incomplet', '<p>Ce lien ne contient pas de numéro de commande. Si vous venez de payer, ouvrez Nettoyage PC : onglet Premium, ou écrivez à ' + CONTACT + '.</p>', 400);
  const cle = await env.LICENCES.get('ses:' + session);
  const lic = cle ? await lire(env, 'lic:' + cle) : null;
  if (!lic) {
    if (essai >= 45) return page('Paiement reçu', '<p>Votre paiement est enregistré, mais la clé n\'est pas encore prête. Rechargez cette page dans quelques minutes.</p><p>Si vous avez acheté depuis Nettoyage PC, Premium s\'activera tout seul dans l\'application. Sinon, écrivez à <b>' + CONTACT + '</b> avec l\'adresse e-mail utilisée pour l\'achat : nous vous enverrons votre clé.</p>');
    const suite = '/merci?session_id=' + encodeURIComponent(session) + '&essai=' + (essai + 1);
    return page('Préparation de votre clé…', '<p class="attente">Paiement reçu. Votre clé est en cours de création, cela prend quelques secondes.</p><p class="petit">La page se met à jour toute seule.</p>', 200, '<meta http-equiv="refresh" content="3;url=' + echapper(suite) + '">');
  }
  if (lic.statut !== 'active') return page('Clé désactivée', '<p>Cette commande a été remboursée ou contestée : la clé n\'est plus active. Pour toute question : ' + CONTACT + '.</p>');
  const corps = '<p>Merci pour votre achat ! Voici votre clé ' + PRODUIT + ' :</p>' +
    '<div class="cle" id="cle">' + echapper(lic.cle) + '</div>' +
    '<p><button type="button" id="copier">Copier la clé</button> <span id="copiee" class="petit"></span></p>' +
    '<ul><li><b>Si vous avez acheté depuis Nettoyage PC</b>, Premium s\'active tout seul : revenez simplement dans l\'application.</li>' +
    '<li><b>Sinon</b>, ouvrez Nettoyage PC, onglet <b>Premium</b>, bouton <b>« J\'ai une clé »</b>, et collez la clé.</li>' +
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
    '<title>' + echapper(titre) + ' · Nettoyage PC</title><style>' +
    ':root{--fond:#060A12;--carte:#0A1220;--bord:#18323F;--texte:#DDEBF5;--discret:#8AA0B4;--accent:#3DF5FF;color-scheme:dark}' +
    '@media (prefers-color-scheme:light){:root{--fond:#EEF3F7;--carte:#fff;--bord:#CBD9E3;--texte:#0E1C26;--discret:#4C6170;--accent:#007A9E;color-scheme:light}}' +
    'body{margin:0;background:var(--fond);color:var(--texte);font:16px/1.6 "Segoe UI",system-ui,sans-serif}' +
    'main{max-width:640px;margin:0 auto;padding:40px max(16px,env(safe-area-inset-left,0px)) 56px}' +
    '.marque{font:600 1.1rem "Bahnschrift","Segoe UI",sans-serif;color:var(--texte);text-decoration:none}' +
    'h1{font:600 1.8rem/1.2 "Bahnschrift","Segoe UI",sans-serif;margin:24px 0 16px}' +
    '.cle{font:600 1.25rem "Cascadia Mono",Consolas,monospace;letter-spacing:.04em;background:var(--carte);border:1px solid var(--accent);padding:16px;margin:12px 0;overflow-wrap:anywhere;user-select:all}' +
    'button{font:600 1rem "Segoe UI",sans-serif;padding:10px 18px;border:0;border-radius:3px;background:var(--accent);color:#04121A;cursor:pointer}' +
    '.petit{color:var(--discret);font-size:.9rem}ul{padding-left:20px}li{margin:6px 0}.attente::before{content:"◌ ";color:var(--accent)}a{color:var(--accent)}' +
    '</style></head><body><main><a class="marque" href="' + SITE + '">Nettoyage PC</a><h1>' + echapper(titre) + '</h1>' + corps + '</main></body></html>';
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
  return page('Administration', message + formRecherche + resultats);
}
