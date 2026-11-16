using BusTrack.App.Services;

namespace BusTrack.App;

public partial class App : Application
{
	public App(NotificationDispatcher dispatcher)
	{
		InitializeComponent();
		// Диспетчер подписывается на снапшоты в конструкторе — держим ссылку, чтобы жил весь процесс.
		_ = dispatcher;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}