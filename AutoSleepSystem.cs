using Terraria;
using Terraria.ModLoader;

namespace AutoSleep
{
	public class AutoSleepSystem : ModSystem
	{
		public static ModKeybind AutoSleepKeybind;

		public override void Load()
		{
			if (Main.dedServ)
				return;

			AutoSleepKeybind = KeybindLoader.RegisterKeybind(Mod, "Auto Sleep", "Z");
		}

		public override void Unload()
		{
			AutoSleepKeybind = null;
		}
	}
}
