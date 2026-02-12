using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace AutoSleep
{
	public class AutoSleepConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		[DefaultValue(true)]
		public bool Enabled { get; set; }

		[DefaultValue(true)]
		public bool ChatMessagesEnabled { get; set; }

		[DefaultValue(true)]
		public bool StopLoopOnDeath { get; set; }

		[DefaultValue(true)]
		public bool StopLoopOnConfigDisabled { get; set; }

		[DefaultValue(true)]
		public bool StopLoopOnInvalidSleepWindow { get; set; }

		[DefaultValue(true)]
		public bool StopLoopOnLeaveBed { get; set; }

		[DefaultValue(true)]
		public bool StopLoopOutsideWindow { get; set; }

		[DefaultValue("19:30")]
		public string SleepStartTime { get; set; }

		[DefaultValue("04:30")]
		public string SleepEndTime { get; set; }

	}
}
