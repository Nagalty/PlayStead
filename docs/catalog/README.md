# Catalogue canonique PlayStead

Le catalogue est généré hors du client desktop. Le workflow
`.github/workflows/catalog-sync.yml` utilise les secrets GitHub Actions
`IGDB_CLIENT_ID` et `IGDB_CLIENT_SECRET`, demande un token Twitch/IGDB en
mémoire, pagine les jeux, puis produit `catalog-payload.json` et
`catalog-manifest.json`.

Les secrets ne sont jamais écrits dans le payload, le manifeste ou les logs.
Le job valide le schéma, le nombre d’entrées, la taille et le SHA-256 avant de
publier l’artefact CI. Le job ne publie pas un serveur permanent : l’artefact
validé peut ensuite être copié vers l’endpoint statique officiel retenu par le
projet. Aucun endpoint de production n’est figé tant qu’il n’est pas attribué.

## Génération locale

Pour un export IGDB déjà obtenu :

```text
dotnet tools/PlayStead.CatalogBuilder/bin/Release/net10.0/PlayStead.CatalogBuilder.dll \
  --input igdb-export.json --output out/catalog --catalog-version 20260930
```

Le mode `--igdb` lit uniquement `IGDB_CLIENT_ID` et
`IGDB_CLIENT_SECRET` dans l’environnement. Le mode `--dry-run` exécute la
génération et toutes les validations sans écrire de payload.

Deux exécutions sur les mêmes données sont déterministes pour le contenu,
l’ordre, les références et les genres. `generatedAtUtc` varie par défaut ;
`PLAYSTEAD_CATALOG_GENERATED_AT_UTC` permet une comparaison reproductible.

## Client et cache

Le client n’appelle jamais IGDB. Il synchronise uniquement une URL de
manifeste configurée par `PLAYSTEAD_CANONICAL_CATALOG_MANIFEST_URL`, valide le
hash et importe le catalogue dans une transaction SQLite. Le dernier cache
valide reste utilisable hors ligne.

## Publication et rollback

L’artefact CI est conservé 90 jours. La publication statique de production
doit exposer un manifeste stable et conserver la version précédente. Pour un
rollback, repointer ce manifeste vers le couple manifeste/payload précédent ;
le client refusera les versions invalides et conservera son catalogue local
actuel.

`007 First Light` n’est jamais codé en dur. Il ne sera résolu qu’après
publication d’une entrée canonique exacte contenant une référence provider
fiable.
