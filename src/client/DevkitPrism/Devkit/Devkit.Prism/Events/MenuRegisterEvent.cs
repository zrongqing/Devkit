using Devkit.Core.UI.Models;

namespace DevKitApp.Core.PrismEvents;

/// <summary>
/// Represents a publish-subscribe event that is triggered when a menu is registered.
/// </summary>
/// <remarks>This event is typically used to notify subscribers when a new menu instance is available or has been
/// registered within the application. Subscribers can handle this event to perform actions in response to menu
/// registration. Thread safety and event delivery semantics depend on the underlying PubSubEvent
/// implementation.</remarks>
public class MenuRegisterEvent : PubSubEvent<Menu>
{

}
