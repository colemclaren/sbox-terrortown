using System;
using Sandbox;

namespace TerrorTown {
	public struct DeathTarget {
		public BaseEntity Entity;
		public string Socket;
		public Vector3 Offset;

		public static readonly DeathTarget NoTarget = new DeathTarget {
			Entity = null,
				Socket = string.Empty,
				Offset = Vector3.Zero
		};
	}

	[ClassLibrary(Name = "TerrorTownGamemode")]
	class Gamemode : BaseGamemode {
		public static Gamemode Current { get; set; }

		[Replicate]
		public Hud MyHud { get; private set; }

		[Replicate]
		public Phase Phase { get; set; }

		[Replicate]
		public int Round { get; set; }

		private double _timeToStartRound;
		private double _timeToEndRound;
		private double _timeToPrepRound;

		public double TimeToEndRound => _timeToEndRound;

		[Replicate]
		public int RoundTimerSeconds { get; set; }

		protected List<Player> _players;
		public int PlayerCount => _players.Count;

		[Replicate]
		public int TotalPlayers { get; set; } = 0;

		public DeathTarget DeathTarget { get; set; }

		// Round Prep and Post Round Settings
		[ConsoleVariable(
			Name = "ttt_preptime_seconds",
			Help = "The length of the preparation phase that occurs after players spawn in and before traitors are selected and a new round begins. Specified in seconds."
		)]
		public static int TTTPrepTimeSeconds { get; set; } = 30;

		[ConsoleVariable(
			Name = "ttt_firstpreptime",
			Help = "The length of the preparation phase for only the first round after a map loads.It is useful to make this higher than ttt_preptime_seconds so that players do not have to sit out a round just because they loaded slightly too slowly."
		)]
		public static int TTTFirstPrepTimeSeconds { get; set; } = 60;

		[ConsoleVariable(
			Name = "ttt_posttime_seconds",
			Help = "The length of time after a round has ended before the next preparation phase begins. The round report is displayed at the start of this phase. During this phase, stats/points are no longer tracked."
		)]
		public static int TTTPostTimeSeconds { get; set; } = 30;

		// Round Length Settings
		[ConsoleVariable(
			Name = "ttt_haste",
			Help = "Enables Haste Mode. In Haste Mode, the initial round time is short. Every death increases it by some amount. Puts pressure on traitors to keep things moving, which is more interesting for the innocent players."
		)]
		public static int TTTHasteEnabled { get; set; } = 1;

		[ConsoleVariable(
			Name = "ttt_haste_starting_minutes",
			Help = "Replaces ttt_roundtime_minutes when Haste Mode is on. Sets the initial time limit. (Haste Mode only)"
		)]
		public static double TTTHasteStartingMinutes { get; set; } = 5.0;

		[ConsoleVariable(
			Name = "ttt_haste_minutes_per_death",
			Help = "Specifies the number of minutes that is added to the round time for each death. Setting this to 0.5 will result in 0.5 * 60 = 30 seconds being added. (Haste Mode only)"
		)]
		public static double TTTHasteMinutesPerDeath { get; set; } = 0.5;

		[ConsoleVariable(
			Name = "ttt_roundtime_minutes",
			Help = "The time limit for each round, given in minutes, when Haste Mode is disabled."
		)]
		public static double TTTRoundTimeLimitMinutes { get; set; } = 10.0;

		// Traitor and Detective Settings
		[ConsoleVariable(
			Name = "ttt_traitor_pct",
			Help = "Percentage of total players that will be a traitor. The number of players will be multiplied by this number, and then rounded down. If the result is less than 1 or more than the player count, it is clamped to those values."
		)]
		public static double TTTPercentageTraitors { get; set; } = 0.25;

		[ConsoleVariable(
			Name = "ttt_traitor_max",
			Help = "Maximum number of traitors."
		)]
		public static int TTTMaxTraitors { get; set; } = 32;

		[ConsoleVariable(
			Name = "ttt_detective_min_players",
			Help = "Minimum number of players before detectives enter play. At lower playercounts it will be purely normal innocents vs traitors, at higher ones some innocents will be detective."
		)]
		public static int TTTMinPlayersForDetectives { get; set; } = 8;

		[ConsoleVariable(
			Name = "ttt_detective_pct",
			Help = "Percentage of total players that will be a detective (detective innocent). Handled similar to ttt_traitor_pct (rounded down etc)."
		)]
		public static double TTTPercentageDetectives { get; set; } = 0.13;

