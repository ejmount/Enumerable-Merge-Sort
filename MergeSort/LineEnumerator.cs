using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MergeSort
{
	class LineEnumerator : IEnumerator<string>, IEnumerator
	{
		private StreamReader reader;
		protected const int pageSize = 4 * 1024;
		protected char[] blockBuffer = new char[2 * pageSize];
		protected char[] currentLine = new char[1024];
		protected int currentLineLength = 0;
		protected int lengthOfBlockContents = -1;
		protected int cursor = 0;
		protected int uncopiedStringLength = 0;

		protected int Line = 0;


		public LineEnumerator(Stream s)
		{
			if (!s.CanSeek) throw new ArgumentException();
			reader = new StreamReader(s);
		}

		public string Current
		{
			get;
			protected set;
		}

		public void Dispose()
		{
			reader.Close();
		}

		object System.Collections.IEnumerator.Current
		{
			get { return this.Current; }
		}

		private void SaveStringSoFar()
		{
			Array.Copy(blockBuffer, cursor, currentLine, currentLineLength, uncopiedStringLength);
			currentLineLength += uncopiedStringLength;
			
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
						cursor += System.Environment.NewLine.Length;
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

		private void CyclePageForward()
		{
			Array.Copy(blockBuffer, pageSize, blockBuffer, 0, pageSize);
			lengthOfBlockContents -= pageSize;
			lengthOfBlockContents += reader.ReadBlock(blockBuffer, lengthOfBlockContents, pageSize);
			cursor -= pageSize;
		}

		static readonly char[] newline = System.Environment.NewLine.ToCharArray();
		static readonly int newLineSize = System.Environment.NewLine.Length;
		static readonly int negNewLineSize = -newLineSize;

		private bool NewlineAtIndex(int pos)
		{
			if (pos <= negNewLineSize || pos >= lengthOfBlockContents) return false;

			for (int i = 0; i < newline.Length; i++)
			{
				if (blockBuffer[pos + i] != newline[i] || pos + i >= lengthOfBlockContents)
					return false;
			}
			return true;
		}



		public void Reset()
		{
			if (reader.BaseStream.CanSeek)
			{
				reader.BaseStream.Seek(0, SeekOrigin.Begin);
				blockBuffer = new char[2 * pageSize];
				currentLine = new char[1024];
				currentLineLength = 0;
				lengthOfBlockContents = -1;
				cursor = 0;
				uncopiedStringLength = 0;
			}
			else
			{
				throw new NotSupportedException(); // We can't support the reset
			}
		}
	}
	
	public class FileLineEnumerable : IEnumerable<string>
	{
		FileInfo file;

		public FileLineEnumerable(FileInfo F)
		{
			this.file = F;
		}

		IEnumerator<string> IEnumerable<string>.GetEnumerator()
		{
			return new LineEnumerator(File.OpenRead(file.FullName));
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return new LineEnumerator(File.OpenRead(file.FullName));
		}
	}
}
