# PlayStead — Catalogue canonique des jeux
## Spécification d’architecture

**Date :** 16 septembre 2026
**Statut :** Design validé
**Projet :** PlayStead
**Branche cible :** `feat/0.4.1-media-foundation`

---

## 1. Objectif

PlayStead doit disposer d’une identité de jeu stable, universelle et indépendante des boutiques.

Un AppID Steam, un ProductId GOG, un identifiant Epic, IGDB, SteamGridDB ou RAWG ne constitue pas l’identité d’un jeu dans PlayStead. PlayStead possède sa propre identité canonique.

Exemple :

```text
PlayStead-001284
Gray Zone Warfare

Références externes
├─ Steam       → 2479810
├─ GOG         → identifiant GOG
├─ Epic        → identifiant Epic
├─ IGDB        → identifiant IGDB
└─ SteamGridDB → identifiant SteamGridDB
```

Le même jeu doit obtenir exactement le même `PlaySteadGameId` chez tous les utilisateurs.

```text
Chez Nagalty
Steam 2479810 → PlayStead-001284

Chez Michel
Steam 2479810 → PlayStead-001284
```

Un client PlayStead ne peut jamais créer lui-même un identifiant canonique mondial définitif.

---

## 2. Principes architecturaux

Le système respecte les principes suivants :

1. PlayStead possède son propre espace d’identités canoniques.
2. Les identifiants Steam, GOG, Epic, IGDB, SteamGridDB, RAWG, etc. sont des références externes.
3. Les faux négatifs sont préférés aux mauvaises fusions.
4. Un titre identique ou très proche ne suffit jamais à fusionner automatiquement deux jeux.
5. PlayStead reste entièrement utilisable hors connexion pour les jeux déjà connus localement.
6. L’absence de réseau ne doit jamais empêcher un scan local, l’affichage de la bibliothèque ou le lancement d’un jeu.
7. Les jeux inconnus ou non résolus peuvent recevoir une identité locale provisoire.
8. Une identité provisoire doit pouvoir être réconciliée plus tard sans perte de données.
9. Les opérations de fusion, redirection, séparation et migration ne doivent jamais supprimer silencieusement des données utilisateur.
10. Toute association importante du catalogue conserve sa provenance.
11. Les décisions `UserConfirmed` et `UserRejected` sont persistantes.
12. Les données utilisateur locales sont séparées du catalogue canonique mondial.
13. Le format du catalogue reste portable vers une autre infrastructure.
14. Les mises à jour du catalogue sont transactionnelles, vérifiées et récupérables.
15. Les identifiants publics PlayStead ne sont jamais recyclés.
16. Le client peut proposer de nouveaux contenus, mais seul le serveur PlayStead peut attribuer un identifiant canonique global définitif.

---

## 3. Niveaux d’identité

PlayStead utilise trois niveaux d’identité distincts.

### 3.1 `LocalGameId`

`LocalGameId` est l’identité technique locale utilisée par la base utilisateur.

Il reste adapté aux relations locales, à la compatibilité avec l’existant et aux données propres à une installation de PlayStead.

Un `LocalGameId` ne doit pas être réinterprété comme identifiant mondial.

Exemple :

```text
LocalGameId
= 54f8... UUID

CanonicalPlaySteadId
= PlayStead-001284
```

### 3.2 `PlaySteadGameId`

Le `PlaySteadGameId` est l’identité publique canonique globale attribuée uniquement par l’autorité centrale du catalogue.

Exemple :

```text
PlayStead-001284
```

Il est :

- stable ;
- globalement unique ;
- indépendant des boutiques ;
- identique sur tous les clients PlayStead ;
- immuable ;
- jamais réutilisé.

Le catalogue possède également un identifiant technique interne immuable, par exemple UUID ou ULID, séparé de l’identifiant public lisible.

### 3.3 `PS-TEMP`

Si un jeu ne peut pas être résolu, le client crée un identifiant local provisoire :

```text
PS-TEMP-<ULID>
```

Exemple :

```text
PS-TEMP-01K5...
```

Un `PS-TEMP` n’est jamais considéré comme un identifiant canonique global définitif.

