namespace SimpleContainer
{
	public interface ISimpleLogWriter
	{
		void WriteIndent(int count);
		void WriteName(string name);
		void WriteMeta(string meta);
		void WriteUsedContract(string contractName);
		void WriteNewLine();
	}
}