# Nettoyage PC - fabrique le site officiel (dossier docs, publié par GitHub Pages).
# Lancer depuis le dossier du code source :  python site/fabriquer-site.py
# Les pages légales sont tirées des fichiers legal\*.txt (les mêmes que dans le logiciel) :
# modifie les .txt, relance ce script, et le site suit. Le lien d'achat et la version viennent du code.
# Option : --apercu DOSSIER  écrit aussi une copie pour un aperçu (page d'accueil sans en-tête HTML).
import html, os, re, shutil, sys

RACINE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SORTIE = os.path.join(RACINE, 'docs')
DEPOT = 'damiencoussot-ai/nettoyage-pc'
SITE = 'https://damiencoussot-ai.github.io/nettoyage-pc/'
TELECHARGER = 'https://github.com/%s/releases/latest/download/NettoyagePC-Setup.exe' % DEPOT
VERSIONS = 'https://github.com/%s/releases' % DEPOT

def lire(chemin):
    return open(os.path.join(RACINE, chemin), encoding='utf-8-sig').read().replace('\r\n', '\n')

def constante(fichier, motif, defaut=''):
    m = re.search(motif, lire(fichier))
    return m.group(1) if m else defaut

VERSION = constante('src/Noyau/Base.cs', r'Version = "([\d.]+)"')
URL_ACHAT = constante('src/Noyau/Licence.cs', r'UrlAchatIntegree = "([^"]*)"')
if not re.match(r'^https://(buy\.stripe\.com|[a-z0-9-]+\.lemonsqueezy\.com)/', URL_ACHAT or ''): URL_ACHAT = ''
EMAIL = constante('legal/MENTIONS-LEGALES.txt', r'\nE-mail : (.+)', '[E-MAIL DE CONTACT]').strip()
EMAIL_A_COMPLETER = EMAIL.startswith('[')

def taille_installateur():
    d = os.path.join(RACINE, 'livraison-' + VERSION, 'NettoyagePC-Setup-%s.exe' % VERSION)
    if os.path.exists(d): return '%.1f Mo' % (os.path.getsize(d) / 1048576.0)
    return 'moins de 1 Mo'

def marquer(texte_html):
    """Les passages entre crochets (à compléter) sont surlignés pour ne pas être oubliés."""
    return re.sub(r'\[([A-ZÉÈÀÇ][^\]]{2,})\]', r'<mark class="a-completer">[\1]</mark>', texte_html)

def liens(texte_html):
    return re.sub(r'(https://[^\s<>()]+[^\s<>().,;:])', r'<a href="\1">\1</a>', texte_html)

def en_ligne(t):
    return marquer(liens(html.escape(t, quote=False)))

# ------------------------------------------------------------ pages légales
LEGAUX = [
    ('CONDITIONS-DE-VENTE', 'conditions-de-vente.html', 'Conditions de vente'),
    ('LICENCE', 'licence.html', 'Contrat de licence'),
    ('CONFIDENTIALITE', 'confidentialite.html', 'Confidentialité'),
    ('MENTIONS-LEGALES', 'mentions-legales.html', 'Mentions légales'),
]

def est_titre(l):
    lettres = re.sub(r'[^A-Za-zÀ-ÿ]', '', l)
    return len(lettres) >= 3 and l == l.upper() and not l.startswith('-') and ':' not in l and '[' not in l and len(l) < 100

def texte_vers_html(txt):
    lignes = txt.strip('﻿').split('\n')
    titre, date = lignes[0].strip(), (lignes[1].strip() if len(lignes) > 1 else '')
    out, liste, formulaire = [], False, False
    def fermer_liste():
        nonlocal liste
        if liste: out.append('</ul>'); liste = False
    for brute in lignes[2:]:
        l = brute.strip()
        if re.fullmatch(r'-{8,}', l):
            fermer_liste()
            out.append('</div>' if formulaire else '<div class="formulaire">'); formulaire = not formulaire
            continue
        if not l: fermer_liste(); continue
        if l.startswith('- '):
            if not liste: out.append('<ul>'); liste = True
            out.append('<li>%s</li>' % en_ligne(l[2:]))
            continue
        fermer_liste()
        m = re.match(r'^(\d+)\.\s+(.*)$', l)
        if est_titre(l):
            ident = ' id="article-%s"' % m.group(1) if m else ''
            out.append('<h2%s>%s</h2>' % (ident, en_ligne(l)))
            continue
        m2 = re.match(r'^(\d+\.\d+)\s+(.*)$', l)
        if m2 and len(l) < 70 and not l.endswith('.'):
            out.append('<h3 id="article-%s">%s</h3>' % (m2.group(1).replace('.', '-'), en_ligne(l)))
            continue
        if m2:
            out.append('<p><strong>%s</strong> %s</p>' % (m2.group(1), en_ligne(m2.group(2))))
            continue
        out.append('<p>%s</p>' % en_ligne(l))
    fermer_liste()
    if formulaire: out.append('</div>')
    return titre, date, '\n'.join(out)