Il peut apparaître lorsque :

- la machine est hors connexion ;
- `catalog.db` est trop ancien ;
- le jeu est absent du catalogue canonique ;
- le cas est ambigu ;
- les preuves disponibles sont insuffisantes.

Quand une identité canonique est trouvée :

```text
PS-TEMP-01K5...
        ↓
PlayStead-001284
```

la migration préserve toutes les données locales concernées.

---

## 4. Architecture offline-first

PlayStead sépare la base utilisateur et le catalogue canonique.

```text
playstead.db
├─ jeux locaux
├─ installations
├─ sessions
├─ corrections
├─ notifications
├─ UserConfirmed / UserRejected
├─ identités provisoires
└─ historique des migrations

catalog.db
├─ contenus canoniques
├─ PlaySteadGameId
├─ références provider
├─ alias
├─ relations entre contenus
├─ redirections
└─ métadonnées du catalogue
```

`catalog.db` ne contient aucune donnée personnelle.

Le client le considère principalement en lecture seule.

Un contenu inconnu découvert localement reste dans `playstead.db` sous forme provisoire jusqu’à sa résolution.

---

## 5. Catalogue embarqué

Chaque version de PlayStead embarque une version complète récente de `catalog.db`.

Exemple :

```text
PlayStead 0.5.0
└─ catalog.db v138
```

Le fichier local grandit naturellement au fur et à mesure que le catalogue mondial s’enrichit.

Ainsi, une installation fraîche de PlayStead sans Internet peut résoudre immédiatement tous les jeux déjà connus dans la version embarquée.

Les artworks ne sont pas intégrés directement dans `catalog.db`. Le catalogue stocke uniquement les identifiants, références et métadonnées nécessaires pour les retrouver.

---

## 6. Synchronisation du catalogue

Quand Internet est disponible, PlayStead peut actualiser `catalog.db` indépendamment des mises à jour de l’application.

Exemple :

```text
catalog.db v138
   │
   ├─ delta 138 → 139
   ├─ delta 139 → 140
   └─ delta 140 → 141
          │
          ▼
     catalog.db v141
```

Si le catalogue local est trop ancien, PlayStead télécharge directement une version complète récente au lieu d’appliquer une longue chaîne de deltas.

Procédure de mise à jour :

```text
1. Télécharger le catalogue complet ou le delta candidat.
2. Vérifier la compatibilité du schéma.
3. Vérifier la version du catalogue.
4. Vérifier le hash cryptographique.
5. Vérifier la signature.
6. Appliquer dans un emplacement temporaire.
7. Valider la base résultante.
8. Remplacer atomiquement le catalogue actif.
9. Ne supprimer l’ancienne version qu’après succès complet.
```

En cas d’échec, le catalogue précédent reste actif et PlayStead continue de fonctionner.

---

## 7. Modèle canonique de contenu

Le catalogue s’appuie sur une abstraction générale `CatalogContent`.

Conceptuellement :

```text
CatalogContent
├─ InternalContentId
├─ PublicId
├─ ContentKind
├─ CanonicalTitle
├─ NormalizedTitle
├─ ReleaseDate
├─ Developer
├─ Publisher
├─ Status
└─ RedirectTarget
```

Les deux types principaux visibles côté utilisateur sont :

```text
Game
DLC
```

Les contenus techniques ou auxiliaires sont reliés aux jeux via des relations explicites et ne polluent pas la bibliothèque comme des jeux autonomes.

---

## 8. Jeux

Un produit jouable de manière autonome reçoit un `PlaySteadGameId`.

Exemple :

```text
PlayStead-001284
Type = Game
```

Les données communes au jeu s’attachent à l’identité canonique :

```text
PlaySteadGame
├─ métadonnées
├─ identité artwork commune
├─ sessions PlayStead
├─ statistiques
├─ références provider
└─ relations
```

Les données spécifiques à une boutique ou à une installation restent provider-specific.

---

## 9. DLC

Un DLC reste un DLC, qu’il soit narratif, cosmétique, gameplay, armes, skins, soundtrack ou autre.

Exemples :

