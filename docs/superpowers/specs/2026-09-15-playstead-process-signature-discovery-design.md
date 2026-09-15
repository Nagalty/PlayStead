# PlayStead — Process Signature Discovery Design

**Date :** 2026-09-15

**Statut :** design autoritaire de Bugfix B ; aucune implémentation livrée par ce document.

**Workspace :** `D:\Dev\PlayStead\worktrees\0.4.1-media-foundation`

**Branche :** `feat/0.4.1-media-foundation`

**Baseline :** `c39201d60d260d512d4059236499aaaef75d4d1e` — `fix(library): marshal session updates to UI dispatcher`.

Cette spec complète la spec Sessions 0.3 et tranche les règles de découverte qu'elle laissait ouvertes. Pour les signatures découvertes automatiquement, les règles de preuve, de matching et de revalidation ci-dessous prévalent sur le matching historique par nom. Les règles de sessions observées, heartbeat, recovery et corrections restent applicables.

## 1. Context

Sources de référence :

- [Sessions 0.3 — design](2026-09-12-playstead-0.3-sessions-design.md), notamment sections 2, 5, 6, 9, 15 à 20.
- [Sessions 0.3 — implementation](../plans/2026-09-12-playstead-0.3-sessions-implementation.md), Tasks 3, 4 et 6.
- `ProcessSignature`, `ProcessSignatureEntry`, `ProcessSignatureMatcher`, `SessionRuntime` dans `PlayStead.Core/Sessions`.
- `SqliteProcessSignatureStore`, migration `003_sessions.sql`, `SqliteLibraryStore` dans Data.
- `WindowsProcessSnapshotSource`, `SteamLocalLibrarySource`, `LocalStartupPipeline`, `GameLaunchService` et `PlaySteadHost`.

L'audit local a trouvé 28 installations, aucune signature et aucune session persistée. Le lancement Steam fonctionne, mais il ne produit aucune association processus/jeu. Bugfix A a corrigé séparément la restitution des notifications Library au Dispatcher WPF.

Task 7 Home Media Integration reste dans `stash@{0}` (`wip/task7-home-media-integration`). Elle ne fait pas partie de B1/B2/B3.

## 2. Problem Statement

Le runtime consomme des signatures déjà persistées. Aucune découverte production ne les crée ; les tests existants injectent leurs signatures. La présence de `Origin = Discovered` ne constitue pas une implémentation d'apprentissage.

Le matcher compare uniquement les noms sans distinction de casse. Il perdrait donc une association fiable par chemin au moment de la persistance. Enfin, `UpsertAsync` peut remplacer une signature sans protéger son origine.

Il faut apprendre une association expliquable et durable sans transformer chaque exécutable d'une installation en jeu suivi.

## 3. Product Principles

- **Silent learning** : l'utilisateur lance normalement ses jeux. Aucune question obligatoire « quel .exe est le jeu ? », aucun popup de confirmation.
- **False negative > false positive** : doute ou ambiguïté implique `NO SIGNATURE` ; accumuler plus de preuves ne force jamais une promotion.
- Une observation, une décision et une signature acceptée sont trois objets différents.
- L'inventaire, le nom, la taille, le suffixe `Shipping`, le premier processus et la durée ne prouvent jamais seuls `Main`.
- Aucun temps passé n'est reconstruit depuis les épisodes d'apprentissage.
- Les décisions ont des raisons structurées ; aucun score numérique opaque.

## 4. Goals

1. Inventorier localement les exécutables sous les installations présentes.
2. Accumuler silencieusement des preuves sur plusieurs épisodes observés, y compris entre redémarrages PlayStead.
3. Promouvoir automatiquement seulement une association répondant au contrat strict de la section 10.
4. Conserver le chemin confirmé et protéger les signatures explicites.
5. Réutiliser un seul cycle de capture des processus pour découverte et sessions.
6. Permettre une validation runtime sans données fictives ni règles par jeu.

## 5. Non-Goals

Pas de ML, cloud, base externe de signatures, hooks kernel, ETW avancé, second service ou poller. Pas de fingerprint cryptographique, de PID parent obligatoire, de suivi du contenu des fenêtres ni d'inspection mémoire des jeux.

Pas d'UI de correction dans B1/B2/B3. Une future action humaine utilisera `Origin = Manual`. Pas de modification de Task 7, du temps provider, des règles Steam de lancement ou de `BackgroundServiceExceptionBehavior`.

