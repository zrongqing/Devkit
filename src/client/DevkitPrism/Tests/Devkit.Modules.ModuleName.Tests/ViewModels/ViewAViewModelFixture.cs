using Devkit.Modules.ModuleName.ViewModels;
using Devkit.Services.Interfaces;
using Moq;
using Xunit;

namespace Devkit.Modules.ModuleName.Tests.ViewModels;

public class ViewAViewModelFixture
{
    private const string MessageServiceDefaultMessage = "Some Value";
    private readonly Mock<IMessageService> _messageServiceMock;
    private readonly Mock<IRegionManager> _regionManagerMock;

    public ViewAViewModelFixture()
    {
        _messageServiceMock = new Mock<IMessageService>();
        _messageServiceMock.Setup(x => x.GetMessage()).Returns(MessageServiceDefaultMessage);

        _regionManagerMock = new Mock<IRegionManager>();
    }

    [Fact]
    public void MessagePropertyValueUpdated()
    {
        var vm = new ViewAViewModel(_regionManagerMock.Object, _messageServiceMock.Object);

        _messageServiceMock.Verify(x => x.GetMessage(), Times.Once);

        Assert.Equal(MessageServiceDefaultMessage, vm.Message);
    }

    [Fact]
    public void MessageINotifyPropertyChangedCalled()
    {
        var vm = new ViewAViewModel(_regionManagerMock.Object, _messageServiceMock.Object);
        Assert.PropertyChanged(vm, nameof(vm.Message), () => vm.Message = "Changed");
    }
}
