# PlayStead 0.4.1 — Phase 2C : Identity Decision Integration

## 1. Objectif

La Phase 2C ajoute les décisions humaines explicites aux résolutions d’identité `MatchProbable` et `Ambiguous`. Depuis le Notification Center, l’utilisateur peut confirmer, rejeter ou choisir un candidat. Les décisions sont locales, persistantes, réversibles et prioritaires sur toute résolution automatique.

## 2. Priorité des décisions humaines

La hiérarchie est : `UserConfirmed` > candidats filtrés par `UserRejected` > resolver automatique > PS-TEMP.

- Une décision humaine n’est jamais écrasée par un scan automatique.
- Un `UserConfirmed` actif court-circuite la résolution automatique.
- Un `UserRejected` filtre uniquement le candidat concerné.
- Le resolver automatique ne travaille que lorsqu’aucun `UserConfirmed` actif n’existe.

## 3. Persistance séparée

Les décisions ne surchargent pas `game_identity_resolutions`. Une table dédiée `game_identity_decisions` est introduite conceptuellement avec au minimum :

`decision_id`, `game_id`, `catalog_content_id`, `decision_type`, `created_utc`, `updated_utc`, `revoked_utc` nullable.

`DecisionType` vaut `1 = UserConfirmed` ou `2 = UserRejected`.

Contraintes : plusieurs rejets actifs peuvent exister pour un `GameId`, un seul `UserConfirmed` actif existe au maximum par `GameId`, et un couple `GameId + CatalogContentId` ne peut pas être simultanément confirmé et rejeté activement. Les lignes révoquées sont conservées ; aucune décision historique n’est supprimée silencieusement.

## 4. Confirmation

Confirmer un candidat crée ou active `UserConfirmed`, synchronise atomiquement `games.canonical_content_id`, révoque tout ancien `UserConfirmed` actif pour ce `GameId` et révoque le rejet actif du même candidat, s’il existe. La confirmation devient l’autorité locale et les scans futurs ne peuvent pas la remplacer automatiquement.

## 5. Rejet

Un rejet cible exactement le couple `GameId local + CatalogContentId candidat`. Il ne rejette pas le jeu local entier. Le candidat ne peut plus être reproposé automatiquement tant que son rejet est actif ; d’autres candidats restent possibles et plusieurs rejets peuvent coexister.

Si le candidat rejeté est actuellement confirmé, la même transaction révoque `UserConfirmed`, met `games.canonical_content_id` à `NULL` et active `UserRejected`.

## 6. Choix dans un cas ambigu

Choisir un candidat dans `Ambiguous` crée `UserConfirmed` pour ce candidat. Les autres candidats ne deviennent pas automatiquement rejetés : ils restent neutres et peuvent redevenir candidats si la confirmation est révoquée.

## 7. Révocation

Révoquer une confirmation renseigne `revoked_utc`, remet `games.canonical_content_id` à `NULL` et rend la résolution automatique à nouveau applicable. Les rejets existants restent actifs et l’historique demeure consultable. Le remplacement d’une confirmation révoque l’ancienne, active la nouvelle et s’effectue dans la même transaction.

Pour un même couple, la dernière décision humaine explicite active prévaut : confirmer un candidat rejeté révoque son rejet ; rejeter un candidat confirmé révoque sa confirmation et efface le rattachement canonique.

## 8. Filtrage et absence de candidats

Avant toute proposition, les candidats ayant un `UserRejected` actif pour le `GameId` sont retirés. Une confirmation active court-circuite les propositions et fournit directement son `CatalogContentId`.

Si tous les candidats connus sont rejetés, le `GameId` et son PS-TEMP historique sont conservés, aucun rattachement canonique actif n’existe et le jeu reste utilisable. La notification peut être `Resolved` si aucune décision ne reste. Un futur candidat non rejeté peut être proposé et réactiver la notification stable.

## 9. Notification Center

Les décisions sont effectuées dans le panneau Notification Center existant ; aucune page Identity dédiée n’est créée. `MatchProbable` expose Confirmer/Rejeter. `Ambiguous` expose les candidats, le choix candidat par candidat et, selon le flux exact, le rejet ciblé.

Après une action réussie, le panneau et ses badges/listes sont rafraîchis. La notification Identity est résolue uniquement lorsque le problème global est terminé. Une révocation ultérieure peut réactiver la même notification via le mécanisme de Phase 2B. Aucun système parallèle de notifications n’est introduit.

## 10. Transactions et séparation des responsabilités

Sont atomiques : remplacement d’un `UserConfirmed`, confirmation d’un candidat rejeté, rejet du candidat confirmé, révocation avec remise à `NULL` du canonique, et création/activation d’une décision avec synchronisation du canonique.

`game_identity_resolutions` continue de représenter la résolution automatique/courante ; `game_identity_decisions` représente les décisions humaines. Le pipeline doit pouvoir distinguer la proposition du resolver, la décision utilisateur et le rattachement effectif dans `games.canonical_content_id`.

## 11. Historique et testabilité

L’historique est conservé par `revoked_utc` ; aucun event sourcing complet ni table d’audit supplémentaire n’est requis. Les invariants à tester sont : priorité de `UserConfirmed`, filtrage des rejets, rejets multiples, confirmation unique, transitions atomiques, réactivation d’un rejet confirmé, rejet d’une confirmation, révocation vers `NULL`, reprise du resolver, absence de rejets implicites dans `Ambiguous`, conservation du PS-TEMP, apparition d’un nouveau candidat, résolution/réactivation correcte de la notification, résistance aux scans automatiques et propagation de l’annulation.

## 12. Hors périmètre

Pas de réseau, serveur central, IGDB/SteamGridDB/RAWG live, fuzzy matching, ranking avancé, ML, event sourcing complet, cloud sync, compte distant, collaboration multi-utilisateur, édition complète du catalogue, suppression physique de l’historique ou nouvelle route de navigation Identity. Pas de UserConfirmed/UserRejected hors du Notification Center.

## 13. Principes d’implémentation

Réutiliser les services Phase 2A et le Notification Center Phase 2B, avec des transactions SQLite explicites et un modèle local simple. Ne pas introduire d’event bus, CQRS, mediator, nouvelle dépendance NuGet ou architecture parallèle.

## 14. Auto-revue

Cette spec doit rester compatible avec les contrats et le pipeline des Phases 2A/2B. Toute implémentation doit garantir une seule confirmation active par `GameId`, conserver toutes les décisions révoquées et ne jamais supprimer le PS-TEMP lors d’un rejet.