## 6. Current Architecture

`SteamLocalLibrarySource` lit les manifests puis fournit `DiscoveredInstallation(Provider, ExternalId, Title, InstallPath, InstalledSizeBytes, ObservedAtUtc)`. `SqliteLibraryStore` crée les identités durables et restitue `LibrarySnapshot`.

Steam est actuellement la seule source locale production enregistrée ; les enums Epic/Gog/Manual ne sont pas des scanners implémentés. Le manifest ne fournit pas de principal au modèle générique. Son `LauncherPath` peut désigner Steam lui-même. `GameLaunchService` ouvre une URI ; son succès signifie que la demande a été transmise, pas qu'un principal est présent.

Le schéma courant est en version 5. Les signatures ont un parent par GameId et des entrées ordonnées `ExecutableName / Kind`. Les rôles sont `Main / Auxiliary / Excluded`, les origines `Discovered / Manual / BuiltIn`.

`ProcessSnapshot` fournit PID, nom, chemin nullable et démarrage nullable. Il ne fournit pas de PID parent. `SessionRuntime` capture, matche puis persiste ; deux snapshots principaux confirment une session. Cette confirmation n'est pas un mécanisme de découverte.

## 7. Target Architecture

```text
LibrarySnapshot (installations présentes et identités durables)
    -> inventaire Platform, hors boucle de polling
    -> génération locale de candidats

capture Windows unique par cycle
    -> observations génériques et qualité de capture
    -> coordinateur Core d'apprentissage
    -> politique pure -> décision expliquée
    -> Data : écriture conditionnelle d'une signature acceptée
    -> matching des signatures admissibles sur le même snapshot
    -> transitions/persistance Sessions -> UI existante
```

L'inventaire est préparé après chargement du snapshot Library initial puis après un scan/import pertinent. Il ne bloque pas l'affichage du cache Library. Tant qu'il n'est pas complet et valide, l'installation ne peut pas être promue.

Le lancement PlayStead peut ajouter un `LaunchIntent` avant l'ouverture de l'URI. Il ne capture pas les processus une seconde fois. La dernière capture du monitor sert de référence ; si elle ne permet pas de prouver l'état préalable, l'épisode n'est pas complet. Les lancements externes utilisent les mêmes transitions absent/présent/absent, sans dépendre d'un événement Steam.

## 8. Domain Model

Les noms ci-dessous fixent les concepts ; les fichiers et signatures C# précis seront arrêtés dans le plan d'implémentation.

| Concept | Données indispensables |
|---|---|
| InstallationScope | GameId, InstallationId, racine canonique, présence, génération d'inventaire |
| ExecutableCandidate | chemin canonique complet, nom, révision fichier, éventuels indices d'exclusion et leurs raisons |
| FileRevision | taille en octets et LastWriteTimeUtc ; absence/lecture impossible est un résultat distinct |
| ProcessObservation | identité PID + StartedAtUtc, chemin fiable, numéro du snapshot, instant observé |
| CaptureQuality | capture complète/incomplète, rupture connue de continuité, identités nouvelles non attribuables |
| LearningEpisode | EpisodeId, scope/génération, bornes réellement observées, intervalles des candidats, qualité et raisons |
| LearningState | version de politique, génération, premier épisode qualifiant conservé, éventuel épisode de confirmation, état courant |
| DiscoveryDecision | PromoteMain, InsufficientEvidence ou Ambiguous ; candidat éventuel et raisons |
| AcceptedSignature | ProcessSignature existante, enrichie du chemin et des informations de validation des entrées découvertes |

Une seule identité de candidat est promue `Main` par décision automatique initiale. Le support historique de plusieurs principaux n'autorise pas à transformer plusieurs candidats ambigus en autant de principaux.

Les origines sont conservées. Priorité d'autorité : `Manual > BuiltIn > Discovered`. Il s'agit d'une priorité d'écriture et de validation, pas d'un classement automatique entre noms d'exécutables.

## 9. Evidence Model

**Inventaire :** chemins résolus sous une racine connue, révisions, exhaustivité, liens/reparse points refusés si leur destination n'est pas vérifiable, et métadonnées locales descriptives facultatives.

**Observation :** apparition après un état absent connu, identité stable PID+démarrage, présence sur plusieurs captures, disparition observée et absence confirmée. Aucun lien parent/enfant n'est inventé.

