﻿using BepInEx.Configuration;
using ClassicItemsReturns.Modules;
using IL.RoR2.EntityLogic;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace ClassicItemsReturns.Items.Rare
{
    public class USB : ItemBase<USB>
    {
        public override string ItemName => "Classified Access Codes";

        public override string ItemLangTokenName => "USB";

        public override ItemTier Tier => ItemTier.Tier3;

        public override GameObject ItemModel => LoadItemModel("USB");

        public override Sprite ItemIcon => LoadItemSprite("USB");

        public override bool unfinished => true;

        public static GameObject atlasCannonNetworkPrefab;
        public static GameObject teleporterVisualNetworkPrefab;
        public static GameObject atlasCannonInteractablePrefab;
        public static InteractableSpawnCard atlasCannonSpawnCard;

        public static bool cannonSpawned = false;
        public static bool cannonActivated = false;
        private static bool addedTeleporterVisual = false;
        
        //This is used for special stages that don't have a Teleporter interaction. Teleporter ignores this.
        private static bool firedCannon = false;

        private static GameObject teleporterVisualNetworkInstance;

        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            return new ItemDisplayRuleDict();
        }

        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.AIBlacklist,
            ItemTag.Damage
        };

        public static float CalcDamagePercent(int itemCount)
        {
            return 1f - (0.6f * Mathf.Pow(0.8f, itemCount - 1));
        }

        public override void CreateAssets(ConfigFile config)
        {
            atlasCannonNetworkPrefab = Assets.LoadObject("AtlasCannonTarget");
            atlasCannonNetworkPrefab.AddComponent<RoR2.Billboard>();
            atlasCannonNetworkPrefab.AddComponent<NetworkIdentity>();
            var controller = atlasCannonNetworkPrefab.AddComponent<AtlasCannonController>();
            atlasCannonNetworkPrefab.AddComponent<DestroyOnTimer>().duration = controller.delayBeforeFiring + controller.lifetimeAfterFiring + 2f;
            ContentAddition.AddNetworkedObject(atlasCannonNetworkPrefab);

            teleporterVisualNetworkPrefab = Assets.LoadObject("AtlasCannonTeleporterVisual");
            teleporterVisualNetworkPrefab.AddComponent<NetworkIdentity>();
            teleporterVisualNetworkPrefab.AddComponent<AtlasTeleporterBeamController>();
            ContentAddition.AddNetworkedObject(teleporterVisualNetworkPrefab);

            atlasCannonInteractablePrefab = Assets.LoadObject("AtlasCannonInteractable");
            atlasCannonInteractablePrefab.layer = LayerIndex.CommonMasks.interactable;
            atlasCannonInteractablePrefab.AddComponent<NetworkIdentity>();
            ChildLocator cl = atlasCannonInteractablePrefab.GetComponent<ChildLocator>();
            Transform modelTransform = cl.FindChild("Model");

            Highlight highlight = atlasCannonInteractablePrefab.AddComponent<Highlight>();
            highlight.targetRenderer = modelTransform.GetComponent<Renderer>();
            highlight.strength = 1f;
            highlight.highlightColor = Highlight.HighlightColor.interactive;
            highlight.isOn = false;

            PurchaseInteraction pi = atlasCannonInteractablePrefab.AddComponent<PurchaseInteraction>();
            pi.cost = 0;
            pi.costType = CostTypeIndex.None;
            pi.displayNameToken = "CLASSICITEMSRETURNS_INTERACTABLE_ATLASCANNON_NAME";
            pi.contextToken = "CLASSICITEMSRETURNS_INTERACTABLE_ATLASCANNON_CONTEXT";
            pi.setUnavailableOnTeleporterActivated = true;
            pi.isShrine = false;
            pi.isGoldShrine = false;
            pi.ignoreSpherecastForInteractability = false;
            pi.available = true;

            atlasCannonInteractablePrefab.AddComponent<AtlasCannonInteractableController>();

            EntityLocator el = atlasCannonInteractablePrefab.AddComponent<EntityLocator>();
            el.entity = atlasCannonInteractablePrefab;

            ContentAddition.AddNetworkedObject(atlasCannonInteractablePrefab);

            atlasCannonSpawnCard = ScriptableObject.CreateInstance<InteractableSpawnCard>();
            atlasCannonSpawnCard.maxSpawnsPerStage = 1;
            atlasCannonSpawnCard.occupyPosition = true;
            atlasCannonSpawnCard.prefab = atlasCannonInteractablePrefab;
            atlasCannonSpawnCard.slightlyRandomizeOrientation = false;
            atlasCannonSpawnCard.requiredFlags = RoR2.Navigation.NodeFlags.None;
            atlasCannonSpawnCard.orientToFloor = true;
            atlasCannonSpawnCard.hullSize = HullClassification.Human;
            atlasCannonSpawnCard.sendOverNetwork = false;
        }

        public override void Hooks()
        {
            RoR2.Stage.onStageStartGlobal += Stage_onStageStartGlobal;

            On.RoR2.TeleporterInteraction.ChargingState.OnEnter += ChargingState_OnEnter;

            On.RoR2.TeleporterInteraction.Start += TeleporterInteraction_Start;

            On.EntityStates.Missions.BrotherEncounter.Phase1.OnMemberAddedServer += Phase1_OnMemberAddedServer;
            On.EntityStates.Missions.Goldshores.GoldshoresBossfight.SetBossImmunity += GoldshoresBossfight_SetBossImmunity;
            On.RoR2.ScriptedCombatEncounter.Spawn += ScriptedCombatEncounter_Spawn;
            On.RoR2.SceneDirector.PopulateScene += SceneDirector_PopulateScene;
            On.RoR2.CharacterBody.OnInventoryChanged += CharacterBody_OnInventoryChanged;
        }

        private void CharacterBody_OnInventoryChanged(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            orig(self);
            if (NetworkServer.active && !cannonSpawned && self.inventory.GetItemCount(ItemDef) > 0)
            {
                //TODO
            }
        }

        private void SceneDirector_PopulateScene(On.RoR2.SceneDirector.orig_PopulateScene orig, SceneDirector self)
        {
            orig(self);
            if (NetworkServer.active
                && !cannonSpawned
                && TeleporterInteraction.instance
                && Util.GetItemCountForTeam(TeamIndex.Player, ItemDef.itemIndex, false, true) > 0)
            {
                PlaceAtlasCannonInteractable(self.rng);
            }
        }

        private void ScriptedCombatEncounter_Spawn(On.RoR2.ScriptedCombatEncounter.orig_Spawn orig, ScriptedCombatEncounter self, ref ScriptedCombatEncounter.SpawnInfo spawnInfo)
        {
            orig(self, ref spawnInfo);

            //Don't like how hardcoded this is.
            SceneDef currentScene = SceneCatalog.GetSceneDefForCurrentScene();
            if (currentScene && currentScene.cachedName == "voidraid" && !TeleporterInteraction.instance && !firedCannon && cannonActivated)
            {
                firedCannon = true;
                foreach (CharacterMaster master in CharacterMaster.readOnlyInstancesList)
                {
                    TargetCannon(master);
                }
            }
        }

        private void GoldshoresBossfight_SetBossImmunity(On.EntityStates.Missions.Goldshores.GoldshoresBossfight.orig_SetBossImmunity orig, EntityStates.Missions.Goldshores.GoldshoresBossfight self, bool newBossImmunity)
        {
            orig(self, newBossImmunity);

            if (NetworkServer.active && !newBossImmunity && !firedCannon && cannonActivated)
            {
                firedCannon = true;
                foreach (CharacterMaster master in self.scriptedCombatEncounter.combatSquad.readOnlyMembersList)
                {
                    TargetCannon(master);
                }
            }
        }

        private void TeleporterInteraction_Start(On.RoR2.TeleporterInteraction.orig_Start orig, TeleporterInteraction self)
        {
            orig(self);
            if (NetworkServer.active && self.bossGroup && self.bossGroup.combatSquad)
            {
                self.bossGroup.combatSquad.onMemberAddedServer += TargetCannon;
            }
        }

        private void Phase1_OnMemberAddedServer(On.EntityStates.Missions.BrotherEncounter.Phase1.orig_OnMemberAddedServer orig, EntityStates.Missions.BrotherEncounter.Phase1 self, CharacterMaster master)
        {
            orig(self, master);
            if (cannonActivated) TargetCannon(master);
        }

        private void TargetCannon(CharacterMaster master)
        {
            if (!NetworkServer.active || !cannonActivated) return;
            CharacterBody body = master.GetBody();
            if (body
                && (body.isChampion || body.isBoss)
                && body.healthComponent
                && body.teamComponent
                && TeamMask.GetEnemyTeams(TeamIndex.Player).HasTeam(body.teamComponent.teamIndex))
            {
                GameObject cannonObject = UnityEngine.Object.Instantiate(atlasCannonNetworkPrefab, body.transform);
                AtlasCannonController controller = cannonObject.GetComponent<AtlasCannonController>();
                if (controller)
                {
                    controller.targetHealthComponent = body.healthComponent;
                }
                NetworkServer.Spawn(cannonObject);
            }
        }

        //TODO: Tie this to activating the interactable
        public static void AddTeleporterVisualServer()
        {
            if (!NetworkServer.active) return;

            if (!TeleporterInteraction.instance
                || TeleporterInteraction.instance.currentState.GetType() != typeof(TeleporterInteraction.IdleState)) return;

            if (teleporterVisualNetworkInstance)
            {
                UnityEngine.Object.Destroy(teleporterVisualNetworkInstance);
                teleporterVisualNetworkInstance = null;
            }

            //Component on this will resolve the positioning clientside.
            teleporterVisualNetworkInstance = GameObject.Instantiate(teleporterVisualNetworkPrefab);
            NetworkServer.Spawn(teleporterVisualNetworkInstance);
    }

        private void ChargingState_OnEnter(On.RoR2.TeleporterInteraction.ChargingState.orig_OnEnter orig, EntityStates.BaseState self)
        {
            orig(self);
            if (NetworkServer.active && teleporterVisualNetworkInstance)
            {
                UnityEngine.Object.Destroy(teleporterVisualNetworkInstance);
                teleporterVisualNetworkInstance = null;
            }
        }

        private void Stage_onStageStartGlobal(Stage obj)
        {
            cannonActivated = false;
            addedTeleporterVisual = false;
            firedCannon = false;
            cannonSpawned = false;
        }

        public static void PlaceAtlasCannonInteractable(Xoroshiro128Plus rng)
        {
            DirectorPlacementRule placementRule = new DirectorPlacementRule
            {
                placementMode = DirectorPlacementRule.PlacementMode.Random
            };
            GameObject result = DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(atlasCannonSpawnCard, placementRule, rng));
            if (result)
            {
                Debug.Log("ClassicItemsReturns: Placed Atlas Cannon interactable.");
            }
            else
            {
                Debug.LogError("ClassicItemsReturns: Failed to place Atlas Cannon interactable.");
            }
        }
    }

    public class AtlasCannonController : NetworkBehaviour
    {
        public float delayBeforeFiring = 5f;
        public float lifetimeAfterFiring = 1f;
        public HealthComponent targetHealthComponent;
        public float laserFireWidth = 15f;

        [SyncVar]
        private bool _hasFired = false;
        private bool hasFiredLocal = false;

        private LineRenderer laser;
        private SpriteRenderer crosshairRenderer, rotatorRenderer;
        private Transform rotatorTransform;
        private uint soundId;

        private float stopwatch;
        private float rotationStopwatch;
        private float laserFadeStopwatch;

        private void Awake()
        {
            ChildLocator cl = base.GetComponent<ChildLocator>();
            if (cl)
            {
                Transform crosshairTransform = cl.FindChild("Crosshair");
                if (crosshairTransform)
                {
                    crosshairRenderer = crosshairTransform.GetComponent<SpriteRenderer>();
                }

                rotatorTransform = cl.FindChild("Rotator");
                if (rotatorTransform)
                {
                    rotatorRenderer = rotatorTransform.GetComponent<SpriteRenderer>();
                }

                Transform laserTransform = cl.FindChild("Laser");
                if(laserTransform)
                {
                    laser = laserTransform.GetComponent<LineRenderer>();
                }
            }
            stopwatch = 0f;
            rotationStopwatch = 0f;
            laserFadeStopwatch = 0f;
            soundId = Util.PlaySound("Play_captain_utility_variant_laser_loop", base.gameObject);
        }

        private void FixedUpdate()
        {
            //Hide targeting indicator after laser has been fired
            if (_hasFired && !hasFiredLocal)
            {
                hasFiredLocal = true;
                AkSoundEngine.StopPlayingID(soundId);
                Util.PlaySound("Play_captain_utility_variant_impact", base.gameObject);
                if (crosshairRenderer) crosshairRenderer.enabled = false;
                if (rotatorRenderer) rotatorRenderer.enabled = false;
                laser.widthMultiplier = laserFireWidth;
                stopwatch = 0f;
            }

            if (NetworkServer.active) FixedUpdateServer();
        }

        private void FixedUpdateServer()
        {
            if (!targetHealthComponent || !targetHealthComponent.alive)
            {
                Destroy(base.gameObject);
                return;
            }

            stopwatch += Time.fixedDeltaTime;
            if (!_hasFired)
            {
                if (stopwatch >= delayBeforeFiring)
                {
                    FireCannonServer();
                }
            }
            else if (hasFiredLocal)
            {
                //Wait for hasFiredLocal since some VFX rely on that
                if (stopwatch >= lifetimeAfterFiring)
                {
                    Destroy(base.gameObject);
                }
            }
        }

        private void FireCannonServer()
        {
            if (!NetworkServer.active) return;
            _hasFired = true;

            if (targetHealthComponent)
            {
                int itemCount = Mathf.Max(1, Util.GetItemCountForTeam(TeamIndex.Player, USB.Instance.ItemDef.itemIndex, false, true));
                float damage = targetHealthComponent.fullCombinedHealth * USB.CalcDamagePercent(itemCount);
                Vector3 damagePosition = targetHealthComponent.body ? targetHealthComponent.body.corePosition : base.transform.position;
                targetHealthComponent.TakeDamage(new DamageInfo()
                {
                    damage = damage,
                    damageType = DamageType.BypassArmor | DamageType.BypassBlock,
                    attacker = null,
                    canRejectForce = true,
                    force = Vector3.zero,
                    crit = false,
                    damageColorIndex = DamageColorIndex.Item,
                    dotIndex = DotController.DotIndex.None,
                    inflictor = base.gameObject,
                    position = damagePosition,
                    procChainMask = default,
                    procCoefficient = 0f
                });
            }
        }

        //Controls how fast the center thing rotates
        private void Update()
        {
            if (!hasFiredLocal)
            {
                if (laser)
                {
                    laser.SetPositions(new Vector3[]
                    {
                    base.transform.position + Vector3.up * 1000f,
                    base.transform.position + Vector3.down * 1000f,
                    });
                }

                if (rotatorTransform)
                {
                    rotationStopwatch += Time.deltaTime;
                    if (rotationStopwatch >= 2f) rotationStopwatch -= 2f;
                    rotatorTransform.localRotation = Quaternion.Euler(new Vector3(0f, 0f, 360f * rotationStopwatch));
                }
            }
            else
            {
                laserFadeStopwatch += Time.deltaTime;
                float laserFadePercent = 1f - (laserFadeStopwatch / lifetimeAfterFiring);
                laser.widthMultiplier = laserFireWidth * laserFadePercent;
            }
        }

        private void OnDestroy()
        {
            if (!hasFiredLocal) AkSoundEngine.StopPlayingID(soundId);
        }
    }

    public class AtlasTeleporterBeamController : MonoBehaviour
    {
        private LineRenderer lineRenderer;

        private void Awake()
        {
            if (!TeleporterInteraction.instance)
            {
                if (NetworkServer.active)
                {
                    Destroy(base.gameObject);
                }
                return;
            }

            lineRenderer = base.GetComponent<LineRenderer>();
            if (lineRenderer)
            {
                lineRenderer.SetPositions(new Vector3[]
                {
                TeleporterInteraction.instance.transform.position,
                TeleporterInteraction.instance.transform.position + 1000f * Vector3.up
                });
            }
        }
    }

    public class AtlasCannonInteractableController : MonoBehaviour
    {
        private PurchaseInteraction pi;
        private void Awake()
        {
            pi = base.GetComponent<PurchaseInteraction>();
            if (pi)
            {
                pi.onPurchase.AddListener(new UnityAction<Interactor>(AtlasCannonOnPurchase));
            }
        }

        private void AtlasCannonOnPurchase(Interactor interactor)
        {
            if (USB.cannonActivated) return;

            if (pi)
            {
                pi.lastActivator = interactor;
                pi.available = false;
            }
           
            USB.cannonActivated = true;
            Chat.SendBroadcastChat(new Chat.SubjectFormatChatMessage
            {
                subjectAsCharacterBody = interactor.GetComponent<CharacterBody>(),
                baseToken = "CLASSICITEMSRETURNS_INTERACTABLE_ATLASCANNON_USE_MESSAGE"
            });
        }
    }
}
