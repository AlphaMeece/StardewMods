using Microsoft.Xna.Framework;
using Netcode;
using SpaceShared.APIs;
using ContentPatcher;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using SpaceCore;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.GameData.Objects;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using StardewValley.Buffs;
using SpaceCore.Spawnables;
using Microsoft.Xna.Framework.Graphics;
using xTile.Tiles;
using StardewValley.GameData.Shops;

namespace SkillRings
{
    internal sealed class ModEntry : Mod
    {
        private ModConfig cfg;

        private bool hasSpaceLuckSkill = false;
        private bool hasSVE = false;

        private float expMultiplier = 0f;
        private int[] oldExperience = { 0, 0, 0, 0, 0, 0 };

        private List<string> ringIDs = new List<string>();
        private string[] moddedSkillIds = { };
        private bool hasModdedSkills = false;
        private int[] moddedSkillExperience = { };

        private bool ringsChecked = false;
        private bool ringsGenerated = false;

        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.GameLaunched += new EventHandler<GameLaunchedEventArgs>(onGameLaunched);
            helper.Events.GameLoop.UpdateTicked += new EventHandler<UpdateTickedEventArgs>(onUpdateTicked);
            helper.Events.GameLoop.DayStarted += new EventHandler<DayStartedEventArgs>(onDayStarted);
            helper.Events.Input.ButtonPressed += new EventHandler<ButtonPressedEventArgs>(onButtonPressed);
            helper.Events.Content.AssetRequested += new EventHandler<AssetRequestedEventArgs>(onAssetRequested);
            //helper.Events.Specialized.LoadStageChanged += new EventHandler<LoadStageChangedEventArgs>(onLoadStateChanged);

            //Adding console commands to the game
            //Fixes the health of the player if it was messed up by the mod
            helper.ConsoleCommands.Add("fixhealth", "Changes max health to what it should be, take off combat rings and don't have combat buffs on\n\nUsage: fixhealth", new Action<string, string[]>(fixHealth));
            //Converts the held broken ring into its fixed tier 3 equivalent
            helper.ConsoleCommands.Add("fixring", "Fixes the currently held broken ring, converting it to its tier 3 equivalent", new Action<string, string[]>(fixRing));
            helper.ConsoleCommands.Add("sendmail", "Sends mail for a tier 3 ring, usage:\n\tsendmail [skill]\n\twhere skill is one of: farming, fishing, foraging, combat, mining, foragingr1, foragingr2", new Action<string, string[]>(sendMail));
            
            //Load the config file
            cfg = helper.ReadConfig<ModConfig>();
        }

        private void fixHealth(string command, string[] args)
        {
            int skillLevel = Game1.player.GetSkillLevel(4);
            int num = 100;
            for (int index = 0; index < skillLevel; ++index)
            {
                switch (index)
                {
                    case 4:
                        if (Game1.player.professions.Contains(24))
                        {
                            num += 15;
                            break;
                        }
                        break;
                    case 9:
                        if (Game1.player.professions.Contains(27))
                        {
                            num += 25;
                            break;
                        }
                        break;
                    default:
                        num += 5;
                        break;
                }
            }
            if (Game1.player.mailReceived.Contains("qiCave"))
                num += 25;
            Game1.player.maxHealth = num;
            Game1.player.health = num;
        }

