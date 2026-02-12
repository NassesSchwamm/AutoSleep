using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace AutoSleep
{
	public class AutoSleepPlayer : ModPlayer
	{
		private const int SearchRangeTiles = 6;
		private const int AttemptIntervalTicks = 30;
		private bool sleepLoopActive;
		private int attemptCooldown;
		private bool notifiedOutsideWindow;
		private bool notifiedInvalidTime;

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (AutoSleepSystem.AutoSleepKeybind == null || !AutoSleepSystem.AutoSleepKeybind.JustPressed)
				return;

			if (sleepLoopActive)
			{
				StopSleepLoop("manual toggle");
				return;
			}

			var config = ModContent.GetInstance<AutoSleepConfig>();
			if (config == null || !config.Enabled)
			{
				Log("AutoSleep: disabled in config.");
				return;
			}

			if (!TryGetSleepWindowMinutes(config, out int startMinutes, out int endMinutes, out string timeError))
			{
				Log($"AutoSleep: {timeError}");
				return;
			}

			if (!TryFindTouchingBed(Player, out int bedX, out int bedY))
			{
				Log("AutoSleep: stand in a bed to toggle.");
				return;
			}

			sleepLoopActive = true;
			attemptCooldown = 0;
			notifiedOutsideWindow = false;
			Log("AutoSleep: enabled.");

			if (IsWithinSleepWindow(startMinutes, endMinutes))
			{
				Player.sleeping.StartSleeping(Player, bedX, bedY);
				Log("AutoSleep: sleeping.");
			}
			else
			{
				Log("AutoSleep: outside sleep hours.");
				notifiedOutsideWindow = true;
			}
		}

		public override void PostUpdate()
		{
			if (!sleepLoopActive)
				return;

			var config = ModContent.GetInstance<AutoSleepConfig>();
			if (config == null)
			{
				StopSleepLoop("config unavailable");
				return;
			}

			bool hasValidWindow = TryGetSleepWindowMinutes(config, out int startMinutes, out int endMinutes, out string timeError);
			bool inWindow = hasValidWindow && IsWithinSleepWindow(startMinutes, endMinutes);
			bool allowStopConditions = config.StopLoopOutsideWindow || inWindow;

			if (!config.Enabled)
			{
				if (config.StopLoopOnConfigDisabled && allowStopConditions)
					StopSleepLoop("disabled in config");
				return;
			}

			if (Player.dead)
			{
				if (config.StopLoopOnDeath && allowStopConditions)
					StopSleepLoop("cannot sleep while dead");
				else if (Player.sleeping.isSleeping)
					Player.sleeping.StopSleeping(Player);
				return;
			}

			if (!hasValidWindow)
			{
				if (config.StopLoopOnInvalidSleepWindow && allowStopConditions)
				{
					StopSleepLoop(timeError);
					return;
				}

				if (!notifiedInvalidTime)
				{
					Log($"AutoSleep: {timeError}");
					notifiedInvalidTime = true;
				}
				return;
			}

			notifiedInvalidTime = false;

			if (config.StopLoopOnLeaveBed && allowStopConditions && !Player.sleeping.isSleeping && !TryFindTouchingBed(Player, out _, out _))
			{
				StopSleepLoop("left bed");
				return;
			}

			if (!inWindow)
			{
				if (Player.sleeping.isSleeping)
				{
					Player.sleeping.StopSleeping(Player);
					Log("AutoSleep: waking (outside sleep hours).");
				}
				else if (!notifiedOutsideWindow)
				{
					Log("AutoSleep: outside sleep hours.");
					notifiedOutsideWindow = true;
				}
				return;
			}

			notifiedOutsideWindow = false;

			if (Player.sleeping.isSleeping)
				return;

			if (attemptCooldown > 0)
			{
				attemptCooldown--;
				return;
			}

			attemptCooldown = AttemptIntervalTicks;
			if (TrySleepNearestBed(Player, startMinutes, endMinutes, out string feedback))
			{
				Log(feedback);
				return;
			}

			if (!string.IsNullOrEmpty(feedback))
				Log(feedback);
		}

		private void StopSleepLoop(string cause)
		{
			sleepLoopActive = false;
			attemptCooldown = 0;
			notifiedOutsideWindow = false;
			notifiedInvalidTime = false;

			if (Player.sleeping.isSleeping)
				Player.sleeping.StopSleeping(Player);

			if (string.IsNullOrEmpty(cause))
				Log("AutoSleep: loop stopped.");
			else
				Log($"AutoSleep: loop stopped ({cause}).");
		}

		private static bool TrySleepNearestBed(Player player, int startMinutes, int endMinutes, out string feedback)
		{
			var config = ModContent.GetInstance<AutoSleepConfig>();
			if (config == null || !config.Enabled)
			{
				feedback = "AutoSleep: disabled in config.";
				return false;
			}

			if (player.dead)
			{
				feedback = "AutoSleep: cannot sleep while dead.";
				return false;
			}

			if (player.sleeping.isSleeping)
			{
				feedback = "AutoSleep: already sleeping.";
				return false;
			}

			if (!TryFindTouchingBed(player, out int bestX, out int bestY))
			{
				feedback = "AutoSleep: step into a bed to sleep.";
				return false;
			}

			if (!IsWithinSleepWindow(startMinutes, endMinutes))
			{
				feedback = "AutoSleep: outside sleep hours.";
				return false;
			}

			player.sleeping.StartSleeping(player, bestX, bestY);
			feedback = "AutoSleep: sleeping.";
			return true;
		}

		private static bool TryFindTouchingBed(Player player, out int bedX, out int bedY)
		{
			int centerX = (int)(player.Center.X / 16f);
			int centerY = (int)(player.Center.Y / 16f);

			bedX = -1;
			bedY = -1;
			float bestDistSq = float.MaxValue;

			int minX = Math.Max(0, centerX - SearchRangeTiles);
			int maxX = Math.Min(Main.maxTilesX - 1, centerX + SearchRangeTiles);
			int minY = Math.Max(0, centerY - SearchRangeTiles);
			int maxY = Math.Min(Main.maxTilesY - 1, centerY + SearchRangeTiles);

			for (int x = minX; x <= maxX; x++)
			{
				for (int y = minY; y <= maxY; y++)
				{
					Tile tile = Main.tile[x, y];
					if (tile == null || !tile.HasTile || tile.TileType != TileID.Beds)
						continue;

					GetBedTopLeft(x, y, tile, out int foundBedX, out int foundBedY);
					Rectangle bedRect = new Rectangle(foundBedX * 16, foundBedY * 16, 4 * 16, 2 * 16);
					if (!player.Hitbox.Intersects(bedRect))
						continue;

					Vector2 bedCenter = new Vector2((foundBedX + 2) * 16f, (foundBedY + 1) * 16f);
					if (!Collision.CanHitLine(player.Center, 0, 0, bedCenter, 0, 0))
						continue;

					float dx = (foundBedX + 2) - centerX;
					float dy = (foundBedY + 1) - centerY;
					float distSq = dx * dx + dy * dy;

					if (distSq < bestDistSq)
					{
						bestDistSq = distSq;
						bedX = foundBedX;
						bedY = foundBedY;
					}
				}
			}

			return bedX != -1;
		}

		private static bool IsWithinSleepWindow(AutoSleepConfig config)
		{
			double hour = GetGameTimeHours();
			if (!TryGetSleepWindowMinutes(config, out int startMinutes, out int endMinutes, out _))
				return false;

			return IsWithinSleepWindow(startMinutes, endMinutes, hour);
		}

		private static bool IsWithinSleepWindow(int startMinutes, int endMinutes)
		{
			return IsWithinSleepWindow(startMinutes, endMinutes, GetGameTimeHours());
		}

		private static bool IsWithinSleepWindow(int startMinutes, int endMinutes, double hour)
		{
			double start = startMinutes / 60.0;
			double end = endMinutes / 60.0;

			if (start == end)
				return true;

			if (start < end)
				return hour >= start && hour < end;

			return hour >= start || hour < end;
		}

		private static double GetGameTimeHours()
		{
			double time = Main.time;
			if (!Main.dayTime)
				time += 54000.0;

			time = time / 86400.0 * 24.0;
			time += 4.5;

			if (time >= 24.0)
				time -= 24.0;

			return time;
		}

		private static bool TryGetSleepWindowMinutes(AutoSleepConfig config, out int startMinutes, out int endMinutes, out string error)
		{
			error = null;
			startMinutes = 0;
			endMinutes = 0;

			if (!TryParseTimeToMinutes(config.SleepStartTime, out startMinutes, out string startError))
			{
				error = $"invalid start time \"{config.SleepStartTime}\". {startError}";
				return false;
			}

			if (!TryParseTimeToMinutes(config.SleepEndTime, out endMinutes, out string endError))
			{
				error = $"invalid end time \"{config.SleepEndTime}\". {endError}";
				return false;
			}

			return true;
		}

		private static bool TryParseTimeToMinutes(string value, out int minutes, out string error)
		{
			error = null;
			minutes = 0;
			if (string.IsNullOrWhiteSpace(value))
			{
				error = "Value is empty. Use HH:MM (e.g., 19:30) or total minutes (0-1440).";
				return false;
			}

			string trimmed = value.Trim();
			if (trimmed.Contains(":"))
			{
				string[] parts = trimmed.Split(':');
				if (parts.Length != 2)
				{
					error = "Use HH:MM (e.g., 19:30).";
					return false;
				}

				if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hours))
				{
					error = "Hours must be an integer between 0 and 24.";
					return false;
				}

				if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mins))
				{
					error = "Minutes must be an integer between 0 and 59.";
					return false;
				}

				if (hours < 0 || hours > 24)
				{
					error = "Hours must be between 0 and 24.";
					return false;
				}

				if (mins < 0 || mins > 59)
				{
					error = "Minutes must be between 0 and 59.";
					return false;
				}

				if (hours == 24 && mins != 0)
				{
					error = "24:00 is the only valid 24:xx value.";
					return false;
				}

				minutes = (hours % 24) * 60 + mins;
				return true;
			}

			if (trimmed.Contains("."))
			{
				if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double hoursDecimal))
				{
					error = "Decimal hours must be a number like 19.5.";
					return false;
				}

				if (hoursDecimal < 0.0 || hoursDecimal > 24.0)
				{
					error = "Decimal hours must be between 0.0 and 24.0.";
					return false;
				}

				minutes = (int)Math.Round(hoursDecimal * 60.0, MidpointRounding.AwayFromZero) % 1440;
				return true;
			}

			if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int totalMinutes))
			{
				error = "Use HH:MM (e.g., 19:30) or total minutes (0-1440).";
				return false;
			}

			if (totalMinutes < 0 || totalMinutes > 1440)
			{
				error = "Total minutes must be between 0 and 1440.";
				return false;
			}

			minutes = totalMinutes % 1440;
			return true;
		}

		private static void Log(string message)
		{
			if (string.IsNullOrEmpty(message))
				return;

			var config = ModContent.GetInstance<AutoSleepConfig>();
			if (config == null || !config.ChatMessagesEnabled)
				return;

			Main.NewText(message, 80, 255, 80);
		}

		private static void GetBedTopLeft(int x, int y, Tile tile, out int bedX, out int bedY)
		{
			int frameX = tile.TileFrameX / 18;
			int frameY = tile.TileFrameY / 18;
			int bedWidth = 4;
			int bedHeight = 2;

			int offsetX = frameX % bedWidth;
			int offsetY = frameY % bedHeight;

			bedX = x - offsetX;
			bedY = y - offsetY;
		}
	}
}