**Exclusion :** une règle générique explicite peut disqualifier un installateur, reporter, outil de développement, serveur ou composant auxiliaire reconnu. Les règles négatives sont versionnées, nommées et testées. Elles ne sélectionnent jamais implicitement un principal. Un motif peut produire un faux négatif ; il ne doit jamais augmenter la confiance d'un autre fichier à lui seul.

**Concurrence :** tous les exécutables non exclus de l'inventaire restent des candidats plausibles, y compris ceux qui n'ont pas été observés. L'absence d'observation n'est pas une preuve qu'un fichier est inoffensif. Un candidat non expliqué peut donc bloquer durablement la promotion.

**Qualité :** PID sans démarrage fiable, chemin inaccessible, inventaire incomplet, suspension/reprise, capture échouée ou identité nouvelle au chemin inconnu durant l'épisode rendent celui-ci non qualifiant. Ce dernier choix peut refuser un épisode à cause d'un processus sans rapport ; c'est le compromis conservateur retenu sans parenté fiable.

Les champs de version, taille et durée servent à contextualiser ou invalider. Aucun d'eux ne prouve une sémantique de jeu. Les observations répétées ne transforment pas une ambiguïté persistante en certitude.

## 10. Promotion Policy

### 10.1 Épisode complet et qualifiant

Un épisode d'installation commence après **deux snapshots consécutifs** sans processus candidat attribuable non exclu, puis une apparition. Il finit après **deux snapshots consécutifs** où tous ces processus non exclus ont disparu. Les processus explicitement exclus ne déterminent pas ces bornes ; un service exclu persistant ne maintient pas l'épisode ouvert. Le nombre deux réutilise l'anti-transitoire existant ; il ne constitue pas un seuil de confiance sémantique.

Un démarrage de PlayStead alors que le candidat est déjà présent donne un épisode partiel, non qualifiant. Un arrêt/crash de PlayStead avant observation de fin ne termine jamais artificiellement un épisode.

Un épisode qualifiant possède un inventaire complet inchangé, des chemins et identités fiables, et aucune rupture connue. Le candidat principal proposé doit être observé sur au moins deux captures consécutives et jusqu'à la dernière capture positive de l'épisode.

Pour chaque autre candidat non exclu, une seule explication automatique est admise en première version : **compagnon de démarrage observé**. Sa première apparition doit précéder ou coïncider avec celle du principal proposé. Il doit disparaître avant ce principal, puis rester absent sur au moins deux captures consécutives où ce principal demeure présent, sans réapparaître avant la fin. Il n'est pas persisté comme auxiliaire ni exclu globalement sur cette seule base.

Un candidat non observé et non exclu, deux survivants équivalents, un concurrent apparaissant après le principal proposé ou une relation incompatible ne sont pas résolus par cette règle. L'épisode reste ambigu. Un anti-cheat vivant pendant toute la durée du jeu reste notamment concurrent.

### 10.2 Prédicat exact de promotion

La politique retourne `PromoteMain(C)` si et seulement si toutes les conditions suivantes sont réunies :

1. Aucune signature Manual/BuiltIn n'existe pour le jeu ; l'état Data permet une insertion ou une revalidation conditionnelle de Discovered.
2. Le scope désigne une installation présente, à racine fiable et non ambiguë. Un chemin qui peut être attribué à plusieurs installations n'est pas promu.
3. C est un fichier inventorié sous cette racine, non exclu, avec chemin confirmé et révision lisible inchangée.
4. **Deux épisodes complets distincts et non chevauchants** du même scope, de la même génération et de la même version de politique proposent exactement C.
5. Dans chacun, C satisfait la présence et la fin de la section 10.1 ; tout autre candidat est explicitement exclu ou expliqué comme compagnon de démarrage. Aucun candidat plausible non résolu ne reste.
6. Entre ces deux épisodes, aucun épisode contradictoire, changement d'inventaire ou défaut de qualité n'a été ignoré. Les deux épisodes retenus sont successifs dans la séquence admissible.
7. La révision fichier est relue avant acceptation ; la génération persistée, le jeton de concurrence et la protection d'origine sont vérifiés atomiquement avec l'écriture Data, selon les sections 13 à 17. Cette transaction ne rend pas le filesystem atomique.