        private void fixRing(string command, string[] args)
        {
            if (Game1.player.ActiveItem == null) return;
            if (Game1.player.ActiveItem.QualifiedItemId == "(O)AlphaMeece.SkillRings_FishingRingB")
            {
                Game1.player.reduceActiveItemByOne();
                getTier3Ring("AlphaMeece.SkillRings_FishingRing3");
                Monitor.Log("Got the Ring of the Legendary Angler.", (LogLevel)1);
            }
            else if (Game1.player.ActiveItem.QualifiedItemId == "(O)AlphaMeece.SkillRings_FarmingRingB")
            {
                Game1.player.reduceActiveItemByOne();
                getTier3Ring("AlphaMeece.SkillRings_FarmingRing3");
                Monitor.Log("Got the Ring of Nature's Oracle.", (LogLevel)1);
            }
            else if (Game1.player.ActiveItem.QualifiedItemId == "(O)AlphaMeece.AlphaMeece.SkillRings_ForagingRingB")
            {
                Game1.player.reduceActiveItemByOne();
                getTier3Ring("AlphaMeece.SkillRings_ForagingRing3");
                Monitor.Log("Got the Ring of Natural Bounty.", (LogLevel)1);
            }
            else if (Game1.player.ActiveItem.QualifiedItemId == "(O)AlphaMeece.SkillRings_MiningRingB")
            {
                Game1.player.reduceActiveItemByOne();
                getTier3Ring("AlphaMeece.SkillRings_MiningRing#");
                Monitor.Log("Got the Ring of Dwarven Luck.", (LogLevel)1);
            }
            else if (Game1.player.ActiveItem.QualifiedItemId == "(O)AlphaMeece.SkillRings_CombatRingB")
            {
                Game1.player.reduceActiveItemByOne();
                getTier3Ring("AlphaMeece.SkillRings_CombatRing3");
                Monitor.Log("Got the Ring of the War God.", (LogLevel)1);
            }
            else if (Game1.player.ActiveItem.QualifiedItemId == "(O)AlphaMeece.SkillRings_ExperienceRingB")
            {
                Game1.player.reduceActiveItemByOne();
                getTier3Ring("AlphaMeece.SkillRings_ExperienceRing3");
                Monitor.Log("Got the Ring of Ineffable Knowledge.", (LogLevel)1);
            }
            else
            {
                Monitor.Log("Player not holding a broken ring.", (LogLevel)1);
            }
        }

        private void getTier3Ring(string id)
        {
            Game1.flashAlpha = 1.0F;
            Game1.player.holdUpItemThenMessage(new StardewValley.Object(id, 1, false, -1, 0), true);
            if (!Game1.player.addItemToInventoryBool(ItemRegistry.Create(id), false))
                Game1.createItemDebris(new StardewValley.Object(id, 1, false, -1, 0), Game1.player.getStandingPosition(), 1, null, -1);
            Game1.player.jitterStrength = 0.0F;
            Game1.screenGlowHold = false;
        }

        private void onDayStarted(object sender, DayStartedEventArgs e)
        {
            if(!ringsChecked)
            {
                moddedSkillIds = Skills.GetSkillList();
                if (moddedSkillIds.Length != 0) hasModdedSkills = true;

                if (hasModdedSkills)
                {
                    List<int> oldExp = new List<int>();
                    foreach (var id in moddedSkillIds)
                    {
                        oldExp.Add(Skills.GetExperienceFor(Game1.player, id));
                    }
                    moddedSkillExperience = oldExp.ToArray();
                }

                ringsChecked = true;

                Helper.GameContent.InvalidateCache("Data/Objects"); 
                Helper.GameContent.InvalidateCache("Data/Shops");

                ringsGenerated = true;
            }

            oldExperience = Game1.player.experiencePoints.ToArray();
            handleMail();
        }

        private void onGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            hasSpaceLuckSkill = Helper.ModRegistry.IsLoaded("spacechase0.LuckSkill");
            hasSVE = Helper.ModRegistry.IsLoaded("FlashShifter.SVECode");

