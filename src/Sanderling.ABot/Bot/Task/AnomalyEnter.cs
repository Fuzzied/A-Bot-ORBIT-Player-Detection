using System.Collections.Generic;
using System.Linq;
using Sanderling.Motor;
using Sanderling.Parse;
using BotEngine.Common;
using Sanderling.ABot.Parse;
using WindowsInput.Native;

namespace Sanderling.ABot.Bot.Task
{
	public class AnomalyEnter : IBotTask
	{
		public const string NoSuitableAnomalyFoundDiagnosticMessage = "no suitable anomaly found. waiting for anomaly to appear.";

		public Bot bot;

		static public bool AnomalySuitableGeneral(Interface.MemoryStruct.IListEntry scanResult) =>
			scanResult?.CellValueFromColumnHeader("Group")?.RegexMatchSuccessIgnoreCase("combat") ?? false;

		public IEnumerable<IBotTask> Component
		{
			get
			{
				var memoryMeasurementAtTime = bot?.MemoryMeasurementAtTime;
				var memoryMeasurementAccu = bot?.MemoryMeasurementAccu;

				var memoryMeasurement = memoryMeasurementAtTime?.Value;

				if (!memoryMeasurement.ManeuverStartPossible())
					yield break;

				var probeScannerWindow = memoryMeasurement?.WindowProbeScanner?.FirstOrDefault();

				var scanResultCombatSite =
					probeScannerWindow?.ScanResultView?.Entry?.FirstOrDefault(AnomalySuitableGeneral);

				if (null == scanResultCombatSite)
					yield return new DiagnosticTask
					{
						MessageText = NoSuitableAnomalyFoundDiagnosticMessage,
					};

				if (null != scanResultCombatSite)
				{

					var menuResult = memoryMeasurement?.Menu?.ToList();
					if (null == menuResult)
					{
						yield return scanResultCombatSite.ClickMenuEntryByRegexPattern(bot, "");
					}
					else
					{
						menuResult = memoryMeasurement?.Menu?.ToList();

						var menuResultWarp = menuResult?[0].Entry.ToArray();
						var menuResultSelectWarpMenu = menuResultWarp?[1];
						if (menuResult.Count < 2)
						{
							yield return menuResultSelectWarpMenu.ClickMenuEntryByRegexPattern(bot, "");
						}
						else
						{
							var menuSpecificDistance = menuResult[1]?.Entry.ToArray();
							bot?.SetSkipAnomaly(false);
							bot?.SetOwnAnomaly(false);

							yield return menuSpecificDistance[3].ClickMenuEntryByRegexPattern(bot, "within 30 km");
						}
					}
				}
			}
		}

		public IEnumerable<MotionParam> Effects => null;
	}

	public class SkipAnomaly : IBotTask
	{
		public const string NoSuitableAnomalyFoundDiagnosticMessage = "no suitable anomaly found. waiting for anomaly to appear.";

		public Bot bot;

		static public bool ActuallyAnomaly(Interface.MemoryStruct.IListEntry scanResult) =>
			scanResult?.CellValueFromColumnHeader("Distance")?.RegexMatchSuccessIgnoreCase("km") ?? false;

		public IEnumerable<IBotTask> Component
		{
			get
			{
				var memoryMeasurementAtTime = bot?.MemoryMeasurementAtTime;
				var memoryMeasurementAccu = bot?.MemoryMeasurementAccu;

				var memoryMeasurement = memoryMeasurementAtTime?.Value;

				var probeScannerWindow = memoryMeasurement?.WindowProbeScanner?.FirstOrDefault();
				var scanActuallyAnomaly =
					probeScannerWindow?.ScanResultView?.Entry?.FirstOrDefault(ActuallyAnomaly);

				if (null != scanActuallyAnomaly)
				{
					yield return scanActuallyAnomaly.ClickMenuEntryByRegexPattern(bot, "Ignore Result");
				}
				else
				{
					yield break;
				}
			}
		}

		public IEnumerable<MotionParam> Effects => null;
	}

	public class ReloadAnomalies : IBotTask
	{
		public const string NoSuitableAnomalyFoundDiagnosticMessage = "no suitable anomaly found. waiting for anomaly to appear.";

		public IEnumerable<IBotTask> Component => null;
		
		public IEnumerable<MotionParam> Effects
		{
			get
			{
				var APPS = VirtualKeyCode.APPS;

				yield return APPS.KeyboardPress();
				yield return APPS.KeyboardPress();
			}
		}
	}
}
