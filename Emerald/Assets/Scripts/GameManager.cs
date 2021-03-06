﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Network = EmeraldNetwork.Network;
using S = ServerPackets;

public class GameManager : MonoBehaviour
{
    public List<GameObject> WarriorModels;

    private GameObject UserGameObject;
    private Dictionary<uint, PlayerObject> Players = new Dictionary<uint, PlayerObject>();
    [HideInInspector]
    public static List<ItemInfo> ItemInfoList = new List<ItemInfo>();

    [HideInInspector]
    public static NetworkInfo networkInfo;
    [HideInInspector]
    public static GameStage gameStage;
    [HideInInspector]
    public static GameSceneManager GameScene;
    [HideInInspector]
    public static UserObject User;    
    [HideInInspector]
    public static MirScene CurrentScene;
    [HideInInspector]
    public static float NextAction;
    [HideInInspector]
    public static float InputDelay;
    [HideInInspector]
    public static bool UIDragging;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    // Start is called before the first frame update
    void Start()
    {
        gameStage = GameStage.Login;

        networkInfo = new NetworkInfo();
        string fileName = Application.dataPath + "/Settings.json";
        string json = string.Empty;

        if (File.Exists(fileName))
            json = File.ReadAllText(fileName);
        else
        {
            json = JsonUtility.ToJson(networkInfo);
            File.WriteAllText(fileName, json);
        }
        
        networkInfo = JsonUtility.FromJson<NetworkInfo>(json);
        Network.Connect();
    }

    public void MapInformation(S.MapInformation p)
    {
        FindObjectOfType<LoadScreenManager>().LoadScene(p.FileName);
        //SceneManager.LoadSceneAsync(p.FileName, LoadSceneMode.Additive);
    }

    public void UserInformation(S.UserInformation p)
    {
        User.gameObject.SetActive(true);
        UserGameObject = Instantiate(WarriorModels[0], User.transform.position, Quaternion.identity);
        
        User.Player = UserGameObject.GetComponent<PlayerObject>();
        User.Player.name = p.Name;
        User.Player.Class = p.Class;
        User.Player.Gender = p.Gender;
        User.Level = p.Level;
        
        GameScene.UpdateCharacterIcon();

        User.Player.CurrentLocation = new Vector2(p.Location.X, p.Location.Y);
        UserGameObject.transform.position = CurrentScene.Cells[(int)User.Player.CurrentLocation.x, (int)User.Player.CurrentLocation.y].position;

        User.Player.Direction = p.Direction;
        User.Player.Model.transform.rotation = ClientFunctions.GetRotation(User.Player.Direction);

        User.Inventory = p.Inventory;
        User.Equipment = p.Equipment;

        User.BindAllItems();

        Players.Add(p.ObjectID, User.Player);
        User.Player.Camera.SetActive(true);

        Tooltip.cam = User.Player.Camera.GetComponent<Camera>();
    }

    public void UserLocation(S.UserLocation p)
    {
        NextAction = 0;
    }

    public void AttackMode(S.ChangeAMode p)
    {
        GameScene.SetAttackMode(p.Mode);
    }

    public void ObjectPlayer(S.ObjectPlayer p)
    {
        PlayerObject player;
        if (Players.TryGetValue(p.ObjectID, out player))
        {
            player.CurrentLocation = new Vector2(p.Location.X, p.Location.Y);
            player.Direction = p.Direction;
            player.transform.position = CurrentScene.Cells[p.Location.X, p.Location.Y].position;
            player.Model.transform.rotation = ClientFunctions.GetRotation(p.Direction);
            player.gameObject.SetActive(true);
            return;
        }

        player = Instantiate(WarriorModels[0], CurrentScene.Cells[p.Location.X, p.Location.Y].position, Quaternion.identity).GetComponent<PlayerObject>();
        player.CurrentLocation = new Vector2(p.Location.X, p.Location.Y);
        player.Direction = p.Direction;
        player.Model.transform.rotation = ClientFunctions.GetRotation(p.Direction);
        Players.Add(p.ObjectID, player);        
    }