Le minimum de deux épisodes est une **design decision justifiée par le prédicat de répétition** : un épisode établit une hypothèse, un épisode indépendant la confirme. Un seul ne peut démontrer une répétition. Aucun troisième lancement arbitraire, probabilité chiffrée ou vote majoritaire n'est introduit. Deux épisodes ne suffisent pas si une seule autre condition manque.

Ce contrat est une inférence conservatrice de comportement, pas une garantie de reconnaissance sémantique de tout logiciel. Les limites de cette inférence font partie du gate, section 24.

### 10.3 Refus expliqués

`Ambiguous` signifie plusieurs identités plausibles, attribution d'installation ambiguë ou concurrence non expliquée. `InsufficientEvidence` signifie preuve incomplète, un seul épisode qualifiant, identité inaccessible ou révision non validée. Dans les deux cas : **NO SIGNATURE nouvellement acceptée**.

Raisons minimales : `IncompleteInventory`, `UnreliablePath`, `PartialEpisode`, `CaptureGap`, `UnknownProcessIdentity`, `UnobservedCompetitor`, `EquivalentCandidates`, `ConflictingEpisodes`, `RevisionChanged`, `ProtectedSignature`, `AwaitingIndependentEpisode`.

Une décision de refus ne remplace jamais la signature d'un autre type. Une Discovered devenue invalide est suspendue par le mécanisme explicite de revalidation, pas effacée par une simple décision ambiguë.

## 11. Learning Across Launches

L'état de l'épisode courant appartient à un coordinateur Core singleton, piloté séquentiellement par le monitor. Les preuves d'épisodes **terminés** sont persistées localement afin de survivre au redémarrage PlayStead ; les observations brutes en cours restent en mémoire.

Par installation et génération, conserver au maximum l'épisode qualifiant de référence et son épisode indépendant de confirmation. Après un épisode contradictoire ou une rupture, effacer la référence qualifiante ; ne pas sélectionner deux anciens succès en ignorant les échecs intermédiaires. Une ambiguïté répétée ne s'accumule pas en faveur d'un candidat.

Un redémarrage abandonne l'épisode incomplet, recharge les résumés terminés et exige un nouvel état absent connu avant un nouvel épisode. Le redémarrage seul ne vaut pas second lancement.

La promotion intervient après la clôture du second épisode qualifiant. Les sessions correspondantes ne sont pas recréées : le suivi démarre lors d'une observation future selon les deux snapshots habituels. C'est un faux négatif initial accepté. Un lancement explicite et un lancement externe peuvent fournir les deux épisodes, s'ils satisfont le même contrat.

## 12. Matching by Path

Étendre `ProcessSignatureEntry` avec **`ExecutablePath` nullable**. Toute nouvelle entrée Main Discovered exige un chemin confirmé non vide. Le chemin est fourni canonisé par Platform ; Core compare les identités normalisées sans interroger Windows.

Avec un chemin : correspondance sur le chemin complet, sans distinction de casse Windows, et vérification du nom cohérent. Aucun fallback au nom si le chemin observé est absent ou différent. Pour une entrée Discovered, la révision et le statut de validation doivent aussi être admissibles avant utilisation par le runtime ; ces métadonnées d'apprentissage ne sont pas exigées des entrées Manual/BuiltIn.

Sans chemin : le fallback historique par nom est conservé **uniquement pour Manual et BuiltIn**, afin de maintenir les contrats explicites existants. Une ancienne Discovered sans chemin reste lisible mais est `NeedsRevalidation`, non admissible au matching automatique.

Platform vérifie les limites réelles de répertoire : `C:\Games\FooBar` n'appartient pas à `C:\Games\Foo`. Un chemin relatif, une traversée ou une jonction non résolue ne devient pas une preuve. Deux fichiers de même nom dans deux sous-dossiers restent deux candidats distincts.

## 13. Persistence

Le store des signatures existant reste l'autorité. Une extension additive prévoit :

- chemin nullable des entrées ; révision fichier validée nullable des entrées Discovered ;
- métadonnées Discovered : InstallationId d'origine, génération validée, version de politique, état `Valid / NeedsRevalidation`, jeton de révision pour concurrence ;
- état local d'apprentissage par installation : génération, version de politique, épisode de référence et éventuel épisode de confirmation avec identifiants et raisons.

La génération est une identité locale renouvelée lorsque la racine ou l'inventaire change ; ce n'est pas un score ni un hash cryptographique. Pour comparer l'inventaire entre démarrages, persister sa liste ordonnée de chemins/révisions et l'exhaustivité. Les résumés d'épisodes conservent les intervalles positifs/absents nécessaires au prédicat, sans journal de tous les processus Windows.

