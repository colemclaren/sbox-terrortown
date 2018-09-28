using Sandbox;

namespace TerrorTown {
	[ClassLibrary]
	public partial class HumanControllable : BaseWeaponControllable, ICanBeDamaged, IHasHealth, IHasInventory {
		public static readonly string[] PlayerModels = new string[] {
			"models/player/phoenix.mdl",
			"models/player/arctic.mdl",
			"models/player/guerilla.mdl",
			"models/player.leet.mdl"
		};
	}
}