    public void ObjectWalk(S.ObjectWalk p)
    {
        if (Players.TryGetValue(p.ObjectID, out PlayerObject player))
        {
            player.ActionFeed.Add(new QueuedAction { Action = MirAction.Walking, Direction = p.Direction, Location = new Vector2(p.Location.X, p.Location.Y) });
        }
    }

    public void ObjectRun(S.ObjectRun p)
    {
        if (Players.TryGetValue(p.ObjectID, out PlayerObject player))
        {
            player.ActionFeed.Add(new QueuedAction { Action = MirAction.Running, Direction = p.Direction, Location = new Vector2(p.Location.X, p.Location.Y) });
        }
    }

    public void Chat(S.Chat p)
    {
        GameScene.ChatController.RecieveChat(p.Message, p.Type);
    }

    public void ObjectChat(S.ObjectChat p)
    {
        if (Players.TryGetValue(p.ObjectID, out PlayerObject player))
        {
            //player.ActionFeed.Add(new QueuedAction { Action = MirAction.Running, Direction = p.Direction, Location = new Vector2(p.Location.X, p.Location.Y) });
            GameScene.ChatController.RecieveChat(p.Text, p.Type);
        }
    }

    public void NewItemInfo(S.NewItemInfo info)
    {
        ItemInfoList.Add(info.Info);
    }

    void Update()
    {
        Network.Process();

        ProcessScene();
    }

    static MirDirection MouseDirection()
    {
        Vector2 mousePosition = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        Vector2 middle = new Vector2(Screen.width / 2, Screen.height / 2);

        Vector2 v2 = (mousePosition - middle);
        float angle = Mathf.Atan2(v2.y, v2.x) * Mathf.Rad2Deg;
        if (angle < 0)
            angle = 360 + angle;
        angle = 360 - angle;

        return Functions.MirDrectionFromAngle(angle);
    }

    public static void CheckMouseInput()
    {
        if (CurrentScene == null) return;
        if (User.Player == null) return;
        if (UIDragging) return;

        if (User.Player.ActionFeed.Count == 0 && Time.time > InputDelay)
        {
            if (Input.GetMouseButton(0))
            {
                MirDirection direction = MouseDirection();
                Vector2 newlocation = ClientFunctions.VectorMove(User.Player.CurrentLocation, direction, 1);
                if (CanWalk(newlocation))
                    User.Player.ActionFeed.Add(new QueuedAction { Action = MirAction.Walking, Direction = direction, Location = newlocation });

            }
            else if (Input.GetMouseButton(1))
            {
                MirDirection direction = MouseDirection();
                Vector2 newlocation = ClientFunctions.VectorMove(User.Player.CurrentLocation, direction, 1);
                if (User.WalkStep < 1)
                {
                    if (CanWalk(newlocation))
                        User.Player.ActionFeed.Add(new QueuedAction { Action = MirAction.Walking, Direction = direction, Location = newlocation });
                    User.WalkStep++;
                }
                else
                {
                    Vector2 farlocation = ClientFunctions.VectorMove(User.Player.CurrentLocation, direction, 2);
                    if (CanWalk(newlocation) && CanWalk(farlocation))
                        User.Player.ActionFeed.Add(new QueuedAction { Action = MirAction.Running, Direction = direction, Location = farlocation });
                }
            }
        }
    }

    public static void Bind(UserItem item)
    {
        for (int i = 0; i < ItemInfoList.Count; i++)
        {
            if (ItemInfoList[i].Index != item.ItemIndex) continue;

            item.Info = ItemInfoList[i];

            item.SetSlotSize();

            for (int s = 0; s < item.Slots.Length; s++)
            {
                if (item.Slots[s] == null) continue;

                Bind(item.Slots[s]);
            }

            return;
        }
    }

    void ProcessScene()
    {                
    }

    static bool CanWalk(Vector2 location)
    {
        return CurrentScene.Cells[(int)location.x, (int)location.y].walkable;
    }

    public class NetworkInfo
    {
        public string IPAddress = "127.0.0.1";
        public int Port = 7000;
    }
}
