# PlayStead — Notification Center
## Spécification de design Phase 2B

**Date :** 16 septembre 2026
**Statut :** Design validé
**Périmètre :** Phase 2B — Notification Center

## 1. Portée

Le Notification Center est un sous-système générique de PlayStead. En Phase 2B, son seul producteur réellement utilisé est l’Identity Resolution. Aucun autre producteur n’est implémenté dans cette phase.

Les décisions humaines d’identité (`UserConfirmed`, `UserRejected`, choisir un autre jeu) restent en Phase 2C.

## 2. États et cycle de vie

```text
Unread = 1
Read = 2
Resolved = 3
```

Ouvrir ou fermer le panneau ne change aucun état. Consulter une notification individuelle la marque `Read`. `Read` ne signifie pas `Resolved` : une notification lue et non résolue reste active. `Resolved` signifie que le problème métier n’existe plus.

## 3. Priorités et tri

```text
Info = 1
Warning = 2
ActionRequired = 3
```

Le tri UI est : `ActionRequired`, puis `Warning`, puis `Info`, puis date de mise à jour la plus récente.

## 4. Badge

La cloche du header affiche le nombre total de notifications actives non résolues. Les états `Unread` et `Read` actives comptent tous les deux. Le badge est absent lorsque le compte vaut zéro.

## 5. Déduplication et réactivation

La clé persistée et unique est :

```text
Producer + SubjectId + Reason
```

Le titre et le message ne sont jamais une identité de déduplication. Une réobservation du même problème actif réutilise la même ligne et le même `NotificationId`, conserve l’état, et actualise le contenu et `UpdatedUtc`.

Si une notification `Resolved` réapparaît, la même ligne est réactivée : `state = Unread`, `read_utc = NULL`, `resolved_utc = NULL`, et `updated_utc` est actualisé. Aucune nouvelle ligne n’est créée.

## 6. Modèle de persistance

La base locale `playstead.db` contient une table générique `notifications` :

```text
notification_id TEXT PRIMARY KEY
producer INTEGER NOT NULL
subject_id TEXT NOT NULL
reason TEXT NOT NULL
deduplication_key TEXT NOT NULL UNIQUE
priority INTEGER NOT NULL
state INTEGER NOT NULL
title TEXT NOT NULL
message TEXT NOT NULL
payload_json TEXT NULL
created_utc TEXT NOT NULL
updated_utc TEXT NOT NULL
read_utc TEXT NULL
resolved_utc TEXT NULL
```

Il n’y a pas de table d’occurrences, d’`occurrence_count` ni d’event bus en Phase 2B.

## 7. Producteur Identity

`NotificationProducer` est extensible. La seule valeur utilisée en Phase 2B est :

```text
IdentityResolution = 1
```

Les résultats Identity sont adaptés ainsi :

```text
MatchProbable  → ActionRequired
Ambiguous      → ActionRequired
MatchConfirmed → aucune nouvelle notification
New            → aucune nouvelle notification
```

Clés stables recommandées :

```text
identity:{GameId}:match-probable
identity:{GameId}:ambiguous
```

Quand une résolution ultérieure rend un problème obsolète, le producteur Identity demande la résolution de la notification correspondante. Les actions `Confirmer`, `Rejeter` et `Choisir un autre jeu` sont Phase 2C.

## 8. Service métier

Le service central est `INotificationCenterService`. Il porte le cycle de vie métier et expose conceptuellement :

```text
PublishOrRefreshAsync
MarkReadAsync
ResolveAsync
GetActiveCountAsync
ListAsync
PurgeExpiredResolvedAsync
```

Le store SQLite reste responsable de la persistance et des requêtes.

## 9. Rétention

Les notifications `Unread` et `Read` non résolues sont conservées indéfiniment. Les notifications `Resolved` sont conservées 90 jours. La purge supprime uniquement les lignes satisfaisant simultanément :

```text
state = Resolved
resolved_utc < now - 90 jours
```

Une notification active ne peut jamais être supprimée par cette purge.

## 10. Non-bloquant

Une panne non critique du Notification Center ne doit jamais rendre indisponibles le scan Library, le lancement d’un jeu, la détection de session ou la synchronisation du catalogue. Les erreurs non liées à l’annulation sont isolées au point d’intégration; les `CancellationToken` sont propagés normalement.

## 11. Interface utilisateur

L’interface principale est un panneau latéral droit ouvert depuis la cloche du header et accessible depuis le shell. Un clic sur la cloche ouvre ou ferme le panneau. Un clic sur une notification la marque `Read` et affiche ses détails. Fermer le panneau ne modifie aucun état.

La vue principale affiche les notifications actives et propose le filtre simple `Actives | Résolues`. Les résolues restent consultables pendant leur rétention de 90 jours. Le panneau latéral est l’interface principale; aucune popup modale ne le remplace.

## 12. Architecture

```text
Core  : modèles, enums, contrats et service métier
Data  : persistance et requêtes SQLite
UI    : cloche, badge, panneau droit et ViewModels
Identity producer : adaptation des résultats Identity
```

Il n’y a pas d’event bus générique, de serveur, de réseau, de télémétrie, ni d’intégration IGDB, SteamGridDB ou RAWG en Phase 2B.

## 13. Invariants testables

La couverture de conception doit démontrer :

- déduplication sur la clé stable;
- indépendance entre `Read` et `Resolved`;
- badge comptant toutes les notifications actives;
- réactivation en `Unread`;
- `NotificationId` stable après réactivation;
- purge limitée aux `Resolved` de plus de 90 jours;
- tri par priorité puis date;
- publication Identity pour `MatchProbable` et `Ambiguous`;
- absence de publication pour `MatchConfirmed` et `New`;
- isolation des erreurs non liées à l’annulation;
- propagation de l’annulation;
- ouverture UI ne marquant pas tout `Read`.

## 14. Hors périmètre et YAGNI

Phase 2B n’inclut pas : historique complet des occurrences, event sourcing, event bus, toast ou notifications système Windows, synchronisation cloud, serveur, e-mails, push, snooze, dismiss générique, réglages avancés, règles de muting, ni décisions finales Identity.

Les autres producteurs ne sont pas implémentés. Les actions `UserConfirmed` et `UserRejected` restent explicitement en Phase 2C.