```text
DLC narratif   → DLC
DLC cosmétique → DLC
DLC gameplay   → DLC
```

Un DLC peut recevoir une identité publique :

```text
PlayStead-DLC-000417
```

et être rattaché à son jeu via :

```text
RequiresBaseGame
```

Une catégorie secondaire facultative peut être utilisée pour l’affichage ou les filtres :

```text
Narrative
Cosmetic
Gameplay
Soundtrack
Other
```

Cette catégorie secondaire ne change jamais le type fondamental.

Par défaut, la fiche d’un jeu affiche uniquement les DLC possédés ou détectés.

Une action **Voir tous les DLC** permet d’afficher l’ensemble des DLC connus du catalogue.

Un DLC classique n’a pas son propre temps de jeu. Son activité appartient au jeu de base.

---

## 10. Extensions standalone

Une extension réellement jouable sans posséder ou lancer le jeu de base est considérée comme un `Game`.

Elle reçoit donc son propre `PlaySteadGameId` et peut être reliée au jeu d’origine via :

```text
StandaloneExpansionOf
```

---

## 11. Éditions et packages commerciaux

Standard, Deluxe, Gold, Ultimate, Supporter, Collector, « Merguez Edition », « Caviar Edition » ou toute autre édition commerciale restent le même jeu canonique.

```text
Standard Edition
Ultimate Edition
Merguez Edition
Caviar Edition

→ même PlaySteadGameId
```

Les offres commerciales sont représentées séparément :

```text
Package
├─ Provider
├─ ExternalPackageId
├─ Name
└─ Includes
   ├─ Game
   └─ DLC
```

Une édition ne peut jamais créer un nouveau `PlaySteadGameId` uniquement parce qu’elle inclut davantage de contenu.

---

## 12. Remasters et remakes

Une édition enrichie ou un bundle reste le même jeu.

Un remaster vendu comme produit distinct et possédant sa propre identité/exécution reçoit un nouveau `PlaySteadGameId` avec :

```text
RemasterOf
```

Un remake complet reçoit un nouveau `PlaySteadGameId` avec :

```text
RemakeOf
```

Exemple :

```text
PlayStead-001000  Original
PlayStead-004000  RemasterOf → PlayStead-001000
PlayStead-008000  RemakeOf   → PlayStead-001000
```

---

## 13. Démos et prologues

Les démos et prologues sont des contenus distincts reliés au jeu complet.

Relations :

```text
DemoOf
PrologueOf
```

L’identifiant provider d’une démo ne doit jamais être interprété comme celui du jeu complet.

---

## 14. Playtests, PTU et clients de test

Un playtest, PTU, EPTU, experimental client ou canal de test n’est pas considéré comme un nouveau jeu lorsqu’il s’agit simplement d’un environnement de test du même produit.

Relation :

```text
TestClientOf
```

Exemple :

```text
Star Citizen
├─ LIVE
├─ PTU
└─ EPTU
```

Les installations et les sessions peuvent conserver un canal distinct, tout en restant liées au même jeu canonique.

---

## 15. Serveurs dédiés, SDK et outils

Les serveurs dédiés, éditeurs, SDK, mod tools et outils similaires sont des contenus techniques liés au jeu.

Relations :

```text
DedicatedServerOf
ToolFor
```

Ils sont masqués de la bibliothèque normale par défaut.

Ils ne deviennent un `Game` autonome que s’ils sont réellement jouables indépendamment.

---

## 16. Références provider

Les services externes sont représentés par `ProviderRef`.

Conceptuellement :

```text
ProviderRef
├─ ContentId
├─ Provider
├─ ExternalId
├─ ExternalType
├─ Provenance
├─ Confidence
└─ ObservedAtUtc
```

Providers possibles :

```text
Steam
GOG
Epic
Microsoft
itch.io
IGDB
SteamGridDB
RAWG
```

Une référence provider canonique ne peut pointer que vers un seul contenu canonique actif.

---

## 17. Sources externes

PlayStead reste indépendant de ses sources externes.

Rôle prévu :