Écrire aux frontières d'épisode et aux changements de validation, pas à chaque snapshot. L'acceptation d'une signature et la consommation des preuves correspondantes sont une transaction unique. En cas de crash avant commit, aucune signature partiellement promue n'est visible.

L'API d'apprentissage est distincte de `UpsertAsync` destiné aux écritures explicites : `TryInsertDiscoveredIfAbsentAsync` pour la première acceptation, et une opération conditionnelle de revalidation pour une Discovered existante. Le nom final peut suivre le style du store ; les garanties atomiques sont obligatoires.

## 14. Manual/BuiltIn Protection

Une découverte ne remplace jamais Manual ou BuiltIn, y compris si elle a lu l'absence de signature juste avant une écriture concurrente. La vérification d'origine est effectuée **dans la transaction d'écriture**, jamais seulement dans le coordinateur.

Une première insertion échoue sans effet si une signature est apparue entre-temps. Une revalidation exige `Origin = Discovered`, le jeton attendu et l'état attendu. Un conflit conserve l'autorité existante et recharge l'état ; aucun retry aveugle avec Upsert.

Une future correction humaine peut remplacer une BuiltIn/Discovered avec une action explicite et `Origin = Manual`. Un import BuiltIn ne remplace pas Manual. Aucune UI ni import de signatures intégrées n'est ajouté par B1/B2/B3.

## 15. Lifecycle / Invalidation

**Chemin inaccessible :** aucune promotion ; abandon de l'épisode qualifiant en cours. Pour une Discovered acceptée, suspendre son admissibilité tant que sa révision ne peut être validée. Aucun retour au matching par nom. Une session déjà active suit la clôture/recovery existante au dernier instant fiable ; aucun temps incertain n'est ajouté.

**Mise à jour ou déplacement :** toute différence de racine, présence, ensemble de fichiers, taille ou LastWriteTimeUtc ouvre une nouvelle génération. Les preuves anciennes ne sont pas transférées. La Discovered est `NeedsRevalidation` et deux nouveaux épisodes qualifiants sont nécessaires. Ne jamais traduire automatiquement un ancien chemin vers un nouveau dossier.

**Fichier modifié au même chemin :** une différence de taille ou de LastWriteTimeUtc invalide. La version/description PE peut aider au diagnostic mais ne remplace pas ce contrôle. Une modification préservant exactement ces attributs n'est pas détectable par ce modèle ; aucun hash complet n'est introduit pour la première version.

**Au redémarrage :** revalider racine et inventaire avant de réactiver une Discovered précédemment Valid. Une génération inchangée permet de conserver les épisodes complets et la signature validée. Une ancienne Discovered sans chemin ou métadonnées de validation repart en apprentissage.

La préparation asynchrone de l'inventaire est un état `Pending`, pas une preuve d'absence. Différer la décision de recovery d'une session Discovered concernée jusqu'au premier résultat de validation, sans bloquer l'affichage du cache UI. Pendant cette attente, ne pas avancer son heartbeat. Si la validation réussit, reprendre les règles existantes de recovery sur une capture fraîche, sans combler l'intervalle non observé ; si elle échoue ou invalide la signature, clôturer au dernier instant persisté fiable. Une annulation laisse l'état récupérable au prochain démarrage. Les signatures explicites ne dépendent pas de cette attente.

**Pendant l'exécution :** vérifier la révision du candidat avant chaque observation utilisée pour son apprentissage ou son matching Discovered ; ces lectures ciblées sont distinctes d'un inventaire récursif. L'inventaire complet est rafraîchi au démarrage, après rescan pertinent et avant qualification d'un nouvel épisode. Un nouveau chemin observé sous la racine suspend la qualification et déclenche un inventaire hors cycle critique.

**Installations absentes ou chevauchantes :** suspendre l'apprentissage/admissibilité automatique ; aucune préférence d'installation ne résout une attribution ambiguë. Les signatures Manual/BuiltIn ne sont ni réécrites ni supprimées par ces mécanismes ; une entrée explicite avec chemin reste soumise à son matching exact.

**Changement de politique :** nouvelle version, invalidation des preuves et revalidation des Discovered. Le résultat n'est pas appliqué rétroactivement aux sessions terminées.

## 16. Layer Responsibilities

