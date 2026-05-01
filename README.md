# Oracle — Tactical RPG isométrique (Unity)

Jeu tactique au tour par tour en vue isométrique 2.5D (deck de sorts, passifs). Ce dépôt est **la racine du projet Unity** : ouvre ce dossier directement dans l’éditeur.

## Démarrage rapide

| Élément | Détail |
|--------|--------|
| **Unity** | **2022.3.62f3** LTS (voir `ProjectSettings/ProjectVersion.txt`) |
| **Pipeline** | URP |
| **Scène principale** | `Assets/Monjeu.unity` |

Après clone : laisser Unity régénérer `Library/` (non versionné). Ne pas committer `Library/`, `Logs/`, `UserSettings/`.

## Déjà fonctionnel (local)

- Grille isométrique, pathfinding A*, caméra, génération d’arène et zones de spawn  
- Phases : **passifs** → **placement** → **combat** (`CombatInitializer`)  
- Tours, PA/PM, sorts (zones, cooldowns), UI deck / HUD / timer  
- Données : nombreux `SpellData` / `PassiveData` sous `Assets/_Game/ScriptableObjects/`  
- Menu Unity **Oracle** (génération de contenu, wizards) pour l’outilery Editor  

Code gameplay principal : `Assets/_Game/Scripts/` (`Core`, `Combat`, `UI`, `Editor`).

---

## Priorités — par où commencer demain

Ordre recommandé si l’objectif est un **MVP jouable à deux en ligne** :

### 1. Réseau (le plus urgent côté « produit »)

- Le dossier **`Assets/Photon`** contient le SDK, mais **`Assets/_Game` ne l’utilise pas encore** : pas de synchronisation des actions combat.
- À faire : connexion / room, **MasterClient** qui valide les actions, RPC (ou équivalent) pour au minimum : déplacement, cast de sort, fin de tour, choix passif si applicable, gestion **déconnexion**.
- Aujourd’hui le adversaire est **placé en automatique** : à remplacer par un vrai second joueur quand le réseau sera là.

### 2. Navigation entre écrans

- Manque un flux propre : **menu principal** → **lobby / matchmaking** → **scène de combat** (au lieu de tout partir d’une scène de démo unique).
- Ça peut se faire en parallèle du réseau, mais le réseau sans changement de scène reste limité pour un vrai jeu.

### 3. Polish MVP (après ou en parallèle léger)

- Animations perso (toutes directions), VFX par sort, sons UI + combat, cohérence pixel art (PPU, filtre Point, etc.).
- Optionnel rapide à ajouter si besoin : **DOTween** (pas encore dans le flux packages obligatoire du GDD).

### 4. Tests & équilibrage

- Surtout **après** intégration réseau : latence, désynchronisation, timer, bugs de grille, charge Photon.

### 5. Post-MVP

- Modes 2v2 / 3v3, MMR, historique… — détaillé dans la roadmap longue.

---

## Doc complète du projet

Le plan détaillé, les phases GDD et l’**audit à jour** (avril 2026) sont dans **`ROADMAP_ORACLE.txt`** à la racine de ce dossier.

---

## Dépôt

[https://github.com/kyaminq-ui/oracle](https://github.com/kyaminq-ui/oracle) — branche **`main`**.

Bonne session.