```text
IGDB
→ source primaire d’identité/corrélation multi-store
→ source de métadonnées

SteamGridDB
→ source spécialisée artworks
→ covers / heroes / logos / grids

RAWG
→ source secondaire de vérification / fallback
```

Aucun identifiant externe ne devient l’identité canonique PlayStead.

Si une source disparaît, le `PlaySteadGameId` reste stable.

---

## 18. Provenance et preuves

Les associations et données importantes gardent leur provenance.

Conceptuellement :

```text
CatalogEvidence
├─ EvidenceId
├─ ContentId
├─ ClaimType
├─ ClaimValue
├─ Source
├─ SourceReference
├─ Confidence
├─ ObservedAtUtc
└─ Status
```

Statuts :

```text
Active
Superseded
Rejected
```

Exemples de provenance :

```text
IGDB
SteamGridDB
RAWG
ProviderDirect
UserSubmitted
UserConfirmed
UserRejected
AdminConfirmed
CatalogMigration
```

Une contradiction entre deux sources n’écrase jamais silencieusement l’ancienne information.

---

## 19. `GameIdentityResolver`

Le resolver répond à la question :

> À quelle identité canonique PlayStead rattacher ce produit ou cette installation observée ?

Résultats possibles :

```text
MATCH_CONFIRMED
MATCH_PROBABLE
AMBIGUOUS
NEW
```

### `MATCH_CONFIRMED`

Autorisé uniquement avec des preuves fortes et déterministes.

Exemples :

- association canonique déjà connue ;
- mapping provider déjà validé ;
- décision `UserConfirmed` persistante ;
- mapping interne PlayStead explicitement approuvé ;
- identifiant externe partagé considéré comme déterministe selon la politique active.

Le rattachement peut être automatique.

### `MATCH_PROBABLE`

Plusieurs signaux concordent fortement, mais ne suffisent pas pour une fusion automatique.

Exemples de signaux :

- titre normalisé ;
- développeur ;
- éditeur ;
- année/date de sortie ;
- IDs IGDB / SteamGridDB / RAWG concordants mais non déterministes ;
- autres métadonnées convergentes.

Le résultat crée ou conserve un `PS-TEMP` et génère une notification non bloquante.

### `AMBIGUOUS`

Plusieurs candidats sont plausibles, les signaux sont contradictoires ou les preuves sont insuffisantes.

Aucune fusion automatique.

### `NEW`

Aucun candidat crédible n’existe.

Le client conserve un `PS-TEMP` jusqu’à résolution ou création canonique côté serveur.

### Règle absolue

Un score probabiliste, même très élevé, ne peut jamais à lui seul produire `MATCH_CONFIRMED`.

---

## 20. Décisions utilisateur

Quand le resolver ne peut pas confirmer automatiquement un match mais que la probabilité est élevée, PlayStead demande l’avis de l’utilisateur en dernier recours via le Notification Center.

Deux décisions sont persistées :

```text
UserConfirmed
UserRejected
```

`UserConfirmed` devient une preuve positive persistante.

`UserRejected` devient une relation négative persistante afin que PlayStead ne repose pas la même question à chaque scan.

L’utilisateur peut ultérieurement annuler ces décisions dans l’écran de gestion des identités.

---

## 21. Notification Center

Le Notification Center est un sous-système générique indépendant du resolver.

Conceptuellement :

```text
Notification
├─ NotificationId
├─ Type
├─ Severity
├─ Title
├─ Message
├─ CreatedAtUtc
├─ ReadAtUtc
├─ ResolvedAtUtc
├─ Status
├─ RequiresAction
├─ Source
└─ Payload
```

Statuts :

```text
Unread
Read
Resolved
```

Le badge de la cloche affiche le nombre de notifications actives non résolues, même si elles ont déjà été lues.

Rétention :

```text
Unread / Read non résolues
→ conservation illimitée

Resolved
→ conservation 90 jours

Suppression manuelle
→ possible
```

La cloche apparaît dans la barre supérieure, sur le modèle d’un centre de notifications discret.

Le Notification Center ne doit jamais bloquer :

- un scan ;
- le lancement d’un jeu ;
- la détection de session ;
- la synchronisation du catalogue.

---

