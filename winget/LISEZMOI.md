# Velox dans winget (catalogue officiel de Microsoft)

Ces trois fichiers décrivent Velox pour `winget install Velox`. Pour chaque nouvelle version :

1. Publier la version sur GitHub avec `Velox-Setup-X.exe`.
2. Dans `DamienCoussot.Velox.installer.yaml` : mettre la version, l'adresse du fichier et son SHA256
   (celui du fichier `.sha256`). Dans les deux autres : la version.
3. Proposer les fichiers au catalogue : dépôt https://github.com/microsoft/winget-pkgs, dossier
   `manifests/d/DamienCoussot/Velox/X/` (le plus simple : l'outil officiel `wingetcreate`, voir plus bas).

Avec wingetcreate (Windows, une fois : `winget install wingetcreate`) :

    wingetcreate update DamienCoussot.Velox --version X --urls https://github.com/damiencoussot-ai/nettoyage-pc/releases/download/vX/Velox-Setup-X.exe --submit

(la toute première fois : `wingetcreate submit winget` dans ce dossier, une fois les fichiers remplis)
