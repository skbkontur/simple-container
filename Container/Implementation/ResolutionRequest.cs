using System;

namespace SimpleContainer.Implementation
{
	internal class ResolutionRequest
	{
		public Type type;
		public string name;
		public bool createNew;
	}
}