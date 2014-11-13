using System;
using System.Collections.Generic;
using SimpleContainer.Reflection;

namespace SimpleContainer.Hosting
{
	public class Application
	{
		public string HostName { get; private set; }

		public void Run()
		{
			//сюда должен попадать основной поток - метод Main в обычной консольной прилаге
			//здесь возможны два режима работы
			//	просто вызвать единственную реализацию IRunnable
			//	если кто-то выставил IsBackground, то текущий поток должен втухнуть на IShutdownCoordinator.Task.WaitOne();

			//короче, нужна абстракци€, инкапсулирующа€ в себ€ работу с IComponent-ами.
			//именно эта абстракци€ должна торчать в ебучем BootstrapPoller-е.

			//по сути эта абстракци€ должна инкапсулировать SimpleContainer и уметь в правильный момент запускать IComponent-ы
			//она должна имплементить IDisposable
			//эта абстракци€ дожна знать целевой тип - нужно запустить резолв этого типа, чтобы поймать все IComponent-ы, которые
			//вылет€т за врем€ его создани€. “упо зарезолвить всех, кто имплементит IComponent не сканает, т.к. они в разных контрактах
			//могут создаватьс€

			//и все равно не пон€тно, как бл€ть разводить реализации из разных PrimaryAssembly
			//без сохранени€ информации о ссылках это вооще невозможно.
			//шарить деревь€ потомков не получитс€ ?
			//пусть именно эта абстракци€ называетс€ ContainerComponent
		}
	}

	//HostingEnvironment

	//ContainerComponent

	//Application

	//public class ContainerComponent
	//{
	//	public IContainer GetContainer()
	//	{
	//		//возвращаемые контейнеры отличаютс€ только содержимым instanceCache-а

	//		//где в этой модели точка входа ??

	//		//какой интерфейс должен быть у точки входа ?

	//		//как он зав€зан на IShutdownCoordinator ?

	//		//и в какой, бл€ть, все таки момент должны зватьс€ ебаные IComponent-ы ?
	//	}
	//}
	public class ComponentHostingOptions
	{
		internal IComponent Component;

		internal ComponentHostingOptions(IComponent component)
		{
			Component = component;
		}

		internal void Initialize()
		{
			Component.Initialize(this);
		}

		internal void Stop()
		{
			if (OnStop == null)
				return;
			try
			{
				OnStop();
			}
			catch (Exception e)
			{
				var message = string.Format("error stopping component [{0}]", Component.GetType().FormatName());
				throw new SimpleContainerException(message, e);
			}
		}

		public bool IsBackground { get; set; }
		public Action OnStop;
	}
}