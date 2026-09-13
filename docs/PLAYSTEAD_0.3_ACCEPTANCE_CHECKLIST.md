# PlayStead 0.3 — Checklist d'acceptation

**Task 13 : IN PROGRESS.** Cette checklist consigne les preuves déjà validées, reprises pour les Steps 5–6. Elle ne constitue pas l'exécution du final gate.

## Preuves déjà validées

- [x] Version produit autoritaire : `ProductVersion.Current = "0.3.0-dev"`.
- [x] Baseline 0.3 fraîche : **349/349 tests PASS**.
- [x] Build Release : **0 warning / 0 error**.
- [x] Runtime smoke : **PASS**.
- [x] Recovery smoke : **PASS**.
- [x] Baseline de performance réalisée ; aucun SLA inventé ni engagement de performance déduit de cette mesure.
- [x] Checklists historiques 0.1 et 0.2 protégées et conservées sans modification.

Ces preuves proviennent des validations antérieures. Les Steps 5–6 mettent à jour la documentation et le seuil CI ; ils ne relancent ni le build ni les 349 tests.

## Périmètre 0.3 documenté

- Source de vérité fondée sur les processus et leurs signatures.
- Polling **2 s**, confirmation après **2 snapshots**, heartbeat persisté **5 s**.
- Recovery après crash/reboot sans temps inventé.
- Systray et corrections manuelles traçables, séparées des observations.
- Temps local PlayStead distinct du temps provider.
- Page Sessions, historique, détail, badge live Bibliothèque et composants UI réutilisables.
- **0.4 reste la refonte visuelle globale.**

## Protection des références historiques

| Checklist | SHA-256 de référence |
|---|---|
| [0.1](PLAYSTEAD_0.1_ACCEPTANCE_CHECKLIST.md) | `AB81A1A878A7658DA17C9E219A509E5D9CB28CFC6AFCBA5E1C5BB99972E41FD7` |
| [0.2](PLAYSTEAD_0.2_ACCEPTANCE_CHECKLIST.md) | `8D4B7130367775D1B6FE98153E70CDC1F6C042FC27E595C2A77A23C4A17DC413` |

## Gates encore ouverts

- [ ] Exécuter le final gate 0.3 : **pas encore exécuté**.
- [ ] Effectuer le commit final : **pas encore effectué**.
- [ ] Clore Task 13 après validation des étapes restantes.

La clôture de Task 13 et l'achèvement de la version ne sont pas revendiqués ici.
