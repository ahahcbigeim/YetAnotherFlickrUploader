using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManyConsole;

namespace YetAnotherFlickrUploader.Helpers
{
	public class Options
	{
		private const string ShareWithFamily = "--family";
		private const string ShareWithFriends = "--friends";

		public static ModesEnum GetModeFromArgs(string modeSwitch)
		{
			ModesEnum result = ModesEnum.Upload;

			if (!string.IsNullOrEmpty(modeSwitch))
			{
				switch (modeSwitch)
				{
					case ShareWithFamily:
						result = ModesEnum.ShareWithFamily;
						break;
					case ShareWithFriends:
						result = ModesEnum.ShareWithFriends;
						break;
					default:
						throw new ArgumentOutOfRangeException("Invalid switch '" + modeSwitch + "'.");
				}
			}

			return result;
		}

	}

	public enum ModesEnum
	{
		Upload,
		ShareWithFamily,
		ShareWithFriends
	}
}