| Couche | Responsabilité | Interdit |
|---|---|---|
| Core | Preuves, transitions d'apprentissage, politique pure, orchestration générique par interfaces | Windows, Steam, SQLite, WPF |
| Platform | Inventaire, révisions, chemins fiables, capture et qualité de capture | Décider sémantiquement Main |
| Providers | Indices fiables facultatifs traduits en preuves génériques | Heuristiques par jeu dans Core ; réseau dans le monitor |
| Data | Migrations, persistance locale et protections atomiques | Scoring et sélection de candidats |
| UI/composition | Inscription DI, raccordement startup/scan/lancement et cycle de vie | Politique de classification dans les vues/ViewModels |

Le premier périmètre n'ajoute aucun fournisseur de preuve distante. Des exclusions génériques reposant sur des conventions connues sont négatives et testées ; aucun AppID, titre ou nom propre de jeu ne pilote Core.

## 17. Concurrency

Un seul propriétaire effectue la capture des processus : le cycle existant de `SessionRuntime`, déclenché par `SessionMonitor`. La découverte reçoit cette même capture avant le matching ; elle n'appelle pas la source de son côté. L'enveloppe de capture sera enrichie pour signaler sa qualité sans nécessiter de PID parent.

Le coordinateur sérialise les transitions d'apprentissage. Les événements scan et launch lui transmettent des données immuables, sans modifier directement son état. Les inventaires peuvent être calculés hors du monitor, avec publication atomique d'une génération complète ; un résultat d'ancienne génération est abandonné.

La génération courante est vérifiée avant consommation des résultats puis dans la transaction Data. Les révisions de fichiers sont relues avant acceptation ; une mutation disque concurrente ultérieure sera détectée avant le prochain matching. Aucune transaction SQLite n'est maintenue ouverte pendant une énumération disque.

La protection SQL conditionnelle est nécessaire même avec un coordinateur unique, pour les écritures explicites futures. Aucune attente synchrone du Dispatcher n'est introduite. Bugfix A reste applicable aux notifications UI.

## 18. Failure Handling

- Inventaire incomplet/erreur d'accès : résultat de preuve insuffisante, pas une liste vide considérée complète.
- Capture ou métadonnées indisponibles : rompre la qualification ; ne pas interpréter une erreur comme disparition confirmée.
- Annulation : propager la cancellation à la frontière d'orchestration ; aucune promotion après annulation ni épisode artificiellement terminé. Les transactions déjà commitées restent valides.
- Persistance indisponible : ne pas annoncer une promotion acceptée ; conserver le fonctionnement des sessions déjà reconnues lorsque possible et journaliser la dégradation. Aucun catch global silencieux.
- Fermeture : abandonner l'épisode incomplet ; les résumés terminés déjà persistés restent utilisables. Aucun scan imposé pendant la fermeture.

Journaliser les changements d'état et raisons de refus, avec déduplication par génération/raison. Ne pas produire un log identique toutes les deux secondes. L'absence de signature n'entraîne aucun popup ; ce n'est pas automatiquement un élément « À signaler ».

## 19. Migration

B2 ajoutera une migration de version **6** depuis la baseline de schéma 5, sans modifier les migrations historiques. Ajouter les champs nullable, les métadonnées de validation et le stockage des preuves terminées. Le détail SQL sera spécifié avec les tests B2.

Aucun chemin ni preuve ne sont fabriqués pour les lignes existantes. Manual/BuiltIn restent lisibles avec leur comportement legacy sans chemin. Discovered sans validation est initialisée `NeedsRevalidation`.

Conserver signatures, ordre des entrées, sessions et corrections existantes. Les nouveaux objets d'apprentissage référencent des jeux/installations existants et sont nettoyés lors de leur suppression. Migration transactionnelle, backup et rollback via `DatabaseInitializer` ; couvrir base neuve, version 5 réelle, réexécution et erreur intermédiaire.

## 20. B1 / B2 / B3 Scope

| Tranche | Livrable | Gate autonome |
|---|---|---|
| B1 — Discovery contracts + inventory + pure decision policy | Modèles de preuve, qualité, inventaire local et politique des sections 9/10 | Tests unitaires et filesystem ; aucune promotion réelle ni changement DB/runtime production |
| B2 — Runtime observation + persistence | Accumulation/rechargement des preuves, chemin, validation, migration et opérations conditionnelles | Intégration capture simulée + SQLite ; protection des origines et non-régression Sessions ; activation production différée |
| B3 — Production orchestration + real runtime gate | Raccordement startup/scan/lancement et capture partagée, apprentissage externe, lifecycle et observabilité | Application réelle, preuves puis signature puis session/historique, sans UI manuelle de sélection |

