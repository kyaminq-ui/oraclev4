using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Point d’entrée Photon PUN 2 : connexion, room, callbacks. Le combat synchro est dans <see cref="OracleCombatNetBridge"/> (même GameObject).
/// Nécessite un <see cref="PhotonView"/> sur ce GameObject (pour le bridge).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PhotonView))]
public class OracleNetworkHub : MonoBehaviourPunCallbacks
{
    public static OracleNetworkHub Instance { get; private set; }

    [Header("Connexion")]
    [Tooltip("Si vrai, ConnectUsingSettings() au démarrage.")]
    public bool autoConnectOnStart = true;
    [Tooltip("Sépare les joueurs par version (obligatoire si tu changes le protocole réseau).")]
    public string gameVersion = "oracle-dev-1";
    [Tooltip("Active la synchro de scène Photon (LoadLevel côté master répliqué). À utiliser quand le flux menu → combat sera prêt.")]
    public bool automaticallySyncScene = false;

    [Header("Room (MVP 1v1)")]
    public byte maxPlayersInRoom = 2;
    [Tooltip("Préfixe du nom de room en dev. La room complète sera prefix + suffix aléatoire si join random échoue.")]
    public string devRoomNamePrefix = "oracle_";
    [Tooltip("Après join room : charge cette scène si non vide (nécessite Automatically Sync Scene ou gestion manuelle).")]
    public string combatSceneName = "";

    [Header("Debug")]
    public bool logPhotonCallbacks = true;

    /// <summary>Connecté à Photon et prêt à rejoindre / créer une room.</summary>
    public bool IsReadyForRoom { get; private set; }

    public event Action OnReadyForRoom;
    public event Action OnLeftRoomEvent;
    public event Action<Player> OnRemotePlayerJoined;
    public event Action<Player> OnRemotePlayerLeft;
    public event Action<string> OnStatusMessage;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        PhotonNetwork.AutomaticallySyncScene = automaticallySyncScene;
    }

    void Start()
    {
        if (autoConnectOnStart)
            Connect();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // =========================================================
    // API — Connexion / Room (appeler depuis UI boutons plus tard)
    // =========================================================

    /// <summary>Démarre la connexion au cloud (ou rejoint une room si déjà connecté).</summary>
    public void Connect()
    {
        PhotonNetwork.GameVersion = gameVersion;
        if (PhotonNetwork.IsConnected)
        {
            JoinOrCreateQuickMatchRoom();
            return;
        }
        EmitStatus("Connexion Photon…");
        PhotonNetwork.ConnectUsingSettings();
    }

    public void Disconnect()
    {
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();
        IsReadyForRoom = false;
    }

    /// <summary>Join random parmi les rooms vides / incomplets, sinon création.</summary>
    public void JoinOrCreateQuickMatchRoom()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            EmitStatus("Pas encore prêt — connecte d’abord.");
            return;
        }
        EmitStatus("Recherche d’une room…");
        // PUN 2 : expectedMaxPlayers filtre les rooms (0 = n’importe quelle taille).
        PhotonNetwork.JoinRandomRoom(null, maxPlayersInRoom);
        // Si aucune room : callback OnJoinRandomFailed → CreateRoom
    }

    public void CreateNamedRoom(string roomName)
    {
        if (!PhotonNetwork.IsConnectedAndReady) return;
        var opts = new RoomOptions { MaxPlayers = maxPlayersInRoom, IsVisible = true, IsOpen = true };
        PhotonNetwork.CreateRoom(roomName, opts, TypedLobby.Default);
    }

    // =========================================================
    // PunCallbacks
    // =========================================================

    public override void OnConnected()
    {
        if (logPhotonCallbacks) Debug.Log("[OracleNet] OnConnected");
    }

    public override void OnConnectedToMaster()
    {
        if (logPhotonCallbacks) Debug.Log("[OracleNet] OnConnectedToMaster");
        IsReadyForRoom = true;
        OnReadyForRoom?.Invoke();
        EmitStatus("Connecté — prêt pour une room.");
        if (autoConnectOnStart)
            JoinOrCreateQuickMatchRoom();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (logPhotonCallbacks) Debug.Log($"[OracleNet] OnDisconnected: {cause}");
        IsReadyForRoom = false;
        EmitStatus($"Déconnecté : {cause}");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        if (logPhotonCallbacks) Debug.Log($"[OracleNet] OnJoinRandomFailed {returnCode} {message}");
        string roomName = devRoomNamePrefix + Guid.NewGuid().ToString("N").Substring(0, 8);
        EmitStatus($"Création room « {roomName} »…");
        CreateNamedRoom(roomName);
    }

    public override void OnJoinedRoom()
    {
        if (logPhotonCallbacks)
            Debug.Log($"[OracleNet] OnJoinedRoom {PhotonNetwork.CurrentRoom.Name} master={PhotonNetwork.IsMasterClient}");
        EmitStatus($"Dans la room — MasterClient={(PhotonNetwork.IsMasterClient ? "toi" : "autre joueur")}");

        if (!string.IsNullOrEmpty(combatSceneName) && PhotonNetwork.IsMasterClient && automaticallySyncScene)
            PhotonNetwork.LoadLevel(combatSceneName);
    }

    public override void OnLeftRoom()
    {
        if (logPhotonCallbacks) Debug.Log("[OracleNet] OnLeftRoom");
        OnLeftRoomEvent?.Invoke();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (logPhotonCallbacks) Debug.Log($"[OracleNet] Player entered {newPlayer.NickName} #{newPlayer.ActorNumber}");
        OnRemotePlayerJoined?.Invoke(newPlayer);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (logPhotonCallbacks) Debug.Log($"[OracleNet] Player left {otherPlayer.NickName}");
        OnRemotePlayerLeft?.Invoke(otherPlayer);
        // Roadmap : déconnexion adversaire → défaite / abandon. À brancher sur fin de match.
    }

    // Le combat réseau (RPC déplacement / sorts / fin de tour) est dans OracleCombatNetBridge sur ce même GameObject.

    void EmitStatus(string msg)
    {
        OnStatusMessage?.Invoke(msg);
        if (logPhotonCallbacks) Debug.Log($"[OracleNet] {msg}");
    }
}