## 22. Notification de match probable

Exemple :

```text
Possible correspondance détectée

Cyberpunk 2077
Steam ↔ GOG

PlayStead estime qu’il s’agit probablement du même jeu.

[Confirmer]
[Ce sont deux jeux différents]
[Plus tard]
```

Résultat :

```text
Confirmer
→ UserConfirmed
→ rattachement / fusion
→ notification Resolved

Ce sont deux jeux différents
→ UserRejected
→ association négative persistante
→ notification Resolved

Plus tard
→ notification reste active
```

---

## 23. Contributions des clients

Un client peut proposer automatiquement un jeu inconnu au serveur PlayStead, mais il ne peut jamais créer lui-même un `PlaySteadGameId` définitif.

Flux :

```text
Jeu inconnu
    ↓
PS-TEMP
    ↓
ClientSubmission
    ↓
Serveur PlayStead
```

Réponses possibles :

```text
ExistingMatch
AutoAccepted
ReviewRequired
Rejected
Merged
```

Principe :

> Les clients enrichissent le catalogue ; le serveur décide de l’identité canonique.

---

## 24. Données autorisées dans une contribution

Le client peut transmettre uniquement des informations nécessaires à l’identification du contenu.

Exemples :

```text
Provider
ExternalGameId
titre observé
version / édition éventuelle
IGDB ID éventuel
SteamGridDB ID éventuel
RAWG ID éventuel
développeur
éditeur
date de sortie
version du catalogue local
```

Ne sont pas envoyés automatiquement :

```text
chemin utilisateur complet
nom du compte Windows
liste complète des processus
temps de jeu personnel
historique des sessions
liste de fichiers du disque
données privées sans rapport avec le catalogue
```

Si un exécutable constitue un signal utile, seule l’information strictement nécessaire est extraite localement, par exemple son nom, jamais le chemin personnel complet.

---

## 25. Catalogue central

Le serveur PlayStead est l’unique autorité pouvant créer un identifiant canonique global définitif.

Le catalogue central contient notamment :

```text
CatalogContent
ProviderRefs
ContentRelations
CatalogEvidence
Redirects
ClientSubmissions
ReviewQueue
CatalogAuditLog
CatalogVersions
```

Les créations automatiques d’un nouvel ID doivent être rares et fondées sur des preuves strictes.

En cas de doute :

```text
→ REVIEW_REQUIRED
```

plutôt qu’une mauvaise création ou fusion.

---

## 26. Ingestion des sources externes

Pipeline prévu :

```text
IGDB ───────────────┐
Steam ──────────────┤
GOG ────────────────┤
Epic ───────────────┤
SteamGridDB ────────┤
RAWG ───────────────┤
Contributions client┤
Validation admin ───┘
        │
        ▼
Ingestion
        │
        ▼
Normalisation
        │
        ▼
Résolution d’identité
        │
        ├─ preuve suffisante → rattachement / création
        ├─ conflit           → REVIEW_REQUIRED
        └─ doublon           → MERGE
        │
        ▼
Catalogue canonique PlayStead
```

---

## 27. Audit du catalogue

Chaque modification importante du catalogue central doit être traçable.

Conceptuellement :

```text
CatalogAuditLog
├─ Actor / Source
├─ Operation
├─ PreviousValue
├─ NewValue
├─ Reason
└─ TimestampUtc
```

Une mauvaise fusion doit pouvoir être comprise, corrigée et auditée.

---

## 28. Fusion d’identités

Si deux identités canoniques sont confirmées comme représentant le même jeu, PlayStead conserve un survivant déterministe.

Règle par défaut :

> Le plus ancien `PlaySteadGameId` survit.

Exemple :

```text
Avant

PlayStead-001284
└─ Steam

PlayStead-008731
└─ Epic

Après

PlayStead-001284
├─ Steam
└─ Epic

PlayStead-008731
→ Redirect → PlayStead-001284
```

L’identifiant absorbé n’est jamais recyclé.

---

## 29. Redirections

Une redirection permet à une ancienne référence de continuer à fonctionner.

```text
PlayStead-008731
→ PlayStead-001284
```