Chaque tranche suit RED -> GREEN -> review -> gate. Aucun changement Task 7. B1 ou B2 GREEN ne signifie pas que le suivi d'un jeu réel est réparé. Aucun commit de tranche n'est implicite dans la création de cette spec.

## 21. Testing Strategy

**Politique pure :** un candidat fiable avec les deux épisodes exigés ; un seul épisode ; même épisode rejoué ; deux épisodes séparés par une contradiction ; candidat unique sans preuve ; concurrents équivalents ; concurrent non observé ; compagnon de démarrage ; launcher seul indéterminé ; anti-cheat et jeu coextensifs ; plusieurs enfants plausibles sans parenté supposée ; sortie rapide ; chemins homonymes ; attribution à plusieurs installations.

**Filesystem :** répertoire temporaire, sous-dossiers, fichiers illisibles, inventaire partiel, traversées et préfixes trompeurs, jonctions, révisions modifiées, installation déplacée. Aucun exécutable réel n'est lancé par ces tests.

**Observation :** source fake déterministe, identités PID/démarrage, capture avec chemin inaccessible, nouveau PID non attribuable, épisodes incomplets, lancement externe, reprise après arrêt, suspension, cancellation, arrivée d'un inventaire obsolète. Vérifier une seule capture par cycle et l'absence de promotion sur un événement launch seul.

**Data :** round-trip des preuves/chemins, migration legacy, refus atomique face à Manual/BuiltIn, conflits entre revalidation et édition, révision de génération, rollback, crash entre preuves et acceptation, aucune écriture par tick.

**Sessions/UI :** matching exact sans fallback accidentel, anciennes signatures explicites, revalidation Discovered, sessions simultanées, heartbeat/recovery, aucune reconstruction des épisodes d'apprentissage, tests Dispatcher Bugfix A conservés. Les tests automatisés ne dépendent pas d'un jeu commercial ni de Steam distant.

## 22. Runtime Acceptance

Utiliser une installation réelle disponible ou un programme de test contrôlé dans une installation de test distincte. Documenter la racine, les exécutables effectivement observés et les décisions ; ne jamais injecter directement une signature pour prétendre valider la découverte.

1. Partir sans signature automatique pour le cas étudié ; inventaire complet.
2. Observer un premier épisode normal : preuves persistées, aucune session passée inventée et aucun popup.
3. Redémarrer PlayStead entre les épisodes pour vérifier la conservation des preuves terminées.
4. Observer le second épisode : promotion uniquement si le prédicat exact est satisfait ; sinon refus expliqué et silencieux.
5. Après une promotion réelle, un épisode ultérieur ouvre une session, fait progresser le heartbeat, puis produit un historique après fermeture. Vérifier aussi le rechargement de la page Sessions ; une limitation d'actualisation UI ne doit pas être masquée par un faux succès.
6. Vérifier un cas ambigu qui reste sans nouvelle signature, une modification de binaire qui suspend Discovered, et la protection Manual/BuiltIn.
7. Confirmer un seul poller, l'absence d'exception du monitor et un inventaire non récursif à chaque tick ; mesurer l'impact sans inventer de SLA.

Gray Zone est un cas ambigu réel : `GZWClientEAC.exe` a été lancé ; le chemin du second PID n'a pas été prouvé. `GZW\Binaries\Win64\GZWClientSteam-Win64-Shipping.exe` existe sur disque. Ni l'apparition d'EAC ni le nom Shipping ne permettent sa promotion. Si les deux restent plausibles/coextensifs, le résultat attendu reste `Ambiguous`, même après plusieurs lancements.

Enshrouded est potentiellement simple ; Fallout 4 comporte launcher/mod loader ; Ready Or Not comporte installateurs et plusieurs binaires ; Arma Reforger comporte BattlEye et variantes du jeu. Ces exemples alimentent les cas de validation, jamais des branches par jeu.

B3 ne peut être fermé sans au moins un apprentissage positif réel de bout en bout et un refus réel d'ambiguïté. Un refus correct pour Gray Zone ne suffit pas à débloquer Task 7 pour ce jeu.

## 23. Security / Privacy

