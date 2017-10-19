using BotEngine.Common;
using System.Collections.Generic;
using System.Linq;
using Sanderling.Motor;
using Sanderling.Parse;
using System;
using Sanderling.Interface.MemoryStruct;
using Sanderling.ABot.Parse;
using Bib3;

namespace Sanderling.ABot.Bot.Task
{
	public class CombatTask : IBotTask
	{
		const int TargetCountMax = 4;

		public Bot bot;

		public bool Completed { private set; get; }

		public IEnumerable<IBotTask> Component
		{
			get
			{
				var memoryMeasurementAtTime = bot?.MemoryMeasurementAtTime;
				var memoryMeasurementAccu = bot?.MemoryMeasurementAccu;

				var memoryMeasurement = memoryMeasurementAtTime?.Value;

				if (!memoryMeasurement.ManeuverStartPossible())
					yield break;


				// our speed
				var speedMilli = bot?.MemoryMeasurementAtTime?.Value?.ShipUi?.SpeedMilli;

				// Object. They should be shown in overview. If you want orbit "collidable entity" replace the word "asteroid" with a word from the column type
				var Broken = memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry
					?.Where(entry => entry?.Name?.RegexMatchSuccessIgnoreCase("broken") ?? false)
					?.OrderBy(entry => entry?.DistanceMax ?? int.MaxValue)
					?.ToArray();
				// if we not warp and our speed < 20 m/s then try orbit 30 km
				if (speedMilli < 20000)
					yield return Broken.FirstOrDefault().ClickMenuEntryByRegexPattern(bot, "Orbit", "30 km");
				//Looking for Afterburner module
				var moduleAB = memoryMeasurementAccu?.ShipUiModule?.Where(module => module?.TooltipLast?.Value?.LabelText?.Any(
					label => label?.Text?.RegexMatchSuccess("Afterburner", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ?? false)?? false);
				//turn it on
				if  (moduleAB != null)
                    yield return bot.EnsureIsActive(moduleAB);

				bool IsFriendBackgroundColor(ColorORGB color) =>
					color.OMilli == 500 && color.RMilli == 0 && color.GMilli == 150 && color.BMilli == 600;

			  var listOverviewEntryToAttack =
					memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry?.Where(entry => entry?.MainIcon?.Color?.IsRed() ?? false)
					?.OrderBy(entry => bot.AttackPriorityIndex(entry))
					?.OrderBy(entry => entry?.Name?.RegexMatchSuccessIgnoreCase(@"coreli|centi|alvi|pithi|corpii|gistii")) //Frigate
					?.OrderBy(entry => entry?.Name?.RegexMatchSuccessIgnoreCase(@"corelior|centior|alvior|pithior|corpior|gistior")) //Destroyer
					?.OrderBy(entry => entry?.Name?.RegexMatchSuccessIgnoreCase(@"corelum|centum|alvum|pithum|corpum|gistum")) //Cruiser
					?.OrderBy(entry => entry?.Name?.RegexMatchSuccessIgnoreCase(@"corelatis|centatis|alvatis|pithatis|copatis|gistatis")) //Battlecruiser
					?.OrderBy(entry => entry?.Name?.RegexMatchSuccessIgnoreCase(@"core\s|centus|alvus|pith\s|corpus|gist\s")) //Battleship
					?.ThenBy(entry => entry?.DistanceMax ?? int.MaxValue)
					?.ToArray();

				if (listOverviewEntryToAttack.Count() > 0)
				{
					var listOverviewEntryFriends =
					memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry
					?.Where(entry => entry?.ListBackgroundColor?.Any(IsFriendBackgroundColor) ?? false)
					?.ToArray();

					if (OwnAnomaly != true)
					{
						if (listOverviewEntryFriends.Length > 0)
						{
							yield return bot.SkipAnomaly(memoryMeasurement);
							yield return new AnomalyEnter { bot = bot };
						}
						else
						{
							Bot?.SetOwnAnomaly(true);
						}
					}
				}

				var targetSelected =
					memoryMeasurement?.Target?.FirstOrDefault(target => target?.IsSelected ?? false);

				var shouldAttackTarget =
					listOverviewEntryToAttack?.Any(entry => entry?.MeActiveTarget ?? false) ?? false;

				var setModuleWeapon =
					memoryMeasurementAccu?.ShipUiModule?.Where(module => module?.TooltipLast?.Value?.IsWeapon ?? false);

				if (null != targetSelected)
					if (shouldAttackTarget)
						yield return bot.EnsureIsActive(setModuleWeapon);
					else
						yield return targetSelected.ClickMenuEntryByRegexPattern(bot, "unlock");

				var droneListView = memoryMeasurement?.WindowDroneView?.FirstOrDefault()?.ListView;

				var droneGroupWithNameMatchingPattern = new Func<string, DroneViewEntryGroup>(namePattern =>
						droneListView?.Entry?.OfType<DroneViewEntryGroup>()?.FirstOrDefault(group => group?.LabelTextLargest()?.Text?.RegexMatchSuccessIgnoreCase(namePattern) ?? false));

				var droneGroupInBay = droneGroupWithNameMatchingPattern("bay");
				var droneGroupInLocalSpace = droneGroupWithNameMatchingPattern("local space");

				var droneInBayCount = droneGroupInBay?.Caption?.Text?.CountFromDroneGroupCaption();
				var droneInLocalSpaceCount = droneGroupInLocalSpace?.Caption?.Text?.CountFromDroneGroupCaption();

				//	assuming that local space is bottommost group.
				var setDroneInLocalSpace =
					droneListView?.Entry?.OfType<DroneViewEntryItem>()
					?.Where(drone => droneGroupInLocalSpace?.RegionCenter()?.B < drone?.RegionCenter()?.B)
					?.ToArray();

				var droneInLocalSpaceSetStatus =
					setDroneInLocalSpace?.Select(drone => drone?.LabelText?.Select(label => label?.Text?.StatusStringFromDroneEntryText()))?.ConcatNullable()?.WhereNotDefault()?.Distinct()?.ToArray();

				var droneInLocalSpaceIdle =
					droneInLocalSpaceSetStatus?.Any(droneStatus => droneStatus.RegexMatchSuccessIgnoreCase("idle")) ?? false;

				if (shouldAttackTarget)
				{
					if (0 < droneInBayCount && droneInLocalSpaceCount < 5)
						yield return droneGroupInBay.ClickMenuEntryByRegexPattern(bot, @"launch");

					if (droneInLocalSpaceIdle)
						yield return droneGroupInLocalSpace.ClickMenuEntryByRegexPattern(bot, @"engage");
				}

				var overviewEntryLockTarget =
					listOverviewEntryToAttack?.FirstOrDefault(entry => !((entry?.MeTargeted ?? false) || (entry?.MeTargeting ?? false)));

				if (null != overviewEntryLockTarget && !(TargetCountMax <= memoryMeasurement?.Target?.Length))
					yield return overviewEntryLockTarget.ClickMenuEntryByRegexPattern(bot, @"^lock\s*target");

					if (!(0 < listOverviewEntryToAttack?.Length))
					{
						if (0 < droneInLocalSpaceCount)
						{
							yield return droneGroupInLocalSpace.ClickMenuEntryByRegexPattern(bot, @"return.*bay");
						}
						else
						{
							Completed = true;
							Bot?.SetOwnAnomaly(false);
						}
					}
			}
		}

		public IEnumerable<MotionParam> Effects => null;
	}
}
