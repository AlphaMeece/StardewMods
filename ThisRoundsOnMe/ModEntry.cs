using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Triggers;
using ContentPatcher;
using xTile.Dimensions;
using Pathoschild.Stardew.Common.Integrations.GenericModConfigMenu;

namespace ThisRoundsOnMe
{
	public class ModEntry : Mod
	{
		private static ModConfig cfg;
		private PriceToken token = new PriceToken();

		public override void Entry(IModHelper helper)
		{
			TriggerActionManager.RegisterAction("AlphaMeece.ThisRoundsOnMe-BuyARound", ModEntry.BuyARound);

			helper.Events.GameLoop.GameLaunched += gameLaunched;

			cfg = helper.ReadConfig<ModConfig>();
		}

		public static int CalculatePrice()
		{
			if (Game1.player.currentLocation == null) return 400;
			else return Game1.player.currentLocation.characters.Count() * (cfg == null ? 400:cfg.Price);
		}

		public void gameLaunched(object? sender, GameLaunchedEventArgs e)
		{
			var api = this.Helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");

			api.RegisterToken(this.ModManifest, "FriendshipGain", () => new[] { cfg.FriendshipGain.ToString() });
			api.RegisterToken(this.ModManifest, "Price", token);

			var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
			if (configMenu is null)
				return;

			configMenu.Register(
				mod: this.ModManifest,
				reset: () => cfg = new ModConfig(),
				save: () => this.Helper.WriteConfig(cfg)
			);

			configMenu.AddNumberOption(
				mod: this.ModManifest,
				getValue: () => cfg.Price,
				setValue: value => cfg.Price = value,
				name: () => "Cost of a Round",
				min: 0
			);

			configMenu.AddNumberOption(
				mod: this.ModManifest,
				getValue: () => cfg.FriendshipGain,
				setValue: value => cfg.FriendshipGain = value,
				name: () => "Friendship from a Round",
				min: 0,
				max: 250
			);

		}

		public static bool BuyARound(string[] args, TriggerActionContext context, out string error)
		{
			GameLocation location = Game1.player.currentLocation;
			error = "";

			if (!ArgUtility.TryGet(args, 1, out string happiness, out error, allowBlank: false)) return false;

			if (location.Name == "Saloon")
			{
				if (location.characters.Count() > 0)
				{
					if (Game1.player.Money >= CalculatePrice())
					{
						Game1.player.Money -= CalculatePrice();
						Game1.player.activeDialogueEvents.Add("AlphaMeece.ThisRoundsOnMe-BoughtARound", 0);
						foreach (NPC npc in location.characters)
						{
							Game1.player.changeFriendship(int.Parse(happiness), npc);
						}
					}
					else error = "Too poor";
				}
				else error = "No one in Saloon";
			}
			else error = "Player not in Saloon";

			return false;
		}
	}
}
