# Nettoyage PC 5 — nouvelle interface (en construction)

Interface refaite avec [Avalonia](https://avaloniaui.net) (.NET 10) : le dessin passe par la carte graphique,
les animations (anneau, menu, transitions) sont fluides et s'arrêtent quand la fenêtre est en arrière-plan.

- La **version 4** (dossier `src`, WinForms, .NET Framework 4.8) reste la version publiée.
- Ici : la fenêtre (menu, en-tête), l'accueil, et des rubriques « à venir ».
  Données d'exemple pour l'instant ; le moteur de la version 4 sera branché ensuite.
- `NettoyagePC --captures DOSSIER` : rendu sans écran de la fenêtre en PNG (thèmes sombre et clair).

Chaque envoi sur la branche `interface-avalonia` compile sur Linux et Windows ; les captures et le journal
sont sur la branche `captures-avalonia`, le programme Windows en artefact de l'action « Nouvelle interface ».
