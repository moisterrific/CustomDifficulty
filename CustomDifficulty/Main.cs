using Terraria;
using TerrariaApi.Server;
using Newtonsoft.Json;
using System.IO;
using System;
using Terraria.DataStructures;
using System.Collections.Generic;
using System.Linq;
using TShockAPI;
using System.Text;

namespace Koishi
{
	[ApiVersion(2, 0)]
	public class CDMain : TerrariaPlugin
	{
		private string configPath = "tshock\\CustomDifficulty.json";
		private Config config;
		public override string Author => "Gyrodrill";
		public override string Description => "Customize difficulty for SSC players, like random drop x items when killed.";
		public override string Name => "CustomDifficulty";
		public override Version Version => new Version(1, 1);
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
		public static int[] GetRandomInRange(int Count, int Max)
		{
			//var rand = new Random();
			var Seq = new int[Max];
			for (int i = 0; i < Max; i++)
			{
				Seq[i] = i;
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
		private void NetHooks_GetData(GetDataEventArgs args)
		{
			if (args.MsgID == PacketTypes.PlayerDeathV2)
			{
				args.Handled = true;
				args.Msg.reader.BaseStream.Position = args.Index;
				int playerID = args.Msg.whoAmI;
				PlayerDeathReason reason = PlayerDeathReason.FromReader(args.Msg.reader);
				int damage = args.Msg.reader.ReadInt16();
				int direction = args.Msg.reader.ReadByte() - 1;
				bool pvp = ((BitsByte)args.Msg.reader.ReadByte())[0];
				var p = Main.player[playerID];
				StringBuilder notice = new StringBuilder();
				switch (config.UseDropWay)
				{
					case 1:
						{
							{
								int inventoryCount = 0;
								for (int i = 0; i < p.inventory.Length; i++)
								{
									var item = p.inventory[i];
									if (!item.active || item.stack == 0 || item.type == 0)
									{
										continue;
									}
									inventoryCount++;
								}
								List<int> DropSeq = new List<int> { };
								bool DropAll = false;
								if (inventoryCount <= config.ItemDropAmount)
								{
									DropAll = true;
								}
								else
								{
									DropSeq = GetRandomInRange(config.ItemDropAmount, inventoryCount).ToList();
								}
								int inventortIndex = 0;
								for (int i = 0; i < p.inventory.Length; i++)
								{
									var item = p.inventory[i];
									if (!item.active || item.stack == 0 || item.type == 0)
									{
										continue;
									}
									if (DropAll || DropSeq.Contains(inventortIndex))
									{
										notice.Append(TShock.Utils.ItemTag(p.inventory[i]));
										if (config.Vanish)
										{
											p.inventory[i].SetDefaults(0);
											NetMessage.SendData(5, -1, -1, p.inventory[i].name, playerID, i, p.inventory[i].prefix);
										}
										else
										{
											Item.NewItem(p.position, item.width, item.height, item.type, item.stack);
											p.inventory[i].SetDefaults(0);
											NetMessage.SendData(5, -1, -1, p.inventory[i].name, playerID, i, p.inventory[i].prefix);
										}
									}
									inventortIndex++;
								}
							}
							{
								int armorCount = 0;
								for (int i = 0; i < p.armor.Length; i++)
								{
									var item = p.armor[i];
									if (!item.active || item.stack == 0 || item.type == 0)
									{
										continue;
									}
									armorCount++;
								}
								List<int> DropSeq = new List<int> { };
								bool DropAll = false;
								if (armorCount <= config.ItemDropAmount)
								{
									DropAll = true;
								}
								else
								{
									DropSeq = GetRandomInRange(config.ItemDropAmount, armorCount).ToList();
								}
								int armorIndex = 0;
								for (int i = 0; i < p.armor.Length; i++)
								{
									var item = p.armor[i];
									if (!item.active || item.stack == 0 || item.type == 0)
									{
										continue;
									}
									if (DropAll || DropSeq.Contains(armorIndex))
									{
										notice.Append(TShock.Utils.ItemTag(p.armor[i]));
										if (config.Vanish)
										{
											p.armor[i].SetDefaults(0);
											NetMessage.SendData(5, -1, -1, p.armor[i].name, playerID, i + 59, p.armor[i].prefix);
										}
										else
										{
											Item.NewItem(p.position, item.width, item.height, item.type, item.stack);
											p.armor[i].SetDefaults(0);
											NetMessage.SendData(5, -1, -1, p.armor[i].name, playerID, i + 59, p.armor[i].prefix);
										}
									}
									armorIndex++;
								}
							}
							break;
						}
					case 2:
						{
							for (int i = 0; i < p.inventory.Length; i++)
							{
								var item = p.inventory[i];
								if (!item.active || item.stack == 0 || item.type == 0)
								{
									continue;
								}
								if (Main.rand.Next(100) < config.RandomDropRate)
								{
									if (config.Vanish)
									{
										notice.Append(TShock.Utils.ItemTag(p.inventory[i]));
										p.inventory[i].SetDefaults(0);
										NetMessage.SendData(5, -1, -1, p.inventory[i].name, playerID, i, p.inventory[i].prefix);
									}
									else
									{
										Item.NewItem(p.position, item.width, item.height, item.type, item.stack);
										p.inventory[i].SetDefaults(0);
										NetMessage.SendData(5, -1, -1, p.inventory[i].name, playerID, i, p.inventory[i].prefix);
									}
								}
							}
							for (int i = 0; i < p.armor.Length; i++)
							{
								var item = p.armor[i];
								if (!item.active || item.stack == 0 || item.type == 0)
								{
									continue;
								}
								if (Main.rand.Next(100) < config.RandomDropRate)
								{
									if (config.Vanish)
									{
										notice.Append(TShock.Utils.ItemTag(p.armor[i]));
										p.armor[i].SetDefaults(0);
										NetMessage.SendData(5, -1, -1, p.armor[i].name, playerID, i + 59, p.armor[i].prefix);
									}
									else
									{
										Item.NewItem(p.position, item.width, item.height, item.type, item.stack);
										p.armor[i].SetDefaults(0);
										NetMessage.SendData(5, -1, -1, p.armor[i].name, playerID, i + 59, p.armor[i].prefix);
									}
								}
							}
							break;
						}
				}
				if (notice.Length >= 3)
				{
					notice.Append(" was dropped from your inventory!");
					NetMessage.SendData(25, playerID, -1, notice.ToString());
				}
				Main.player[playerID].KillMe(reason, damage, direction, pvp);
				if (Main.netMode == 2)
				{
					NetMessage.SendPlayerDeath(playerID, reason, damage, direction, pvp, -1, args.Msg.whoAmI);
				}
			}
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
		public int RandomDropRate = 10;
		public int UseDropWay = 1;
		public bool Vanish = false;
	}
}