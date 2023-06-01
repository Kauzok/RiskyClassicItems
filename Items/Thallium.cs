﻿using BepInEx.Configuration;
using R2API;
using RiskyClassicItems.Modules;
using RoR2;
using UnityEngine;

namespace RiskyClassicItems.Items
{//https://github.com/swuff-star/LostInTransit/blob/0fc3e096621a2ce65eef50f0e82db125c0730260/LIT/Assets/LostInTransit/Modules/Pickups/Items/Thallium.cs
    public class Thallium : ItemBase<Thallium>
    {
        public float chance = 10f;
        public float chancePerStack = 5f;
        public float enemyAttackDamageCoef = 5f;
        public float enemyMoveSpeedCoef = 1f;
        public float duration = 3f;


        public override string ItemName => "Thallium";

        public override string ItemLangTokenName => "THALLIUM";

        public override string[] ItemFullDescriptionParams => new string[]
        {
            chance.ToString(),
            chancePerStack.ToString(),
            (enemyAttackDamageCoef * 100f).ToString(),
            (enemyMoveSpeedCoef * 100).ToString(),
            duration.ToString(),
        };

        public override ItemTier Tier => ItemTier.Tier1;

        public override GameObject ItemModel => LoadModel();

        public override Sprite ItemIcon => LoadSprite();

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateItem();
            Hooks();
        }

        public override void CreateConfig(ConfigFile config)
        {
        }

        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            return new ItemDisplayRuleDict();
        }

        public override void Hooks()
        {
            Events.PostOnHitEnemy += Events_PostOnHitEnemy;
        }

        private void Events_PostOnHitEnemy(DamageInfo obj, GameObject victimGameObject)
        {
            if (!obj.attacker || !victimGameObject || victimGameObject.TryGetComponent(out CharacterBody victimBody) || !obj.attacker.TryGetComponent(out CharacterBody attackerBody) || !TryGetCount(attackerBody, out var count) || !Util.CheckRoll(Utils.ItemHelpers.StackingLinear(count, chance, chancePerStack)))
                return;
            DotController.InflictDot(victimGameObject, obj.attacker, Modules.Dots.ThalliumDotIndex, duration);
        }
    }
}