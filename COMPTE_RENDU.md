# HotPatata — compte rendu complet (depuis le début) et plan du reste

Jeu coopératif à la première personne : 2 à 4 joueurs traversent un parcours en se passant une patate-bombe. Si elle touche le décor ou reste trop longtemps dans une main (6 s), elle explose et toute l'équipe revient au dernier checkpoint.

**Technique :** Unity 6.3 (6000.3.25f1), URP, Input System, Netcode for GameObjects (hôte autoritaire), Multiplayer Services (Relay + sessions), glTFast. Windows 64 bits.
**Documents de référence :** `PROJECT_SPEC.md` (le gameplay, fait foi), `ARCHITECTURE.md`, `MVP_TASKS.md` (le plan), `CLAUDE.md` (règles pour les prochaines sessions).

---

## 1. Historique complet

### Mise en route
- Installation du plugin Unity pour Claude Code, création de `CLAUDE.md`.
- Lecture des trois documents de spec, inspection du projet, puis travail milestone par milestone avec l'éditeur piloté en direct, un commit par milestone.

### M0 — Base (`b7e13b1`)
Dossiers du plan, couches et matrice de collision (Player, PlayerCatch, Bomb, Environment, Hazard, Trigger), asset de réglages `GameTuning` (toutes les valeurs modifiables dans l'Inspector), fichier de contrôles, assemblies de code et de tests.

### M1 — Bac à sable local `PassSandbox` (`49e3a65`, `df0ecd7`, `f0860c1`, `8cd046b`)
- Joueur : déplacement, saut, temps de coyote, saut mémorisé, contrôle en l'air.
- Patate : machine à états (tenue, lancée, prise avec grâce de 0,35 s, explosion, remise à zéro), mèche de 6 s avec phase d'alerte de 2 s, bip à 4 niveaux d'urgence + pulsation visuelle, explosion au moindre contact du décor, reset en ~1 s.
- Tests automatiques ajoutés (EditMode et PlayMode).
- **Retour de votre premier test :** « playtest good » — la porte du M1.11 est passée.

### Retours de playtest intégrés en cours de M1
- **Vue à la première personne** : le porteur voit la patate dans sa main (pas de modèle de mains).
- **Lancer chargé** : clic gauche maintenu pour charger, relâcher pour lancer (vitesse 10 → 24 m/s en 1 s), avec jauge à l'écran.
- **Prise chronométrée** : clic droit au bon moment, fenêtre courte et réglable, plus rien d'automatique.
- **Correction d'un blocage « Unplayable »** : plus de 80 claviers/souris virtuels fantômes laissés par mes propres essais de simulation dans l'éditeur. Nettoyé, tests réécrits avec une entrée scriptée, consigne écrite dans `CLAUDE.md`.

### M2 — Kit de niveau (`0489c3d`)
Plateformes (basique, étroite, mobile, qui tombe), barre rotative, zone de mort, checkpoint, zone d'arrivée, tremplin. Tous en prefabs réutilisables.

### M3 — Réseau (`16a1d06`)
- Hôte autoritaire pour tout ce qui compte (lancer, prise, mèche, explosion, reset).
- Un joueur par connexion, patate synchronisée, plateformes qui tombent déterministes, reset cohérent, arrivée en cours de partie.
- **Erreur corrigée (`e98e9fd`) :** ma première « preuve » de latence était fausse (la fonction utilisée est obsolète et ne fait rien). Refait avec le vrai simulateur réseau d'Unity, résultats en section 3, et je vous l'ai signalé.

### Votre patate 3D (`9210576`)
Import de votre modèle GLB et de ses textures. Corrections d'UV (motif inversé, tache noire brillante), rotation naturelle et légèrement aléatoire en vol, balancement dans la main.

### Sensation de vitesse (`53d158b`)
Accélération progressive, FOV qui s'élargit avec la vitesse, inclinaison de la caméra, balancement de marche, tassement à l'atterrissage, vignette + aberration chromatique légères, bruit de vent et de pas. Un facteur d'atténuation existe (accessibilité).

### M5 — Parcours `PrototypeCourse` (`9a6de03`)
~290 m : A Cour sûre, B Premier gouffre, C Relais d'escaliers, checkpoint 1, D Paire mobile, E Voies parallèles, checkpoint 2, F Prise verticale (tremplin), checkpoint 3 (mèche à 4,5 s), G Sprint final, arrivée avec chrono et rejouer (R).

### Vérification cloud et M4 — Lobby en ligne (`ef7ab49`, `9bf358b`)
- Vérification du lien avec le compte Unity, Relay et sessions avec de vraies sessions.
- Créer une partie avec code à 6 caractères, rejoindre par code, liste des joueurs (marque de l'hôte), lancement par l'hôte, erreurs lisibles, retour au menu et recréation répétée.
- Correction de sécurité d'entrée (`74a06fc`) : ne jamais donner d'actions à un clavier/souris absent (machines sans clavier détecté).

### Votre test avec un ami à distance et la réponse (`7394df7`)
Retour : prises difficiles au début, latence ou zone trop petite. Réponse :
- **Guidage doux** style Knockout City mais imparfait : la patate est courbée vers le joueur visé (cône de 28°, force 0,7, petite erreur aléatoire, pas de contournement des murs).
- **Zone de prise agrandie** (0,6 → 0,9 m), **fenêtre** 0,25 → 0,4 s, aimant final dans les mains si la fenêtre est ouverte.
- **Retours à l'écran** : cadre sur le joueur visé, marqueur « CATCH! » sur la patate qui arrive, messages « Too early / Too late by N ms » et ping en haut à gauche pour distinguer erreur de timing et latence.
- Un ZIP propre à envoyer à votre ami : `Builds/HotPatata-Win64.zip`.

**Qualité actuelle :** 33 tests EditMode et 75 tests PlayMode passent. Réseau vérifié avec de vrais processus (connexion, apparition, passes, prises, reset, déconnexion, arrivée tardive).

---

## 2. Où on en est par rapport au plan (`MVP_TASKS.md`)

| Milestone | État |
|---|---|
| M0 Base | Fait |
| M1 Sandbox local | Fait (test humain M1.11 passé) |
| M2 Kit de niveau | Fait |
| M3 Réseau | Fait, **sauf M3.7** (validation sur deux machines physiques, commencée avec votre ami mais pas conclue) et **M3.5** (prise sous latence : voir section 3) |
| M4 Lobby | Fait (état « prêt » optionnel non fait) |
| M5 Parcours | Fait, **test humain du parcours complet à faire** |
| M6 Lisibilité | **Partiel** : ping + cadre de visée + marqueur « CATCH! » faits ; le reste ci-dessous |
| M7 Solidité | **Pas commencé formellement** (mesure de latence faite, voir section 3) |
| M8 Finitions | Volontairement pas commencé (à faire seulement si le prototype est amusant à répétition) |

---

## 3. Point ouvert principal : la latence

La prise chronométrée n'est **pas compensée en latence** : le clic droit est jugé par l'hôte à sa réception, donc « en retard » de la moitié du ping. Mesures avec le vrai simulateur (bots qui lancent et attrapent pendant ~35 s) :

| RTT | Prises réussies |
|---|---|
| ~4 ms | 26 / 26 |
| ~114 ms | 24 / 25 |
| ~240 ms | 0 / 15 |
| ~420 ms | 0 / 15 |

Tous les échecs sont « Too late ». Le guidage et la zone agrandie n'y changent rien : c'est un problème de temps, pas de visée.

**Plan (phase 2), à lancer seulement si votre ami joue avec plus de ~110 ms :**
1. L'hôte accepte une prise selon l'**instant du clic du receveur** (heure serveur envoyée avec le clic), avec un court historique de positions de la patate et du receveur.
2. Retenir brièvement l'explosion « contact avec le décor » si la patate vient de passer près d'un receveur éligible.
3. Limite maximale de compensation (ex. 250 ms) pour éviter les abus.
4. Tests rejoués avec le simulateur aux mêmes RTT.

**Ce qu'il me faut :** après une partie avec votre ami, le **ping** affiché en haut à gauche et les messages de prises ratées (« Too late by N ms », « Too early »).

---

## 4. Ce qu'il reste à faire, dans l'ordre proposé

### A. Décisions et vérifications (courtes)
- [ ] Nouveau test avec l'ami : ressenti du guidage (`homingStrength` 0,7, cône 28°, erreur 5°), ping, messages de prises ratées → décide de la phase 2 (section 3) et du réglage fin.
- [ ] **M3.7** : valider le jeu en ligne sur deux machines physiques (critère : boucle de base jouable, aucune course d'autorité critique).
- [ ] **M5** : jouer le parcours complet à deux (ou plus) une fois de bout en bout, noter où ça coince.
- [ ] Renommer « BEEP! » en « HotPatata » dans les docs et le titre du jeu (question posée, sans réponse).
- [ ] Décider quoi faire des réglages Unity non validés dans git : `ProjectSettings/ProjectSettings.asset` (lien compte cloud), `Assets/DefaultNetworkPrefabs.asset`, plus des suppressions de `Assets/Editor/HubForceResolve.cs` non commitées.

### B. M4 — reste optionnel
- [ ] État « prêt » dans le lobby (M4.4 optionnel).
- [ ] Un client tué brutalement reste listé un moment par le service : à surveiller, l'hôte n'affiche déjà que les joueurs réellement connectés.

### C. M6 — Lisibilité et confort
- [ ] **M6.1 Indicateur de porteur** visible de loin, mis à jour immédiatement après une prise.
- [ ] **M6.2 Indicateur receveur / prêt à attraper** (le marqueur « CATCH! » couvre déjà une partie), sans rendre le lancer automatique.
- [ ] **M6.3 Progression** : indication minimale du checkpoint / de la section.
- [ ] **M6.4 Ping « lance-moi »** : seulement si les tests muets montrent le besoin.
- [ ] **M6.5 Réglages d'accessibilité** : menu avec intensité des effets de vue (le paramètre existe, pas d'écran), option anti-flash, volume de l'alerte de la bombe, alerte visuelle qui ne dépend pas que de la couleur.

### D. M7 — Solidité de la version jouable
- [ ] **M7.1** Test de stress : de nombreux échecs de suite (pas de vitesse résiduelle, pas de bombe en double, pas de porteur manquant, pas de contrôles gelés, pas d'exceptions).
- [ ] **M7.2** Tests à 2, 3 et 4 joueurs (apparitions, checkpoints, condition d'arrivée avec les joueurs actifs).
- [ ] **M7.3** Instabilité réseau : rejouer les mesures de latence (section 3) après la phase 2, ajouter perte de paquets et gigue.
- [ ] **M7.4** Premier vrai test externe avec les 7 questions du plan (compréhension de l'échec, appels de passes, lisibilité des prises, équité des ratés, mèche, temps d'inactivité, envie de rejouer). Aucune nouvelle mécanique avant d'avoir relu ces observations.

### E. M8 — Seulement si le prototype est amusant à répétition
Modèles de personnages, animation, effets visuels, vrais sons, décor, polish du lobby et du rejouer, autres parcours, Steam, autres modes. Actuellement provisoires : joueurs en capsules, sons générés par le code, interface IMGUI.

### F. Critères de fin du MVP (liste du plan)
Déjà cochables : session par un hôte, jusqu'à 3 amis par code, 2–4 joueurs, déplacement réactif, une seule bombe, lancer/prise en mouvement, mèche rafraîchie à la prise, bip et pulsation, explosion contact/temps autoritaire, reset rapide, checkpoints, parcours 3–5 min, arrivée collective, rejouer, accord hôte/client. **Reste à confirmer :** pas d'erreurs répétées dans la console sur de longues parties, testeurs qui comprennent les échecs, testeurs qui veulent rejouer.

---

## 5. Comment jouer

- **En local :** menu → *Play Local* (Tab change de joueur).
- **En ligne :** un joueur clique *Host Online* et communique le code ; l'autre tape le code puis *Join with code* ; l'hôte clique *Start*.
- **Ami à distance :** envoyer `Builds/HotPatata-Win64.zip` (80 Mo). Ni compte Unity ni ouverture de ports nécessaires.
- **Commandes :** ZQSD/WASD bouger · Espace sauter · clic gauche maintenu puis relâché = lancer chargé · clic droit = attraper au bon moment · Échap libère la souris · R rejouer après l'arrivée · F1 masque l'interface de debug · F10 quitte.

---

*Détails techniques pour les prochaines sessions : `CLAUDE.md`, `ARCHITECTURE.md`, `PROJECT_SPEC.md`, `MVP_TASKS.md`.*
