using StardewValley;
using xTile.Dimensions;

namespace ThisRoundsOnMe
{
	internal class PriceToken
	{
		private string Price;

		public bool AllowsInput()
		{
			return false;
		}

		public bool CanHaveMultipleValues(string input = null)
		{
			return false;
		}

		public bool UpdateContext()
		{
			string oldPrice = this.Price;
			this.Price = ModEntry.CalculatePrice().ToString();
			return this.Price != oldPrice;
		}

		public bool IsReady()
		{
			return this.Price != null;
		}

		public IEnumerable<string> GetValues(string inputs)
		{
			yield return this.Price;
		}
	}
}