MODELE_LEGAL = '''<!doctype html>
<html lang="fr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<title>{nom} · Nettoyage PC</title>
<meta name="description" content="{nom} de Nettoyage PC.">
<link rel="icon" type="image/png" href="img/icone-64.png">
<style>
:root {{ --fond: #060A12; --carte: #0A1220; --bordure: #18323F; --texte: #DDEBF5; --discret: #8AA0B4; --accent: #3DF5FF; --orange: #FFB547; --sur-accent: #04121A;
  --affichage: "Bahnschrift", "DIN Alternate", "Segoe UI", system-ui, sans-serif; --corps: "Segoe UI", system-ui, -apple-system, Roboto, sans-serif; --console: "Cascadia Mono", Consolas, ui-monospace, monospace; color-scheme: dark; }}
@media (prefers-color-scheme: light) {{ :root:not([data-theme="dark"]) {{ --fond: #EEF3F7; --carte: #FFFFFF; --bordure: #CBD9E3; --texte: #0E1C26; --discret: #4C6170; --accent: #007A9E; --orange: #A85F00; --sur-accent: #FFFFFF; color-scheme: light; }} }}
:root[data-theme="light"] {{ --fond: #EEF3F7; --carte: #FFFFFF; --bordure: #CBD9E3; --texte: #0E1C26; --discret: #4C6170; --accent: #007A9E; --orange: #A85F00; --sur-accent: #FFFFFF; color-scheme: light; }}
* {{ box-sizing: border-box; }}
html, body {{ margin: 0; }}
:root {{ padding-top: env(safe-area-inset-top, 0px); padding-bottom: env(safe-area-inset-bottom, 0px); }}
body {{ background: var(--fond); color: var(--texte); font-family: var(--corps); line-height: 1.65; font-size: 1rem; }}
a {{ color: var(--accent); overflow-wrap: anywhere; }}
:focus-visible {{ outline: 2px solid var(--accent); outline-offset: 3px; }}
.enveloppe {{ max-width: 780px; margin: 0 auto; padding-left: max(16px, env(safe-area-inset-left, 0px)); padding-right: max(16px, env(safe-area-inset-right, 0px)); }}
.entete {{ border-bottom: 1px solid var(--bordure); background: var(--carte); }}
.entete .enveloppe {{ display: flex; align-items: center; justify-content: space-between; gap: 16px; min-height: 60px; flex-wrap: wrap; padding-block: 8px; max-width: 1120px; }}
.marque {{ display: flex; align-items: center; gap: 10px; color: var(--texte); text-decoration: none; font-family: var(--affichage); font-weight: 600; font-size: 1.1rem; }}
.marque img {{ width: 28px; height: 28px; }}
nav.autres {{ display: flex; flex-wrap: wrap; gap: 8px 18px; font-size: .9rem; }}
nav.autres a {{ color: var(--discret); }}
nav.autres a[aria-current] {{ color: var(--texte); text-decoration: none; }}
main {{ padding-block: 40px 64px; }}
.etiquette {{ font-family: var(--console); font-size: .78rem; letter-spacing: .14em; text-transform: uppercase; color: var(--accent); }}
h1 {{ font-family: var(--affichage); font-weight: 600; font-size: clamp(1.6rem, 4vw, 2.3rem); line-height: 1.15; margin: 10px 0 6px; text-wrap: balance; }}
.date {{ color: var(--discret); font-family: var(--console); font-size: .85rem; margin: 0 0 28px; }}
h2 {{ font-family: var(--affichage); font-weight: 600; font-size: 1.2rem; letter-spacing: .04em; margin: 40px 0 12px; padding-top: 16px; border-top: 1px solid var(--bordure); text-wrap: balance; }}
h3 {{ font-size: 1.02rem; margin: 24px 0 8px; }}
p {{ margin: 0 0 12px; }}
ul {{ margin: 0 0 14px; padding-left: 22px; }}
li {{ margin-bottom: 6px; }}
.avis {{ border-left: 3px solid var(--orange); background: var(--carte); padding: 12px 16px; margin: 0 0 24px; font-size: .95rem; }}
.formulaire {{ border: 1px dashed var(--bordure); background: var(--carte); padding: 16px 18px; margin: 12px 0 18px; font-size: .95rem; }}
mark.a-completer {{ background: var(--orange); color: var(--sur-accent); padding: 0 4px; font-family: var(--console); font-size: .85em; }}
.pied {{ border-top: 1px solid var(--bordure); padding-block: 24px; color: var(--discret); font-size: .85rem; }}
</style>
</head>
<body>
<header class="entete"><div class="enveloppe">
  <a class="marque" href="index.html"><img src="img/icone-64.png" alt="" width="28" height="28">Nettoyage PC</a>
  <nav class="autres" aria-label="Informations légales">{nav}</nav>
</div></header>
<main class="enveloppe">
  <span class="etiquette">// Informations légales</span>
  <h1>{titre}</h1>
  <p class="date">{date}</p>
  {avis}
  {corps}
</main>
<footer class="pied"><div class="enveloppe">Ce texte est aussi dans le logiciel : Réglages &gt; Informations légales. <a href="index.html">Retour au site</a></div></footer>
</body>
</html>
'''