            var contentPatcherAPI = Helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");
            contentPatcherAPI.RegisterToken(ModManifest, "TierOneRingPrice", () => new[] { cfg.tier1SkillRingPrice.ToString() });
            contentPatcherAPI.RegisterToken(ModManifest, "TierTwoRingPrice", () => new[] { cfg.tier2SkillRingPrice.ToString() });
            contentPatcherAPI.RegisterToken(ModManifest, "TierThreeRingPrice", () => new[] { cfg.tier3SkillRingPrice.ToString() });
        }

        private void onAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if(cfg.moddedSkillrings)
            {
				if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
				{
					if (hasModdedSkills && ringsChecked)
					{
                        int skillNumber = 0;
						foreach (string id in moddedSkillIds)
						{
							//Monitor.Log(string.Format("Loaded Custom Skill {0}, {1}", id, Skills.GetSkill(id).GetName()), (LogLevel) 1);
							ObjectData Ring1 = new ObjectData()
							{
								Name = string.Format("AlphaMeece.SkillRings_{0}Ring1", id),
								DisplayName = string.Format("{0} 1", Skills.GetSkill(id).GetName()),
								Description = string.Format("+{0} {1}", cfg.tier1SkillRingBoost.ToString(), Skills.GetSkill(id).GetName()),
								Type = "Ring",
								Category = StardewValley.Object.ringCategory,
								Texture = "AlphaMeece.SkillRings/Objects",
								SpriteIndex = 24 + 4 * skillNumber,
								CustomFields = new Dictionary<string, string>(),
								ContextTags = new List<string> { "ring_item" }
							};
							ObjectData Ring2 = new ObjectData()
							{
								Name = string.Format("AlphaMeece.SkillRings_{0}Ring2", id),
								DisplayName = string.Format("{0} 2", Skills.GetSkill(id).GetName()),
								Description = string.Format("+{0} {1}", cfg.tier2SkillRingBoost.ToString(), Skills.GetSkill(id).GetName()),
								Type = "Ring",
								Category = StardewValley.Object.ringCategory,
								Texture = "AlphaMeece.SkillRings/Objects",
								SpriteIndex = 25 + 4 * skillNumber,
								CustomFields = new Dictionary<string, string>(),
								ContextTags = new List<string> { "ring_item" }
							};
							ObjectData Ring3 = new ObjectData()
							{
								Name = string.Format("AlphaMeece.SkillRings_{0}Ring3", id),
								DisplayName = string.Format("{0} 3", Skills.GetSkill(id).GetName()),
								Description = string.Format("+{0} {1}", cfg.tier3SkillRingBoost.ToString(), Skills.GetSkill(id).GetName()),
								Type = "Ring",
								Category = StardewValley.Object.ringCategory,
								Texture = "AlphaMeece.SkillRings/Objects",
								SpriteIndex = 26 + 4 * skillNumber,
								CustomFields = new Dictionary<string, string>(),
								ContextTags = new List<string> { "ring_item" }
							};

							ringIDs.Add(string.Format("AlphaMeece.SkillRings_{0}Ring1", id));
							ringIDs.Add(string.Format("AlphaMeece.SkillRings_{0}Ring2", id));
							ringIDs.Add(string.Format("AlphaMeece.SkillRings_{0}Ring3", id));

							e.Edit(asset =>
							{
								var editor = asset.AsDictionary<string, ObjectData>();
								editor.Data.TryAdd(Ring1.Name, Ring1);
								editor.Data.TryAdd(Ring2.Name, Ring2);
								editor.Data.TryAdd(Ring3.Name, Ring3);
							});

                            skillNumber++;
                            if (skillNumber == 10) skillNumber = 0;
						}
					}
				}

				if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
				{
					e.Edit(asset =>
					{
						for (int i = 0; i < ringIDs.Count; i++)
						{
							var editor = asset.AsDictionary<string, ShopData>();
							string ringID = ringIDs[i];

							string shop = "Traveler";

							if (Game1.objectData.TryGetValue(ringID, out ObjectData ringData))
							{
								if (ringData.CustomFields.TryGetValue("AlphaMeece.SkillRings_Shop", out var customShop))
								{
									if (editor.Data.ContainsKey(customShop))
									{
										shop = customShop;
									}
									else Monitor.Log($"Attempted to add {ringID} to shop {customShop}, but {customShop} does not exist, defaulting to \"Traveler\"", LogLevel.Warn);
								}
							}

							if (editor.Data.TryGetValue(shop, out var merchant))
							{
								bool flag = false;
								foreach (var item in merchant.Items) if (item.ItemId == ringIDs[i]) flag = true;

								int price = cfg.tier1SkillRingPrice;
								if (ringID.EndsWith("2")) price = cfg.tier2SkillRingPrice;
								else if (ringID.EndsWith("3")) price = cfg.tier3SkillRingPrice;

								if (!flag) merchant.Items.Add(new ShopItemData
								{
									ItemId = ringIDs[i],
									Price = price,
									AvoidRepeat = true,
									ApplyProfitMargins = false,
									IgnoreShopPriceModifiers = true
								});
							}
							else Monitor.Log("Failed", (LogLevel)1);
						}
					});
				}
			}
        }

        private bool checkLocations(int[,] coords, Vector2 tile)
        {
            for (int index = 0; index < coords.GetLength(0); ++index)
            {
                int coord1 = coords[index, 0];
                int coord2 = coords[index, 1];
                if (tile == new Vector2(coord1, coord2))
                    return true;
            }
            return false;
        }

        private void onButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;
            //If F9(debug key) is pressed
            if (Helper.Input.IsDown((SButton)120))
            {
                Monitor.Log(string.Format("Cursor At X:{0} Y:{1} \n Player at {2}", e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y, Game1.currentLocation?.Name), (LogLevel)1);
                //if(this.hasWMR)
                //{
                //    foreach(Item allRing in this.moreRings.GetAllRings(Game1.player))
                //        this.Monitor.Log("Ring: " + allRing.Name, (LogLevel) 1);
                //}
            }

            //Decide whether to watch Right Click of the A button on a comtroller
            SButton sbutton = (SButton)1001;
            bool flag = false;
            if (Helper.Input.IsDown((SButton)1001))
            {
                flag = true;
                sbutton = (SButton)1001;
            }
            else if (Helper.Input.IsDown((SButton)6096))
            {
                flag = true;
                sbutton = (SButton)6096;
            }
            if (!flag)
                return;

            if (Game1.player.ActiveItem == null) return;

            //Transform rings
            if (Game1.player.ActiveItem.QualifiedItemId == "(O)AlphaMeece.SkillRings_FishingRingB")
            {
                foreach (Building building in Game1.player.currentLocation.buildings)
                {
                    if (building.buildingType.Value == "Fish Pond" && building.occupiesTile(e.Cursor.GrabTile))
                    {
                        Game1.player.reduceActiveItemByOne();
                        getTier3Ring("AlphaMeece.SkillRings_FishingRing3");
                    }
                }
            }
            else if (Game1.player.ActiveItem.QualifiedItemId == "(O)AlphaMeece.SkillRings_FarmingRingB")
            {
                foreach (FarmAnimal allFarmAnimal in Game1.player.currentLocation.getAllFarmAnimals())
                {
                    Vector2 grabTile = allFarmAnimal.GetGrabTile();
                    int[,] coords = new int[9, 2]
                    {
                        {
                            (int) grabTile.X - 1,
                            (int) grabTile.Y - 1
                        },
                        {
                            (int) grabTile.X,
                            (int) grabTile.Y - 1
                        },
                        {
                            (int) grabTile.X + 1,
                            (int) grabTile.Y - 1
                        },
                        {
                            (int) grabTile.X - 1,
                            (int) grabTile.Y
                        },
                        {
                            (int) grabTile.X,
                            (int) grabTile.Y
                        },
                        {
                            (int) grabTile.X + 1,
                            (int) grabTile.Y
                        },
                        {
                            (int) grabTile.X - 1,
                            (int) grabTile.Y + 1
                        },
                        {
                            (int) grabTile.X,
                            (int) grabTile.Y + 1
                        },
                        {
                            (int) grabTile.X + 1,
                            (int) grabTile.Y + 1
                        }
                    };
                    if (Game1.player.currentLocation == allFarmAnimal.currentLocation && checkLocations(coords, e.Cursor.GrabTile))
                    {
                        Helper.Input.Suppress(sbutton);
                        Game1.player.reduceActiveItemByOne();
                        getTier3Ring("AlphaMeece.SkillRings_FarmingRing3");
                    }
                }
            }
            else if (Game1.player.ActiveItem.QualifiedItemId == "(O)AlphaMeece.SkillRings_CombatRingB")
            {
                int[,] coords = new int[4, 2]
                {
                    {
                        29,
                        6
                    },
                    {
                        30,
                        6
                    },
                    {
                        29,
                        7
                    },
                    {
                        30,
                        7
                    }
                };
                if (!(Game1.currentLocation is MineShaft) || Game1.CurrentMineLevel != 77377 || !checkLocations(coords, e.Cursor.GrabTile))
                    return;
                Helper.Input.Suppress(sbutton);
                Game1.player.reduceActiveItemByOne();
                getTier3Ring("AlphaMeece.SkillRings_CombatRing3");
            }
            else if (Game1.player.ActiveItem.QualifiedItemId == "(O)AlphaMeece.SkillRings_ForagingRingB")
            {
                int[,] coords = new int[11, 2]
                {
                    {
                        8,
                        6
                    },
                    {
                        9,
                        6
                    },
                    {
                        10,
                        6
                    },
                    {
                        7,
                        7
                    },
                    {
                        8,
                        7
                    },
                    {
                        9,
                        7
                    },
                    {
                        10,
                        7
                    },
                    {
                        7,
                        8
                    },
                    {
                        8,
                        8
                    },
                    {
                        9,
                        8
                    },
                    {
                        10,
                        8
                    }
                };
                if (!(Game1.currentLocation is Woods) || !checkLocations(coords, e.Cursor.GrabTile))
                    return;
                Helper.Input.Suppress(sbutton);
                Game1.player.reduceActiveItemByOne();
                getTier3Ring("AlphaMeece.SkillRings_ForagingRing3");
            }
            else if (Game1.player.ActiveItem.QualifiedItemId == "(O)AlphaMeece.SkillRings_MiningRingB")
            {
                int[,] coords;
                if (hasSVE)
                    coords = new int[12, 2]
                    {
                        {
                            8,
                            13
                        },
                        {
                            9,
                            13
                        },
                        {
                            10,
                            13
                        },
                        {
                            11,
                            13
                        },
                        {
                            8,
                            14
                        },
                        {
                            9,
                            14
                        },
                        {
                            10,
                            14
                        },
                        {
                            11,
                            14
                        },
                        {
                            8,
                            15
                        },
                        {
                            9,
                            15
                        },
                        {
                            10,
                            15
                        },
                        {
                            11,
                            15
                        }
                    };
                else
                    coords = new int[9, 2]
                    {
                        {
                            11,
                            12
                        },
                        {
                            12,
                            12
                        },
                        {
                            13,
                            12
                        },
                        {
                            11,
                            13
                        },
                        {
                            12,
                            13
                        },
                        {
                            13,
                            13
                        },
                        {
                            11,
                            14
                        },
                        {
                            12,
                            14
                        },
                        {
                            13,
                            14
                        }
                    };
                if (!(Game1.currentLocation?.Name == "Blacksmith") || !checkLocations(coords, e.Cursor.GrabTile))
                    return;
                Helper.Input.Suppress(sbutton);
                Game1.player.reduceActiveItemByOne();
                getTier3Ring("AlphaMeece.SkillRings_MiningRing3");
            }
            else if (Game1.player.ActiveItem.QualifiedItemId == "(O)AlphaMeece.SkillRings_ExperienceRingB")
            {
                int[,] coords;
                if (hasSVE)
                    coords = new int[12, 2]
                    {
                        {
                            22,
                            4
                        },
                        {
                            23,
                            4
                        },
                        {
                            24,
                            4
                        },
                        {
                            25,
                            4
                        },
                        {
                            22,
                            5
                        },
                        {
                            23,
                            5
                        },
                        {
                            24,
                            5
                        },
                        {
                            25,
                            5
                        },
                        {
                            22,
                            6
                        },
                        {
                            23,
                            6
                        },
                        {
                            24,
                            6
                        },
                        {
                            25,
                            6
                        }
                    };
                else
                    coords = new int[6, 2]
                    {
                        {
                            11,
                            4
                        },
                        {
                            12,
                            4
                        },
                        {
                            13,
                            4
                        },
                        {
                            11,
                            5
                        },
                        {
                            12,
                            5
                        },
                        {
                            13,
                            5
                        }
                    };
                if ((Game1.currentLocation?.Name == "WizardHouseBasement" || Game1.currentLocation?.Name == "WizardBasement" || Game1.currentLocation?.Name == "Custom_WizardBasement") && checkLocations(coords, e.Cursor.GrabTile))
                {
                    Helper.Input.Suppress(sbutton);
                    Game1.player.reduceActiveItemByOne();
                    getTier3Ring("AlphaMeece.SkillRings_ExperienceRing3");
                }
            }
        }

        private void onUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (hasModdedSkills && !ringsGenerated)
            {
                Monitor.Log("Retrying to add rings", (LogLevel)1);
                Helper.GameContent.InvalidateCache("Data/Objects");

                ringsGenerated = true;
            }

            if (!Context.IsPlayerFree || !e.IsOneSecond)
                return;

            List<Buff> buffs = new List<Buff>();

            //Farming
            Buff farmingBuff = new Buff(id: "AlphaMeece.SkillRings_FarmingBuff", duration: Buff.ENDLESS);
            int farmingLevel = -1;

            if (hasRing("AlphaMeece.SkillRings_FarmingRing3"))
                farmingLevel = cfg.tier3SkillRingBoost;
            else if (hasRing("AlphaMeece.SkillRings_FarmingRing2"))
                farmingLevel = cfg.tier2SkillRingBoost;
            else if (hasRing("AlphaMeece.SkillRings_FarmingRing1"))
                farmingLevel = cfg.tier1SkillRingBoost;
            else if (Game1.player.hasBuff("AlphaMeece.SkillRings_FarmingBuff"))
                Game1.player.buffs.Remove("AlphaMeece.SkillRings_FarmingBuff");

            if (farmingLevel != -1)
            {
                farmingBuff.effects.Add(new BuffEffects()
                {
                    FarmingLevel = { farmingLevel }
                });
                buffs.Add(farmingBuff);
            }

            //Fishing
            Buff fishingBuff = new Buff(id: "AlphaMeece.SkillRings_FishingBuff", duration: Buff.ENDLESS);
            int fishingLevel = -1;

            if (hasRing("AlphaMeece.SkillRings_FishingRing3"))
                fishingLevel = cfg.tier3SkillRingBoost;
            else if (hasRing("AlphaMeece.SkillRings_FishingRing2"))
                fishingLevel = cfg.tier2SkillRingBoost;
            else if (hasRing("AlphaMeece.SkillRings_FishingRing1"))
                fishingLevel = cfg.tier1SkillRingBoost;
            else if (Game1.player.hasBuff("AlphaMeece.SkillRings_FishingBuff"))
                Game1.player.buffs.Remove("AlphaMeece.SkillRings_FishingBuff");

            if (fishingLevel != -1)
            {
                fishingBuff.effects.Add(new BuffEffects()
                {
                    FishingLevel = { fishingLevel }
                });
                buffs.Add(fishingBuff);
            }

            //Mining
            Buff miningBuff = new Buff(id: "AlphaMeece.SkillRings_MiningBuff", duration: Buff.ENDLESS);
            int miningLevel = -1;

            if (hasRing("AlphaMeece.SkillRings_MiningRing3"))
                miningLevel = cfg.tier3SkillRingBoost;
            else if (hasRing("AlphaMeece.SkillRings_MiningRing2"))
                miningLevel = cfg.tier2SkillRingBoost;
            else if (hasRing("AlphaMeece.SkillRings_MiningRing1"))
                miningLevel = cfg.tier1SkillRingBoost;
            else if (Game1.player.hasBuff("AlphaMeece.SkillRings_MiningBuff"))
                Game1.player.buffs.Remove("AlphaMeece.SkillRings_MiningBuff");

            if (miningLevel != -1)
            {
                miningBuff.effects.Add(new BuffEffects()
                {
                    MiningLevel = { miningLevel }
                });
                buffs.Add(miningBuff);
            }

            //Foraging
            Buff foragingBuff = new Buff(id: "AlphaMeece.SkillRings_ForagingBuff", duration: Buff.ENDLESS);
            int foragingLevel = -1;

            if (hasRing("AlphaMeece.SkillRings_ForagingRing3"))
                foragingLevel = cfg.tier3SkillRingBoost;
            else if (hasRing("AlphaMeece.SkillRings_ForagingRing2"))
                foragingLevel = cfg.tier2SkillRingBoost;
            else if (hasRing("AlphaMeece.SkillRings_ForagingRing1"))
                foragingLevel = cfg.tier1SkillRingBoost;
            else if (Game1.player.hasBuff("AlphaMeece.SkillRings_ForagingBuff"))
                Game1.player.buffs.Remove("AlphaMeece.SkillRings_ForagingBuff");

            if (foragingLevel != -1)
            {
                foragingBuff.effects.Add(new BuffEffects()
                {
                    ForagingLevel = { foragingLevel }
                });
                buffs.Add(foragingBuff);
            }

            //Combat
            Buff combatBuff = new Buff(id: "AlphaMeece.SkillRings_CombatBuff", duration: Buff.ENDLESS);
            int combatLevel = -1;

            if (hasRing("AlphaMeece.SkillRings_CombatRing3"))
                combatLevel = cfg.tier3SkillRingBoost;
            else if (hasRing("AlphaMeece.SkillRings_CombatRing2"))
                combatLevel = cfg.tier2SkillRingBoost;
            else if (hasRing("AlphaMeece.SkillRings_CombatRing1"))
                combatLevel = cfg.tier1SkillRingBoost;
            else if (Game1.player.hasBuff("AlphaMeece.SkillRings_CombatBuff"))
                Game1.player.buffs.Remove("AlphaMeece.SkillRings_CombatBuff");

            if (combatLevel != -1)
            {
                combatBuff.effects.Add(new BuffEffects()
                {
                    CombatLevel = { combatLevel },
                    Attack = { combatLevel * 2 },
                    Defense = { combatLevel * 2 },
                    Immunity = { combatLevel * 2 }
                });
                buffs.Add(combatBuff);
            }

            //Experience
            Buff experienceBuff = new Buff(id: "AlphaMeece.SkillRings_ExperienceBuff", duration: Buff.ENDLESS);
            float expLevel = -1f;

            if (hasRing("AlphaMeece.SkillRings_ExperienceRing3"))
                expLevel = cfg.tier3ExperienceRingBoost;
            else if (hasRing("AlphaMeece.SkillRings_ExperienceRing2"))
                expLevel = cfg.tier2ExperienceRingBoost;
            else if (hasRing("AlphaMeece.SkillRings_ExperienceRing1"))
                expLevel = cfg.tier1ExperienceRingBoost;
            else if (Game1.player.hasBuff("AlphaMeece.SkillRings_ExperienceBuff"))
            {
                Game1.player.buffs.Remove("AlphaMeece.SkillRings_ExperienceBuff");
                expMultiplier = 0f;
            }


            if (expLevel != -1f)
            {
                expMultiplier = expLevel;
                buffs.Add(experienceBuff);
            }

            //Modded skills
            if(hasModdedSkills)
            {
                foreach(string id in moddedSkillIds)
                {
                    Dictionary<string, string> moddedBuffEffect = new Dictionary<string, string>();
                    int moddedBuffLevel = -1;

                    if(hasRing(string.Format("AlphaMeece.SkillRings_{0}Ring3", id)))
                        moddedBuffLevel = cfg.tier3SkillRingBoost;
                    else if(hasRing(string.Format("AlphaMeece.SkillRings_{0}Ring2", id)))
                        moddedBuffLevel = cfg.tier2SkillRingBoost;
                    else if(hasRing(string.Format("AlphaMeece.SkillRings_{0}Ring1", id)))
                        moddedBuffLevel = cfg.tier1SkillRingBoost;
                    else if(Game1.player.hasBuff(string.Format("AlphaMeece.SkillRings_{0}Buff", id)))
                        Game1.player.buffs.Remove(string.Format("AlphaMeece.SkillRings_{0}Buff", id));

                    if(moddedBuffLevel != -1)
                    {
                        moddedBuffEffect.Add(string.Format("spacechase.SpaceCore.SkillBuff.{0}", id), moddedBuffLevel.ToString());

                        Skills.SkillBuff moddedBuff = new Skills.SkillBuff(new Buff(id: string.Format("AlphaMeece.SkillRings_{0}Buff", id), duration: Buff.ENDLESS),
                            string.Format("AlphaMeece.SkillRings_{0}Buff", id),
                            moddedBuffEffect);

                        buffs.Add(moddedBuff);
                    }
                   
                }
            }

            //Luck
            if (hasSpaceLuckSkill)
            {
                Buff luckBuff = new Buff(id: "AlphaMeece.SkillRings_LuckBuff", duration: Buff.ENDLESS);
                int luckLevel = -1;

                if (hasRing("AlphaMeece.SkillRings_LuckRing3"))
                    luckLevel = cfg.tier3SkillRingBoost;
                else if (hasRing("AlphaMeece.SkillRings_LuckRing2"))
                    luckLevel = cfg.tier2SkillRingBoost;
                else if (hasRing("AlphaMeece.SkillRings_LuckRing1"))
                    luckLevel = cfg.tier1SkillRingBoost;
                else if (Game1.player.hasBuff("AlphaMeece.SkillRings_LuckBuff"))
                    Game1.player.buffs.Remove("AlphaMeece.SkillRings_LuckBuff");

                if (luckLevel != -1)
                {
                    luckBuff.effects.Add(new BuffEffects()
                    {
                        LuckLevel = { luckLevel }
                    });
                    buffs.Add(luckBuff);
                }
            }

            foreach (var item in buffs)
            {
                if(Game1.player.hasBuff(item.id)) continue;
                item.visible = false;
                Game1.player.applyBuff(item);
            }

            if (Game1.player.hasBuff("AlphaMeece.SkillRings_ExperienceBuff"))
            {
                if (oldExperience != Game1.player.experiencePoints.ToArray())
                {
                    for (int skill = 0; skill < 6; skill++)
                    {
                        int currentExp = Game1.player.experiencePoints.ElementAt(skill);
                        if (currentExp > oldExperience[skill])
                        {
                            Game1.player.gainExperience(skill, (int)Math.Ceiling((currentExp - oldExperience[skill]) * expMultiplier));
                            //Monitor.Log($"Gained experience from experience ring\nCurrent Multiplier:{1 + expMultiplier}\nExp Change:{currentExp} - {oldExperience[skill]} = {currentExp - oldExperience[skill]}\nGained Experience: {Math.Ceiling((currentExp - oldExperience[skill]) * expMultiplier)}\nNew Total: {Game1.player.experiencePoints.ElementAt(skill)}", LogLevel.Debug);
                        }
                    }
                }
                if(hasModdedSkills)
                {
                    for(int i = 0; i < moddedSkillIds.Length; i++)
                    {
                        string id = moddedSkillIds[i];
                        if(Skills.GetExperienceFor(Game1.player, id) > moddedSkillExperience[i])
                        {
                            Skills.AddExperience(Game1.player, id, (int) Math.Ceiling((Skills.GetExperienceFor(Game1.player, id) - moddedSkillExperience[i]) * expMultiplier));
                        }
                        moddedSkillExperience[i] = Skills.GetExperienceFor(Game1.player, id);
                    }
                }
            }
            if(hasModdedSkills)
            {
                List<int> oldExp = new List<int>();
                foreach (var id in moddedSkillIds)
                {
                    oldExp.Add(Skills.GetExperienceFor(Game1.player, id));
                }
                moddedSkillExperience = oldExp.ToArray();
            }
            oldExperience = Game1.player.experiencePoints.ToArray();
        }

        private bool hasRing(string Id)
        {
            return Game1.player.isWearingRing(Id);
        }

        private void sendMail(string command, string[] args)
        {
            if(args.Length == 0) {
                Monitor.Log("Please enter a skill", LogLevel.Error);
                return;
            }
            switch(args[0].ToLower())
            {
                case "farming":
                    Game1.player.mailbox.Add("AlphaMeece.SkillRings_GrassyRing");
                    break;
                case "fishing":
                    Game1.player.mailbox.Add("AlphaMeece.SkillRings_DustyRing");
                    break;
                case "foraging":
                    Game1.player.mailbox.Add("AlphaMeece.SkillRings_StickyRing");
                    break;
                case "mining":
                    Game1.player.mailbox.Add("AlphaMeece.SkillRings_StoneRing");
                    break;
                case "combat":
                    Game1.player.mailbox.Add("AlphaMeece.SkillRings_CursedRing");
                    break;
                case "foragingr1":
                    Game1.player.mailbox.Add("AlphaMeece.SkillRings_Foraging1Recipe");
                    break;
                case "foragingr2":
                    Game1.player.mailbox.Add("AlphaMeece.SkillRings_Foraging2Recipe");
                    break;
                default:
                    Monitor.Log("Please enter one of: foraging, fishing, farming, combat, mining, foragingr1, foragingr2", LogLevel.Warn);
                    break;
            }
        }

        private void handleMail()
        {
            if (!Game1.player.mailReceived.Contains("AlphaMeece.SkillRings_DustyRing"))
            {
                if (Game1.player.getFriendshipHeartLevelForNPC("Willy") >= 4) Game1.player.mailbox.Add("AlphaMeece.SkillRings_DustyRing");
            }

            if (!Game1.player.mailReceived.Contains("AlphaMeece.SkillRings_StoneRing"))
            {
                if (Game1.player.getFriendshipHeartLevelForNPC("Dwarf") >= 4) Game1.player.mailbox.Add("AlphaMeece.SkillRings_StoneRing");
            }

            if (!Game1.player.mailReceived.Contains("AlphaMeece.SkillRings_StickyRing"))
            {
                if (Game1.player.getFriendshipHeartLevelForNPC("Linus") >= 4) Game1.player.mailbox.Add("AlphaMeece.SkillRings_StickyRing");
            }

            if (!Game1.player.mailReceived.Contains("AlphaMeece.SkillRings_GrassyRing"))
            {
                if (Game1.player.getFriendshipHeartLevelForNPC("Marnie") >= 4) Game1.player.mailbox.Add("AlphaMeece.SkillRings_GrassyRing");
            }

            if (!Game1.player.mailReceived.Contains("AlphaMeece.SkillRings_CursedRing"))
            {
                if (Game1.player.getFriendshipHeartLevelForNPC("Abigail") >= 4) Game1.player.mailbox.Add("AlphaMeece.SkillRings_CursedRing");
            }

            //Recipes
            if (!Game1.player.mailReceived.Contains("AlphaMeece.SkillRings_Foraging1Recipe"))
            {
                if (Game1.player.getFriendshipHeartLevelForNPC("Linus") >= 1) Game1.player.mailbox.Add("AlphaMeece.SkillRings_Foraging1Recipe");
            }

            if (!Game1.player.mailReceived.Contains("AlphaMeece.SkillRings_Foraging2Recipe"))
            {
                if (Game1.player.getFriendshipHeartLevelForNPC("Linus") >= 2) Game1.player.mailbox.Add("AlphaMeece.SkillRings_Foraging2Recipe");
            }
        }
    }
}