Une chaîne de redirections doit toujours aboutir à un contenu actif.

Les boucles sont interdites.

---

## 30. Réconciliation d’un `PS-TEMP`

Quand un contenu provisoire reçoit une identité canonique :

```text
PS-TEMP-01K...
→ PlayStead-001284
```

PlayStead remappe transactionnellement :

- installations ;
- sessions ;
- corrections ;
- médias ;
- notifications ;
- relations locales pertinentes.

Le `PS-TEMP` est conservé comme redirect historique local si nécessaire.

---

## 31. Séparation après mauvaise fusion

Une mauvaise fusion doit pouvoir être réparée.

Exemple :

```text
PlayStead-001284
├─ Steam A
└─ GOG B
```

Si `GOG B` est finalement un autre jeu :

```text
PlayStead-001284
└─ Steam A

PlayStead-009452
└─ GOG B
```

Le serveur distribue une opération de type `Split`.

Les données historiques clairement attribuables à une installation ou provider peuvent être remappées automatiquement.

Les données ambiguës ne sont jamais déplacées sur une simple supposition.

---

## 32. Journal local des migrations d’identité

`playstead.db` garde la trace des changements d’identité appliqués.

Conceptuellement :

```text
IdentityMigrationLog
├─ MigrationId
├─ Type
├─ SourceId
├─ TargetId
├─ AppliedAtUtc
└─ CatalogVersion
```

Types possibles :

```text
TempResolved
Merge
Split
RedirectApplied
```

Les opérations sont idempotentes.

Un redémarrage ou une réapplication du même delta ne doit jamais provoquer de duplication ou perte de données.

---

## 33. Gestion manuelle des identités

PlayStead propose un écran **Identités & relations**.

Il permet de consulter :

- l’identité canonique ;
- les provider refs ;
- leur provenance ;
- les décisions utilisateur ;
- les relations de contenu ;
- les redirects ;
- l’historique des migrations.

Actions possibles :

- annuler un `UserRejected` ;
- confirmer manuellement que deux entrées représentent le même jeu ;
- signaler une mauvaise association ;
- séparer localement une association erronée ;
- consulter les raisons d’un rapprochement.

Une correction locale n’est jamais équivalente à une modification directe du catalogue mondial.

Le client peut appliquer immédiatement une correction à sa bibliothèque puis transmettre un signal de correction au serveur.

---

## 34. Affichage des DLC

Dans la fiche d’un jeu :

```text
DLC
├─ DLC possédé/détecté
├─ DLC possédé/détecté
└─ Voir tous les DLC
```

Le catalogue complet des DLC n’est pas affiché par défaut afin d’éviter de transformer la fiche en vitrine commerciale.

---

## 35. Sécurité et confidentialité

Le catalogue mondial répond à la question :

> Quel contenu existe et à quelle identité canonique correspond-il ?

Il ne répond pas à :

> Qui possède ou joue à ce contenu ?

Ces deux domaines restent séparés.

Les endpoints d’administration du catalogue sont distincts et authentifiés.

Les téléchargements de catalogue sont vérifiés par :

- version de schéma ;
- version du catalogue ;
- hash ;
- signature.

---

## 36. Hébergement V1

La V1 utilise le VPS OVH existant actuellement utilisé pour BKSI, mais PlayStead doit rester totalement isolé du point de vue logiciel et des données.

Le VPS physique peut héberger plusieurs produits :

```text
VPS OVH
│
├─ BKSI
│  ├─ services
│  ├─ base
│  └─ secrets
│
└─ PlayStead
   ├─ Catalog API
   ├─ Catalog DB
   ├─ catalog.db
   ├─ deltas
   └─ secrets
```

PlayStead utilise :

- un service dédié ;
- des répertoires dédiés ;
- une base dédiée ;
- des secrets dédiés ;
- idéalement un compte système ou conteneur dédié ;
- des sauvegardes séparées.

Le VPS BKSI est seulement l’infrastructure physique initiale. Il ne fait pas partie de l’identité publique de PlayStead.

---

## 37. Endpoint V1

La V1 utilise un endpoint HTTPS temporaire sur l’infrastructure existante.

