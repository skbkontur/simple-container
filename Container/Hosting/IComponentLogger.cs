using System;

namespace SimpleContainer.Hosting
{
	public interface IComponentLogger
	{
		IDisposable OnRunComponent(Type componentType);
		//todo убрать это
		void TRASH_DumpConstructionLog(string constructionLog);
	}
}