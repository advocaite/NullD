﻿/*
 * Copyright (C) 2011 - 2018 NullD project
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */

using NullD.Core.GS.Items;
using NullD.Net.GS.Message.Fields;
using NullD.Net.GS.Message;
using System.Collections.Generic;
using NullD.Core.GS.Objects;

namespace NullD.Core.GS.Players
{
    // these ids are transmitted by the client when equipping an item
    public enum EquipmentSlotId
    {
        Inventory = 0, Helm = 1, Chest = 2, Off_Hand = 3, Main_Hand = 4, Hands = 5, Belt = 6, Feet = 7,
        Shoulders = 8, Legs = 9, Bracers = 10, Ring_right = 11, Ring_left = 12, Neck = 13,
        Skills = 14, Stash = 15, Gold = 16, StashSize = 17, Vendor = 18, Socket = 20,
        PetRightHand = 21,
        PetLeftHand = 22,
        PetSpecial = 23,
        PetNeck = 24,
        PetRightFinger = 25,
        PetLeftFinger = 26, // TODO: Should this be here? Its not really an eq. slot /fasbat
    }

    class Equipment : IRevealable
    {
        public int EquipmentSlots { get { return _equipment.GetLength(0); } }
        public Dictionary<uint, Item> Items { get; private set; }
        private readonly Player _owner; // Used, because most information is not in the item class but Actors managed by the world

        private uint[] _equipment;      // array of equiped items_id (not item)

        public Equipment(Player owner)
        {
            this._equipment = new uint[27];
            this._owner = owner;
            this.Items = new Dictionary<uint, Item>();
        }

        /// <summary>
        /// Equips an item in an equipment slot
        /// </summary>
        public void EquipItem(Item item, int slot)
        {
            if (item != null)
            {
                _equipment[slot] = item.DynamicID;
                if (!Items.ContainsKey(item.DynamicID))
                    Items.Add(item.DynamicID, item);
                item.Owner = _owner;
                item.Attributes[GameAttribute.Item_Equipped] = true; // Probaly should be handled by Equipable class /fasbat
                item.Attributes.SendChangedMessage(_owner.InGameClient);
                AddBonus(item, _owner);
                item.SetInventoryLocation(slot, 0, 0);
            }
        }

        public void AddBonus(Item item, Player Master)
        {
            #region Damage Bonus
            var a = Master.Attributes[GameAttribute.Damage_Weapon_Bonus_Min, 0];
            var b = item.Attributes[GameAttribute.Damage_Min, 0];
            Master.Attributes[GameAttribute.Damage_Weapon_Bonus_Min, 0] += item.Attributes[GameAttribute.Damage_Min, 0];
            Master.Attributes[GameAttribute.Damage_Weapon_Bonus_Delta, 0] += item.Attributes[GameAttribute.Damage_Delta, 0];
            var c = Master.Attributes[GameAttribute.Damage_Weapon_Bonus_Min, 0];
            Master.Attributes.BroadcastChangedIfRevealed();
            #endregion

            var s1 = Master.Attributes[GameAttribute.Strength_Item];
            var s2 = Master.Attributes[GameAttribute.Strength_Item, 0];

            var d1 = Master.Attributes[GameAttribute.Dexterity_Item];
            var d2 = Master.Attributes[GameAttribute.Dexterity_Item, 0];

            var i1 = Master.Attributes[GameAttribute.Intelligence_Item];
            var i2 = Master.Attributes[GameAttribute.Intelligence_Item, 0];

            #region Бонус к магическому запасу
            Master.Attributes[GameAttribute.Resource_Max_Bonus, 1] += item.Attributes[GameAttribute.Resource_Max_Bonus, 1];
            #endregion


        }
        public void DeleteBonus(Item item, Player Master)
        {
            #region Damage Bonus
            var a = Master.Attributes[GameAttribute.Damage_Weapon_Min, 0];
            var b = Master.Attributes[GameAttribute.Damage_Weapon_Delta, 0];
            Master.Attributes[GameAttribute.Damage_Weapon_Bonus_Min, 0] -= item.Attributes[GameAttribute.Damage_Min, 0];
            Master.Attributes[GameAttribute.Damage_Weapon_Bonus_Delta, 0] -= item.Attributes[GameAttribute.Damage_Delta, 0];
            Master.Attributes.BroadcastChangedIfRevealed();
            #endregion

            #region Бонус к магическому запасу
            Master.Attributes[GameAttribute.Resource_Max_Bonus, 1] -= item.Attributes[GameAttribute.Resource_Max_Bonus, 1];
            #endregion
        }

        public void EquipItem(uint itemID, int slot)
        {
            EquipItem(_owner.Inventory.GetItem(itemID), slot);
        }

