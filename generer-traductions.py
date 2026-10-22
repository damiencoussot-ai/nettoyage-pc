# Génère src/Noyau/Traductions.cs à partir de traductions.tsv
import sys
lignes=[]; prefixes=[]; motifs=[]; vus=set()
for n,l in enumerate(open('traductions.tsv',encoding='utf-8').read().split('\n'),1):
    if not l.strip() or l.startswith('#'): continue
    c=l.split('\t')
    if len(c)!=3: sys.exit('ligne %d : %d colonnes' % (n,len(c)))
    if c[0] in vus: continue
    vus.add(c[0])
    if c[0].startswith('^'): motifs.append(c)
    elif c[0].startswith('~'):
        # début de phrase : l'espace final de la dernière colonne disparaît facilement à l'édition, on le remet
        a0=c[0].lstrip('~'); prefixes.append([a0]+[x+' ' if a0.endswith(' ') and not x.endswith(' ') else x for x in c[1:]])
    else: lignes.append(c)
def cs(t, brut=False):
    if not brut: t=t.replace('\\n','\n')
    return '"'+t.replace('\\','\\\\').replace('"','\\"').replace('\n','\\n')+'"'
out=['// Fichier généré par generer-traductions.py à partir de traductions.tsv : ne pas modifier à la main.','namespace NettoyagePC','{','    internal static partial class Langue','    {','        private static readonly string[] Table =','        {']
for a,b,c in lignes: out.append('            %s, %s, %s,' % (cs(a),cs(b),cs(c)))
out+=['        };','        private static readonly string[] Prefixes =','        {']
for a,b,c in prefixes: out.append('            %s, %s, %s,' % (cs(a),cs(b),cs(c)))
out+=['        };','        private static readonly string[] Motifs =','        {']
for a,b,c in motifs: out.append('            %s, %s, %s,' % (cs(a,True),cs(b),cs(c)))
out+=['        };','    }','}','']
open('src/Noyau/Traductions.cs','w',encoding='utf-8').write('\n'.join(out))
print(len(lignes),'phrases,',len(prefixes),'débuts,',len(motifs),'motifs')
