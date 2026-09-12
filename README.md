# PlayStead

**Version actuelle : `0.1.0-dev`**

PlayStead est une application Windows locale destinée à construire une bibliothèque de jeux fiable à partir des installations réellement présentes sur la machine. La fondation 0.1 privilégie une approche **local-first** : détection locale, persistance locale et démarrage à partir du cache avant tout rafraîchissement.

## État du projet

La version `0.1.0-dev` constitue la fondation technique de PlayStead. Elle n'est pas la release publique 1.0.

La portée 0.1 est volontairement limitée à **Windows 10/11** et à la détection locale **Steam**. Les autres plateformes et intégrations ne font pas partie de cette fondation.

## Fonctionnalités validées en 0.1

- détection locale des bibliothèques et manifests Steam ;
- modèle provider-neutral pour préparer les futures sources sans les simuler aujourd'hui ;
- persistance de la bibliothèque dans **SQLite** ;
- démarrage **cache-first**, puis rafraîchissement local ;
- comportement **single-instance** avec transfert d'invocation vers l'instance principale ;
- activation de la fenêtre principale lors d'une seconde invocation ;
- sauvegarde et restauration de la position, de la taille et de l'état maximisé de la fenêtre ;
- données utilisateur stockées sous `%LOCALAPPDATA%\PlayStead`, jamais à côté de l'exécutable.

## Stack technique

- C# / **.NET 10**
- **WPF**
- SQLite
- Microsoft Generic Host / DI
- xUnit
- PowerShell 7 pour les gates et audits
- GitHub Actions pour la CI Windows

## Architecture

La solution est découpée en cinq projets de production :

- `PlayStead.Core` — domaine, contrats de persistance et coordination de scan ;
- `PlayStead.Data` — SQLite, migrations, health check et persistance ;
- `PlayStead.Platform` — chemins Windows et instance unique ;
- `PlayStead.Providers` — sources locales, actuellement Steam ;
- `PlayStead.UI` — WPF, composition root, runtime et présentation de la bibliothèque.

Les tests sont regroupés dans cinq projets miroirs.

## Données utilisateur

PlayStead utilise le profil local Windows :

```text
%LOCALAPPDATA%\PlayStead
```

Exemples de données persistantes :

```text
%LOCALAPPDATA%\PlayStead\Data\playstead.db
%LOCALAPPDATA%\PlayStead\Data\window-placement.json
```

Aucune base SQLite ni préférence de fenêtre ne doit être créée dans le répertoire de l'application.

## Build et tests

Depuis la racine du repo :

```powershell
dotnet restore PlayStead.sln
dotnet build PlayStead.sln --configuration Release --no-restore /warnaserror
dotnet test PlayStead.sln --configuration Release --no-build
```

Baseline locale validée pour la fondation 0.1 :

```text
89/89 tests PASS
0 warning
0 error
```

La CI Windows applique le même principe de gate sur `windows-latest` avec .NET 10 et résultats TRX.

## Portée actuelle

PlayStead 0.1 n'essaie pas encore d'être un launcher universel ni un remplacement de Steam. Cette étape établit la base locale : modèle de bibliothèque, détection Steam, cache SQLite, cycle de vie WPF, instance unique et persistance de l'état de fenêtre.

Les fonctionnalités ultérieures seront ajoutées progressivement dans les versions `0.x`. La version `1.0` correspondra à la première release publique complète.
