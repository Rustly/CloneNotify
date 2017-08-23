using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using TShockAPI;
using TShockAPI.DB;

namespace CloneNotify
{
	public static class DB
	{
		private static IDbConnection db;
		public static List<CloneInfo> Clones { get; set; }

		public static void Connect()
		{
			string[] dbHost = TShock.Config.MySqlHost.Split(':');
			db = new MySqlConnection()
			{
				ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
					dbHost[0],
					dbHost.Length == 1 ? "3306" : dbHost[1],
					TShock.Config.MySqlDbName,
					TShock.Config.MySqlUsername,
					TShock.Config.MySqlPassword)

			};

			SqlTableCreator sqlcreator = new SqlTableCreator(db, new MysqlQueryCreator());

			sqlcreator.EnsureTableStructure(new SqlTable("Clones",
				new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, Unique = true, Length = 7, AutoIncrement = true },
				new SqlColumn("CharacterName", MySqlDbType.Text) { Length = 40 },
				new SqlColumn("IP", MySqlDbType.Text) { Length = 20 },
				new SqlColumn("UUID", MySqlDbType.Text) { Length = 40 }));
		}

		public static void Reload()
		{
			Clones = new List<CloneInfo>();
			string query = "SELECT * FROM Clones;";
			using (QueryResult reader = db.QueryReader(query))
			{
				while (reader.Read())
				{
					CloneInfo clone = new CloneInfo()
					{
						ID = reader.Get<int>("ID"),
						Character = reader.Get<string>("CharacterName"),
						IP = reader.Get<string>("IP"),
						UUID = reader.Get<string>("UUID")
					};
					Clones.Add(clone);
				}
			}
		}

		public static void AddClone(CloneInfo clone)
		{
			bool exists = Clones.Exists(e => e.Character == clone.Character && e.IP == clone.IP && e.UUID == clone.UUID);

			if (exists)
				return;

			string query = $"INSERT INTO Clones (CharacterName, IP, UUID) VALUES ('{clone.Character}', '{clone.IP}', '{clone.UUID}');";
			if (db.Query(query) != 1)
				TShock.Log.ConsoleError("Error inserting new character into clone DB.");
			Clones.Add(clone);
		}

		public static List<string> GetClones(string IP, string uuid = "ThisIsNotARealUuid")
		{
			List<string> names = new List<string>();
			Clones.Where(e => e.IP == IP || e.UUID == uuid).ForEach(e => { if (!names.Contains(e.Character)) names.Add(e.Character); });

			return names;
		}
	}
}
