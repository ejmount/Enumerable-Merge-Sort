using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace MergeSort
{
	/// <summary>
	/// Wrapper to allow files to be treated as IEnumerables, e.g. for merge sorting.
	/// 
	/// </summary>
	public class FileLineEnumerable : IEnumerable<string>
	{
		private FileInfo file;

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
		protected const int pageSize = 4 * 1024;
		protected char[] blockBuffer = new char[2 * pageSize];
		protected char[] currentLine = new char[1024];
		protected int currentLineLength = 0;
		protected int cursor = 0;
		protected int lengthOfBlockContents = -1;
		protected int Line = 0;
		protected StreamReader reader;
		protected int uncopiedStringLength = 0;

		public string Current
		{
			get;
			protected set;
		}

		object System.Collections.IEnumerator.Current
		{
			get { return this.Current; }
		}

		public LineEnumerator(Stream s)
		{
			if (!s.CanSeek) throw new ArgumentException();
			reader = new StreamReader(s);
		}

		public void Dispose()
		{
			reader.Close();
		}

		public bool MoveNext()
		{
			try
			{
				if (lengthOfBlockContents == -1)
				{
					lengthOfBlockContents = reader.ReadBlock(blockBuffer, 0, blockBuffer.Length);
				}

				if (cursor + uncopiedStringLength >= lengthOfBlockContents)
				{
					return false;
				}

				bool done = false;

				uncopiedStringLength = 0;

				while (!done)
				{
					if (currentLineLength + uncopiedStringLength >= currentLine.Length)
					{
						ExpandCurrentLineBuffer();
					}

					if ((cursor + uncopiedStringLength >= lengthOfBlockContents
						|| NewlineAtIndex(cursor + uncopiedStringLength)))
					{
						SaveStringSoFar();

						done = true;
						this.Current = new string(currentLine, 0, currentLineLength);
						Line++;
						currentLineLength = 0;
						cursor += uncopiedStringLength;
						cursor += Environment.NewLine.Length;
						uncopiedStringLength = 0;
						break;
					}

					if ((cursor + uncopiedStringLength) > pageSize)
					{
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