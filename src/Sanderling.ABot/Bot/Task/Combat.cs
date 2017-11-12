using BotEngine.Common;
using System.Collections.Generic;
using System.Linq;
using Sanderling.Motor;
using Sanderling.Parse;
using System;
using Sanderling.Interface.MemoryStruct;
using Sanderling.ABot.Parse;
using Bib3;
using BotEngine.Interface;
using WindowsInput.Native;
using BotEngine.Motor;

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
				var ArmorPERMATANK = true;

				var memoryMeasurementAtTime = bot?.MemoryMeasurementAtTime;
				var memoryMeasurementAccu = bot?.MemoryMeasurementAccu;

				var memoryMeasurement = memoryMeasurementAtTime?.Value;

				if (!memoryMeasurement.ManeuverStartPossible())
					yield break;

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

				var listOverviewDreadCheck = memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry?.Where(entry => entry?.Name?.RegexMatchSuccess("Dreadnought") ?? true)
					.ToList();

				var armorHitPoitns = memoryMeasurement?.ShipUi?.HitpointsAndEnergy?.Armor;
				var armorRepairers =
					memoryMeasurementAccu?.ShipUiModule?.Where(module => module?.TooltipLast?.Value?.IsArmorRepairer == true);
				var filteredArmorRepairers = armorRepairers?.Where(x => !x.RampActive && x.GlowVisible == true);

				if (ArmorPERMATANK == true)
				{
					yield return bot.EnsureIsActive(armorRepairers);
				}
				else
				{
					if (armorHitPoitns < 830 && armorHitPoitns > 450)
					{
						yield return bot.EnsureIsActive(armorRepairers);
					}
					else if (armorHitPoitns > 900)
					{
						yield return bot.DeactivateModule(filteredArmorRepairers);
					}
				}

				var probeScannerWindow = memoryMeasurement?.WindowProbeScanner?.FirstOrDefault();
				var scanActuallyAnomaly =
					probeScannerWindow?.ScanResultView?.Entry?.FirstOrDefault(ActuallyAnomaly);

				if (listOverviewDreadCheck.Count() > 0)
				{
					yield return new RetreatTask { Bot = bot };
				}
				else
				{



					if (listOverviewEntryToAttack.Count() > 0)
					{
						var listOverviewEntryFriends =
						memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry
						?.Where(entry => entry?.ListBackgroundColor?.Any(IsFriendBackgroundColor) ?? false)
						?.ToArray();

						if (bot?.OwnAnomaly != true)
						{
							if (listOverviewEntryFriends.Length > 0)
							{

								yield return new AnomalyEnter { bot = bot };
							}
							else
							{
								bot?.SetOwnAnomaly(true);
							}
						}

						if (bot?.OwnAnomaly != true)
						{
							if (listOverviewEntryFriends.Count() == 0)
							{
								bot?.SetOwnAnomaly(true);
								bot?.SetSkipAnomaly(false);
							}
							else
							{
								if (null == scanActuallyAnomaly && bot?.SkipAnomaly != true)
								{
									yield return new ReloadAnomalies();
								}
								else if (null != scanActuallyAnomaly && bot?.SkipAnomaly != true)
								{
									yield return new SkipAnomaly { bot = bot };
									bot?.SetSkipAnomaly(true);
								}
								else if (null != scanActuallyAnomaly && bot?.SkipAnomaly == true)
								{
									yield return new SkipAnomaly { bot = bot };
									bot?.SetSkipAnomaly(true);
								}
								else if (null == scanActuallyAnomaly && bot?.SkipAnomaly == true)
								{
									yield return new AnomalyEnter { bot = bot };
								}
							}
						}
					}

					var droneListView = memoryMeasurement?.WindowDroneView?.FirstOrDefault()?.ListView;

					var droneGroupWithNameMatchingPattern = new Func<string, DroneViewEntryGroup>(namePattern =>
							droneListView?.Entry?.OfType<DroneViewEntryGroup>()?.FirstOrDefault(group => group?.LabelTextLargest()?.Text?.RegexMatchSuccessIgnoreCase(namePattern) ?? false));

					var droneGroupInBay = droneGroupWithNameMatchingPattern("bay");
					var droneGroupInLocalSpace = droneGroupWithNameMatchingPattern("local space");

					var droneInBayCount = droneGroupInBay?.Caption?.Text?.CountFromDroneGroupCaption();
					var droneInLocalSpaceCount = droneGroupInLocalSpace?.Caption?.Text?.CountFromDroneGroupCaption();

					var setAfterbunner =
						memoryMeasurementAccu?.ShipUiModule?.Where(module => module?.TooltipLast?.Value?.IsAfterburner ?? false);


					if (listOverviewEntryToAttack.Count() > 0 && bot?.OwnAnomaly == true)
					{
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

						yield return bot?.EnsureIsActive(setAfterbunner); //ACTIVE AF/MWD

						//	assuming that local space is bottommost group.
						var setDroneInLocalSpace =
							droneListView?.Entry?.OfType<DroneViewEntryItem>()
							?.Where(drone => droneGroupInLocalSpace?.RegionCenter()?.B < drone?.RegionCenter()?.B)
							?.ToArray();

						var droneInLocalSpaceSetStatus =
							setDroneInLocalSpace?.Select(drone => drone?.LabelText?.Select(label => label?.Text?.StatusStringFromDroneEntryText()))?.ConcatNullable()?.WhereNotDefault()?.Distinct()?.ToArray();

						var droneInLocalSpaceIdle =
							droneInLocalSpaceSetStatus?.Any(droneStatus => droneStatus.RegexMatchSuccessIgnoreCase("idle")) ?? false;

						var overviewEntryLockTarget =
							listOverviewEntryToAttack?.FirstOrDefault(entry => !((entry?.MeTargeted ?? false) || (entry?.MeTargeting ?? false)));

						var NPCtargheted = memoryMeasurement?.Target?.Length;
						var CurrentlyTarget = memoryMeasurement?.Target?.FirstOrDefault(target => target?.IsSelected ?? false);

						var ShipManeuverStatus = memoryMeasurement.ShipUi?.Indication?.ManeuverType;

						if (0 < droneInBayCount && droneInLocalSpaceCount < 5)
							yield return droneGroupInBay.ClickMenuEntryByRegexPattern(bot, @"launch");

						if (droneInLocalSpaceIdle && NPCtargheted > 0)
							yield return droneGroupInLocalSpace.ClickMenuEntryByRegexPattern(bot, @"engage");

						if (NPCtargheted == null)
						{
							if (ShipManeuverStatus != ShipManeuverTypeEnum.Orbit)
								yield return new OrbitTarghet { target = overviewEntryLockTarget, targetLocked = null };
							if (overviewEntryLockTarget.DistanceMax < 60000)
								yield return new LockTarghet { target = overviewEntryLockTarget };
						}

						if (NPCtargheted != null && ShipManeuverStatus != ShipManeuverTypeEnum.Orbit)
						{
							yield return new OrbitTarghet { target = null, targetLocked = CurrentlyTarget };
						}

						if (NPCtargheted != null && NPCtargheted < TargetCountMax && ShipManeuverStatus == ShipManeuverTypeEnum.Orbit)
						{
							if (overviewEntryLockTarget.DistanceMax < 60000)
								yield return new LockTarghet { target = overviewEntryLockTarget };
						}
					}

					else if (listOverviewEntryToAttack.Count() == 0)
					{
						if (!(0 < listOverviewEntryToAttack?.Length))
						{
							if (0 < droneInLocalSpaceCount)
							{
								yield return droneGroupInLocalSpace.ClickMenuEntryByRegexPattern(bot, @"return.*bay");
							}
							else
							{
								Completed = true;
								bot?.SetOwnAnomaly(false);
								bot?.SetSkipAnomaly(false);
							}
						}
					}
				}
			}
		}

		public IEnumerable<MotionParam> Effects => null;
		static public bool ActuallyAnomaly(Interface.MemoryStruct.IListEntry scanResult) =>
			scanResult?.CellValueFromColumnHeader("Distance")?.RegexMatchSuccessIgnoreCase("km") ?? false;
	}

	public class LockTarghet : IBotTask
	{
		public Sanderling.Parse.IOverviewEntry target;
		public IEnumerable<IBotTask> Component => null;

		public IEnumerable<MotionParam> Effects
		{
			get
			{

				var ctrlKey = VirtualKeyCode.CONTROL;

				yield return ctrlKey.KeyDown();
				yield return target.MouseClick(MouseButtonIdEnum.Left);
				yield return ctrlKey.KeyUp();
			}
		}
	}

	public class OrbitTarghet : IBotTask
	{
		public Sanderling.Parse.IOverviewEntry target;
		public Sanderling.Parse.IShipUiTarget targetLocked;
		public IEnumerable<IBotTask> Component => null;

		public IEnumerable<MotionParam> Effects
		{
			get
			{

				var maiuscKEY = VirtualKeyCode.SHIFT;

				yield return maiuscKEY.KeyDown();

				if (targetLocked != null)
				{
					yield return targetLocked.MouseClick(MouseButtonIdEnum.Left);
				}
				else
				{
					yield return target.MouseClick(MouseButtonIdEnum.Left);
				}

				yield return maiuscKEY.KeyUp();
			}
		}
	}
}
