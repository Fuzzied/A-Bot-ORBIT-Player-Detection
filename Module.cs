using BotEngine.Motor;
using Sanderling.Accumulation;
using Sanderling.Motor;
using System.Collections.Generic;
using System.Linq;
using Sanderling.Parse;
using WindowsInput.Native;
using System.Linq;
using BotEngine.Common;


namespace Sanderling.ABot.Bot.Task
{
	static public class ModuleTaskExtension
	{
		static public bool? IsActive(
			this IShipUiModule module,
			Bot bot)
		{
			if (bot?.MouseClickLastAgeStepCountFromUIElement(module) <= 1)
				return null;

			if (bot?.ToggleLastAgeStepCountFromModule(module) <= 1)
				return null;

			return module?.RampActive;
		}

		static public IBotTask EnsureIsActive(
			this Bot bot,
			IShipUiModule module)
		{
			if (module?.IsActive(bot) ?? true)
				return null;

			return new ModuleToggleTask { bot = bot, module = module };
		}

		static public IBotTask EnsureIsActive(
			this Bot bot,
			IEnumerable<IShipUiModule> setModule) =>
			new BotTask { Component = setModule?.Select(module => bot?.EnsureIsActive(module)) };
	}

	public class ModuleToggleTask : IBotTask
	{
		public Bot bot;

		public IShipUiModule module;

		public IEnumerable<IBotTask> Component => null;

		public IEnumerable<MotionParam> Effects
		{
			get
			{
				var toggleKey = module?.TooltipLast?.Value?.ToggleKey;

				if (0 < toggleKey?.Length)
					yield return toggleKey?.KeyboardPressCombined();

				yield return module?.MouseClick(MouseButtonIdEnum.Left);
			}
		}
	}

	public class SkipAnomalyF : IBotTask
	{
		public Sanderling.Parse.IMemoryMeasurement MemoryMeasurement;
		public IEnumerable<IBotTask> Component => null;
		public static bool ActuallyAnomaly(Interface.MemoryStruct.IListEntry scanResult) =>
			scanResult?.CellValueFromColumnHeader("Distance")?.RegexMatchSuccessIgnoreCase("km") ?? false;
		public static bool AnomalySuitableGeneral(Interface.MemoryStruct.IListEntry scanResult) =>
			scanResult?.CellValueFromColumnHeader("Group")?.RegexMatchSuccessIgnoreCase("combat") ?? false;
		public IEnumerable<MotionParam> Effects
		{
			get
			{
				var altKey = VirtualKeyCode.MENU;
				var pKey = VirtualKeyCode.VK_P;

				yield return altKey.KeyDown();
				yield return pKey.KeyDown();
				yield return altKey.KeyUp();
				yield return pKey.KeyUp();

				yield return altKey.KeyDown();
				yield return pKey.KeyDown();
				yield return altKey.KeyUp();
				yield return pKey.KeyUp();

				var probeScannerWindow = MemoryMeasurement?.WindowProbeScanner?.FirstOrDefault();
				var scanActuallyAnomaly =
					probeScannerWindow?.ScanResultView?.Entry?.FirstOrDefault(ActuallyAnomaly);

				if (null != scanActuallyAnomaly)
				{
					var menuResult = MemoryMeasurement?.Menu?.ToList();
					if (null == menuResult)
					{
						yield return scanActuallyAnomaly.MouseClick(MouseButtonIdEnum.Right);
					}
					else
					{
						menuResult = MemoryMeasurement?.Menu?.ToList();
						var menuResultToUse = menuResult[0].Entry?.ToList();
						if (menuResultToUse[2].Text == "Ignore Result")
						{
							yield return menuResultToUse[2].MouseClick(MouseButtonIdEnum.Left);
						}
					}
				}
			}
		}
	}
}
