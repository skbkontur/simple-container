using SimpleContainer.Infection;

namespace SimpleContainer.Tests.Helpers
{
	public class TestContractAttribute : RequireContractAttribute
	{
		public TestContractAttribute(string contractName) : base(contractName)
		{
		}
	}
}