using Terraria;
using TerrariaApi.Server;
using Newtonsoft.Json;
using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TShockAPI;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria.Localization;

namespace Koishi
{
	[ApiVersion(2, 1)]
	public class CDMain : TerrariaPlugin
	{
		private string configPath = "tshock\\CustomDifficulty.json";
		private Config config;
		public override string Author => "Gyrodrill";
		public override string Description => "Customize difficulty for SSC players, like random drop x items when killed.";
		public override string Name => "CustomDifficulty";
		public override Version Version => new Version(1, 3);
		public CDMain(Main game) : base(game)
		{
		}
		public override void Initialize()
		{
			ServerApi.Hooks.NetGetData.Register(this, NetHooks_GetData);
			ReadConfig(configPath, new Config(), ref config);
			Commands.ChatCommands.Add(new Command("Koishi.CustomDifficulty.reload", CDReload, "cdr", "cdreload"));
		}
		private void CDReload(CommandArgs args)
		{
			ReadConfig(configPath, new Config(), ref config);
		}
		public static int[] GetRandomInRange(int Count, int Max, int[] Seq = null)
		{
			if (Seq == null)
			{
				Seq = new int[Max];
				for (int i = 0; i < Max; i++)
				{
					Seq[i] = i;
				}
			}
			int end = Seq.Length;
			int[] ret = new int[Count];
			for (int i = 0; i < Count; i++)
			{
				int r = Main.rand.Next(0, end);
				end--;
				ret[i] = Seq[r];
				Seq[r] = Seq[end];
				Seq[end] = ret[i];
			}
			return ret;
		}
		public static T[] GetRandomInArray<T>(int Count, T[] Seq)
		{
			int end = Seq.Length;
			T[] ret = new T[Count];
			for (int i = 0; i < Count; i++)
			{
				int r = Main.rand.Next(0, end);
				end--;
				ret[i] = Seq[r];
				Seq[r] = Seq[end];
				Seq[end] = ret[i];
			}
			return ret;
		}
		private void NetHooks_GetData(GetDataEventArgs args)
		{
			if (args.MsgID == PacketTypes.PlayerDeathV2)
			{
				args.Msg.reader.BaseStream.Position = args.Index;
				int playerID = args.Msg.whoAmI;
				var p = Main.player[playerID];
				if (p.difficulty != 0)
				{
					return;
				}

				StringBuilder notice = new StringBuilder();
				switch (config.UseDropWay)
				{
					case 1:
						{
							Drop1AndAppendNotice(p, playerID, p.inventory, config.ItemDropAmount, notice, 0);
							Drop1AndAppendNotice(p, playerID, p.armor, config.EquipDropAmount, notice, 58);
							Drop1AndAppendNotice(p, playerID, p.miscEquips, config.MiscDropAmount, notice, 88);
							break;
						}

					case 2:
						{
							Drop2AndAppendNotice(p, playerID, p.inventory, config.ItemDropAmount, notice, 0);
							Drop2AndAppendNotice(p, playerID, p.armor, config.EquipDropAmount, notice, 58);
							Drop2AndAppendNotice(p, playerID, p.miscEquips, config.MiscDropAmount, notice, 88);
							break;
						}
				}

				if (notice.Length >= 3)
				{
					notice.Append(" was dropped from your inventory!");
					NetMessage.SendChatMessageToClient(NetworkText.FromLiteral(notice.ToString()), Color.Red, playerID);
				}
			}
		}
		public void Drop1AndAppendNotice(Player p, int playerID, Item[] inv, int count, StringBuilder notice, int os)
		{
			var invs = inv.Where(item => item.active && item.stack != 0 && item.type != 0);
			var DropSeq = GetRandomInArray(config.ItemDropAmount, invs.ToArray());
			var pil = inv.ToList();
			while (DropSeq.Any())
			{
				var d = pil.IndexOf(DropSeq[0]);
				notice.Append(TShock.Utils.ItemTag(inv[d]));
				DropItem(p, playerID, pil[d], os + d);
			}
		}
		public void Drop2AndAppendNotice(Player p, int playerID, Item[] inv, int rate, StringBuilder notice, int os)
		{
			for (int i = 0; i < inv.Length; i++)
			{
				if (Main.rand.Next(100) < config.RandomDropRate)
				{
					notice.Append(TShock.Utils.ItemTag(inv[i]));
					DropItem(p, playerID, inv[i], os + i);
				}
			}
		}
		public void DropItem(Player p, int playerID, Item i, int itemID)
		{
			if (!config.Vanish)
			{
				Item.NewItem(p.position, i.width, i.height, i.type, i.stack);
			}
			i.TurnToAir();
			NetMessage.SendData(5, -1, -1, null, playerID, itemID, i.prefix);
		}
		public static void ReadConfig<ConfigType>(string path, ConfigType defaultConfig, ref ConfigType config)
		{
			if (!File.Exists(path))
			{
				config = defaultConfig;
				File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
			}
			else
			{
				config = JsonConvert.DeserializeObject<ConfigType>(File.ReadAllText(path));
				File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
			}
		}
	}
	public class Config
	{
		public int ItemDropAmount = 2;
		public int EquipDropAmount = 1;
		public int MiscDropAmount = 1;
		public int RandomDropRate = 10;
		public int UseDropWay = 1;
		public bool Vanish = false;
	}
}