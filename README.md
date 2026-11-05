# Velox

Nettoyage, accélération et sécurité pour Windows 10 et 11 : chaque action est expliquée, réversible, et rien ne se fait sans votre accord.

- **Site officiel** : https://damiencoussot-ai.github.io/nettoyage-pc/
- **Télécharger la dernière version** : https://github.com/damiencoussot-ai/nettoyage-pc/releases/latest
- Chaque version est publiée avec son empreinte SHA256 (fichier `.sha256`).

Version gratuite sans publicité ni pistage. Velox Premium : 4,99 € TTC par mois, sans engagement, 3 PC.

Informations légales : [conditions de vente](https://damiencoussot-ai.github.io/nettoyage-pc/conditions-de-vente.html) · [contrat de licence](https://damiencoussot-ai.github.io/nettoyage-pc/licence.html) · [confidentialité](https://damiencoussot-ai.github.io/nettoyage-pc/confidentialite.html) · [mentions légales](https://damiencoussot-ai.github.io/nettoyage-pc/mentions-legales.html)

Windows est une marque de Microsoft Corporation. Velox est un logiciel indépendant, ni affilié à Microsoft ni approuvé par Microsoft.

## Code

- `nouvelle-interface/` : l'application (Avalonia, .NET 10), compilée et testée sur Windows par GitHub Actions.
- `src/Noyau`, `src/Moteur` : le moteur (nettoyage, analyses, licence...), compilé dans l'application.
- La version 4 (Windows Forms) a été retirée du code en 5.7 : la version 5 la remplace entièrement,
  et les PC encore en version 4 reçoivent la version 5 par leur mise à jour habituelle.