Toutes les données restent dans les emplacements locaux PlayStead existants. Aucune écriture dans les installations des jeux. Aucun envoi des chemins, noms de processus, preuves ou sessions à un provider.

Conserver uniquement les informations nécessaires aux installations suivies. Les processus inconnus hors scope n'ont pas leur nom/chemin persisté ; seule une raison de qualité d'épisode est nécessaire. Pas de lignes de commande, contenu de fenêtre, mémoire ou identifiant utilisateur Steam dans les preuves.

Un inventaire ne charge ni n'exécute les binaires. Ne pas demander une élévation utilisateur pour rendre la découverte nominale possible. Une protection Windows peut conduire durablement à `NO SIGNATURE`.

## 24. Risks

La répétition d'un comportement n'est pas une preuve absolue de sémantique : un outil peut imiter le cycle d'un jeu, un launcher peut être le seul fichier visible alors que le principal est inaccessible ou extérieur à l'installation. Cette première politique privilégie le refus dès qu'un concurrent ou défaut de qualité est connu ; elle ne promet pas une classification parfaite de logiciels arbitraires. Si le gate révèle un faux positif, suspendre la promotion concernée et revoir la politique, jamais ajouter une exception par jeu.

Les règles d'exclusion génériques peuvent produire des faux négatifs. Les concurrents non observés et processus inconnus peuvent empêcher de nombreux apprentissages. Ce coût est accepté ; aucune relaxation automatique selon le nombre de lancements n'est permise.

Taille et date ne détectent pas une modification qui préserve ces attributs. Ce n'est pas un mécanisme anti-altération. Les courses filesystem ne sont pas supprimées par une transaction DB ; la revalidation avant matching limite leur portée.

La disponibilité tardive d'un inventaire ou un lancement déjà en cours peut perdre un épisode. Un refus n'autorise ni backfill ni collecte plus intrusive. Le gate doit rendre ces limites visibles dans son rapport sans imposer une nouvelle UX de diagnostic.

## 25. Explicit Design Decisions

| Sujet | Décision autoritaire |
|---|---|
| État temporaire | Coordinateur Core singleton ; épisode courant en mémoire |
| Survie au redémarrage | Inventaire/génération et résumés d'épisodes terminés persistés ; épisode incomplet abandonné |
| Promotion minimale | Prédicat complet de la section 10 ; deux épisodes indépendants car la répétition exige une référence et une confirmation |
| Ambiguïté | Décision explicite avec concurrents/raisons ; aucune signature nouvelle, aucun popup |
| Chemin inaccessible | Preuve insuffisante ; aucun fallback automatique par nom |
| Mise à jour/déplacement | Nouvelle génération, preuves réinitialisées et Discovered à revalider |
| Ancienne Discovered sans chemin | Lisible, non admissible au matching jusqu'à nouvel apprentissage |
| Manual/BuiltIn | Jamais écrasées par apprentissage ; protection atomique en Data |
| Observateurs | Capture unique du cycle runtime partagée avec découverte |
| Changement au même chemin | Invalidation sur taille/date ; pas de hash cryptographique dans cette version |
| Revalidation | Suspension sans perte de la signature ; nouvelle preuve complète puis remplacement conditionnel Discovered uniquement |
| Persistance minimale | Chemins/révisions, origine/validation, génération/inventaire et deux résumés qualifiants au maximum ; pas d'historique brut global |
| UX nominale | Apprentissage silencieux ; UI manuelle hors B1/B2/B3 |
| Lancement externe | Même contrat d'épisode ; LaunchIntent facultatif, jamais preuve suffisante |
| Temps initial | Épisodes d'apprentissage non convertis en sessions ; suivi futur seulement |

### Self-review du design

- Les trois résultats de politique sont distincts de l'acceptation transactionnelle.
- Les refus et contradictions ne sont pas contournés par un compteur de lancements.
- Les chemins et révisions ne sont pas inventés pour les données historiques.
- Manual/BuiltIn sont protégées lors de l'écriture concurrente, pas seulement lors d'une lecture préalable.
- Aucune règle Steam/Gray Zone n'entre dans Core ; les exemples restent des cas de validation.
- B1, B2 et B3 ont des livrables/gates séparés ; Task 7 et l'UI manuelle restent hors scope.
- Les limites d'observation et de révision sont explicites ; aucun score, seuil statistique ou diagnostic de jeu fictif n'est présenté comme une preuve.