		[ConsoleVariable(
			Name = "ttt_detective_max",
			Help = "Maximum number of detectives. Can be used to cap or disable detectives."
		)]
		public static int TTTMaxDetectives { get; set; } = 32;

		// Gameplay Settings
		[ConsoleVariable(
			Name = "ttt_minimum_players",
			Help = "Number of players that must be present before the round begins. This is checked before the preparation phase starts, and before the actual round begins."
		)]
		public static int TTTMinimumPlayers { get; set; } = 2;

		[ConsoleVariable(
			Name = "ttt_postround_dm",
			Help = "Enables damage after a round has ended. Kills are not recorded for scoring purposes, so it's a free for all."
		)]
		public static int TTTPostRoundDMEnabled { get; set; } = 1;

		protected override void Initialize() {
			base.Initialize();

			Current = this;

			if (Server) {
				MyHud = World.CreateAndSpawnEntity<Hud>();

				Phase = Phase.Wait;
				RoundNumber = 0;
				RoundTimerSeconds = 0;
				TotalPlayers = 0;
				_timeToPrepRound = 0;

				_players = new List<Player>();
			}

			PreloadAssets();
		}

		protected static void PreloadAssets() {
			foreach (var mdl in HumanControllable.PlayerModels) {
				SkeletalModel.Library.Get(mdl, false);
			}

			Sound.Library.Get("Sounds/Weapons/pistol/pistol_fire2.wav", false);
		}

		protected override void Tick() {
			base.Tick();

			if (Authority) {
				ServerTick();
			}

			if (Client && Player.Local != null && (Player.Local.Controlling is DeathCamera == false)) {
				ClearDeathTarget();
			}
		}

		protected void ServerTick() {
			switch (ROUND) {
				case ROUND.WAIT:
					TTTWaitForRound();
					break;

				case ROUND.PREP:
					TTTPrepareRound();
					break;

				case ROUND.BEGIN:
					TTTBeginRound();
					break;

				case ROUND.ACTIVE:
					TTTActiveRound();
					break;

				case ROUND.END:
					TTTEndRound();
					break;

				default:
					throw new NotSupportedException();
			}
		}

		protected bool TTTNotEnoughPlayers() {
			TotalPlayers = PlayerCount;

			return (PlayerCount < TTTMinimumPlayers);
		}

		protected void TTTChangePhase(Phase phase) {
			Phase = phase;
		}

		protected void TTTWaitForRound() {
			if (Time.Now < _timeToPrepRound) {
				return;
			}

			if (TTTNotEnoughPlayers()) {
				/*if (PlayerCount >= 1)
				{
					MyHud.BroadcastMessage("Not enough players to start a new round...");
				}*/

				return;
			}

			TTTChangePhase(Phase.Prep);
		}

		protected void TTTPrepareRound() {
			if (Time.Now < _timeToPrepRound) {
				return;
			}

			if (TTTNotEnoughPlayers()) {
				TTTChangePhase(Phase.Wait);
				return;
			}

			MyHud.BroadcastMessage($"A new round begins in {TTTPrepTimeSeconds} seconds. Prepare yourself!");

			foreach (var player in _players) {
				if (player == null) continue;

				player.Team = (int) Team.Terror;
				RespawnPlayer(player);
			}

			BroadcastTTTPrepareRound();

			_timeToStartRound = Time.Now + TTTPrepTimeSeconds;
			TTTChangePhase(Phase.Begin);
		}

		protected void TTTBeginRound() {
			if (Time.Now < _timeToStartRound) {
				return;
			}

			if (TTTNotEnoughPlayers()) {
				TTTChangePhase(Phase.Wait);
				return;
			}

			var traitor = _players[Random.Int(0, _players.Count - 1)];

			foreach (var player in _players) {
				if (player == null) continue;

				player.Team = (int) Team.Terror;
				player.Role = (int) Role.Innocent;
				RespawnPlayer(player);
			}

			RoundNumber++;

			BroadcastTTTBeginRound();

			_timeToEndRound = Time.Now + RoundTimeLimitMinutes;
			TTTChangePhase(Phase.Active);
		}

		protected void TTTActiveRound() {
			// Round Alive Checks etc
			// TTTChangePhase(Phase.End);
		}

