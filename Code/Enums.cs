using System;
using Sandbox;

namespace TerrorTown
{
	// Teams
	public enum TEAM {
		SPEC,
		TERROR
	}
	
	// Round Status
	public enum ROUND {
		WAIT,
		PREP,
		ACTIVE,
		POST
	}

	// Player Roles
	public enum ROLE {
		INNOCENT,
		TRAITOR,
		DETECTIVE
	}

	// Game Event Logs
	public enum EVENT {
		KILL,
		SPAWN,
		GAME,
		FINISH,
		SELECTED,
		BODYFOUND,
		CREDITFOUND,
		C4PLANT,
		C4DISARM,
		C4EXPLODE
	}

	// Win Types
	public enum WIN {
		NONE,
		TRAITOR,
		INNOCENT,
		TIMELIMIT
	}

	// Weapon Types
	public enum WEAPON {
		NONE,
		UNARMED,
		MELEE,
		PISTOL,
		HEAVY,
		NADE,
		CARRY,
		EQUIP1,
		EQUIP2,
		ROLE
	}

	// Kill Types (for last words)
	public enum KILL {
		NORMAL,
		SUICIDE,
		FALL,
		BURN
	}

	// Mute Types
	public enum MUTE {
		NONE,
		TERROR,
		SPEC,
		ALL
	}
}