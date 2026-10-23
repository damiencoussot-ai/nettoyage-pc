# Nettoyage PC 5 — nouvelle interface (en construction)

Interface refaite avec [Avalonia](https://avaloniaui.net) (.NET 10) : le dessin passe par la carte graphique,
les animations (anneau, menu, transitions) sont fluides et s'arrêtent quand la fenêtre est en arrière-plan.

- La **version 4** (dossier `src`, WinForms, .NET Framework 4.8) reste la version publiée.
- **Un seul moteur pour les deux** : ce projet compile directement `src/Noyau` et `src/Moteur`
  (nettoyage, analyses, licence, bilan de l'accueil, catalogue des actions). Rien n'est recopié.
- Fait : fenêtre (menu, en-tête), accueil avec le vrai bilan du PC (calculé en arrière-plan) et les
  mesures en direct, actions du moteur (simulation par défaut, question avant toute action réelle,
  panneau de progression avec « Arrêter »). Les autres rubriques arrivent une à une.
- `NettoyagePC --captures DOSSIER [--reel]` : rendu sans écran de la fenêtre en PNG (thèmes sombre et clair) ;
  avec `--reel` (Windows), l'accueil montre le vrai bilan du PC, en lecture seule.
- Les modes sans fenêtre sont ceux de la version 4 : `--moteur ...`, `--commande ...`, `--etat`, `--verifier-cle`.

Chaque envoi sur la branche `interface-avalonia` compile sur Linux et Windows ; les captures et le journal
sont sur la branche `captures-avalonia`. Sous Windows, des essais en lecture seule (état du PC, analyse complète
en simulation, captures avec le vrai bilan) sont poussés sur la branche `captures-avalonia-windows`,
et le programme est disponible en artefact de l'action « Nouvelle interface ».
