﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Network = EmeraldNetwork.Network;
using C = ClientPackets;
using S = ServerPackets;

public class GameSceneManager : MonoBehaviour
{
    protected static UserObject User
    {
        get { return GameManager.User; }
    }

    public TMP_InputField ChatBar;      
    public Scrollbar ScrollBar;
    public ChatController ChatController;
    public EventSystem eventSystem;
    public TMP_Text CharacterName;
    public TMP_Text CharacterLevel;
    public Image CharacterIcon;
    public Sprite[] CharacterIcons = new Sprite[Enum.GetNames(typeof(MirClass)).Length * Enum.GetNames(typeof(MirGender)).Length];
    public Button AttackModeButton;
    public TMP_Text AttackModeText;
    public Sprite[] AttackModeIcons = new Sprite[Enum.GetNames(typeof(AttackMode)).Length];
    public Sprite[] AttackModeHoverIcons = new Sprite[Enum.GetNames(typeof(AttackMode)).Length];
    public Sprite[] AttackModeDownIcons = new Sprite[Enum.GetNames(typeof(AttackMode)).Length];    

    [HideInInspector]
    public InventoryController Inventory;
    public Image SelectedItemImage;
    public MirItemCell[] EquipmentCells = new MirItemCell[14];
    public MirItemCell[] BeltCells = new MirItemCell[6];

    [HideInInspector]
    public bool PickedUpGold;

    private MirItemCell _selectedCell;
    [HideInInspector]
    public MirItemCell SelectedCell
    {
        get { return _selectedCell; }
        set
        {
            if (_selectedCell == value) return;

            _selectedCell = value;
            OnSelectedCellChanged();
        }
    }

    private void OnSelectedCellChanged()
    {
        if (SelectedCell == null)
            SelectedItemImage.gameObject.SetActive(false);
        else
        {
            SelectedItemImage.gameObject.SetActive(true);
            SelectedItemImage.transform.position = Input.mousePosition;
            SelectedItemImage.sprite = Resources.Load<Sprite>($"UI/Items/{SelectedCell.Item.Info.Image}");
        }
    }

    void Awake()
    {
        GameManager.GameScene = this;             
    }

    void Start()
    {
        ScrollBar.size = 0.4f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (ChatBar.gameObject.activeSelf)
            {
                
                if (ChatBar.text.Length > 0)
                    Network.Enqueue(new C.Chat() { Message = ChatBar.text });
                ChatBar.text = string.Empty;
                ChatBar.gameObject.SetActive(false);
            }
            else
            {
                ChatBar.gameObject.SetActive(true);
                ChatBar.Select();
            }
        }

        if ((Input.GetMouseButton(0) || Input.GetMouseButton(1)) && !eventSystem.IsPointerOverGameObject())
        {
            GameManager.CheckMouseInput();
        }

        if (SelectedItemImage.gameObject.activeSelf)
        {
            SelectedItemImage.transform.position = Input.mousePosition;
            SelectedItemImage.transform.SetAsLastSibling();
        }
    }

    public void MoveItem(S.MoveItem p)
    {
        MirItemCell toCell, fromCell;

        switch (p.Grid)
        {
            case MirGridType.Inventory:
                fromCell = p.From < User.BeltIdx ? BeltCells[p.From] : Inventory.Cells[p.From];
                break;
            default:
                return;
        }

        switch (p.Grid)
        {
            case MirGridType.Inventory:
                toCell = p.To < User.BeltIdx ? BeltCells[p.To] : Inventory.Cells[p.To];
                break;
            default:
                return;
        }

        if (toCell == null || fromCell == null) return;

        toCell.Locked = false;
        fromCell.Locked = false;

        if (!p.Success) return;

        UserItem i = fromCell.Item;
        fromCell.Item = toCell.Item;
        toCell.Item = i;
    }

    public MirItemCell GetCell(MirItemCell[] cells, ulong id)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].Item == null || cells[i].Item.UniqueID != id) continue;
            return cells[i];
        }
        return null;
    }

    public void EquipItem(S.EquipItem p)
    {
        MirItemCell fromCell;
        MirItemCell toCell = EquipmentCells[p.To];

        switch (p.Grid)
        {
            case MirGridType.Inventory:
                fromCell = GetCell(Inventory.Cells, p.UniqueID) ?? GetCell(BeltCells, p.UniqueID);
                break;
            /*case MirGridType.Storage:
                fromCell = StorageDialog.GetCell(p.UniqueID) ?? BeltDialog.GetCell(p.UniqueID);
                break;*/
            default:
                return;
        }

        if (toCell == null || fromCell == null) return;

        toCell.Locked = false;
        fromCell.Locked = false;

        if (!p.Success) return;

        UserItem i = fromCell.Item;
        fromCell.Item = toCell.Item;
        toCell.Item = i;
        //User.RefreshStats();
    }

    public void RemoveItem(S.RemoveItem p)
    {
        MirItemCell toCell;

        int index = -1;

        for (int i = 0; i < User.Equipment.Length; i++)
        {
            if (User.Equipment[i] == null || User.Equipment[i].UniqueID != p.UniqueID) continue;
            index = i;
            break;
        }

        MirItemCell fromCell = EquipmentCells[index];


        switch (p.Grid)
        {
            case MirGridType.Inventory:
                toCell = p.To < User.BeltIdx ? BeltCells[p.To] : Inventory.Cells[p.To - User.BeltIdx];
                break;
            /*case MirGridType.Storage:
                toCell = StorageDialog.Grid[p.To];
                break;*/
            default:
                return;
        }

        if (toCell == null || fromCell == null) return;

        toCell.Locked = false;
        fromCell.Locked = false;

        if (!p.Success) return;
        toCell.Item = fromCell.Item;
        fromCell.Item = null;
        //User.RefreshStats();
    }

    public void UpdateCharacterIcon()
    {
        CharacterIcon.sprite = CharacterIcons[(int)GameManager.User.Player.Class * 2 + (int)GameManager.User.Player.Gender];
        CharacterName.text = GameManager.User.Player.name;
        CharacterLevel.text = GameManager.User.Level.ToString();
    }

    public void ChangeAttackMode(int amode)
    {
        if (amode >= Enum.GetNames(typeof(AttackMode)).Length) return;
        Network.Enqueue(new C.ChangeAMode() { Mode = (AttackMode)amode });
    }

    public void SetAttackMode(AttackMode amode)
    {
        AttackModeButton.GetComponent<Image>().sprite = AttackModeIcons[(int)amode];

        SpriteState state = new SpriteState();
        state = AttackModeButton.spriteState;
        state.highlightedSprite = AttackModeHoverIcons[(int)amode];
        state.pressedSprite = AttackModeDownIcons[(int)amode];

        AttackModeButton.spriteState = state;

        AttackModeText.text = amode.ToString();
    }


}