Cette adresse ne doit jamais être affichée dans l’interface PlayStead.

Le client centralise l’URL dans une seule configuration :

```text
CatalogServiceOptions.BaseUri
```

Aucune fonctionnalité ne doit dépendre directement du nom de domaine temporaire.

Après lancement et validation des premiers retours, PlayStead pourra adopter un domaine dédié :

```text
api.playstead.xxx
catalog.playstead.xxx
```

Le changement doit être transparent pour l’architecture.

---

## 38. Domaine PlayStead

L’achat d’un domaine PlayStead est volontairement différé.

Stratégie :

```text
Développement
→ endpoint temporaire

Lancement initial
→ Microsoft Store + GitHub
→ endpoint non exposé dans l’UI

Premiers retours validés
→ achat éventuel du domaine PlayStead
→ migration DNS / BaseUri
```

Le domaine ne doit pas être une dépendance pour la V1.

---

## 39. Distribution de l’application

Le canal principal prévu pour le lancement est le Microsoft Store.

GitHub joue principalement le rôle de :

- dépôt du projet ;
- transparence ;
- historique ;
- issues ;
- documentation ;
- confiance technique.

Le Microsoft Store est le canal principal de découverte, installation et mises à jour pour les premiers utilisateurs.

---

## 40. Invariants

Les règles suivantes doivent toujours rester vraies :

1. Une `ProviderRef` canonique ne pointe que vers un seul contenu actif.
2. Un `PlaySteadGameId` public n’est jamais recyclé.
3. Une redirection finit toujours sur un contenu actif.
4. Les boucles de redirection sont interdites.
5. La résolution d’un `PS-TEMP` ne perd aucune donnée utilisateur.
6. `UserRejected` empêche la reproposition automatique de la même relation.
7. Une fusion automatique ne repose jamais uniquement sur des signaux probabilistes.
8. `catalog.db` peut être remplacé sans altérer les données utilisateur de `playstead.db`.
9. Une panne réseau ne bloque ni la bibliothèque, ni le scan, ni le lancement des jeux déjà connus.
10. Une mise à jour de catalogue invalide n’écrase jamais le dernier catalogue valide.
11. Les migrations d’identité sont idempotentes.
12. Une mauvaise fusion peut être réparée sans réutiliser un ancien ID public.

---

## 41. Stratégie de tests

### 41.1 Tests unitaires du resolver

Exemples :

```text
Steam ID canonique connu
→ MATCH_CONFIRMED

Titre identique uniquement
→ jamais MATCH_CONFIRMED

Plusieurs preuves fortes mais probabilistes
→ MATCH_PROBABLE

UserRejected existant
→ proposition bloquée

Redirect connu
→ cible canonique retournée
```

### 41.2 Tests d’intégration

Couverture minimale :

- `PS-TEMP → PlaySteadGameId` ;
- merge ;
- split ;
- redirects ;
- DLC / Game ;
- packages ;
- remasters ;
- playtests ;
- Notification Center ;
- persistance des décisions utilisateur ;
- mise à jour complète de `catalog.db` ;
- deltas ;
- rollback du catalogue ;
- reprise après interruption.

### 41.3 Tests d’invariants / propriétés

Générer automatiquement de nombreux contenus, provider refs, merges, splits et redirects afin de vérifier :

```text
aucune boucle
aucune ProviderRef canonique dupliquée
aucune donnée orpheline
aucun PublicId réutilisé
aucune migration non idempotente
```

### 41.4 Runtime gates

Scénarios réels obligatoires :

```text
PC online + jeu connu
PC offline + jeu connu
PC offline + jeu inconnu
retour online + résolution PS-TEMP
match probable + notification
confirmation utilisateur
rejet utilisateur
mise à jour catalog.db
catalogue corrompu → rollback
redirect
merge
split
```

---

## 42. Déploiement par phases

L’architecture est trop importante pour être implémentée en une seule tâche.

### Phase 1 — Canonical Catalog Foundation

Objectif :

