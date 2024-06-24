﻿using BepInEx.Configuration;
using R2API;
using RoR2;
using RoR2.Items;
using UnityEngine;
using UnityEngine.Networking;

namespace ClassicItemsReturns.Items
{
    internal class ArmsRace : ItemBase<ArmsRace>
    {
        public float cooldown = 10;
        public int missileCount = 4;
        public int missileCountPerStack = 4;
        public float damageCoeff = 1;

        public static ConfigEntry<bool> requireMechanical;

        public override string ItemName => "Arms Race";
        public override string ItemLangTokenName => "ARMSRACE";
        public override object[] ItemFullDescriptionParams => new object[]
        {
            cooldown,
            missileCount,
            missileCountPerStack,
            (damageCoeff*100f)
        };

        public override ItemTier Tier => ItemTier.Tier2;

        public override GameObject ItemModel => LoadItemModel("ArmsRace");

        public override Sprite ItemIcon => LoadItemSprite("ArmsRace");

        public static ItemDef ArmsRaceDroneItemDef => ArmsRaceDroneItem.Instance.ItemDef;

        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.AIBlacklist,
            ItemTag.BrotherBlacklist,
            ItemTag.Damage
        };

        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            return new ItemDisplayRuleDict();
        }

        public override void CreateConfig(ConfigFile config)
        {
            ArmsRace.requireMechanical = config.Bind(ConfigCategory, "Mechanical Allies Only", true, "Arms Race only applies to Drones and other Mechanical allies.");
        }

        public class ArmsRaceBehavior : BaseItemBodyBehavior
        {
            [ItemDefAssociation(useOnClient = false, useOnServer = true)]
            public static ItemDef GetItemDef() => ArmsRace.Instance.ItemDef;

            public MinionOwnership minionOwnership;

            private const float display2Chance = 0.1f;

            private int previousStack;

            private Xoroshiro128Plus rng;

            private Inventory inventory;
            private CharacterMaster master;

            private void OnEnable()
            {
                ulong seed = Run.instance.seed ^ (ulong)((long)Run.instance.stageClearCount);
                this.rng = new Xoroshiro128Plus(seed);
                MasterSummon.onServerMasterSummonGlobal += MasterSummon_onServerMasterSummonGlobal;

                if (body.inventory)
                {
                    inventory = body.inventory;
                    inventory.onInventoryChanged += Inv_onInventoryChanged;
                }

                if (body.master) master = body.master;

                minionOwnership = body.GetComponent<MinionOwnership>();
                UpdateAllMinions(inventory.GetItemCount(ArmsRace.Instance.ItemDef));
            }

            private void Inv_onInventoryChanged()
            {
                UpdateAllMinions(inventory.GetItemCount(ArmsRace.Instance.ItemDef));
            }

            private void OnDisable()
            {
                MasterSummon.onServerMasterSummonGlobal -= MasterSummon_onServerMasterSummonGlobal;
                if (inventory)
                {
                    inventory.onInventoryChanged -= Inv_onInventoryChanged;
                }
                this.UpdateAllMinions(0);
            }

            private void UpdateAllMinions(int newStack)
            {
                if (NetworkServer.active && master)
                {
                    MinionOwnership.MinionGroup minionGroup = MinionOwnership.MinionGroup.FindGroup(master.netId);
                    if (minionGroup != null)
                    {
                        foreach (MinionOwnership minionOwnership in minionGroup.members)
                        {
                            if (!minionOwnership) continue;
                            CharacterMaster component = minionOwnership.GetComponent<CharacterMaster>();
                            if (component && component.inventory)
                            {
                                CharacterBody body2 = component.GetBody();
                                if (body2)
                                {
                                    this.UpdateMinionInventory(component.inventory, body2.bodyFlags, newStack);
                                }
                            }
                        }
                        this.previousStack = newStack;
                    }
                }
            }

            private void MasterSummon_onServerMasterSummonGlobal(MasterSummon.MasterSummonReport summonReport)
            {
                if (body && body.master && body.master == summonReport.leaderMasterInstance)
                {
                    CharacterMaster summonMasterInstance = summonReport.summonMasterInstance;
                    if (summonMasterInstance)
                    {
                        CharacterBody body = summonMasterInstance.GetBody();
                        if (body)
                        {
                            UpdateMinionInventory(summonMasterInstance.inventory, body.bodyFlags, stack);
                        }
                    }
                }
            }

            private void UpdateMinionInventory(Inventory inventory, CharacterBody.BodyFlags bodyFlags, int newStack)
            {
                if (newStack < 0) newStack = 0;
                if (inventory && (!ArmsRace.requireMechanical.Value || bodyFlags.HasFlag(CharacterBody.BodyFlags.Mechanical)))
                {
                    int itemCount = inventory.GetItemCount(ArmsRaceDroneItemDef);
                    //Need to use a seperate display cause the display add/removal conflicts with spare drone parts
                    //int itemCount2 = inventory.GetItemCount(DLC1Content.Items.DroneWeaponsDisplay1);
                    //int itemCount3 = inventory.GetItemCount(DLC1Content.Items.DroneWeaponsDisplay2);
                    if (itemCount < newStack)
                    {
                        inventory.GiveItem(ArmsRaceDroneItemDef, newStack - itemCount);
                    }
                    else if (itemCount > newStack)
                    {
                        inventory.RemoveItem(ArmsRaceDroneItemDef, itemCount - newStack);
                    }
                    /*if (itemCount2 + itemCount3 <= 0)
                    {
                        ItemDef itemDef = DLC1Content.Items.DroneWeaponsDisplay1;
                        if (UnityEngine.Random.value < display2Chance)
                        {
                            itemDef = DLC1Content.Items.DroneWeaponsDisplay2;
                        }
                        inventory.GiveItem(itemDef, 1);
                        return;
                    }*/
                }
                else
                {
                    inventory.ResetItem(DLC1Content.Items.DroneWeaponsBoost);
                    //inventory.ResetItem(DLC1Content.Items.DroneWeaponsDisplay1);
                    //inventory.ResetItem(DLC1Content.Items.DroneWeaponsDisplay2);
                }
            }
        }
    }
}