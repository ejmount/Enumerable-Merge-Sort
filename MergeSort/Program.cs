using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace MergeSort
{
	public static class MergeSort
	{
		const bool debug = false;
		static int itemsOut = 0;

		/// <summary>
		/// Merge sort the given sorted collections.
		/// </summary>
		/// <typeparam name="T">The type contained within the collections.</typeparam>
		/// <param name="streamsIn">The collections to merge. EVERY COLLECTION MUST BE SORTED.</param>
		/// <returns>The aggregate of all items in streamsIn, in order.</returns>
		public static IEnumerable<T> Merge<T>(params IEnumerable<T>[] streamsIn) where T : IComparable<T>
		{
			var streams = new List<IEnumerator<T>>(streamsIn.Length);

			if (debug) // Make sure all the streams are ordered.
			{
				System.Diagnostics.Stopwatch S = System.Diagnostics.Stopwatch.StartNew();
				foreach (var list in streamsIn)
				{
					var arrr = list.ToList();
					var res = Enumerable.Range(0, arrr.Count - 1).Select(i => arrr[i].CompareTo(arrr[i + 1])).ToList();

					var test = res.All(i => i <= 0);

					if (!test)
						throw new InvalidDataException();
				}
				S.Stop();
				Debug.WriteLine("Took {0}ms to test", S.ElapsedMilliseconds);
			}

			for (int i = 0; i < streamsIn.Length; i++)
			{
				var e = streamsIn[i].GetEnumerator();
				if (e.MoveNext())
				{
					streams.Add(e);
				}
			}

			while (streams.Count > 0)
			{
				int smallIndex = 0;
				for (int i = 0; i < streams.Count; i++)
				{
					if (streams[i].Current.CompareTo(streams[smallIndex].Current) < 0)
						smallIndex = i;
				}

				yield return streams[smallIndex].Current;

				if (debug)
				{
					itemsOut++;
					if (itemsOut % 100000==0) { Debug.WriteLine("{0:n0} items merged", itemsOut); }
				}

				if (!streams[smallIndex].MoveNext())
				{
					streams.RemoveAt(smallIndex);
				}

			}
		}
				
	}
}
