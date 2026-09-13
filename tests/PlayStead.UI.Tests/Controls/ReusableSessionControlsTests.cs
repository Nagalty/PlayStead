using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;

namespace PlayStead.UI.Tests.Controls;

public sealed class ReusableSessionControlsTests
{
    [Fact]
    [Trait("Task12", "Controls")]
    public void SectionHeader_instantiates_and_exposes_bindable_Title()
    {
        RunSta(
            () =>
            {
                var control =
                    CreateControl(
                        "PlayStead.UI.Controls.SectionHeader");

                AssertDependencyProperty(
                    control.GetType(),
                    "Title",
                    typeof(string));
            });
    }

    [Fact]
    [Trait("Task12", "Controls")]
    public void StatusBadge_instantiates_and_exposes_bindable_Text()
    {
        RunSta(
            () =>
            {
                var control =
                    CreateControl(
                        "PlayStead.UI.Controls.StatusBadge");

                AssertDependencyProperty(
                    control.GetType(),
                    "Text",
                    typeof(string));
            });
    }

    [Fact]
    [Trait("Task12", "Controls")]
    public void KpiCard_instantiates_and_exposes_bindable_Label_and_Value()
    {
        RunSta(
            () =>
            {
                var control =
                    CreateControl(
                        "PlayStead.UI.Controls.KpiCard");

                AssertDependencyProperty(
                    control.GetType(),
                    "Label",
                    typeof(string));

                AssertDependencyProperty(
                    control.GetType(),
                    "Value",
                    typeof(string));
            });
    }

    [Fact]
    [Trait("Task12", "Controls")]
    public void EmptyState_instantiates_and_exposes_bindable_Message()
    {
        RunSta(
            () =>
            {
                var control =
                    CreateControl(
                        "PlayStead.UI.Controls.EmptyState");

                AssertDependencyProperty(
                    control.GetType(),
                    "Message",
                    typeof(string));
            });
    }

    private static UserControl CreateControl(
        string fullTypeName)
    {
        var assembly =
            typeof(
                PlayStead.UI.Library.LibraryView)
                .Assembly;

        var type =
            assembly.GetType(
                fullTypeName,
                throwOnError: false,
                ignoreCase: false);

        Assert.NotNull(
            type);

        var instance =
            Activator.CreateInstance(
                type!);

        return Assert.IsAssignableFrom<UserControl>(
            instance);
    }

    private static void AssertDependencyProperty(
        Type controlType,
        string propertyName,
        Type expectedPropertyType)
    {
        var clrProperty =
            controlType.GetProperty(
                propertyName,
                BindingFlags.Instance |
                BindingFlags.Public);

        Assert.NotNull(
            clrProperty);

        Assert.Equal(
            expectedPropertyType,
            clrProperty!.PropertyType);

        var dependencyPropertyField =
            controlType.GetField(
                $"{propertyName}Property",
                BindingFlags.Public |
                BindingFlags.Static);

        Assert.NotNull(
            dependencyPropertyField);

        var dependencyProperty =
            Assert.IsType<DependencyProperty>(
                dependencyPropertyField!
                    .GetValue(
                        null));

        Assert.Equal(
            expectedPropertyType,
            dependencyProperty.PropertyType);

        Assert.Equal(
            controlType,
            dependencyProperty.OwnerType);
    }

    private static void RunSta(
        Action action)
    {
        Exception? failure =
            null;

        var thread =
            new Thread(
                () =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception exception)
                    {
                        failure =
                            exception;
                    }
                });

        thread.SetApartmentState(
            ApartmentState.STA);

        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo
                .Capture(
                    failure)
                .Throw();
        }
    }
}
