using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Pont gameplay ↔ Photon : validation MasterClient + exécution identique sur tous les clients.
/// Convention 1v1 : slot 0 = <see cref="CombatInitializer.player"/> (équipe A), slot 1 = <see cref="CombatInitializer.opponent"/> (équipe B) — identique sur toutes les instances.
/// Le <b>MasterClient</b> contrôle le slot 0, l’autre joueur le slot 1. Le perso sous contrôle local = <see cref="GetLocalControlledCharacter"/>.
/// À placer sur le <b>même</b> GameObject que <see cref="OracleNetworkHub"/> (un seul <see cref="PhotonView"/>).
/// </summary>
[RequireComponent(typeof(PhotonView))]
[DisallowMultipleComponent]
public class OracleCombatNetBridge : MonoBehaviour
{
    public static OracleCombatNetBridge Instance { get; private set; }

    [Header("Conditions")]
    [Tooltip("Nombre minimum de joueurs dans la room pour router les commandes en réseau (2 = 1v1).")]
    public int minPlayersForNetworkCombat = 2;

    [Header("Debug")]
    public bool logRpc = true;

    PhotonView _pv;

    /// <summary>Vrai si Photon indique une partie multi locale (room + assez de joueurs).</summary>
    public bool ShouldSendCommandsOverNetwork =>
        PhotonNetwork.InRoom &&
        PhotonNetwork.CurrentRoom != null &&
        PhotonNetwork.CurrentRoom.PlayerCount >= minPlayersForNetworkCombat;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _pv = GetComponent<PhotonView>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // =========================================================
    // Résolution slot ↔ personnage (même référence monde sur tous les clients)
    // =========================================================

    /// <summary>Toujours : 0 = équipe A (<c>CombatInitializer.player</c>), 1 = équipe B (<c>opponent</c>).</summary>
    public TacticalCharacter GetCharacterForCombatSlot(int combatSlot)
    {
        var ci = CombatInitializer.Instance;
        if (ci == null) return null;
        return combatSlot == 0 ? ci.player : ci.opponent;
    }

    /// <summary>Slot du personnage : équipe A = 0, équipe B = 1.</summary>
    public int GetCombatSlotForCharacter(TacticalCharacter ch)
    {
        var ci = CombatInitializer.Instance;
        if (ci == null || ch == null) return -1;
        if (ch == ci.player) return 0;
        if (ch == ci.opponent) return 1;
        return -1;
    }

    /// <summary>Équipe contrôlée par cette machine en 1v1 en ligne (1 = hôte, 2 = client).</summary>
    public int GetLocalTeamId()
    {
        if (!ShouldSendCommandsOverNetwork) return 1;
        return PhotonNetwork.IsMasterClient ? 1 : 2;
    }

    /// <summary>
    /// Personnage que THIS client déplace / utilise en combat (hôte = team A, invité = team B).
    /// Hors ligne 2 joueurs : <c>CombatInitializer.player</c> comme avant.
    /// </summary>
    public TacticalCharacter GetLocalControlledCharacter()
    {
        var ci = CombatInitializer.Instance;
        if (ci == null) return null;
        if (!ShouldSendCommandsOverNetwork)
            return ci.player;
        return PhotonNetwork.IsMasterClient ? ci.player : ci.opponent;
    }

    bool SenderMayControlSlot(int combatSlot, Player sender)
    {
        if (sender == null) return false;
        if (combatSlot == 0) return sender.IsMasterClient;
        if (combatSlot == 1) return !sender.IsMasterClient;
        return false;
    }

    // =========================================================
    // API appelée par l’input / HUD (retourne true si le réseau a pris en charge l’action)
    // =========================================================

    public bool TrySubmitMove(TacticalCharacter character, Cell targetCell)
    {
        if (!ShouldSendCommandsOverNetwork || targetCell == null) return false;
        var local = GetLocalControlledCharacter();
        if (local == null || character != local) return false;

        int slot = GetCombatSlotForCharacter(character);
        if (slot < 0) return false;
        int tx   = targetCell.GridX;
        int ty   = targetCell.GridY;

        if (PhotonNetwork.IsMasterClient)
            _pv.RPC(nameof(RpcApplyMove), RpcTarget.All, slot, tx, ty);
        else
            _pv.RPC(nameof(RpcMasterValidateMove), RpcTarget.MasterClient, slot, tx, ty);
        return true;
    }

