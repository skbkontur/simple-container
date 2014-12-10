using System;

namespace SimpleContainer.Hosting
{
	public interface IComponentLogger
	{
		IDisposable OnRunComponent(ServiceInstance<IComponent> component);
		//todo убрать это
		void TRASH_DumpConstructionLog(string constructionLog);
	}
}