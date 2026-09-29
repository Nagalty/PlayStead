# Charte de voix PlayStead

## Identité

PlayStead parle comme un compagnon qui connaît ta machine et tes habitudes de jeu.

La voix est :

- familière et complice ;
- légèrement taquine ;
- naturelle pour un joueur PC ;
- directe sans être agressive ;
- honnête sur son niveau de certitude ;
- simple dès qu’un terme courant suffit.

Elle évite le ton froid, administratif ou inutilement technique.

## Certitude avant personnalité

La personnalité ne doit jamais embellir une information incertaine.

Quand PlayStead sait, il peut parler franchement :

> Toi, tu utilises des mods sur ce jeu, c’est certain. Je le vois, tu sais.

Quand PlayStead doute, le doute doit rester explicite :

> Il me semble que tu utilises des mods sur ce jeu, mais j’ai encore un doute.

Quand PlayStead ne sait pas, il n’invente rien. L’information est masquée ou présentée comme inconnue.

## Vocabulaire

Préférer le langage d’un joueur PC :

- Mods
- Sauvegardes
- Jeu
- Session
- Temps joué
- Mise à jour

Garder les termes techniques (`Provider`, `Baseline`, `Evidence`, `Snapshot`, `Detected state`, etc.) dans le code et les diagnostics internes autant que possible.

## Ton contextuel

La voix peut utiliser un humour léger, une petite pique ou la première personne lorsque cela aide à rendre un état compréhensible :

> T’as été plutôt calme sur celui-là dernièrement.

> Celui-là a bougé depuis ta dernière partie.

> J’ai encore aucun point de repère pour ce dossier.

Éviter les formulations administratives comme « Aucune activité récente détectée » ou « Modification détectée » lorsqu’une phrase naturelle et honnête est possible.

## Où laisser la personnalité parler

Les commandes et libellés purement fonctionnels restent courts :

`Jouer`, `Ouvrir le dossier`, `Supprimer`, `Restaurer`, `Filtres`, `Trier par`.

La personnalité s’exprime surtout dans les états vides, résumés, alertes, suggestions, explications et messages contextuels. PlayStead ne transforme pas toute l’interface en conversation permanente.

## Questions de contrôle

Avant d’ajouter une microcopy, vérifier :

1. Est-ce qu’un joueur PC parlerait comme ça ?
2. Est-ce que cela ressemble à PlayStead plutôt qu’à Windows ?
3. Le niveau de certitude est-il honnête ?
4. La personnalité apporte-t-elle quelque chose ?
5. Le texte reste-t-il court ?

## Exemples canoniques pour les mods

`PossiblyModded` :

> Il me semble que tu utilises des mods sur ce jeu, mais j’ai encore un doute.

`ConfirmedModded` :

> Toi, tu utilises des mods sur ce jeu, c’est certain. Je le vois, tu sais.

`Unknown` : le bloc Mods est masqué.
