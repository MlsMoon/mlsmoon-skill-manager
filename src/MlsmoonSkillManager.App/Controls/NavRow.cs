using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MlsmoonSkillManager.App.Controls;

public class NavRow : Control
{
    static NavRow() => ThemeChrome.Default<NavRow>();

    public NavRow() => ThemeChrome.UseAppStyle(this);

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(NavRow),
        new PropertyMetadata(""));

    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail),
        typeof(string),
        typeof(NavRow),
        new PropertyMetadata(""));

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(NavRow),
        new PropertyMetadata(false));

    public static readonly DependencyProperty DetailIsErrorProperty = DependencyProperty.Register(
        nameof(DetailIsError),
        typeof(bool),
        typeof(NavRow),
        new PropertyMetadata(false));

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command),
        typeof(ICommand),
        typeof(NavRow));

    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter),
        typeof(object),
        typeof(NavRow));

    public static readonly DependencyProperty RemoveCommandProperty = DependencyProperty.Register(
        nameof(RemoveCommand),
        typeof(ICommand),
        typeof(NavRow));

    public static readonly DependencyProperty RemoveCommandParameterProperty = DependencyProperty.Register(
        nameof(RemoveCommandParameter),
        typeof(object),
        typeof(NavRow));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Detail
    {
        get => (string)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool DetailIsError
    {
        get => (bool)GetValue(DetailIsErrorProperty);
        set => SetValue(DetailIsErrorProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => (ICommand?)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public object? RemoveCommandParameter
    {
        get => GetValue(RemoveCommandParameterProperty);
        set => SetValue(RemoveCommandParameterProperty, value);
    }
}