        /// <summary>
        /// Removes an item from the equipment slot it uses
        /// returns the used equipmentSlot
        /// </summary>
        public int UnequipItem(Item item)
        {
            if (!Items.ContainsKey(item.DynamicID))
                return 0;
            Items.Remove(item.DynamicID);

            var slot = item.EquipmentSlot;
            if (_equipment[slot] == item.DynamicID)
            {
                _equipment[slot] = 0;
                item.Attributes[GameAttribute.Item_Equipped] = false; // Probaly should be handled by Equipable class /fasbat
                item.Attributes.SendChangedMessage(_owner.InGameClient);
                DeleteBonus(item, _owner);
                return slot;
            }

            return 0;
        }

        /// <summary>
        /// Returns whether an item is equipped
        /// </summary>
        public bool IsItemEquipped(uint itemID)
        {
            return Items.ContainsKey(itemID);
        }

        public bool IsItemEquipped(Item item)
        {
            return IsItemEquipped(item.DynamicID);
        }

        private VisualItem GetEquipmentItem(EquipmentSlotId equipSlot)
        {
            if (_equipment[(int)equipSlot] == 0)
            {
                return new VisualItem()
                {
                    GbId = -1, // 0 causes error logs on the client  - angerwin
                    Field1 = 0,
                    Field2 = 0,
                    Field3 = 0,
                };
            }

            return Items[(_equipment[(int)equipSlot])].CreateVisualItem();
        }

        private D3.Hero.VisualItem GetEquipmentItemForToon(EquipmentSlotId equipSlot)
        {
            if (_equipment[(int)equipSlot] == 0)
            {
                return D3.Hero.VisualItem.CreateBuilder()
                    .SetGbid(-1)
                    .SetDyeType(0)
                    .SetEffectLevel(0)
                    .SetItemEffectType(-1)
                    .Build();
            }

            return Items[(_equipment[(int)equipSlot])].GetVisualItem();
        }

        public VisualItem[] GetVisualEquipment()
        {
            return new VisualItem[8]
                    {
                        GetEquipmentItem(EquipmentSlotId.Helm),
                        GetEquipmentItem(EquipmentSlotId.Chest),
                        GetEquipmentItem(EquipmentSlotId.Feet),
                        GetEquipmentItem(EquipmentSlotId.Hands),
                        GetEquipmentItem(EquipmentSlotId.Main_Hand),
                        GetEquipmentItem(EquipmentSlotId.Off_Hand),
                        GetEquipmentItem(EquipmentSlotId.Shoulders),
                        GetEquipmentItem(EquipmentSlotId.Legs),
                    };
        }
        public VisualItem[] GetVisualEquipmentHireling()
        {
            return new VisualItem[6]
                    {
                        GetEquipmentItem(EquipmentSlotId.PetLeftHand),
                        GetEquipmentItem(EquipmentSlotId.PetRightHand),
                        GetEquipmentItem(EquipmentSlotId.PetSpecial),
                        GetEquipmentItem(EquipmentSlotId.PetNeck),
                        GetEquipmentItem(EquipmentSlotId.PetRightFinger),
                        GetEquipmentItem(EquipmentSlotId.PetLeftFinger),
                    };
        }

        public D3.Hero.VisualEquipment GetVisualEquipmentForToon()
        {
            var visualItems = new[]
            {       
                    GetEquipmentItemForToon(EquipmentSlotId.Helm),
                    GetEquipmentItemForToon(EquipmentSlotId.Chest),
                    GetEquipmentItemForToon(EquipmentSlotId.Feet),
                    GetEquipmentItemForToon(EquipmentSlotId.Hands),
                    GetEquipmentItemForToon(EquipmentSlotId.Main_Hand),
                    GetEquipmentItemForToon(EquipmentSlotId.Off_Hand),
                    GetEquipmentItemForToon(EquipmentSlotId.Shoulders),
                    GetEquipmentItemForToon(EquipmentSlotId.Legs),
            };
            return D3.Hero.VisualEquipment.CreateBuilder().AddRangeVisualItem(visualItems).Build();
        }

        internal Item GetEquipment(int targetEquipSlot)
        {
            return GetItem(this._equipment[targetEquipSlot]);
        }

        internal Item GetEquipment(EquipmentSlotId targetEquipSlot)
        {
            return GetEquipment((int)targetEquipSlot);
        }

        public bool Reveal(Player player)
        {
            foreach (var item in Items.Values)
            {
                item.Reveal(player);
            }
            return true;
        }

        public bool Unreveal(Player player)
        {
            foreach (var item in Items.Values)
            {
                item.Unreveal(player);
            }
            return true;
        }

        public Item GetItem(uint itemId)
        {
            Item item;
            if (!Items.TryGetValue(itemId, out item))
                return null;
            return item;
        }
    }
}