    public bool TrySubmitCast(TacticalCharacter character, Cell targetCell, SpellData spell)
    {
        if (!ShouldSendCommandsOverNetwork || targetCell == null || spell == null) return false;
        var local = GetLocalControlledCharacter();
        if (local == null || character != local) return false;

        int slot = GetCombatSlotForCharacter(character);
        if (slot < 0) return false;
        int tx   = targetCell.GridX;
        int ty   = targetCell.GridY;
        string spellName = spell.spellName;

        if (PhotonNetwork.IsMasterClient)
            _pv.RPC(nameof(RpcApplyCastSpell), RpcTarget.All, slot, spellName, tx, ty);
        else
            _pv.RPC(nameof(RpcMasterValidateCastSpell), RpcTarget.MasterClient, slot, spellName, tx, ty);
        return true;
    }

    /// <summary>Appelé depuis le HUD « Fin de tour » quand c’est le tour du personnage local.</summary>
    public bool TrySubmitEndTurn(TacticalCharacter actingCharacter)
    {
        if (!ShouldSendCommandsOverNetwork) return false;
        var local = GetLocalControlledCharacter();
        if (local == null || actingCharacter != local) return false;

        int slot = GetCombatSlotForCharacter(actingCharacter);
        if (slot < 0) return false;

        if (PhotonNetwork.IsMasterClient)
            _pv.RPC(nameof(RpcApplyEndTurn), RpcTarget.All, slot);
        else
            _pv.RPC(nameof(RpcMasterValidateEndTurn), RpcTarget.MasterClient, slot);
        return true;
    }

    // =========================================================
    // RPC — validation (MasterClient uniquement)
    // =========================================================

    [PunRPC]
    void RpcMasterValidateMove(int combatSlot, int targetGridX, int targetGridY, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!SenderMayControlSlot(combatSlot, info.Sender))
        {
            if (logRpc) Debug.LogWarning($"[OracleCombatNet] Move rejetée : slot {combatSlot} pas autorisé pour actor {info.Sender.ActorNumber}");
            return;
        }

        var ch   = GetCharacterForCombatSlot(combatSlot);
        var cell = GridManager.Instance != null ? GridManager.Instance.GetCell(targetGridX, targetGridY) : null;
        if (ch == null || cell == null || !ch.CanMoveTo(cell))
        {
            if (logRpc) Debug.LogWarning("[OracleCombatNet] Move refusée côté master (état ou path invalide).");
            return;
        }

