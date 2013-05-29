using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace MergeSort
{
	/// <summary>
	/// Wrapper to allow files to be treated as IEnumerables, e.g. for merge sorting.
	/// </summary>
	public class FileLineEnumerable : IEnumerable<string>
	{
		private FileInfo file;
		/// <summary>
		/// Build an Enumerable object which returns the lines of the file.
		/// </summary>
		/// <param name="F">The file to construct an Enumerable from</param>
		public FileLineEnumerable(FileInfo F)
		{
			this.file = F;
		}

		IEnumerator<string> IEnumerable<string>.GetEnumerator()
		{
			return new LineEnumerator(File.OpenRead(file.FullName));
			#warning File handles might be leaking. 
			// The project I originally wrote this for was too quick/dirty/small to care about leaked filehandles,
			// but a bigger project might need to watch for them. 
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<string>)this).GetEnumerator();
		}
	}

	
	public class LineEnumerator : IEnumerator<string>, IEnumerator
	{
		/* General strategy:
		 * 
		 * Load in two "pages" of the file into a buffer to begin with.
		 * While there is still stream content left, 
		 *		step forward through the page, keeping track of how far we stepped. (uncopiedStringLength)
		 *		If we encounter a newline, 
		 *			save the section between where we started (cursor) and the newline to `currentLine` (our output)
		 *			Set the cursor to the next character after the found newline.
		 *			Break.
		 *		If we step across the halfway point of the buffer, 
		 *			copy the second half of the buffer over the first
		 *			load in the next page into the second half
		 *			move the current position backwards one page's worth. 
		 *			start loop again.
		 */

		/// <summary>
		/// The size of one page. Ideally, this would be exactly the size of the cache.
		/// </summary>
		protected const int pageSize = 4 * 1024;

		/// <summary>
		/// Data storage. 
		/// </summary>
		protected char[] blockBuffer = new char[2 * pageSize];

		/// <summary>
		/// The line accumulated so far. Used in case a page cycle cuts off the beginning of the string.
		/// </summary>
		protected char[] currentLine = new char[1024];

		/// <summary>
		/// the length of the filled portion of currentLine.
		/// </summary>
		protected int currentLineLength = 0;

		/// <summary>
		/// The position in blockBuffer we started out from.
		/// </summary>
		protected int cursor = 0;

		/// <summary>
		/// If there isn't one page of data left in the stream, blockBuffer might not be entirely full.
		/// If so, <code>lengthOfBlockContents &lt; pageSize</code>
		/// </summary>
		protected int lengthOfBlockContents = -1;

		/// <summary>
		/// Debug field. Knowing how many lines have been parsed is occasionally useful. 
		/// </summary>
		protected int Line = 0;

		/// <summary>
		/// A wrapper around the stream being read. 
		/// </summary>
		protected StreamReader reader;
		/// <summary>
		/// The length of the string beginning at <code>cursor</code> that hasn't been explicitly saved yet. 
		/// </summary>
		protected int uncopiedStringLength = 0;

		/// <summary>
		/// The current line, or null if one hasn't been read yet. 
		/// </summary>
		public string Current
		{
			get;
			protected set;
		}

		object System.Collections.IEnumerator.Current
		{
			get { return this.Current; }
		}

		/// <summary>
		/// Build a new Enumerator from the given seekable stream.
		/// </summary>
		/// <param name="s"></param>
		public LineEnumerator(Stream s)
		{
			if (!s.CanSeek) throw new ArgumentException("s");
			reader = new StreamReader(s);
		}

		/// <summary>
		/// Dispose this and close the underlying stream.
		/// </summary>
		public void Dispose()
		{
			reader.Close();
		}

		/// <summary>
		/// Get the next line of the stream. Result is assigned to this.Current
		/// </summary>
		/// <returns>True if sucessful, false otherwise</returns>
		public bool MoveNext()
		{

			try
			{
				// We haven't read anything yet.
				if (lengthOfBlockContents == -1)
				{
					lengthOfBlockContents = reader.ReadBlock(blockBuffer, 0, blockBuffer.Length);
					// Fill ALL the buffer
				}

				// ... This block should never fire, but I'm not sure enough of that to take it out completely.
				if (cursor + uncopiedStringLength >= lengthOfBlockContents)
				{
					//return false;
					throw new InvalidOperationException();
				}

				bool done = false;

				uncopiedStringLength = 0;

				while (!done)
				{
					if (currentLineLength + uncopiedStringLength >= currentLine.Length)
					{
						ExpandCurrentLineBuffer(); // We must construct additional space.
					}

					// If either we ran off the end of the data, 
					// or we found a newline, 
					if ((cursor + uncopiedStringLength >= lengthOfBlockContents
						|| NewlineAtIndex(cursor + uncopiedStringLength)))
					{
						SaveStringSoFar();

						done = true;
						this.Current = new string(currentLine, 0, currentLineLength);
						Line++;
						currentLineLength = 0;
						// Step forward to the start of the next line.
						cursor += uncopiedStringLength;
						cursor += Environment.NewLine.Length;
						uncopiedStringLength = 0;
						break;
					}

					// We wandered off the end of the page, 
					if ((cursor + uncopiedStringLength) > pageSize)
					{
						// Save what we have so that we still have it after the page cycle. 
						SaveStringSoFar();
						cursor += uncopiedStringLength;
						uncopiedStringLength = 0;
						CyclePageForward();
					}
					uncopiedStringLength++;
				}
				return true;
			}
			catch (IOException)
			{
				return false;
			}
		}

		/// <summary>
		/// Reset the Enumerator to the beginning of the stream.
		/// </summary>
		public void Reset()
		{
			try {
				reader.BaseStream.Seek(0, SeekOrigin.Begin);
				blockBuffer = new char[2 * pageSize];
				currentLine = new char[1024];
				currentLineLength = 0;
				lengthOfBlockContents = -1;
				cursor = 0;
				uncopiedStringLength = 0;
			}
			catch (IOException e)
			{
				throw new NotSupportedException("Cannot reset Enumerator", e);
			}
		}

		private void CyclePageForward()
		{
			Array.Copy(blockBuffer, pageSize, blockBuffer, 0, pageSize);
			lengthOfBlockContents -= pageSize;
			lengthOfBlockContents += reader.ReadBlock(blockBuffer, lengthOfBlockContents, pageSize);
			cursor -= pageSize;
		}

		private void ExpandCurrentLineBuffer()
		{
			var temp = new char[currentLine.Length * 2];
			for (int i = 0; i < currentLine.Length; i++)
			{
				temp[i] = currentLine[i];
			}
			currentLine = temp;
		}

		private bool NewlineAtIndex(int pos)
		{
			if (pos <= -Environment.NewLine.Length || pos >= lengthOfBlockContents) return false;

			for (int i = 0; i < Environment.NewLine.Length; i++)
			{
				if (blockBuffer[pos + i] != Environment.NewLine[i] || pos + i >= lengthOfBlockContents)
					return false;
			}
			return true;
		}

		private void SaveStringSoFar()
		{
			Array.Copy(blockBuffer, cursor, currentLine, currentLineLength, uncopiedStringLength);
			currentLineLength += uncopiedStringLength;
		}
	}
}