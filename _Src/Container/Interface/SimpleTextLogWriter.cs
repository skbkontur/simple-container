using System.Text;

namespace SimpleContainer.Interface
{
	public class SimpleTextLogWriter : ISimpleLogWriter
	{
		private readonly StringBuilder builder = new StringBuilder();

		public void WriteIndent(int count)
		{
			builder.Append('\t', count);
		}

		public void WriteName(string name)
		{
			builder.Append(name);
		}

		public void WriteMeta(string meta)
		{
			builder.Append(meta);
		}

		public void WriteUsedContract(string contractName)
		{
			builder.Append("[");
			builder.Append(contractName);
			builder.Append("]");
		}

		public void WriteNewLine()
		{
			builder.AppendLine();
		}

		public string GetText()
		{
			return builder.ToString().Trim();
		}
	}
}