        _pv.RPC(nameof(RpcApplyMove), RpcTarget.All, combatSlot, targetGridX, targetGridY);
    }

    [PunRPC]
    void RpcMasterValidateCastSpell(int combatSlot, string spellName, int targetGridX, int targetGridY, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!SenderMayControlSlot(combatSlot, info.Sender)) return;

        var ch   = GetCharacterForCombatSlot(combatSlot);
        var cell = GridManager.Instance != null ? GridManager.Instance.GetCell(targetGridX, targetGridY) : null;
        var spell = FindSpellInDeck(ch, spellName);

        if (ch == null || cell == null || spell == null)
        {
            if (logRpc) Debug.LogWarning("[OracleCombatNet] Cast refusé côté master (données).");
            return;
        }

        var sc = ch.GetComponent<SpellCaster>();
        if (sc == null || !sc.WouldAcceptCast(spell, cell))
        {
            if (logRpc) Debug.LogWarning("[OracleCombatNet] Cast refusé côté master (WouldAcceptCast).");
            return;
        }

        _pv.RPC(nameof(RpcApplyCastSpell), RpcTarget.All, combatSlot, spellName, targetGridX, targetGridY);
    }

    [PunRPC]
    void RpcMasterValidateEndTurn(int combatSlot, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!SenderMayControlSlot(combatSlot, info.Sender)) return;

        var ch = GetCharacterForCombatSlot(combatSlot);
        if (TurnManager.Instance == null || ch == null || TurnManager.Instance.CurrentCharacter != ch)
        {
            if (logRpc) Debug.LogWarning("[OracleCombatNet] EndTurn refusé (pas le bon tour).");
            return;
        }

        _pv.RPC(nameof(RpcApplyEndTurn), RpcTarget.All, combatSlot);
    }

    // =========================================================
    // RPC — application (tous les clients)
    // =========================================================

    [PunRPC]
    void RpcApplyMove(int combatSlot, int targetGridX, int targetGridY)
    {
        var ch   = GetCharacterForCombatSlot(combatSlot);
        var cell = GridManager.Instance != null ? GridManager.Instance.GetCell(targetGridX, targetGridY) : null;
        if (ch == null || cell == null)
        {
            if (logRpc) Debug.LogWarning("[OracleCombatNet] RpcApplyMove : personnage ou cellule null.");
            return;
        }
        if (logRpc) Debug.Log($"[OracleCombatNet] ApplyMove slot={combatSlot} → ({targetGridX},{targetGridY})");
        ch.MoveToCell(cell);
    }

    [PunRPC]
    void RpcApplyCastSpell(int combatSlot, string spellName, int targetGridX, int targetGridY)
    {
        var ch   = GetCharacterForCombatSlot(combatSlot);
        var cell = GridManager.Instance != null ? GridManager.Instance.GetCell(targetGridX, targetGridY) : null;
        var spell = FindSpellInDeck(ch, spellName);
        if (ch == null || cell == null || spell == null)
        {
            if (logRpc) Debug.LogWarning("[OracleCombatNet] RpcApplyCastSpell : données invalides.");
            return;
        }

        var sc = ch.GetComponent<SpellCaster>();
        if (sc == null) return;

        sc.CancelSpell();
        if (!sc.SelectSpell(spell))
        {
            if (logRpc) Debug.LogWarning("[OracleCombatNet] Apply cast : SelectSpell a échoué (état divergent ?).");
            return;
        }
        if (logRpc) Debug.Log($"[OracleCombatNet] ApplyCast slot={combatSlot} spell={spellName} @({targetGridX},{targetGridY})");
        sc.TryCast(cell);
    }

    [PunRPC]
    void RpcApplyEndTurn(int combatSlot)
    {
        var ch = GetCharacterForCombatSlot(combatSlot);
        if (TurnManager.Instance == null || ch == null) return;
        if (TurnManager.Instance.CurrentCharacter != ch)
        {
            if (logRpc) Debug.LogWarning("[OracleCombatNet] ApplyEndTurn ignoré (tour déjà avancé ?).");
            return;
        }
        if (logRpc) Debug.Log($"[OracleCombatNet] ApplyEndTurn slot={combatSlot}");
        TurnManager.Instance.EndTurn();
    }

    static SpellData FindSpellInDeck(TacticalCharacter ch, string spellName)
    {
        if (ch == null || string.IsNullOrEmpty(spellName)) return null;
        foreach (var s in ch.ActiveSpells)
        {
            if (s != null && s.spellName == spellName) return s;
        }
        return null;
    }

    // =========================================================
    // Placement 1v1 (cases déploiement équipe A / B)
    // =========================================================

    /// <summary>Soumet un placement ; répliqué sur toutes les instances après validation MasterClient.</summary>
    public void SubmitPlacement(int combatSlot, int gridX, int gridY)
    {
        if (!ShouldSendCommandsOverNetwork) return;
        if (PhotonNetwork.IsMasterClient)
            ValidatePlacementAndBroadcast(combatSlot, gridX, gridY, PhotonNetwork.LocalPlayer);
        else
            _pv.RPC(nameof(RpcMasterValidatePlacement), RpcTarget.MasterClient, combatSlot, gridX, gridY);
    }

    [PunRPC]
    void RpcMasterValidatePlacement(int combatSlot, int gridX, int gridY, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!SenderMayControlSlot(combatSlot, info.Sender)) return;
        ValidatePlacementAndBroadcast(combatSlot, gridX, gridY, info.Sender);
    }

    void ValidatePlacementAndBroadcast(int combatSlot, int gridX, int gridY, Player _)
    {
        var ci = CombatInitializer.Instance;
        var ag = ci != null ? ci.arenaGenerator : null;
        if (ci == null || ag == null || GridManager.Instance == null)
        {
            if (logRpc) Debug.LogWarning("[OracleCombatNet] Placement : scène pas prête.");
            return;
        }
        if (ci.CurrentPhase != CombatInitializer.CombatPhase.Placement)
        {
            if (logRpc) Debug.LogWarning("[OracleCombatNet] Placement ignoré (phase != Placement).");
            return;
        }

        var cell = GridManager.Instance.GetCell(gridX, gridY);
        if (cell == null || cell.IsOccupied)
        {
            if (logRpc) Debug.LogWarning("[OracleCombatNet] Placement refusé (cellule).");
            return;
        }

        int teamNum = combatSlot == 0 ? 1 : 2;
        var spawns = ag.GetSpawnCells(teamNum);
        if (spawns == null || !spawns.Contains(cell))
        {
            if (logRpc) Debug.LogWarning($"[OracleCombatNet] Placement refusé (hors spawn équipe {teamNum}).");
            return;
        }

        _pv.RPC(nameof(RpcApplyPlacement), RpcTarget.All, combatSlot, gridX, gridY);
    }

    [PunRPC]
    void RpcApplyPlacement(int combatSlot, int gridX, int gridY)
    {
        var cell = GridManager.Instance != null ? GridManager.Instance.GetCell(gridX, gridY) : null;
        if (CombatInitializer.Instance == null || cell == null)
        {
            if (logRpc) Debug.LogWarning("[OracleCombatNet] RpcApplyPlacement ignoré.");
            return;
        }
        if (logRpc) Debug.Log($"[OracleCombatNet] ApplyPlacement slot={combatSlot} @({gridX},{gridY})");
        CombatInitializer.Instance.OnNetworkPlacementApplied(combatSlot, cell);
    }
}