def titre_lisible(t):
    # « CONDITIONS DE VENTE DE NETTOYAGE PC PREMIUM » -> « Conditions de vente de Nettoyage PC Premium »
    t = t.capitalize()
    for mot in ['Nettoyage pc', 'nettoyage pc']: t = t.replace(mot, 'Nettoyage PC')
    return t.replace(' premium', ' Premium').replace(' pc ', ' PC ')

# ------------------------------------------------------------ images
def images(dossier_img):
    os.makedirs(dossier_img, exist_ok=True)
    src = os.path.join(RACINE, 'site', 'img')
    for n in os.listdir(src):
        shutil.copy2(os.path.join(src, n), os.path.join(dossier_img, n))

def accueil():
    t = lire('site/accueil.html')
    achat_actif = URL_ACHAT != ''
    valeurs = {
        'VERSION': VERSION, 'TELECHARGER': TELECHARGER, 'RELEASES': VERSIONS, 'SITE': SITE, 'TAILLE': taille_installateur(),
        'URL_ACHAT': URL_ACHAT or '#acheter', 'ACHAT_ACTIF': 'oui' if achat_actif else 'non',
        'TEXTE_ACHAT': 'Acheter · 9,99 €' if achat_actif else 'Achat bientôt disponible',
        'NOTE_ACHAT': "Votre clé s'affiche juste après le paiement. Acheté depuis l'application, Premium s'active tout seul." if achat_actif else "La boutique ouvre bientôt. En attendant, l'essai gratuit de 7 jours est dans l'application, onglet Premium.",
        'EMAIL': ("l'adresse de l'éditeur indiquée dans les conditions de vente" if EMAIL_A_COMPLETER else '<span class="adresse-email">%s</span>' % html.escape(EMAIL)),
        'CONTACT': ('Assistance : l\'adresse de contact sera publiée ici à l\'ouverture de la boutique Premium.' if EMAIL_A_COMPLETER else 'Assistance et remboursements : <span class="adresse">%s</span>' % html.escape(EMAIL)),
    }
    for k, v in valeurs.items(): t = t.replace('{{%s}}' % k, v)
    reste = re.findall(r'\{\{\w+\}\}', t)
    if reste: sys.exit('Valeurs manquantes dans accueil.html : %s' % reste)
    return t

def page_complete(corps_accueil):
    # la page d'accueil est écrite sans en-tête HTML (pour l'aperçu) : on l'enveloppe pour GitHub Pages
    return ('<!doctype html>\n<html lang="fr">\n<head>\n<meta charset="utf-8">\n'
            '<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">\n'
            '<style>:root{padding-top:env(safe-area-inset-top,0px);padding-bottom:env(safe-area-inset-bottom,0px)}</style>\n'
            + corps_accueil.replace('<header class="entete">', '</head>\n<body>\n<header class="entete">', 1) + '\n</body>\n</html>\n')

def fabriquer(dossier, apercu=False):
    os.makedirs(dossier, exist_ok=True)
    images(os.path.join(dossier, 'img'))
    a = accueil()
    open(os.path.join(dossier, 'index.html'), 'w', encoding='utf-8').write(a if apercu else page_complete(a))
    for cle, fichier, nom in LEGAUX:
        titre, date, corps = texte_vers_html(lire('legal/%s.txt' % cle))
        nav = ' '.join('<a href="%s"%s>%s</a>' % (f, ' aria-current="page"' if f == fichier else '', n) for _, f, n in LEGAUX)
        avis = ('<p class="avis">Les passages surlignés seront complétés avec les coordonnées de l\'éditeur avant l\'ouverture de la boutique Premium. '
                'D\'ici là, rien n\'est vendu : seule la version gratuite est proposée.</p>') if 'a-completer' in corps else ''
        page = MODELE_LEGAL.format(nom=nom, nav=nav, titre=html.escape(titre_lisible(titre)), date=html.escape(date), corps=corps, avis=avis)
        open(os.path.join(dossier, fichier), 'w', encoding='utf-8').write(page)
    if not apercu:
        open(os.path.join(dossier, '.nojekyll'), 'w').write('')
    a_completer = len(re.findall(r'a-completer', ''.join(open(os.path.join(dossier, f), encoding='utf-8').read() for _, f, _ in LEGAUX)))
    print('%s : version %s, achat %s, %d passage(s) à compléter dans les textes légaux' % (dossier, VERSION, 'actif' if URL_ACHAT else 'pas encore ouvert', a_completer))

if __name__ == '__main__':
    fabriquer(SORTIE)
    if '--apercu' in sys.argv:
        fabriquer(sys.argv[sys.argv.index('--apercu') + 1], apercu=True)