		protected void TTTEndRound() {
			BroadcastTTTEndRound();

			_timeToPrepRound = Time.Now + TTTPostTimeSeconds;
			TTTChangePhase(Phase.Wait);
		}

		[Multicast]
		protected void BroadcastTTTWaitForRound() {

		}

		[Multicast]
		protected void BroadcastTTTPrepareRound() {

		}

		[Multicast]
		protected void BroadcastTTTBeginRound() {

		}

		[Multicast]
		protected void BroadcastTTTEndRound() {

		}

		public static Color TeamColor(Team team) {
			switch (team) {
				case Team.Terror:
					return new Color(0, 0.8, 0);
				case Team.Spectator:
					new Color(0.8, 0.8, 0);
				default:
					return Color.White;
			}
		}

		public static double TeamSpawnOffset(Team team) {
			switch (team) {
				case Team.Terror:
					return 70;
				case Team.Spectator:
					return 140;
				default:
					return 0;
			}
		}

		public override Controllable CreateControllable(Player player) {
			switch ((Team) player.Team) {
				case Team.Spectator:
					return new SpectatorControllable();
				case Team.Terror:
					return new HumanControllable();
				default:
					return null;
			}
		}

		public override void LoadMap(string name) {
			// Disable motion blur no matter what client settings are?
			DefaultPostProcess.MotionBlurAmount = 0;

			base.LoadMap(name);
		}

		public override void OnPlayerJoined(Player player) {
			base.OnPlayerJoined(player);

			_players.Add(player);

			MyHud.BroadcastMessage($"{player.Name} has joined.");

			if (Authority) {
				player.Team = (Phase == Phase.Prep) ? (int) Team.Terror : (int) Team.Spectator;

				RespawnPlayer(player);
			}
		}

		public override void OnPlayerLeave(Player player) {
			base.OnPlayerLeave(player);

			_players.Remove(player);

			MyHud.BroadcastMessage($"{player.Name} has left.");
		}

		public override void OnPlayerDied(Player player, Controllable controllable) {
			base.OnPlayerDied(player);

			if (player == null) {
				return;
			}

			MyHud.BroadcastMessage($"{player.Name} has died.");

			if (Authority) {
				var position = controllable.Position;
				var eyeAngles = controllable.EyeAngles;

				var deathCamera = new DeathCamera();
				deathCamera.Spawn();
				player.Controlling = deathCamera;

				deathCamera.Position = position;
				deathCamera.Teleport(position);

				deathCamera.EyeAngles = eyeAngles;
				deathCamera.ClientEyeAngles = eyeAngles;

				// Handle team role shit
			}
		}

		public override void OnPlayerMessage(string playerName, int team, string message) {
			var color = TeamColor((Team) team);
			MyHud?.Chatbox?.AddMessage(playerName, message, color);
		}

		public override bool AllowPlayerMessage(Player sender, Player receive, string message) {
			return true;
		}

		public override void RespawnPlayer(Player player) {
			Log.Assert(Authority);

			if (player.Controlling is DeathCamera deathCamera && deathCamera.IsValid) {
				deathCamera.ClientClearTarget();
			}

			player.Controlling?.Destroy();

			var controllable = CreateControllable(player);
            controllable.Spawn();
			player.Controlling = controllable;

			var spawnPoint = FindSpawnPoint();

			if (spawnPoint == null) {
				Log.Warning("Player {0} couldn't find a spawn point.", player);

				return;
			}

			var spawnangles = spawnPoint.Rotation.ToAngles();
			spawnangles = spawnangles.WithZ(0).WithY(spawnangles.Y + 180.0f);

			var eyeAngles = Quaternion.FromAngles(spawnangles);

			var team = (Team) player.Team;
			var heightOffset = TeamSpawnOffset(team);

			var position = spawnPoint.Position + Vector3.Up * heightOffset;
			controllable.Position = position;
			controllable.Teleport(position);
			controllable.ClientLocation = position;
			controllable.EyeAngles = eyeAngles;
			controllable.ClientEyeAngles = eyeAngles;

			controllable.OnRespawned();
		}

		public override void OnLocalInput() {
			base.OnLocalInput();
		}

		public void ClearDeathTarget() {
			DeathTarget = DeathTarget.NoTarget;
		}

		public void RegisterDeath(Player killer, Player killed, Team killedTeam) {
			MyHud?.AddDeathLog(killer.Name, killed.Name, (Team) killer.Team, killedTeam);
		}
	}
}