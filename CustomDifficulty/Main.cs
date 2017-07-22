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
using System.Data;
using MySql.Data.MySqlClient;
using Mono.Data.Sqlite;
using TShockAPI.DB;

namespace Koishi
{
	[ApiVersion(2, 1)]
	public class CDMain : TerrariaPlugin
	{
        public static IDbConnection DeathDropLog;
        private string configPath = "tshock\\CustomDifficulty.json";
		private Config config;
		public override string Author => "Gyrodrill";
		public override string Description => "Customize difficulty for SSC players, like random drop x items when killed.";
		public override string Name => "CustomDifficulty";
		//As fast as firefox!
		public override Version Version => new Version(1, 4);
		public CDMain(Main game) : base(game)
		{
		}
		public override void Initialize()
		{
            switch (TShock.Config.StorageType.ToLower())
            {
                case "mysql":
                    string[] host = TShock.Config.MySqlHost.Split(':');
                    DeathDropLog = new MySqlConnection()
                    {
                        ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                            host[0],
                            host.Length == 1 ? "3306" : host[1],
                            TShock.Config.MySqlDbName,
                            TShock.Config.MySqlUsername,
                            TShock.Config.MySqlPassword)
                    };
                    break;
                case "sqlite":
                    string sql = Path.Combine(TShock.SavePath, "DeathLog.sqlite");
                    DeathDropLog = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
                    break;
            }
            SqlTableCreator sqlcreator = new SqlTableCreator(DeathDropLog,
                DeathDropLog.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            sqlcreator.EnsureTableStructure(new SqlTable("DeathDropLog",
                new SqlColumn("Time", MySqlDbType.String, 200),
                new SqlColumn("Dropper", MySqlDbType.VarChar) { Length = 100 },
                new SqlColumn("ItemName", MySqlDbType.String, 70),
                new SqlColumn("Prefix", MySqlDbType.String, 100),
                new SqlColumn("Stack", MySqlDbType.String, 100),
                new SqlColumn("WorldID", MySqlDbType.Int32),
                new SqlColumn("LocationX", MySqlDbType.String, 20),
                new SqlColumn("LocationY", MySqlDbType.String, 100)));

            sqlcreator.EnsureTableStructure(new SqlTable("PlayerDeathLog",
               new SqlColumn("Time", MySqlDbType.String, 200),
               new SqlColumn("Account", MySqlDbType.VarChar) { Length = 100 },
               new SqlColumn("WorldID", MySqlDbType.Int32),
               new SqlColumn("Message", MySqlDbType.LongText),
               new SqlColumn("LocationX", MySqlDbType.String, 100),
               new SqlColumn("LocationY", MySqlDbType.String, 100)));

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
			if (Seq.Length <= Count)
			{
				return Seq;
			}
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
                string whyudie = "";
                using (var reader = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)))
                {

                    byte victimid = reader.ReadByte();

                    Terraria.DataStructures.PlayerDeathReason reason = Terraria.DataStructures.PlayerDeathReason.FromReader(reader);
                    whyudie = Convert.ToString(reason.GetDeathText(p.name));
                    
                }

                    var deathlogadd = DeathDropLog.Query("INSERT INTO PlayerDeathLog (Time, Account, WorldID, Message, LocationX, LocationY) VALUES (@0, @1, @2, @3, @4, @5);", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"), p.name, Main.worldID, whyudie, p.position.X / 16, p.position.Y / 16);
                if (p.difficulty != 0 || !TShock.ServerSideCharacterConfig.Enabled)
				{
					return;
				}
				StringBuilder notice = new StringBuilder();
				switch (config.UseDropWay)
				{
					case 1:
						{
							Drop1AndAppendNotice(Main.player[playerID], Main.player[playerID].inventory, config.ItemDropAmount, notice, 0);
							Drop1AndAppendNotice(Main.player[playerID], Main.player[playerID].armor, config.EquipDropAmount, notice, 59);
							Drop1AndAppendNotice(Main.player[playerID], Main.player[playerID].miscEquips, config.MiscDropAmount, notice, 89);
							break;
						}
					case 2:
						{
							Drop2AndAppendNotice(Main.player[playerID], Main.player[playerID].inventory, config.ItemDropAmount, notice, 0);
							Drop2AndAppendNotice(Main.player[playerID], Main.player[playerID].armor, config.EquipDropAmount, notice, 59);
							Drop2AndAppendNotice(Main.player[playerID], Main.player[playerID].miscEquips, config.MiscDropAmount, notice, 89);
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
		public void Drop1AndAppendNotice(Player p, Item[] inv, int count, StringBuilder notice, int os)
		{
			var invs = inv.Where(item => item.active && item.stack != 0 && item.type != 0);
			var amount = 0;
			switch (os)
			{
				case 0:
					amount = config.ItemDropAmount;
					break;
				case 59:
					amount = config.EquipDropAmount;
					break;
				default:
					amount = config.MiscDropAmount;
					break;
			}
			var DropSeq = GetRandomInArray(amount, invs.ToArray()).ToList();
			var pil = inv.ToList();
			while (DropSeq.Any())
			{
				var d = pil.IndexOf(DropSeq[0]);
				
                if (pil[d].Name != "")
                {
                    notice.Append(TShock.Utils.ItemTag(pil[d]));
                    var logadd = DeathDropLog.Query("INSERT INTO DeathDropLog (Time, Dropper, ItemName, Prefix, Stack, WorldID, LocationX, LocationY) VALUES (@0, @1, @2, @3, @4, @5, @6, @7);", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"), p.name, pil[d].Name, pil[d].prefix, pil[d].stack, Main.worldID, p.position.X/16, p.position.Y/16);
                    DropItem(p, pil[d], os + d);
                }
				DropSeq.RemoveAt(0);
			}
		}
		public void Drop2AndAppendNotice(Player p, Item[] inv, int rate, StringBuilder notice, int os)
		{
			for (int i = 0; i < inv.Length; i++)
			{
				if (Main.rand.Next(100) < config.RandomDropRate)
				{
                    if (inv[i].Name != "")
                    {
                        notice.Append(TShock.Utils.ItemTag(inv[i]));
                        var logadd = DeathDropLog.Query("INSERT INTO DeathDropLog (Time, Dropper, ItemName, Prefix, Stack, WorldID, LocationX, LocationY) VALUES (@0, @1, @2, @3, @4, @5, @6, @7);", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"), p.name, inv[i].Name, inv[i].prefix, inv[i].stack,Main.worldID, p.position.X/16, p.position.Y/16);
                        DropItem(p, inv[i], os + i);
                    }
                }
			}
		}
		public void DropItem(Player p, Item i, int itemID)
		{
			if (!config.Vanish)
			{
				var id = Item.NewItem(p.position, i.width, i.height, i.type, i.stack, true, i.prefix, true);
                
            }
			i.stack = i.type = i.netID = 0;
			i.active = false;
			NetMessage.SendData(5, -1, -1, null, p.whoAmI, itemID, i.prefix);
            
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
