using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace CloneNotify
{
	[ApiVersion(2, 1)]
	public class CloneMain : TerrariaPlugin
	{
		#region Plugin Info
		public override string Name => "CloneNotify";
		public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;
		public override string Author => "Zaicon";
		public override string Description => "A rewritten version of CloneChecker plugin.";
		#endregion

		public CloneMain(Main game) : base(game)
		{
			Order = 1;
		}

		#region Initialize/Dispose
		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
			GeneralHooks.ReloadEvent += OnReload;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
				GeneralHooks.ReloadEvent -= OnReload;
			}
			base.Dispose(disposing);
		}
		#endregion

		public static List<int> disabledNotify;

		#region Hooks
		private void OnInitialize(EventArgs args)
		{
			DB.Connect();
			DB.Reload();

			Commands.ChatCommands.Add(new Command("clones.check", CheckClones, "clones"));
			Commands.ChatCommands.Add(new Command("clones.check", ToggleCloneNotify, "clonenotify"));

			disabledNotify = new List<int>();
		}

		private void OnGreetPlayer(GreetPlayerEventArgs args)
		{
			var player = TShock.Players[args.Who];

			if (player == null || !player.Active || string.IsNullOrWhiteSpace(player.Name))
				return;

			//AddClone checks to see if it exists before actually adding to db
			DB.AddClone(new CloneInfo() { Character = player.Name, IP = player.IP, UUID = player.UUID });
			var clones = DB.GetClones(player.IP, player.UUID);

			var notifies = TShock.Players.Where(e => e != null && e.Active && e.IsLoggedIn && e.HasPermission("clones.check") && !disabledNotify.Contains(e.User.ID)).ToList();
			if (clones.Count > 5)
				notifies.ForEach(e => e.SendInfoMessage($"{player.Name} has a lot of clones, including: {string.Join(", ", clones.GetRange(clones.Count - 3, 3))}. Use /clones for more clones."));
			else
				notifies.ForEach(e => e.SendInfoMessage($"{player.Name} also goes by: {string.Join(", ", clones)}"));
		}

		private void OnReload(ReloadEventArgs e)
		{
			DB.Reload();
		}
		#endregion

		#region Commands
		private void CheckClones(CommandArgs args)
		{
			// clones -<a/i/o> <info>

			if (args.Parameters.Count != 2)
			{
				args.Player.SendErrorMessage($"Invalid syntax: {TShock.Config.CommandSpecifier}clones <-a/-i/-n/-o> <search>");
				return;
			}

			switch (args.Parameters[0])
			{
				case "-a":
				case "-A":
					var user = TShock.Users.GetUserByName(args.Parameters[1]);
					if (user == null)
					{
						args.Player.SendErrorMessage("Unknown account.");
						return;
					}
					var iplist = JsonConvert.DeserializeObject<List<string>>(user.KnownIps);
					if (iplist.Count == 0 || string.IsNullOrWhiteSpace(user.UUID))
					{
						args.Player.SendErrorMessage("This is an empty account.");
						return;
					}
					var clones = DB.GetClones(iplist.Last(), user.UUID);
					args.Player.SendInfoMessage($"{user.Name}'s clones: {string.Join(", ", clones)}");
					break;
				case "-i":
				case "-I":
					clones = DB.GetClones(args.Parameters[1]);
					args.Player.SendInfoMessage($"Character matches for IP {args.Parameters[1]}: {string.Join(", ", clones)}");
					break;
				case "-n":
				case "-N":
					var cloneinfo = DB.GetCloneInfo(args.Parameters[1]);
					clones = DB.GetClones(cloneinfo.IP, cloneinfo.UUID);
					args.Player.SendInfoMessage($"{cloneinfo.Character}'s clones: {string.Join(", ", clones)}");
					break;
				case "-o":
				case "-O":
					var users = TShock.Utils.FindPlayer(args.Parameters[1]);
					if (users.Count == 0)
					{
						args.Player.SendErrorMessage($"No matches found for player {args.Parameters[1]}.");
						return;
					}
					else if (users.Count > 1)
					{
						TShock.Utils.SendMultipleMatchError(args.Player, users.Select(e => e.Name));
						return;
					}
					clones = DB.GetClones(users[0].IP, users[0].UUID);
					args.Player.SendInfoMessage($"{users[0].Name}'s clones: {string.Join(", ", clones)}");
					break;
				default:
					args.Player.SendErrorMessage($"Invalid syntax: {TShock.Config.CommandSpecifier}clones <-a/-i/-n/-o> <search>");
					break;
			}
		}

		private void ToggleCloneNotify(CommandArgs args)
		{
			if (disabledNotify.Contains(args.Player.User.ID))
			{
				disabledNotify.Remove(args.Player.User.ID);
				args.Player.SendSuccessMessage("You will now receive clone notifications.");
			}
			else
			{
				disabledNotify.Add(args.Player.User.ID);
				args.Player.SendSuccessMessage("You will not receive clone notifications.");
			}
		}
		#endregion
	}
}
