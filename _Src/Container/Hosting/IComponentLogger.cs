using System;

namespace SimpleContainer.Hosting
{
	public interface IComponentLogger
	{
		IDisposable OnRunComponent(ServiceInstance<IComponent> component);
		void DumpConstructionLog(string constructionLog);
	}
}