- introduire le modèle canonique local ;
- définir les contrats du catalogue ;
- créer `catalog.db` minimal ;
- ajouter version de schéma et version du catalogue ;
- préserver entièrement le comportement actuel ;
- aucune dépendance réseau obligatoire ;
- aucun serveur public requis.

### Phase 2 — Identity Resolver + PS-TEMP + Notification Center

Objectif :

- implémenter `GameIdentityResolver` ;
- introduire `MATCH_CONFIRMED`, `MATCH_PROBABLE`, `AMBIGUOUS`, `NEW` ;
- gérer les `PS-TEMP` ;
- créer le Notification Center générique ;
- persister `UserConfirmed` et `UserRejected`.

### Phase 3 — Central Catalog Service + Sync

Objectif :

- créer le Catalog Service sur le VPS OVH ;
- exposer version, résolution, catalogue complet et deltas ;
- mettre en place la synchronisation transactionnelle ;
- gérer les contributions clientes ;
- conserver une BaseUri centralisée.

### Phase 4 — Ingestion externe

Objectif :

- intégrer IGDB comme source primaire de corrélation ;
- intégrer SteamGridDB pour les artworks ;
- utiliser RAWG comme contrôle/fallback éventuel ;
- mettre en place provenance, review et audit.

### Phase 5 — Relations avancées + nouveaux providers

Objectif :

- DLC complets ;
- packages commerciaux ;
- remaster/remake ;
- demo/prologue/playtest/PTU ;
- dedicated servers et tools ;
- merge/split administrables ;
- activation réelle d’Epic et GOG ;
- affichage du temps de jeu agrégé multi-source après définition de règles de déduplication fiables.

---

## 43. Hors périmètre immédiat

Cette spécification ne signifie pas que tout doit être construit maintenant.

Sont explicitement différés :

- domaine PlayStead dédié ;
- migration du VPS vers une infrastructure propre à PlayStead ;
- Epic complet ;
- GOG complet ;
- synchronisation de compte utilisateur ;
- réseau social ;
- cloud saves ;
- recommandations de jeux ;
- boutique ;
- publicité ;
- analytics utilisateur invasifs ;
- agrégation naïve des temps de jeu multi-provider.

---

## 44. Critères d’acceptation architecturaux

L’architecture sera considérée correctement mise en œuvre lorsque :

1. un même jeu peut partager un `PlaySteadGameId` sur plusieurs providers ;
2. le même provider ID conduit au même `PlaySteadGameId` sur plusieurs machines ;
3. un jeu inconnu peut fonctionner localement avec un `PS-TEMP` ;
4. le retour en ligne peut résoudre ce `PS-TEMP` sans perte de données ;
5. un match probable ne provoque aucune fusion sans preuve suffisante ;
6. une demande utilisateur est non bloquante et persistante ;
7. `catalog.db` fonctionne hors connexion ;
8. le catalogue peut être mis à jour indépendamment de PlayStead ;
9. une mauvaise mise à jour du catalogue peut être annulée ;
10. les fusions et séparations sont traçables ;
11. les DLC, éditions, remasters, playtests et outils ne créent pas de doublons logiques ;
12. l’endpoint réseau peut changer sans modifier l’architecture métier ;
13. PlayStead reste fonctionnel si le serveur central est indisponible.

---

## 45. Décision finale

PlayStead adopte un catalogue canonique mondial dont il contrôle les identités.

Le modèle cible est :

```text
Steam / GOG / Epic / autres
          │
          ▼
     ProviderRefs
          │
          ▼
   PlaySteadGameId
   universel et stable
          │
          ├─ catalog.db offline
          ├─ Catalog Service central
          ├─ GameIdentityResolver
          ├─ Notification Center
          └─ migrations / provenance / audit
```

Les clients peuvent enrichir le système, mais ils ne deviennent jamais l’autorité canonique.

Le catalogue central décide de l’identité globale.

Le client reste offline-first.

L’infrastructure V1 utilise le VPS OVH existant, avec une séparation stricte entre BKSI et PlayStead et sans exposer l’adresse temporaire dans l’interface utilisateur.

Le domaine PlayStead dédié est volontairement différé jusqu’après le lancement et l’analyse des premiers retours utilisateurs.
